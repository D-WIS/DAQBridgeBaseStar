using DWIS.RigOS.Common.Worker;

namespace DWIS.DAQBridge.BaseStar.Model;

public class ConfigurationForBaseStarSimulation : ConfigurationForOPCUA
{
    /// <summary>Optional namespace override keyed by the signal's default OPC-UA ID.</summary>
    public Dictionary<string, string>? OPCUANameSpaces { get; set; }

    public int SimulationSeed { get; set; } = 20261017;
    public TimeSpan SimulationConditionGenerationPeriod { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan SimulationDataGenerationPeriod { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan SimulationWiredPipeDataGenerationPeriod { get; set; } = TimeSpan.FromSeconds(2.5);
    public TimeSpan SimulationOPCUAPublishPeriod { get; set; } = TimeSpan.FromSeconds(5);
}
