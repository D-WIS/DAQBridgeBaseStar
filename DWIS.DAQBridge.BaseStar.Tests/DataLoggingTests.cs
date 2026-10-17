using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using DWIS.API.DTO;
using DWIS.Client.ReferenceImplementation;
using DWIS.DAQBridge.BaseStar.Model;
using DWIS.DAQBridge.BaseStar.Server;
using DWIS.RigOS.Common.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace DWIS.DAQBridge.BaseStar.Tests;

public class DataLoggingTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
    private static MechanicalSubOutputDataMudPulseTelemetry Data(double value = 123.5) => new()
    {
        AnnulusPressure = new ScalarProperty { Value = value }, MaxRawWeight = new ScalarProperty { Value = 456.25 }
    };

    [Fact]
    public void SnapshotIsImmutableCompactAndIgnoresSemanticMetadata()
    {
        var data = Data();
        var snapshot = BaseStarSnapshot.Capture(data);
        data.AnnulusPressure!.Value = 42;
        Assert.NotEqual(snapshot, BaseStarSnapshot.Capture(data));
        data.AnnulusPressure.Value = 123.5;
        data.AnnulusPressure.SparQLQuery = "CHANGED QUERY";
        Assert.Equal(snapshot, BaseStarSnapshot.Capture(data));
        var entry = snapshot.ToEntry(Start);
        Assert.Equal(snapshot, BaseStarSnapshot.ReadLast(entry.SchemaLine + "\n" + entry.RecordLine));
        using var record = JsonDocument.Parse(entry.RecordLine);
        Assert.Equal(new[] { "t", "v" }, record.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(26, record.RootElement.GetProperty("v").GetArrayLength());
        Assert.DoesNotContain("SparQL", entry.RecordLine);
        Assert.DoesNotContain("Manifest", entry.RecordLine);
        Assert.DoesNotContain("HasDynamicValue", entry.SchemaLine);
    }

    [Fact]
    public void NonFiniteValuesAreNotLoggedAsInvalidJsonOrPublished()
    {
        var data = new MechanicalSubOutputDataMudPulseTelemetry { MaxRawWeight = new ScalarProperty { Value = double.NaN } };
        var snapshot = BaseStarSnapshot.Capture(data);
        Assert.False(snapshot.HasValues);
        Assert.Empty(snapshot.ChangesFrom(null));
        using var json = JsonDocument.Parse(snapshot.ToEntry(Start).RecordLine);
    }

    [Fact]
    public async Task FilesRotateAtUtcBoundaryCompressAndKeepOneSchema()
    {
        using var temp = new TemporaryDirectory();
        var writer = new BaseStarDataLogWriter(temp.Path, TimeSpan.FromHours(6), 1024 * 1024);
        var snapshot = BaseStarSnapshot.Capture(Data());
        await writer.AppendAsync(BaseStarHistoryDataKind.Raw, Start.AddHours(5), snapshot.ToEntry(Start.AddHours(5)));
        await writer.AppendAsync(BaseStarHistoryDataKind.Raw, Start.AddHours(5).AddMinutes(1), snapshot.ToEntry(Start.AddHours(5).AddMinutes(1)));
        await writer.MaintainAsync(Start.AddHours(6)); // Rotation also runs without a new data change.
        Assert.Empty(Directory.GetFiles(temp.Path, "*.jsonl"));
        using var archive = ZipFile.OpenRead(Assert.Single(Directory.GetFiles(temp.Path, "*.zip")));
        using var reader = new StreamReader(Assert.Single(archive.Entries).Open());
        string[] lines = (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal(snapshot, BaseStarSnapshot.ReadLast(string.Join("\n", lines)));
        await writer.AppendAsync(BaseStarHistoryDataKind.Raw, Start.AddHours(6).ToOffset(TimeSpan.FromHours(2)), snapshot.ToEntry(Start.AddHours(6)));
        Assert.EndsWith("20261017T060000Z.jsonl", Assert.Single(Directory.GetFiles(temp.Path, "*.jsonl")));
    }

    [Fact]
    public async Task StorageCapRemovesOldestOwnedFilesAndCanRemoveOversizedActiveFile()
    {
        using var temp = new TemporaryDirectory();
        var writer = new BaseStarDataLogWriter(temp.Path, TimeSpan.FromHours(6), 1024 * 1024);
        var entry = BaseStarSnapshot.Capture(Data()).ToEntry(Start);
        await writer.AppendAsync(BaseStarHistoryDataKind.Raw, Start, entry);
        await writer.AppendAsync(BaseStarHistoryDataKind.Raw, Start.AddHours(6), entry);
        string active = Assert.Single(Directory.GetFiles(temp.Path, "*.jsonl"));
        long activeBytes = new FileInfo(active).Length;
        string config = System.IO.Path.Combine(temp.Path, "config.json");
        await File.WriteAllTextAsync(config, "preserve");
        string foreign = System.IO.Path.Combine(temp.Path, "basestar-raw-not-a-timestamp.jsonl");
        await File.WriteAllTextAsync(foreign, "preserve");
        writer = new BaseStarDataLogWriter(temp.Path, TimeSpan.FromHours(6), activeBytes);
        await writer.MaintainAsync(Start.AddHours(6));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.zip"));
        Assert.True(File.Exists(active));
        writer = new BaseStarDataLogWriter(temp.Path, TimeSpan.FromHours(6), 1);
        await writer.MaintainAsync(Start.AddHours(6));
        Assert.False(File.Exists(active));
        Assert.Equal("preserve", await File.ReadAllTextAsync(config));
        Assert.Equal("preserve", await File.ReadAllTextAsync(foreign));
    }

    [Fact]
    public async Task LateAppendPreservesArchivedRecords()
    {
        using var temp = new TemporaryDirectory();
        var writer = new BaseStarDataLogWriter(temp.Path, TimeSpan.FromHours(6), 1024 * 1024);
        var entry = BaseStarSnapshot.Capture(Data()).ToEntry(Start);
        await writer.AppendAsync(BaseStarHistoryDataKind.Raw, Start, entry);
        await writer.MaintainAsync(Start.AddHours(6));
        await writer.AppendAsync(BaseStarHistoryDataKind.Raw, Start.AddMinutes(1), entry);
        await writer.MaintainAsync(Start.AddHours(6));
        using var zip = ZipFile.OpenRead(Assert.Single(Directory.GetFiles(temp.Path, "*.zip")));
        using var reader = new StreamReader(Assert.Single(zip.Entries).Open());
        Assert.Equal(3, (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void HistoryHas16PureTagsAndReusesSlotsAfter48Hours()
    {
        var manifest = BaseStarBlackboardHistory.CreateManifest();
        Assert.Equal(16, manifest.ProvidedVariables.Count);
        Assert.All(manifest.ProvidedVariables, v => Assert.Equal("string", v.DataType));
        Assert.Empty(manifest.InjectedNodes);
        Assert.Empty(manifest.InjectedReferences);
        Assert.Equal(BaseStarBlackboardHistory.GetBucketSlot(Start), BaseStarBlackboardHistory.GetBucketSlot(Start.AddHours(48)));
        var entry = BaseStarSnapshot.Capture(Data()).ToEntry(Start);
        string first = BaseStarBlackboardHistory.Append(null, Start, entry);
        string next = BaseStarBlackboardHistory.Append(first, Start.AddHours(48), entry);
        Assert.True(BaseStarBlackboardHistory.TryDeserialize(next, out var bucket));
        Assert.Equal(Start.AddHours(48), bucket!.BucketStartUtc);
        Assert.Equal(2, bucket.JsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void HistoryRestoresExistingRecordsExpiresWithoutNewDataAndRetriesRejectedWrites()
    {
        var client = DispatchProxy.Create<IOPCUADWISClient, ClientStub>();
        var stub = (ClientStub)client;
        var history = new BaseStarBlackboardHistoryPublisher();
        var snapshot = BaseStarSnapshot.Capture(Data());
        history.Initialize(client, Start);
        history.Append(BaseStarHistoryDataKind.Raw, Start, snapshot.ToEntry(Start));
        history.Initialize(client, Start.AddMinutes(1));
        Assert.Equal(snapshot, history.Latest(BaseStarHistoryDataKind.Raw));
        stub.Reject = true;
        Assert.Throws<InvalidOperationException>(() => history.Append(BaseStarHistoryDataKind.Raw, Start, snapshot.ToEntry(Start)));
        stub.Reject = false;
        history.Append(BaseStarHistoryDataKind.Raw, Start, snapshot.ToEntry(Start));
        string payload = stub.History.Values.Single(v => !string.IsNullOrEmpty(v));
        Assert.True(BaseStarBlackboardHistory.TryDeserialize(payload, out var bucket));
        Assert.Equal(3, bucket!.JsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        history.Maintain(Start.AddHours(48));
        Assert.All(stub.History.Values, v => Assert.Equal("", v));
    }

    [Fact]
    public async Task WorkerLogsOnlyChangesAndPublishesOnlyChangedSignals()
    {
        using var temp = new TemporaryDirectory();
        using var worker = new TestWorker(temp.Path);
        var data = Data();
        await worker.Sample(data, Start);
        Assert.Equal(2, worker.Stub.SignalWrites);
        await worker.Sample(data, Start.AddSeconds(1));
        await worker.Sample(data, Start.AddSeconds(2));
        Assert.Equal(2, worker.Stub.SignalWrites);
        Assert.All(Directory.GetFiles(temp.Path, "*.jsonl"), path => Assert.Equal(2, File.ReadAllLines(path).Length));
        data.MaxRawWeight!.Value = 999;
        await worker.Sample(data, Start.AddSeconds(3));
        Assert.Equal(3, worker.Stub.SignalWrites);
        Assert.All(Directory.GetFiles(temp.Path, "*.jsonl"), path => Assert.Equal(3, File.ReadAllLines(path).Length));
        await worker.Sample(data, Start.AddHours(48));
        Assert.Equal(3, worker.Stub.SignalWrites);
        Assert.All(worker.Stub.History.Values, v => Assert.True(string.IsNullOrEmpty(v)));
    }

    [Fact]
    public async Task WorkerRetriesRejectedSignalWithoutDuplicatingRawRecords()
    {
        using var temp = new TemporaryDirectory();
        using var worker = new TestWorker(temp.Path);
        var data = Data();
        worker.Stub.RejectSignals = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.Sample(data, Start));
        worker.Stub.RejectSignals = false;
        await worker.Sample(data, Start.AddSeconds(1));
        Assert.Equal(2, File.ReadAllLines(Assert.Single(Directory.GetFiles(temp.Path, "basestar-raw-*.jsonl"))).Length);
        Assert.Equal(2, File.ReadAllLines(Assert.Single(Directory.GetFiles(temp.Path, "basestar-published-*.jsonl"))).Length);
    }

    [Fact]
    public async Task HistoryExpiresWhileSourceIsOffline()
    {
        using var temp = new TemporaryDirectory();
        using var worker = new TestWorker(temp.Path);
        await worker.Sample(Data(), Start);
        worker.MaintainWithoutInput(Start.AddHours(48));
        Assert.All(worker.Stub.History.Values, value => Assert.Equal("", value));
    }

    [Fact]
    public async Task FileLoggingFailureDoesNotBlockSignalPublication()
    {
        using var temp = new TemporaryDirectory();
        string file = System.IO.Path.Combine(temp.Path, "not-a-directory");
        await File.WriteAllTextAsync(file, "preserve");
        using var worker = new TestWorker(file);
        await worker.Sample(Data(), Start);
        await worker.Sample(Data(), Start.AddSeconds(1));
        Assert.Equal(2, worker.Stub.SignalWrites);
        Assert.Equal("preserve", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public void ConfigurationDefaultsMatchRetentionContract()
    {
        var config = new ConfigurationForBaseStar();
        Assert.True(config.EnableDataLogging);
        Assert.True(config.EnableBlackboardHistory);
        Assert.Equal("/home", config.DataLogDirectory);
        Assert.Equal(TimeSpan.FromHours(6), config.DataLogSegmentDuration);
        Assert.Equal(1024L * 1024 * 1024, config.MaximumDataLogStorageBytes);
    }

    private sealed class TestWorker : Worker
    {
        public ClientStub Stub { get; }
        public TestWorker(string directory) : base(NullLogger<IDWISWorker<ConfigurationForBaseStar>>.Instance, null)
        {
            Configuration = new() { DataLogDirectory = directory };
            _OPCUAClient = _DWISClient = DispatchProxy.Create<IOPCUADWISClient, ClientStub>();
            Stub = (ClientStub)_DWISClient;
            foreach (var property in BaseStarSnapshot.Properties)
            {
                var result = new QueryResult();
                result.Add(new QueryResultRow { Items = new() { new() { NameSpace = "outputs", ID = property.Name } } });
                PlaceHolders.Add(property, result);
            }
        }
        public void MaintainWithoutInput(DateTimeOffset time)
        {
            _OPCUAClient = null;
            MaintainBlackboardHistory(time);
        }
        public Task Sample(MechanicalSubOutputDataMudPulseTelemetry data, DateTimeOffset time) => ProcessSampleAsync(data, time, true, CancellationToken.None);
    }

    public class ClientStub : DispatchProxy
    {
        public Dictionary<string, string> History { get; } = new();
        public int SignalWrites { get; private set; }
        public bool Reject { get; set; }
        public bool RejectSignals { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "get_Connected": return true;
                case "Inject":
                    var manifest = (ManifestFile)args![0]!;
                    return new ManifestInjectionResult
                    {
                        Success = true,
                        ProvidedVariables = manifest.ProvidedVariables.Select(v => new InjectionMapping
                        {
                            ManifestItemID = v.VariableID,
                            InjectedID = new DWISNodeID { NameSpaceIndex = 2, ID = v.VariableID }
                        }).ToList()
                    };
                case "ReadAnyVariables":
                    var nodes = ((ushort, string)[])args![1]!;
                    args[0] = new object[] { History.GetValueOrDefault(nodes[0].Item2, "") };
                    return true;
                case "UpdateProvidedVariables":
                    if (Reject) return false;
                    foreach (var (id, value, time) in (IList<(string, object, DateTime)>)args![0]!) History[id] = (string)value;
                    return true;
                case "UpdateAnyVariables":
                    if (RejectSignals) return false;
                    SignalWrites += (((string, string, object, DateTime)[])args![0]!).Length;
                    return true;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "basestar-tests-" + Guid.NewGuid());
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
