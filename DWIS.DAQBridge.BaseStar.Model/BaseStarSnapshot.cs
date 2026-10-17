using System.Reflection;
using System.Text.Json;
using DWIS.RigOS.Common.Worker;

namespace DWIS.DAQBridge.BaseStar.Model;

/// <summary>Immutable SI values only; transport timestamps and semantic metadata are excluded.</summary>
public sealed class BaseStarSnapshot : IEquatable<BaseStarSnapshot>
{
    public static IReadOnlyList<PropertyInfo> Properties { get; } = Array.AsReadOnly(
        typeof(MechanicalSubOutputDataMudPulseTelemetry).GetProperties()
            .Where(p => p.PropertyType == typeof(ScalarProperty)).OrderBy(p => p.MetadataToken).ToArray());
    private readonly double?[] _values;
    private BaseStarSnapshot(double?[] values) => _values = values;
    public bool HasValues => _values.Any(v => v.HasValue);
    public double? this[int index] => _values[index];
    public static BaseStarSnapshot Capture(DWISData data) => new(Properties.Select(p =>
    {
        double? value = (p.GetValue(data) as ScalarProperty)?.Value;
        return value.HasValue && double.IsFinite(value.Value) ? value : null;
    }).ToArray());
    public IEnumerable<int> ChangesFrom(BaseStarSnapshot? previous) => Enumerable.Range(0, _values.Length)
        .Where(i => _values[i].HasValue && (previous is null || _values[i] != previous._values[i]));
    public BaseStarLogEntry ToEntry(DateTimeOffset timestampUtc) => new(
        JsonSerializer.Serialize(new { type = "schema", format = "basestar.values.v1", units = "SI",
            fields = Properties.Select(p => p.Name).ToArray() }),
        JsonSerializer.Serialize(new { t = timestampUtc.ToUniversalTime(), v = _values }));
    public static BaseStarSnapshot? ReadLast(string jsonLines)
    {
        try
        {
            string[] lines = jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 2) return null;
            using var schema = JsonDocument.Parse(lines[0]);
            if (schema.RootElement.GetProperty("format").GetString() != "basestar.values.v1" ||
                !schema.RootElement.GetProperty("fields").EnumerateArray().Select(v => v.GetString())
                    .SequenceEqual(Properties.Select(p => p.Name))) return null;
            using var record = JsonDocument.Parse(lines[^1]);
            double?[]? values = record.RootElement.GetProperty("v").Deserialize<double?[]>();
            return values?.Length == Properties.Count && values.All(v => !v.HasValue || double.IsFinite(v.Value))
                ? new(values) : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
    public bool Equals(BaseStarSnapshot? other) => other is not null && _values.AsSpan().SequenceEqual(other._values);
    public override bool Equals(object? obj) => Equals(obj as BaseStarSnapshot);
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (var value in _values) hash.Add(value);
        return hash.ToHashCode();
    }
}

public sealed record BaseStarLogEntry(string SchemaLine, string RecordLine);
public enum BaseStarHistoryDataKind { Raw, Published }
