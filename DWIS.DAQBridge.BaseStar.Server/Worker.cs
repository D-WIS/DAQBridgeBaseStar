using DWIS.Client.ReferenceImplementation;
using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using OSDC.DotnetLibraries.General.Common;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.Server
{
    public class Worker : DWISWorkerWithOPCUA<ConfigurationForBaseStar>
    {
        private MechanicalSubOutputDataMudPulseTelemetry MechanicalSubData { get; set; } = new MechanicalSubOutputDataMudPulseTelemetry();

        private bool _opcConnectionNeedsRecovery;
        private bool _blackboardConnectionNeedsRecovery;

        private TimeSpan OPCUALoopSpan { get; set; } = TimeSpan.FromSeconds(1);

        public Worker(ILogger<IDWISWorker<ConfigurationForBaseStar>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConfigurationUpdater<ConfigurationForBaseStar>.Instance.UpdateConfiguration(this);

                if (!TryEstablishConnections())
                {
                    await WaitBeforeReconnectAsync(stoppingToken);
                    continue;
                }

                try
                {
                    await InitializeConnectedSessionAsync();
                    if (!ConnectionsAreEstablished)
                    {
                        Logger?.LogWarning("A BaseStar bridge connection was lost during session initialization. Returning to connection retry mode.");
                        await WaitBeforeReconnectAsync(stoppingToken);
                        continue;
                    }

                    Logger?.LogInformation("Both the BaseStar OPC-UA endpoint and DWIS Blackboard are connected. Starting the bridge exchange loop.");
                    await Loop(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "The BaseStar bridge session failed. Returning to connection retry mode.");
                    _opcConnectionNeedsRecovery = true;
                    _blackboardConnectionNeedsRecovery = true;
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    Logger?.LogWarning("The BaseStar bridge exchange session ended. Returning to connection retry mode.");
                    await WaitBeforeReconnectAsync(stoppingToken);
                }
            }
        }

        private bool TryEstablishConnections()
        {
            if (Configuration is null)
            {
                Logger?.LogWarning("BaseStar configuration is not available. Waiting before trying again.");
                return false;
            }

            bool opcConnected = TryConnect(
                () => _OPCUAClient,
                ConnectToOPCUA,
                "BaseStar OPC-UA endpoint",
                _opcConnectionNeedsRecovery);
            bool blackboardConnected = TryConnect(
                () => _DWISClient,
                ConnectToBlackboard,
                "DWIS Blackboard",
                _blackboardConnectionNeedsRecovery);

            _opcConnectionNeedsRecovery = !opcConnected;
            _blackboardConnectionNeedsRecovery = !blackboardConnected;

            if (!opcConnected || !blackboardConnected)
            {
                Logger?.LogWarning(
                    "Waiting for bridge connections. BaseStar OPC-UA connected={OPCUAConnected}; DWIS Blackboard connected={BlackboardConnected}.",
                    opcConnected,
                    blackboardConnected);
            }

            return opcConnected && blackboardConnected;
        }

        private bool TryConnect(
            Func<IOPCUADWISClient?> getClient,
            Action createClient,
            string connectionName,
            bool forceReconnect)
        {
            IOPCUADWISClient? client = getClient();
            if (forceReconnect)
            {
                try
                {
                    createClient();
                    return getClient()?.Connected == true;
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "Could not replace the failed {ConnectionName} session.", connectionName);
                    return false;
                }
            }

            if (client?.Connected == true)
            {
                return true;
            }

            try
            {
                if (client is null)
                {
                    createClient();
                }
                else
                {
                    return client.TryReconnect() && client.Connected;
                }

                return getClient()?.Connected == true;
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Could not connect to the {ConnectionName}.", connectionName);
                return false;
            }
        }

        private bool ConnectionsAreEstablished =>
            _OPCUAClient?.Connected == true && _DWISClient?.Connected == true;

        private async Task InitializeConnectedSessionAsync()
        {
            OPCUALoopSpan = Configuration!.OPCUALoopDuration > TimeSpan.Zero
                ? Configuration.OPCUALoopDuration : TimeSpan.FromSeconds(1);
            LoopSpan = Configuration.LoopDuration;
            if (Configuration.InitializeInputOPCUAVariables)
            {
                await RegisterToOPCUA(MechanicalSubData, "BaseStarDataManifest", "Halliburton");
            }
            // Targets belong to the current Blackboard session and must be resolved again.
            PlaceHolders.Clear();
            await RegisterToBlackboard(MechanicalSubData);
        }

        private async Task WaitBeforeReconnectAsync(CancellationToken stoppingToken)
        {
            TimeSpan retryInterval = Configuration?.ConnectionRetryInterval > TimeSpan.Zero
                ? Configuration.ConnectionRetryInterval
                : TimeSpan.FromSeconds(5);

            try
            {
                await Task.Delay(retryInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal host shutdown while waiting for an endpoint.
            }
        }

        protected override async Task Loop(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(OPCUALoopSpan);
            double opcDuration = OPCUALoopSpan.TotalSeconds;
            double dwisDuration = LoopSpan.TotalSeconds;
            int count = 1;
            if (!Numeric.EQ(opcDuration, 0))
            {
                count = (int)(dwisDuration / opcDuration);
            }
            if (count <= 0)
            {
                count = 1;
            }
            int k = 0;
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!ConnectionsAreEstablished)
                {
                    return;
                }
                try
                {
                    await ReadOPCUA(MechanicalSubData, Configuration?.NameSpace, Configuration?.NodeIDPrefix, Configuration?.OPCUAIDs, Configuration?.UnitConversions);
                    if (!ConnectionsAreEstablished)
                    {
                        return;
                    }
                    k++;
                    if (k == count)
                    {
                        await PublishBlackboardAsync(MechanicalSubData, stoppingToken);
                        if (!ConnectionsAreEstablished)
                        {
                            return;
                        }
                        lock (_lock)
                        {
                            if (Logger is not null && Logger.IsEnabled(LogLevel.Information) &&
                                MechanicalSubData.AnnulusPressure is not null &&
                                MechanicalSubData.AnnulusPressure.Value is not null)
                            {
                                Logger.LogInformation("Mechanical Sub Annulus Pressure: " + MechanicalSubData.AnnulusPressure.Value.Value.ToString("F3"));
                            }
                        }
                        k = 0;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger?.LogError(e, "BaseStar bridge exchange failed. Returning to connection retry mode.");
                    // Connected may remain true briefly after a transport operation fails.
                    _opcConnectionNeedsRecovery = true;
                    _blackboardConnectionNeedsRecovery = true;
                    return;
                }
                ConfigurationUpdater<ConfigurationForBaseStar>.Instance.UpdateConfiguration(this);
            }
        }
    }
}
