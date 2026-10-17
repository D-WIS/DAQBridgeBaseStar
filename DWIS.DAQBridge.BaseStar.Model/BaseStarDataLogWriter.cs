using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DWIS.DAQBridge.BaseStar.Model;

/// <summary>Owns only timestamped basestar-raw/published JSONL and ZIP files.</summary>
public sealed class BaseStarDataLogWriter
{
    private readonly string _directory;
    private readonly TimeSpan _segmentDuration;
    private readonly long _maximumBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly UTF8Encoding Utf8 = new(false);
    public BaseStarDataLogWriter(string? directory, TimeSpan segmentDuration, long maximumBytes)
    {
        // Matches the worker configuration's ../home directory when running from source on Windows.
        _directory = OperatingSystem.IsWindows() && (string.IsNullOrWhiteSpace(directory) || directory == "/home")
            ? Path.GetFullPath(Path.Combine("..", "home"))
            : string.IsNullOrWhiteSpace(directory) ? "/home" : Path.GetFullPath(directory);
        _segmentDuration = segmentDuration > TimeSpan.Zero ? segmentDuration : TimeSpan.FromHours(6);
        _maximumBytes = maximumBytes > 0 ? maximumBytes : 1024L * 1024 * 1024;
    }
    public async Task AppendAsync(BaseStarHistoryDataKind kind, DateTimeOffset timestampUtc, BaseStarLogEntry entry)
    {
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(_directory);
            DateTimeOffset start = SegmentStart(timestampUtc);
            string path = Path.Combine(_directory, $"basestar-{kind.ToString().ToLowerInvariant()}-{start:yyyyMMddTHHmmssZ}.jsonl");
            // A late timestamp must not overwrite records already archived for this segment.
            string zipPath = Path.ChangeExtension(path, ".zip");
            if (!File.Exists(path) && File.Exists(zipPath))
            {
                string restoredPath = path + ".tmp";
                try
                {
                    using (var archive = ZipFile.OpenRead(zipPath))
                    await using (var input = archive.Entries.Single().Open())
                    await using (var output = File.Create(restoredPath))
                        await input.CopyToAsync(output);
                    File.Move(restoredPath, path, true);
                }
                finally { if (File.Exists(restoredPath)) File.Delete(restoredPath); }
            }
            bool schemaNeeded = !File.Exists(path) || new FileInfo(path).Length == 0;
            await File.AppendAllTextAsync(path, (schemaNeeded ? entry.SchemaLine + "\n" : "") + entry.RecordLine + "\n", Utf8);
            // The uncompressed file is now authoritative, including any late record.
            if (File.Exists(zipPath)) File.Delete(zipPath);
            await MaintainCoreAsync(start);
        }
        finally { _gate.Release(); }
    }
    public async Task MaintainAsync(DateTimeOffset nowUtc)
    {
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(_directory);
            await MaintainCoreAsync(SegmentStart(nowUtc));
        }
        finally { _gate.Release(); }
    }
    private async Task MaintainCoreAsync(DateTimeOffset currentStart)
    {
        foreach (FileInfo source in OwnedFiles().Where(f => f.Extension == ".jsonl" && ParseStart(f.Name) < currentStart))
        {
            string archivePath = Path.ChangeExtension(source.FullName, ".zip");
            string temporaryPath = archivePath + ".tmp";
            try
            {
                await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(source.Name, CompressionLevel.SmallestSize);
                    await using var output = entry.Open();
                    await using var input = source.OpenRead();
                    await input.CopyToAsync(output);
                }
                File.Move(temporaryPath, archivePath, true);
                source.Delete();
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }
        var files = OwnedFiles().OrderBy(f => ParseStart(f.Name)).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();
        long total = files.Sum(f => f.Length);
        foreach (var file in files)
        {
            if (total <= _maximumBytes) break;
            long length = file.Length;
            file.Delete();
            total -= length;
        }
    }
    private IEnumerable<FileInfo> OwnedFiles() => new DirectoryInfo(_directory).EnumerateFiles()
        .Where(f => (f.Extension == ".jsonl" || f.Extension == ".zip") && ParseStart(f.Name).HasValue);
    private static DateTimeOffset? ParseStart(string name)
    {
        string stem = Path.GetFileNameWithoutExtension(name);
        string? suffix = stem.StartsWith("basestar-raw-", StringComparison.Ordinal) ? stem[13..]
            : stem.StartsWith("basestar-published-", StringComparison.Ordinal) ? stem[19..] : null;
        return suffix is not null && DateTimeOffset.TryParseExact(suffix, "yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start) ? start : null;
    }
    private DateTimeOffset SegmentStart(DateTimeOffset time) => new(time.UtcTicks / _segmentDuration.Ticks * _segmentDuration.Ticks, TimeSpan.Zero);
}
