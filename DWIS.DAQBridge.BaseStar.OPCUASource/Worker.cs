using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.OPCUASource
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
            if (Configuration is not null && _OPCUAClient != null && _OPCUAClient.Connected)
            {
                OPCUALoopSpan = Configuration.OPCUALoopDuration;
                await RegisterToOPCUA(MechanicalSubData, "BaseStarDataManifest", "Halliburton");
                await Loop(stoppingToken);
            }
        }

        protected override async Task Loop(CancellationToken stoppingToken)
        {
            PeriodicTimer timer = new PeriodicTimer(OPCUALoopSpan);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (_OPCUAClient != null && _OPCUAClient.Connected)
                {
                    FillRandomData(MechanicalSubData);
                    await PublishOPCUAAsync(MechanicalSubData, Configuration?.NameSpace, Configuration?.NodeIDPrefix, Configuration?.OPCUAIDs, stoppingToken);
                    lock (_lock)
                    {
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information) && MechanicalSubData.AnnulusPressure is not null && MechanicalSubData.AnnulusPressure.Value is not null)
                        {
                            Logger.LogInformation("Mechanical sub annulus pressure: " + MechanicalSubData.AnnulusPressure.Value);
                        }
                    }
                }
                ConfigurationUpdater<ConfigurationForOPCUA>.Instance.UpdateConfiguration(this);
            }
        }

    }
}
