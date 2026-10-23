using System.Text;
using System.Text.Json;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.ExportDataStructures;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 1) throw new ArgumentException("Usage: ExportDataStructures [output-directory]");
        string directory = args.Length == 1 ? Path.GetFullPath(args[0]) : FindSchemaDirectory();
        Directory.CreateDirectory(directory);
var data = new MechanicalSubOutputData();
        foreach (var (property, manifest) in data.Manifests.Value)
            File.WriteAllText(Path.Combine(directory, property.Name + "Manifest.json"),
                JsonSerializer.Serialize(manifest) + "\n", new UTF8Encoding(false));
        Console.WriteLine($"Exported {data.Manifests.Value.Count} runtime manifests to {directory}.");
    }

    private static string FindSchemaDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DWIS.DAQBridge.BaseStar.ExportDataStructures.csproj")))
                return Path.GetFullPath(Path.Combine(directory.FullName, "..", "DWIS.DAQBridge.BaseStar.Schemas"));
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Specify an output directory when running outside the source tree.");
    }
}
