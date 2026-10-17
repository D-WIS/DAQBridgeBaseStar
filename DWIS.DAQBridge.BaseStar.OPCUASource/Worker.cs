using System.Diagnostics;
using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.OPCUASource;

public class Worker : DWISWorkerWithOPCUA<ConfigurationForBaseStarSimulation>
{
    public Worker(ILogger<IDWISWorker<ConfigurationForBaseStarSimulation>> logger, ILogger<DWISClientOPCF>? loggerDWISClient)
        : base(logger, loggerDWISClient) { }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConnectToOPCUA();
        if (Configuration is null || _OPCUAClient?.Connected != true)
        {
            Logger?.LogError("Could not connect to the BaseStar simulation OPC-UA endpoint.");
            return;
        }
        var simulator = new BaseStarSimulator(Configuration);
        await RegisterToOPCUA(simulator.Data, "BaseStarDataManifest", "Halliburton");
        simulator.Tick(TimeSpan.Zero);
        await PublishAsync(simulator.Data, true, stoppingToken);
        var elapsed = Stopwatch.StartNew();
        using var timer = new PeriodicTimer(simulator.TickPeriod);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            BaseStarSimulationStep step = simulator.Tick(elapsed.Elapsed);
            if (step.ShouldPublish) await PublishAsync(simulator.Data, step.Generated, stoppingToken);
            ConfigurationUpdater<ConfigurationForBaseStarSimulation>.Instance.UpdateConfiguration(this);
        }
    }

    protected override async Task PublishOPCUAAsync(DWISDataWithOPCUA data, string? configuredNamespace,
        string? configuredPrefix, Dictionary<string, string>? configuredIds, CancellationToken stoppingToken)
    {
        if (_OPCUAClient?.Connected != true) throw new InvalidOperationException("The simulation OPC-UA endpoint is unavailable.");
        var values = new List<(string, string, object, DateTime)>();
        DateTime timestamp = DateTime.UtcNow;
        foreach (var (property, node) in data.OPCUANodes.Value)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (property.GetValue(data) is not ScalarProperty { Value: double value }) continue;
            string? ns = string.IsNullOrEmpty(configuredNamespace) ? node.NameSpace : configuredNamespace;
            if (string.IsNullOrEmpty(ns) && node.NameSpaceID.HasValue &&
                _OPCUAClient.GetNameSpace((ushort)node.NameSpaceID.Value, out string resolved)) ns = resolved;
            string? id = node.ID;
            if (id is not null && configuredIds?.TryGetValue(id, out string? mapped) == true) id = mapped;
            id = (configuredPrefix ?? node.PrefixID ?? "") + id;
            if (string.IsNullOrWhiteSpace(ns) || string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException($"Missing OPC-UA address for '{property.Name}'.");
            if (Configuration?.UnitConversions?.TryGetValue(id, out var conversion) == true)
            {
                if (!double.IsFinite(conversion.ConversionFactor) || conversion.ConversionFactor == 0 ||
                    !double.IsFinite(conversion.ConversionOffset))
                    throw new InvalidOperationException($"Invalid simulation unit conversion for '{property.Name}'.");
                value = conversion.FromSI(value);
            }
            if (!double.IsFinite(value)) throw new InvalidOperationException($"Non-finite simulated value for '{property.Name}'.");
            values.Add((ns, id, value, timestamp));
        }
        if (values.Count > 0 && !_OPCUAClient.UpdateAnyVariables(values.ToArray()))
            throw new InvalidOperationException("The OPC-UA server rejected the simulated values.");
        await Task.Yield();
    }

    private async Task PublishAsync(MechanicalSubOutputDataMudPulseTelemetry data, bool generated, CancellationToken token)
    {
        await PublishOPCUAAsync(data, Configuration?.NameSpace, Configuration?.NodeIDPrefix, Configuration?.OPCUAIDs, token);
        Logger?.LogInformation("{Action} BaseStar simulation values: annulus pressure={Pressure:F0} Pa; maximum raw weight={Weight:F0} N",
            generated ? "Generated and published" : "Republished unchanged", data.AnnulusPressure?.Value, data.MaxRawWeight?.Value);
    }
}
