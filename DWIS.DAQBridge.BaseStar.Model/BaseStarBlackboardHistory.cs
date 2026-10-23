using System.Text.Json;
using DWIS.API.DTO;

namespace DWIS.DAQBridge.BaseStar.Model;

public static class BaseStarBlackboardHistory
{
    public const int BucketCount = 8;
    public static readonly TimeSpan BucketDuration = TimeSpan.FromHours(6);
    private const string NodePrefix = "BaseStar";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<string> NodeIds =>
        from kind in Enum.GetValues<BaseStarHistoryDataKind>()
        from slot in Enumerable.Range(0, BucketCount)
        select GetNodeId(kind, slot);

    public static string GetNodeId(BaseStarHistoryDataKind kind, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, BucketCount);
        return $"{NodePrefix}.{kind}.History.{slot}";
    }

    public static DateTimeOffset GetBucketStart(DateTimeOffset timestampUtc)
    {
        long ticks = BucketDuration.Ticks;
        return new DateTimeOffset((timestampUtc.UtcTicks / ticks) * ticks, TimeSpan.Zero);
    }

    public static int GetBucketSlot(DateTimeOffset bucketStartUtc)
    {
        return (int)((bucketStartUtc.UtcTicks / BucketDuration.Ticks) % BucketCount);
    }

    public static string Append(
        string? existingPayload,
        DateTimeOffset timestampUtc,
        BaseStarLogEntry entry)
    {
        DateTimeOffset bucketStart = GetBucketStart(timestampUtc);
        string jsonLines;
        if (TryDeserialize(existingPayload, out BaseStarHistoryBucketPayload? existing) &&
            existing is not null &&
            existing.BucketStartUtc == bucketStart &&
            existing.JsonLines.Split('\n', 2)[0].TrimEnd('\r') == entry.SchemaLine)
        {
            jsonLines = existing.JsonLines.TrimEnd('\r', '\n') + Environment.NewLine + entry.RecordLine + Environment.NewLine;
        }
        else
        {
            jsonLines = entry.SchemaLine + Environment.NewLine + entry.RecordLine + Environment.NewLine;
        }

        return JsonSerializer.Serialize(new BaseStarHistoryBucketPayload(bucketStart, jsonLines), JsonOptions);
    }

    public static bool TryDeserialize(string? payload, out BaseStarHistoryBucketPayload? bucket)
    {
        bucket = null;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            bucket = JsonSerializer.Deserialize<BaseStarHistoryBucketPayload>(payload, JsonOptions);
            return bucket is not null && !string.IsNullOrWhiteSpace(bucket.JsonLines);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static ManifestFile CreateManifest()
    {
        return new ManifestFile
        {
            ManifestName = "BaseStarHistory",
            Provider = new InjectionProvider { Company = "DigiWells", Name = "BaseStarHistory" },
            InjectionInformation = new InjectionInformation
            {
                ServerName = "BaseStar",
                EndPointURL = string.Empty,
                InjectedNodesNamespaceAlias = "BaseStarNodes",
                InjectedVariablesNamespaceAlias = "BaseStarVariables",
                ProvidedVariablesNamespaceAlias = "BaseStar"
            },
            InjectedNodes = new List<InjectedNode>(),
            InjectedReferences = new List<InjectedReference>(),
            InjectedVariables = new List<InjectedVariable>(),
            ProvidedVariables = NodeIds
                .Select(id => new ProvidedVariable { DataType = "string", VariableID = id })
                .ToList()
        };
    }
}

public sealed record BaseStarHistoryBucketPayload(DateTimeOffset BucketStartUtc, string JsonLines);
