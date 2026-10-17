using DWIS.Client.ReferenceImplementation;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.Server;

internal sealed class BaseStarBlackboardHistoryPublisher
{
    private readonly Dictionary<string, string?> _payloads = new(StringComparer.Ordinal);
    private IOPCUADWISClient? _client;
    public void Initialize(IOPCUADWISClient client, DateTimeOffset nowUtc)
    {
        var result = client.Inject(BaseStarBlackboardHistory.CreateManifest());
        if (result?.Success != true || result.ProvidedVariables is null ||
            !result.ProvidedVariables.Select(m => m.ManifestItemID).Order().SequenceEqual(BaseStarBlackboardHistory.NodeIds.Order()) ||
            result.ProvidedVariables.Any(m => m.InjectedID is null))
            throw new InvalidOperationException("BaseStar history tags could not be registered completely.");
        _client = client;
        _payloads.Clear();
        foreach (var mapping in result.ProvidedVariables)
        {
            if (!client.ReadAnyVariables(out object[] values, new[] { (mapping.InjectedID.NameSpaceIndex, mapping.InjectedID.ID) }))
                throw new InvalidOperationException($"Could not read BaseStar history '{mapping.ManifestItemID}'.");
            _payloads[mapping.ManifestItemID] = values.FirstOrDefault() as string;
        }
        Maintain(nowUtc);
    }
    public BaseStarSnapshot? Latest(BaseStarHistoryDataKind kind) => _payloads
        .Where(p => p.Key.StartsWith($"BaseStar.{kind}.History.", StringComparison.Ordinal))
        .Select(p => BaseStarBlackboardHistory.TryDeserialize(p.Value, out var bucket) ? bucket : null)
        .Where(b => b is not null).OrderByDescending(b => b!.BucketStartUtc)
        .Select(b => BaseStarSnapshot.ReadLast(b!.JsonLines)).FirstOrDefault(s => s is not null);

    public void Maintain(DateTimeOffset nowUtc)
    {
        DateTimeOffset current = BaseStarBlackboardHistory.GetBucketStart(nowUtc);
        DateTimeOffset oldest = current - BaseStarBlackboardHistory.BucketDuration * (BaseStarBlackboardHistory.BucketCount - 1);
        foreach (var (id, payload) in _payloads.ToArray())
        {
            if (string.IsNullOrEmpty(payload)) continue;
            if (!BaseStarBlackboardHistory.TryDeserialize(payload, out var bucket) || bucket is null ||
                bucket.BucketStartUtc < oldest || bucket.BucketStartUtc > current ||
                BaseStarBlackboardHistory.GetBucketStart(bucket.BucketStartUtc) != bucket.BucketStartUtc ||
                !id.EndsWith("." + BaseStarBlackboardHistory.GetBucketSlot(bucket.BucketStartUtc), StringComparison.Ordinal))
                Write(id, "", nowUtc);
        }
    }
    public void Append(BaseStarHistoryDataKind kind, DateTimeOffset timestampUtc, BaseStarLogEntry entry)
    {
        string id = BaseStarBlackboardHistory.GetNodeId(kind,
            BaseStarBlackboardHistory.GetBucketSlot(BaseStarBlackboardHistory.GetBucketStart(timestampUtc)));
        _payloads.TryGetValue(id, out string? previous);
        Write(id, BaseStarBlackboardHistory.Append(previous, timestampUtc, entry), timestampUtc);
    }
    private void Write(string id, string payload, DateTimeOffset timestampUtc)
    {
        if (_client?.Connected != true || !_client.UpdateProvidedVariables(new List<(string, object, DateTime)>
            { (id, payload, timestampUtc.UtcDateTime) }))
            throw new InvalidOperationException($"Blackboard rejected BaseStar history update '{id}'.");
        // Commit only after success; a rejected write must not enter the local history cache.
        _payloads[id] = payload;
    }
}
