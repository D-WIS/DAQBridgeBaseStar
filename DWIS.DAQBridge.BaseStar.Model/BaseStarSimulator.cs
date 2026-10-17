using DWIS.RigOS.Common.Worker;

namespace DWIS.DAQBridge.BaseStar.Model;

/// <summary>Synthetic SI data; values are deterministic for a seed and scheduled generation index.</summary>
public sealed class BaseStarSimulator
{
    private readonly int _seed;
    private readonly TimeSpan _conditionPeriod;
    private readonly TimeSpan _dataPeriod;
    private readonly TimeSpan _publishPeriod;
    private long _conditionIndex = -1;
    private long _dataIndex = -1;
    private long _publishIndex = -1;

    public MechanicalSubOutputDataMudPulseTelemetry Data { get; } = new();
    public TimeSpan TickPeriod { get; }

    public BaseStarSimulator(ConfigurationForBaseStarSimulation configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _seed = configuration.SimulationSeed;
        _conditionPeriod = Positive(configuration.SimulationConditionGenerationPeriod, TimeSpan.FromSeconds(10));
        _dataPeriod = Positive(configuration.SimulationDataGenerationPeriod, TimeSpan.FromSeconds(30));
        _publishPeriod = Positive(configuration.SimulationOPCUAPublishPeriod, TimeSpan.FromSeconds(5));
        TickPeriod = new[] { _conditionPeriod, _dataPeriod, _publishPeriod,
            Positive(configuration.OPCUALoopDuration, TimeSpan.FromSeconds(1)), TimeSpan.FromSeconds(1) }.Min();
        foreach (var property in BaseStarSnapshot.Properties) Data.CreateProperty(property);
    }

    public BaseStarSimulationStep Tick(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        long conditionIndex = elapsed.Ticks / _conditionPeriod.Ticks;
        long dataIndex = elapsed.Ticks / _dataPeriod.Ticks;
        long publishIndex = elapsed.Ticks / _publishPeriod.Ticks;
        bool generated = false;
        if (conditionIndex != _conditionIndex)
        {
            FillConditions(conditionIndex, conditionIndex * _conditionPeriod.TotalSeconds);
            _conditionIndex = conditionIndex;
            generated = true;
        }
        if (dataIndex != _dataIndex)
        {
            FillDynamics(dataIndex, dataIndex * _dataPeriod.TotalSeconds);
            _dataIndex = dataIndex;
            generated = true;
        }
        bool publish = generated || publishIndex != _publishIndex;
        _publishIndex = publishIndex;
        return new(generated, publish);
    }

    private Random Sequence(long index, int stream) => new(unchecked(_seed * 397 ^ (int)index * 7919 ^ (int)(index >> 32) ^ stream));
    private static TimeSpan Positive(TimeSpan value, TimeSpan fallback) => value > TimeSpan.Zero ? value : fallback;
    private static void Set(ScalarProperty? property, double value) => property!.Value = value;

    private void FillConditions(long index, double seconds)
    {
        var random = Sequence(index, 17);
        double phase = seconds / 45;
        double annulus = 24e6 + 0.8e6 * Math.Sin(phase / 2) + (random.NextDouble() - 0.5) * 20000;
        Set(Data.AnnulusPressure, annulus);
        Set(Data.StringPressure, annulus + 1e6 + 0.1e6 * Math.Sin(phase));
        Set(Data.ElectronicTemperature, 343.15 + 3 * Math.Sin(phase / 3) + (random.NextDouble() - 0.5) * 0.4);
    }

    private void FillDynamics(long index, double seconds)
    {
        var random = Sequence(index, 31);
        double phase = seconds / 45;
        double rpm = 120 + 20 * Math.Sin(phase) + (random.NextDouble() - 0.5) * 4;
        Set(Data.AverageRotationalSpeed, rpm * 2 * Math.PI / 60);
        Set(Data.MaxRotationalSpeed, (rpm + 15 + 5 * Math.Abs(Math.Sin(phase / 2))) * 2 * Math.PI / 60);
        Set(Data.StickSlipIndicator, 0.05 + 0.4 * Math.Abs(Math.Sin(phase / 2)));
        double radial = 3 + 2 * Math.Abs(Math.Sin(phase)) + random.NextDouble() * 0.2;
        Set(Data.AverageRadialAcceleration, radial);
        Set(Data.PeakRadialAcceleration, radial + 10 + 8 * Math.Abs(Math.Sin(phase / 2)));
        Set(Data.PeakAxialAcceleration, 8 + 20 * Math.Abs(Math.Cos(phase / 3)));

        double rawWeight = 110000 + 20000 * Math.Sin(phase / 3) + (random.NextDouble() - 0.5) * 3000;
        const double tare = -12000;
        Set(Data.AverageRawWeight, rawWeight);
        Set(Data.MinRawWeight, rawWeight - 9000);
        Set(Data.MaxRawWeight, rawWeight + 11000);
        Set(Data.TareBitWeight, tare);
        Set(Data.AverageBitWeight, rawWeight + tare);
        Set(Data.MinBitWeight, rawWeight - 9000 + tare);
        Set(Data.MaxBitWeight, rawWeight + 11000 + tare);

        double rawTorque = 4500 + 900 * Math.Sin(phase / 2) + (random.NextDouble() - 0.5) * 150;
        const double bias = -250;
        Set(Data.AverageRawTorque, rawTorque);
        Set(Data.MinRawTorque, rawTorque - 300);
        Set(Data.MaxRawTorque, rawTorque + 400);
        Set(Data.TorqueBias, bias);
        Set(Data.AverageCorrectedTorque, rawTorque + bias);
        Set(Data.MinCorrectedTorque, rawTorque - 300 + bias);
        Set(Data.MaxCorrectedTorque, rawTorque + 400 + bias);
        // Synthetic example follows the current graph's raw-plus-bias relationships.
        // This is not a manufacturer calibration or a drillstring torque model.
        Set(Data.AverageBitTorque, rawTorque + bias);
        Set(Data.MinBitTorque, rawTorque - 300 + bias);
        Set(Data.MaxBitTorque, rawTorque + 400 + bias);
    }
}

public readonly record struct BaseStarSimulationStep(bool Generated, bool ShouldPublish);
