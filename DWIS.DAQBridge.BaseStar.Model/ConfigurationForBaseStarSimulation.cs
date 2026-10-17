using DWIS.RigOS.Common.Worker;

namespace DWIS.DAQBridge.BaseStar.Model;

public class ConfigurationForBaseStarSimulation : ConfigurationForOPCUA
{
    public int SimulationSeed { get; set; } = 20261017;
    public TimeSpan SimulationConditionGenerationPeriod { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan SimulationDataGenerationPeriod { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan SimulationOPCUAPublishPeriod { get; set; } = TimeSpan.FromSeconds(5);
}
