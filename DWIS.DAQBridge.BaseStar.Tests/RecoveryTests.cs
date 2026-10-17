using System.Reflection;
using DWIS.Client.ReferenceImplementation;
using DWIS.DAQBridge.BaseStar.Server;
using DWIS.RigOS.Common.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace DWIS.DAQBridge.BaseStar.Tests;

public class RecoveryTests
{
    [Fact]
    public void MissingConfigurationKeepsWorkerInRetryMode()
    {
        using TestWorker worker = new();
        worker.Configuration = null;
        Assert.False(Establish(worker));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void DisconnectedClientsAreRetried(bool opcConnected, bool blackboardConnected)
    {
        using TestWorker worker = new();
        ClientStub opc = worker.SetClients(opcConnected, blackboardConnected, out ClientStub blackboard);
        Assert.True(Establish(worker));
        Assert.Equal(opcConnected ? 0 : 1, opc.Reconnects);
        Assert.Equal(blackboardConnected ? 0 : 1, blackboard.Reconnects);
    }

    [Fact]
    public void FailedReconnectDoesNotStopWorkerOrSkipOtherConnection()
    {
        using TestWorker worker = new();
        ClientStub opc = worker.SetClients(false, false, out ClientStub blackboard);
        opc.ThrowOnReconnect = true;
        Assert.False(Establish(worker));
        Assert.Equal(1, blackboard.Reconnects);
    }

    [Fact]
    public async Task LoopDoesNotReadOrPublishWhileDisconnected()
    {
        using TestWorker worker = new();
        worker.SetClients(false, true, out _);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        await worker.RunLoop(timeout.Token);
        Assert.Equal(0, worker.Reads);
    }

    [Fact]
    public async Task ReadFailureEndsSessionAndRequiresRecovery()
    {
        using TestWorker worker = new();
        worker.SetClients(true, true, out _);
        worker.FailRead = true;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        await worker.RunLoop(timeout.Token);
        Assert.Equal(1, worker.Reads);
        foreach (string name in new[] { "_opcConnectionNeedsRecovery", "_blackboardConnectionNeedsRecovery" })
        {
            Assert.True((bool)typeof(Worker).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(worker)!);
        }
    }

    [Fact]
    public async Task ConnectionLostDuringReadSkipsPublication()
    {
        using TestWorker worker = new();
        worker.SetClients(true, true, out _);
        worker.DisconnectDuringRead = true;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        await worker.RunLoop(timeout.Token);
        Assert.Equal(1, worker.Reads);
        Assert.Equal(0, worker.Publishes);
    }

    [Fact]
    public async Task PublishFailureEndsSession()
    {
        using TestWorker worker = new();
        worker.SetClients(true, true, out _);
        worker.FailPublish = true;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        await worker.RunLoop(timeout.Token);
        Assert.Equal(1, worker.Publishes);
    }

    [Fact]
    public async Task RetryWaitHonorsShutdown()
    {
        using TestWorker worker = new();
        worker.Configuration!.ConnectionRetryInterval = TimeSpan.FromHours(1);
        using CancellationTokenSource shutdown = new();
        Task wait = (Task)typeof(Worker).GetMethod("WaitBeforeReconnectAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(worker, new object[] { shutdown.Token })!;
        shutdown.Cancel();
        await wait.WaitAsync(TimeSpan.FromSeconds(3));
    }

    private static bool Establish(Worker worker) => (bool)typeof(Worker)
        .GetMethod("TryEstablishConnections", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(worker, null)!;

    private sealed class TestWorker : Worker
    {
        public int Reads { get; private set; }
        public int Publishes { get; private set; }
        public bool FailPublish { get; set; }
        public bool DisconnectDuringRead { get; set; }
        public bool FailRead { get; set; }
        public TestWorker() : base(NullLogger<IDWISWorker<ConfigurationForBaseStar>>.Instance, null)
        {
            Configuration = new ConfigurationForBaseStar { EnableDataLogging = false, EnableBlackboardHistory = false };
        }
        public ClientStub SetClients(bool opcConnected, bool blackboardConnected, out ClientStub blackboard)
        {
            _OPCUAClient = DispatchProxy.Create<IOPCUADWISClient, ClientStub>();
            _DWISClient = DispatchProxy.Create<IOPCUADWISClient, ClientStub>();
            ClientStub opc = (ClientStub)_OPCUAClient;
            blackboard = (ClientStub)_DWISClient;
            opc.Connected = opcConnected;
            blackboard.Connected = blackboardConnected;
            return opc;
        }
        protected override Task PublishBlackboardAsync(DWISData data, CancellationToken token)
        {
            Publishes++;
            if (FailPublish) throw new InvalidOperationException("Simulated publish failure");
            return Task.CompletedTask;
        }
        public Task RunLoop(CancellationToken token) => Loop(token);
        protected override Task ReadOPCUA(DWISDataWithOPCUA? data, string? ns, string? prefix,
            Dictionary<string, string>? ids, Dictionary<string, UnitConversion>? conversions)
        {
            Reads++;
            ((DWIS.DAQBridge.BaseStar.Model.MechanicalSubOutputDataMudPulseTelemetry)data!).AnnulusPressure = new ScalarProperty { Value = 1 };
            if (DisconnectDuringRead) ((ClientStub)_OPCUAClient!).Connected = false;
            if (FailRead) throw new InvalidOperationException("Simulated transport failure");
            return Task.CompletedTask;
        }
    }

    public class ClientStub : DispatchProxy
    {
        public bool Connected { get; set; }
        public int Reconnects { get; set; }
        public bool ThrowOnReconnect { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_Connected") return Connected;
            if (method.Name == "TryReconnect")
            {
                Reconnects++;
                if (ThrowOnReconnect) throw new InvalidOperationException("Unavailable");
                Connected = true;
                return true;
            }
            throw new NotSupportedException(method.Name);
        }
    }
}
