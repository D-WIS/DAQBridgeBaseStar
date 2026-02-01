using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.DAQBridge.BaseStar.Model;
using DWIS.RigOS.Common.Worker;
using System.Reflection;

namespace DWIS.DAQBridge.BaseStar.OPCUASink
{
    public class Worker : DWISWorker<Configuration>
    {
        private MechanicalSubOutputDataMudPulseTelemetry MechanicalSubData { get; set; } = new MechanicalSubOutputDataMudPulseTelemetry();

        public Worker(ILogger<IDWISWorker<Configuration>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectToBlackboard();
            if (_DWISClient != null && _DWISClient.Connected)
            {
                await RegisterQueries(MechanicalSubData);
                await Loop(stoppingToken);
            }
        }

        protected override async Task Loop(CancellationToken cancellationToken)
        {
            PeriodicTimer timer = new PeriodicTimer(LoopSpan);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ReadBlackboardAsync(MechanicalSubData, cancellationToken);
                if (Logger is not null && Logger.IsEnabled(LogLevel.Information) &&
                    MechanicalSubData.AnnulusPressure is not null &&
                    MechanicalSubData.AnnulusPressure.Value is not null)
                {
                    Logger.LogInformation("Mechanical Sub Annulus Pressure: " + MechanicalSubData.AnnulusPressure.Value.Value.ToString("F3"));
                }
                ConfigurationUpdater<Configuration>.Instance.UpdateConfiguration(this);
            }
        }

    }
}
