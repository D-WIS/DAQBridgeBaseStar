using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.Tests;

public class SimulatorTests
{
    [Fact]
    public void SameSeedAndScheduleProduceSameValuesAndDifferentSeedsProduceDifferentValues()
    {
        var first = new BaseStarSimulator(new() { SimulationSeed = 123 });
        var second = new BaseStarSimulator(new() { SimulationSeed = 123 });
        var different = new BaseStarSimulator(new() { SimulationSeed = 124 });
        foreach (int seconds in new[] { 0, 5, 10, 30, 90, 300 })
        {
            first.Tick(TimeSpan.FromSeconds(seconds));
            second.Tick(TimeSpan.FromSeconds(seconds));
            different.Tick(TimeSpan.FromSeconds(seconds));
            Assert.Equal(BaseStarSnapshot.Capture(first.Data), BaseStarSnapshot.Capture(second.Data));
            Assert.NotEqual(BaseStarSnapshot.Capture(first.Data), BaseStarSnapshot.Capture(different.Data));
        }
    }

    [Fact]
    public void PublishingDoesNotGenerateNewValues()
    {
        var simulator = new BaseStarSimulator(new());
        Assert.Equal(new BaseStarSimulationStep(true, true), simulator.Tick(TimeSpan.Zero));
        var baseline = BaseStarSnapshot.Capture(simulator.Data);
        Assert.Equal(new BaseStarSimulationStep(false, false), simulator.Tick(TimeSpan.FromSeconds(1)));
        Assert.Equal(new BaseStarSimulationStep(false, true), simulator.Tick(TimeSpan.FromSeconds(5)));
        Assert.Equal(baseline, BaseStarSnapshot.Capture(simulator.Data));
        Assert.Equal(new BaseStarSimulationStep(false, false), simulator.Tick(TimeSpan.FromSeconds(5.1)));
    }

    [Fact]
    public void ConditionsAndDynamicsRefreshIndependently()
    {
        var simulator = new BaseStarSimulator(new());
        simulator.Tick(TimeSpan.Zero);
        double? weight = simulator.Data.MaxRawWeight!.Value;
        double? pressure = simulator.Data.AnnulusPressure!.Value;
        Assert.Equal(new BaseStarSimulationStep(true, true), simulator.Tick(TimeSpan.FromSeconds(10)));
        Assert.Equal(weight, simulator.Data.MaxRawWeight.Value);
        Assert.NotEqual(pressure, simulator.Data.AnnulusPressure.Value);
        simulator.Tick(TimeSpan.FromSeconds(30));
        Assert.NotEqual(weight, simulator.Data.MaxRawWeight.Value);
    }

    [Fact]
    public void MissedPollsAdvanceToLatestScheduledSampleWithoutChangingRandomSequence()
    {
        var regular = new BaseStarSimulator(new());
        var delayed = new BaseStarSimulator(new());
        for (int seconds = 0; seconds <= 90; seconds++) regular.Tick(TimeSpan.FromSeconds(seconds));
        delayed.Tick(TimeSpan.Zero);
        delayed.Tick(TimeSpan.FromSeconds(90));
        Assert.Equal(BaseStarSnapshot.Capture(regular.Data), BaseStarSnapshot.Capture(delayed.Data));
        Assert.False(delayed.Tick(TimeSpan.FromSeconds(90.1)).ShouldPublish);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidPeriodsUseDocumentedDefaults(int seconds)
    {
        var simulator = new BaseStarSimulator(new()
        {
            OPCUALoopDuration = TimeSpan.FromSeconds(seconds),
            SimulationConditionGenerationPeriod = TimeSpan.FromSeconds(seconds),
            SimulationDataGenerationPeriod = TimeSpan.FromSeconds(seconds),
            SimulationOPCUAPublishPeriod = TimeSpan.FromSeconds(seconds)
        });
        Assert.Equal(TimeSpan.FromSeconds(1), simulator.TickPeriod);
        simulator.Tick(TimeSpan.Zero);
        Assert.Equal(new BaseStarSimulationStep(false, true), simulator.Tick(TimeSpan.FromSeconds(5)));
        Assert.Equal(new BaseStarSimulationStep(true, true), simulator.Tick(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void All26SignalsAreFiniteSiValuesWithCoherentStatisticsAndOffsets()
    {
        var simulator = new BaseStarSimulator(new());
        for (int seconds = 0; seconds < 3600; seconds += 30)
        {
            simulator.Tick(TimeSpan.FromSeconds(seconds));
            var data = simulator.Data;
            Assert.Equal(26, BaseStarSnapshot.Properties.Count);
            Assert.All(BaseStarSnapshot.Properties, p =>
            {
                var scalar = Assert.IsType<DWIS.RigOS.Common.Worker.ScalarProperty>(p.GetValue(data));
                Assert.True(scalar.Value.HasValue && double.IsFinite(scalar.Value.Value), p.Name);
            });
            Assert.InRange(data.AverageRotationalSpeed!.Value!.Value * 60 / (2 * Math.PI), 98, 142);
            Assert.True(data.MaxRotationalSpeed!.Value >= data.AverageRotationalSpeed.Value);
            Assert.InRange(data.StickSlipIndicator!.Value!.Value, 0, 1);
            Assert.True(data.PeakRadialAcceleration!.Value >= data.AverageRadialAcceleration!.Value);
            Assert.True(data.MinRawWeight!.Value <= data.AverageRawWeight!.Value && data.AverageRawWeight.Value <= data.MaxRawWeight!.Value);
            Assert.True(data.MinRawTorque!.Value <= data.AverageRawTorque!.Value && data.AverageRawTorque.Value <= data.MaxRawTorque!.Value);
            Assert.Equal(data.AverageRawWeight.Value + data.TareBitWeight!.Value, data.AverageBitWeight!.Value);
            Assert.Equal(data.MinRawWeight.Value + data.TareBitWeight.Value, data.MinBitWeight!.Value);
            Assert.Equal(data.MaxRawWeight!.Value + data.TareBitWeight.Value, data.MaxBitWeight!.Value);
            Assert.Equal(data.AverageRawTorque.Value + data.TorqueBias!.Value, data.AverageCorrectedTorque!.Value);
            Assert.Equal(data.MinRawTorque.Value + data.TorqueBias.Value, data.MinCorrectedTorque!.Value);
            Assert.Equal(data.MaxRawTorque!.Value + data.TorqueBias.Value, data.MaxCorrectedTorque!.Value);
            Assert.Equal(data.MaxCorrectedTorque.Value, data.MaxBitTorque!.Value);
            Assert.True(data.StringPressure!.Value > data.AnnulusPressure!.Value);
            Assert.InRange(data.ElectronicTemperature!.Value!.Value, 339, 347);
        }
    }

    [Fact]
    public void NegativeElapsedTimeIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaseStarSimulator(new()).Tick(TimeSpan.FromSeconds(-1)));
}
