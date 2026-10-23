using DWIS.Client.ReferenceImplementation;
using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.Server
{
    public class Worker : DWISWorkerWithOPCUA<ConfigurationForBaseStar, object>
    {
    private MechanicalSubOutputData MechanicalSubData { get; set; } = new MechanicalSubOutputData();

        private BaseStarSnapshot? _lastPublished;
        private BaseStarSnapshot? _lastRawFile;
        private BaseStarSnapshot? _lastPublishedFile;
        private BaseStarSnapshot? _lastRawHistory;
        private BaseStarSnapshot? _lastPublishedHistory;
        private DateTimeOffset? _lastMudPulsePublication;
        private DateTimeOffset? _lastWiredPipePublication;
        private BaseStarDataLogWriter? _dataLogWriter;
        private string? _dataLogSettingsKey;
        private readonly BaseStarBlackboardHistoryPublisher _historyPublisher = new();
        private bool _historyInitialized;
        private IOPCUADWISClient? _historyClient;

        private bool _opcConnectionNeedsRecovery;
        private bool _blackboardConnectionNeedsRecovery;

        private TimeSpan OPCUALoopSpan { get; set; } = TimeSpan.FromSeconds(1);

        public Worker(ILogger<IDWISWorker<ConfigurationForBaseStar>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConfigurationUpdater<ConfigurationForBaseStar>.Instance.UpdateConfiguration(this);

                await MaintainFilesAsync(DateTimeOffset.UtcNow);

                if (!TryEstablishConnections())
                {
                    // History still expires when the source is offline but the Blackboard is reachable.
                    try { MaintainBlackboardHistory(DateTimeOffset.UtcNow); }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning(ex, "Could not maintain BaseStar history while reconnecting.");
                        _blackboardConnectionNeedsRecovery = true;
                        _historyInitialized = false;
                    }
                    await WaitBeforeReconnectAsync(stoppingToken);
                    continue;
                }

                try
                {
                    await InitializeConnectedSessionAsync();
                    if (!ConnectionsAreEstablished)
                    {
                        Logger?.LogWarning("A BaseStar bridge connection was lost during session initialization. Returning to connection retry mode.");
                        await WaitBeforeReconnectAsync(stoppingToken);
                        continue;
                    }

                    Logger?.LogInformation("Both the BaseStar OPC-UA endpoint and DWIS Blackboard are connected. Starting the bridge exchange loop.");
                    await Loop(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "The BaseStar bridge session failed. Returning to connection retry mode.");
                    _opcConnectionNeedsRecovery = true;
                    _blackboardConnectionNeedsRecovery = true;
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    Logger?.LogWarning("The BaseStar bridge exchange session ended. Returning to connection retry mode.");
                    await WaitBeforeReconnectAsync(stoppingToken);
                }
            }
        }

        private bool TryEstablishConnections()
        {
            if (Configuration is null)
            {
                Logger?.LogWarning("BaseStar configuration is not available. Waiting before trying again.");
                return false;
            }

            bool opcConnected = TryConnect(
                () => _OPCUAClient,
                ConnectToOPCUA,
                "BaseStar OPC-UA endpoint",
                _opcConnectionNeedsRecovery);
            bool blackboardConnected = TryConnect(
                () => _DWISClient,
                ConnectToBlackboard,
                "DWIS Blackboard",
                _blackboardConnectionNeedsRecovery);

            _opcConnectionNeedsRecovery = !opcConnected;
            _blackboardConnectionNeedsRecovery = !blackboardConnected;

            if (!opcConnected || !blackboardConnected)
            {
                Logger?.LogWarning(
                    "Waiting for bridge connections. BaseStar OPC-UA connected={OPCUAConnected}; DWIS Blackboard connected={BlackboardConnected}.",
                    opcConnected,
                    blackboardConnected);
            }

            return opcConnected && blackboardConnected;
        }

        private bool TryConnect(
            Func<IOPCUADWISClient?> getClient,
            Action createClient,
            string connectionName,
            bool forceReconnect)
        {
            IOPCUADWISClient? client = getClient();
            if (forceReconnect)
            {
                try
                {
                    createClient();
                    return getClient()?.Connected == true;
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "Could not replace the failed {ConnectionName} session.", connectionName);
                    return false;
                }
            }

            if (client?.Connected == true)
            {
                return true;
            }

            try
            {
                if (client is null)
                {
                    createClient();
                }
                else
                {
                    return client.TryReconnect() && client.Connected;
                }

                return getClient()?.Connected == true;
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Could not connect to the {ConnectionName}.", connectionName);
                return false;
            }
        }

        private bool ConnectionsAreEstablished =>
            _OPCUAClient?.Connected == true && _DWISClient?.Connected == true;

        private async Task InitializeConnectedSessionAsync()
        {
            OPCUALoopSpan = Configuration!.OPCUALoopDuration > TimeSpan.Zero
                ? Configuration.OPCUALoopDuration : TimeSpan.FromSeconds(1);
            LoopSpan = Configuration.LoopDuration;
            if (Configuration.InitializeInputOPCUAVariables)
            {
                await RegisterToOPCUA(MechanicalSubData, "BaseStarDataManifest", "Halliburton");
            }
            // Targets belong to the current Blackboard session and must be resolved again.
            PlaceHolders.Clear();
            await RegisterToBlackboard(MechanicalSubData, false);
            // Re-synchronize live values once for the new session, but keep change-only file logs.
            _lastPublished = null;
            _lastMudPulsePublication = null;
            _lastWiredPipePublication = null;
            _historyInitialized = false;
            EnsureHistoryInitialized(DateTimeOffset.UtcNow);
        }

        private async Task WaitBeforeReconnectAsync(CancellationToken stoppingToken)
        {
            TimeSpan retryInterval = Configuration?.ConnectionRetryInterval > TimeSpan.Zero
                ? Configuration.ConnectionRetryInterval
                : TimeSpan.FromSeconds(5);

            try
            {
                await Task.Delay(retryInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal host shutdown while waiting for an endpoint.
            }
        }

        protected override async Task Loop(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(OPCUALoopSpan);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!ConnectionsAreEstablished)
                {
                    return;
                }
                try
                {
                    await ReadOPCUA(MechanicalSubData, Configuration?.NameSpace, Configuration?.NodeIDPrefix, Configuration?.OPCUAIDs, Configuration?.UnitConversions);
                    if (!ConnectionsAreEstablished)
                    {
                        return;
                    }
                    await ProcessSampleAsync(MechanicalSubData, DateTimeOffset.UtcNow, false, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger?.LogError(e, "BaseStar bridge exchange failed. Returning to connection retry mode.");
                    // Connected may remain true briefly after a transport operation fails.
                    _opcConnectionNeedsRecovery = true;
                    _blackboardConnectionNeedsRecovery = true;
                    return;
                }
                ConfigurationUpdater<ConfigurationForBaseStar>.Instance.UpdateConfiguration(this);
            }
        }

        protected override string? GetNameSpace(OPCUANode node, string? configuredNameSpace)
        {
            if (!string.IsNullOrEmpty(node.ID) &&
                Configuration?.OPCUANameSpaces?.TryGetValue(node.ID, out string? signalNameSpace) == true &&
                !string.IsNullOrWhiteSpace(signalNameSpace))
            {
                return signalNameSpace;
            }
            return base.GetNameSpace(node, configuredNameSpace);
        }

        private void EnsureHistoryInitialized(DateTimeOffset nowUtc)
        {
            if (Configuration?.EnableBlackboardHistory != true)
            {
                _historyInitialized = false;
                return;
            }
            if (_historyInitialized && ReferenceEquals(_historyClient, _DWISClient)) return;
            _historyPublisher.Initialize(_DWISClient!, nowUtc);
            _lastRawHistory = _historyPublisher.Latest(BaseStarHistoryDataKind.Raw);
            _lastPublishedHistory = _historyPublisher.Latest(BaseStarHistoryDataKind.Published);
            _historyClient = _DWISClient;
            _historyInitialized = true;
        }

        protected void MaintainBlackboardHistory(DateTimeOffset nowUtc)
        {
            if (_DWISClient?.Connected != true) return;
            EnsureHistoryInitialized(nowUtc);
            if (_historyInitialized) _historyPublisher.Maintain(nowUtc);
        }

    protected async Task ProcessSampleAsync(MechanicalSubOutputData data,
            DateTimeOffset timestampUtc, bool forcePublication, CancellationToken stoppingToken)
        {
            await MaintainFilesAsync(timestampUtc);
            MaintainBlackboardHistory(timestampUtc);
            BaseStarSnapshot snapshot = BaseStarSnapshot.Capture(data);
            if (!snapshot.HasValues) return;

            if (!snapshot.Equals(_lastRawFile) && await TryLogAsync(BaseStarHistoryDataKind.Raw, timestampUtc, snapshot))
                _lastRawFile = snapshot;
            if (_historyInitialized && !snapshot.Equals(_lastRawHistory))
            {
                _historyPublisher.Append(BaseStarHistoryDataKind.Raw, timestampUtc, snapshot.ToEntry(timestampUtc));
                _lastRawHistory = snapshot;
            }

            bool mudPulseDue = forcePublication || IsDue(timestampUtc, _lastMudPulsePublication,
                Configuration?.MudPulsePublishInterval, TimeSpan.FromSeconds(30));
            bool wiredPipeDue = forcePublication || IsDue(timestampUtc, _lastWiredPipePublication,
                Configuration?.WiredPipePublishInterval, TimeSpan.FromSeconds(2.5));
            if (mudPulseDue) _lastMudPulsePublication = timestampUtc;
            if (wiredPipeDue) _lastWiredPipePublication = timestampUtc;
            int[] changes = snapshot.ChangesFrom(_lastPublished).Where(index =>
                BaseStarSnapshot.Properties[index].Name.EndsWith("WiredPipe", StringComparison.Ordinal)
                    ? wiredPipeDue : mudPulseDue).ToArray();
            if (changes.Length > 0)
            {
                await PublishBlackboardChangesAsync(snapshot, changes, stoppingToken);
                if (!ConnectionsAreEstablished) return;
                _lastPublished = BaseStarSnapshot.Merge(_lastPublished, snapshot, changes);
            }
            if (_lastPublished is null) return;
            if (!_lastPublished.Equals(_lastPublishedFile) &&
                await TryLogAsync(BaseStarHistoryDataKind.Published, timestampUtc, _lastPublished))
                _lastPublishedFile = _lastPublished;
            if (_historyInitialized && !_lastPublished.Equals(_lastPublishedHistory))
            {
                _historyPublisher.Append(BaseStarHistoryDataKind.Published, timestampUtc, _lastPublished.ToEntry(timestampUtc));
                _lastPublishedHistory = _lastPublished;
            }
        }

        protected override async Task PublishBlackboardAsync(DWISData data, CancellationToken stoppingToken)
        {
            BaseStarSnapshot snapshot = BaseStarSnapshot.Capture(data);
            int[] changes = snapshot.ChangesFrom(_lastPublished).ToArray();
            await PublishBlackboardChangesAsync(snapshot, changes, stoppingToken);
            _lastPublished = BaseStarSnapshot.Merge(_lastPublished, snapshot, changes);
        }

        protected virtual async Task PublishBlackboardChangesAsync(BaseStarSnapshot snapshot, IEnumerable<int> changes,
            CancellationToken stoppingToken)
        {
            foreach (int index in changes)
            {
                stoppingToken.ThrowIfCancellationRequested();
                var property = BaseStarSnapshot.Properties[index];
                if (!PlaceHolders.TryGetValue(property, out var targets) || !SendValue(targets, snapshot[index]))
                    throw new InvalidOperationException($"Could not publish BaseStar signal '{property.Name}'.");
                await Task.Yield();
            }
        }

        private static bool IsDue(DateTimeOffset now, DateTimeOffset? previous, TimeSpan? configured, TimeSpan fallback)
        {
            TimeSpan interval = configured > TimeSpan.Zero ? configured.Value : fallback;
            return previous is null || now - previous.Value >= interval;
        }

        private BaseStarDataLogWriter? GetDataLogWriter()
        {
            if (Configuration?.EnableDataLogging != true) return null;
            string settings = $"{Configuration.DataLogDirectory}|{Configuration.DataLogSegmentDuration.Ticks}|{Configuration.MaximumDataLogStorageBytes}";
            if (_dataLogWriter is null || settings != _dataLogSettingsKey)
            {
                _dataLogWriter = new(Configuration.DataLogDirectory, Configuration.DataLogSegmentDuration,
                    Configuration.MaximumDataLogStorageBytes);
                _dataLogSettingsKey = settings;
                _lastRawFile = null;
                _lastPublishedFile = null;
            }
            return _dataLogWriter;
        }
        private async Task MaintainFilesAsync(DateTimeOffset nowUtc)
        {
            try
            {
                var writer = GetDataLogWriter();
                if (writer is not null) await writer.MaintainAsync(nowUtc);
            }
            catch (Exception ex) { Logger?.LogError(ex, "Could not maintain the compact BaseStar data logs."); }
        }
        private async Task<bool> TryLogAsync(BaseStarHistoryDataKind kind, DateTimeOffset timestampUtc, BaseStarSnapshot snapshot)
        {
            try
            {
                var writer = GetDataLogWriter();
                if (writer is null) return false;
                await writer.AppendAsync(kind, timestampUtc, snapshot.ToEntry(timestampUtc));
                return true;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not append the compact BaseStar {Kind} data log.", kind);
                return false;
            }
        }
    }
}
