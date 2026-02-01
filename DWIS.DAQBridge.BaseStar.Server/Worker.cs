using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using OSDC.DotnetLibraries.General.Common;
using System.Reflection;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.Server
{
    public class Worker : DWISWorkerWithOPCUA<ConfigurationForOPCUA>
    {
        private MechanicalSubOutputDataMudPulseTelemetry MechanicalSubData { get; set; } = new MechanicalSubOutputDataMudPulseTelemetry();

        private TimeSpan OPCUALoopSpan { get; set; } = TimeSpan.FromSeconds(1);

        public Worker(ILogger<IDWISWorker<ConfigurationForOPCUA>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectToOPCUA();
            ConnectToBlackboard();
            if (Configuration is not null && _DWISClient != null && _DWISClient.Connected)
            {
                OPCUALoopSpan = Configuration.OPCUALoopDuration;
                await RegisterToBlackboard(MechanicalSubData);
                await Loop(stoppingToken);
            }
        }

        protected override async Task Loop(CancellationToken stoppingToken)
        {
            PeriodicTimer timer = new PeriodicTimer(OPCUALoopSpan);
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
                try
                {
                    // process series
                    await ReadOPCUA(MechanicalSubData, Configuration?.NameSpace, Configuration?.NodeIDPrefix, Configuration?.OPCUAIDs, Configuration?.UnitConversions);
                    k++;
                    if (k == count)
                    {
                        DateTime d1 = DateTime.UtcNow;
                        await PublishBlackboardAsync(MechanicalSubData, stoppingToken);
                        DateTime d2 = DateTime.UtcNow;
                        double elapsed = (d2 - d1).TotalSeconds;
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
                catch (Exception e)
                {
                    Logger?.LogError(e.ToString());
                }
                ConfigurationUpdater<ConfigurationForOPCUA>.Instance.UpdateConfiguration(this);
            }
        }
    }
}
