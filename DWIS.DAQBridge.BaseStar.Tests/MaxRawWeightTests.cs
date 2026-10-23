using System.Reflection;
using DWIS.API.DTO;
using DWIS.Client.ReferenceImplementation;
using DWIS.DAQBridge.BaseStar.Model;
using DWIS.DAQBridge.BaseStar.Server;
using DWIS.RigOS.Common.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace DWIS.DAQBridge.BaseStar.Tests;

public class MaxRawWeightTests
{
    private const string InputNamespace = "http://ddhub.no/BaseStarDataManifest/Variables/";
    private const string InputID = "BaseStarDataManifest.MaxRawWeight";
    private const string OutputNamespace = "http://ddhub.no/BaseStarDataManifestDWISBridge/Variables/";
    private const string OutputID = "BaseStarDataManifestDWISBridge.BaseStarMeasuredMaxRawWeight";

    [Fact]
    public void MaxRawWeightCreatesSemanticScalarWithExistingDoubleContract()
    {
        var data = new MechanicalSubOutputData();
        var property = typeof(MechanicalSubOutputData).GetProperty(nameof(data.MaxRawWeight))!;
        data.CreateProperty(property);
        var scalar = Assert.IsType<ScalarProperty>(data.MaxRawWeight);
        Assert.NotNull(scalar.Manifest);
        Assert.False(string.IsNullOrWhiteSpace(scalar.SparQLQuery));
        var variable = Assert.Single(scalar.Manifest.ProvidedVariables,
            item => item.VariableID == "BaseStarMeasuredMaxRawWeight");
        Assert.Equal("double", variable.DataType);
    }

    [Fact]
    public async Task SourceInitializesAndPublishesMaxRawWeight()
    {
        using var worker = new SourceWorker();
        var client = worker.SetClient();
        var data = new MechanicalSubOutputData();
        worker.Generate(data);
        Assert.NotNull(data.MaxRawWeight?.Value);
        double expected = data.MaxRawWeight.Value.Value;
        await worker.Publish(data);
        Assert.Equal(expected, client.Writes[(InputNamespace, InputID)]);
    }

    [Fact]
    public async Task SourceConvertsSiToConfiguredWireUnitsInOneBatch()
    {
        using var worker = new SourceWorker();
        var client = worker.SetClient();
        worker.Configuration = new ConfigurationForBaseStarSimulation
        {
            UnitConversions = new() { [InputID] = new() { ConversionFactor = 2, ConversionOffset = 2 } }
        };
        var data = new MechanicalSubOutputData { MaxRawWeight = new ScalarProperty { Value = 12345.5 } };
        await worker.Publish(data);
        Assert.Equal(24693, client.Writes[(InputNamespace, InputID)]);
        Assert.Equal(12345.5, data.MaxRawWeight.Value);
        Assert.Equal(1, client.WriteCalls);
    }

    [Fact]
    public async Task SourceRejectsFailedWrites()
    {
        using var worker = new SourceWorker();
        var client = worker.SetClient();
        client.Reject = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.Publish(
            new MechanicalSubOutputData { MaxRawWeight = new ScalarProperty { Value = 1 } }));
    }

    [Fact]
    public async Task WiredPipeSignalCanUseIndependentNamespaceAndNodeId()
    {
        using var worker = new SourceWorker();
        var client = worker.SetClient();
        worker.Configuration = new ConfigurationForBaseStarSimulation
        {
            NameSpace = "urn:fallback",
            NodeIDPrefix = string.Empty,
            OPCUANameSpaces = new() { ["MaxRawWeightWiredPipe"] = "urn:basestar:wired-weight" },
            OPCUAIDs = new() { ["MaxRawWeightWiredPipe"] = "vendor/tag/weight/max" }
        };
        var data = new MechanicalSubOutputData
        {
            MaxRawWeightWiredPipe = new ScalarProperty { Value = 9876.5 }
        };

        await worker.PublishConfigured(data);

        Assert.Equal(9876.5, client.Writes[("urn:basestar:wired-weight", "vendor/tag/weight/max")]);
    }

    [Theory]
    [InlineData(12345.5, 1, 0, 12345.5)]
    [InlineData(24693, 2, 2, 12345.5)]
    public async Task ServerRegistersReadsAndPublishesMaxRawWeight(
        double input, double factor, double offset, double expectedSI)
    {
        using var worker = new ServerWorker();
        var source = worker.SetInput();
        source.Input = input;
        var blackboard = worker.SetBlackboard();
        var data = new MechanicalSubOutputData();
        await worker.Register(data);
        Assert.True(worker.HasMaxRawWeightTarget);
        await worker.Read(data, new Dictionary<string, UnitConversion>
        {
            [InputID] = new() { ConversionFactor = factor, ConversionOffset = offset }
        });
        Assert.Equal(expectedSI, data.MaxRawWeight?.Value);
        await worker.Publish(data);
        Assert.Equal(expectedSI, blackboard.Writes[(OutputNamespace, OutputID)]);
    }

    private sealed class SourceWorker : DWIS.DAQBridge.BaseStar.OPCUASource.Worker
    {
        public SourceWorker() : base(NullLogger<IDWISWorker<ConfigurationForBaseStarSimulation>>.Instance, null) { }
        public ClientStub SetClient()
        {
            _OPCUAClient = DispatchProxy.Create<IOPCUADWISClient, ClientStub>();
            return (ClientStub)_OPCUAClient;
        }
        public void Generate(MechanicalSubOutputData data) => FillRandomData(data);
        public Task Publish(MechanicalSubOutputData data) =>
            PublishOPCUAAsync(data, null, null, null, CancellationToken.None);
        public Task PublishConfigured(MechanicalSubOutputData data) =>
            PublishOPCUAAsync(data, Configuration?.NameSpace, Configuration?.NodeIDPrefix,
                Configuration?.OPCUAIDs, CancellationToken.None);
    }

    private sealed class ServerWorker : Worker
    {
        public ServerWorker() : base(NullLogger<IDWISWorker<ConfigurationForBaseStar>>.Instance, null) { }
        public bool HasMaxRawWeightTarget => PlaceHolders.Keys.Any(p => p.Name == "MaxRawWeight");
        public ClientStub SetInput()
        {
            _OPCUAClient = DispatchProxy.Create<IOPCUADWISClient, ClientStub>();
            return (ClientStub)_OPCUAClient;
        }
        public ClientStub SetBlackboard()
        {
            _DWISClient = DispatchProxy.Create<IOPCUADWISClient, ClientStub>();
            return (ClientStub)_DWISClient;
        }
        public Task Register(MechanicalSubOutputData data) => RegisterToBlackboard(data, false);
        public Task Read(MechanicalSubOutputData data, Dictionary<string, UnitConversion> conversions) =>
            ReadOPCUA(data, null, null, null, conversions);
        public Task Publish(MechanicalSubOutputData data) => PublishBlackboardAsync(data, CancellationToken.None);
    }

    public class ClientStub : DispatchProxy
    {
        public double Input { get; set; }
        public int WriteCalls { get; private set; }
        public bool Reject { get; set; }
        public Dictionary<(string Namespace, string ID), double> Writes { get; } = new();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "get_Connected": return true;
                case "UpdateAnyVariables":
                    WriteCalls++;
                    if (Reject) return false;
                    foreach (var (ns, id, value, timestamp) in ((string, string, object, DateTime)[])args![0]!)
                        Writes[(ns, id)] = Assert.IsType<double>(value);
                    return true;
                case "ReadAnyVariables":
                    var nodes = ((string, string)[])args![1]!;
                    bool found = nodes[0] == (InputNamespace, InputID);
                    args[0] = found ? new object[] { Input } : Array.Empty<object>();
                    return found;
                case "GetQueryResult":
                    var result = new QueryResult();
                    var row = new QueryResultRow { Items = new List<NodeIdentifier>
                    {
                        new() { NameSpace = OutputNamespace, ID = OutputID }
                    }};
                    result.Add(row);
                    return result;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
