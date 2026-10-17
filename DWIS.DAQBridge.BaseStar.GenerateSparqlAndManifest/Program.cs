using System.Text;
using System.Text.Json;
using DWIS.DAQBridge.BaseStar.Model;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;

namespace DWIS.DAQBridge.BaseStar.GenerateSparqlAndManifest;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8 = new(false);

    private static void Main(string[] args)
    {
        string directory = args.Length == 1 ? Path.GetFullPath(args[0]) : FindProjectDirectory();
        if (args.Length > 1) throw new ArgumentException("Usage: GenerateSparqlAndManifest [output-directory]");
        Directory.CreateDirectory(directory);
        var data = new MechanicalSubOutputDataMudPulseTelemetry();
        using var documentation = new StreamWriter(Path.Combine(directory, "MechanicalSubOutputDataMudPulseTelemetry.md"), false, Utf8);
        documentation.WriteLine("# BaseStar signal manifests and discovery queries");
        documentation.WriteLine();
        documentation.WriteLine("Generated from the model's runtime manifests and query sets. Do not edit directly.");
        foreach (var (property, manifest) in data.Manifests.Value)
        {
            File.WriteAllText(Path.Combine(directory, $"BaseStar.{property.Name}.json"),
                JsonSerializer.Serialize(manifest, JsonOptions) + "\n", Utf8);
            documentation.WriteLine($"\n## {property.Name}\n");
            documentation.WriteLine("```mermaid");
            documentation.WriteLine(GeneratorSparQLManifestFile.GetMermaid(manifest));
            documentation.WriteLine("```");
            var queries = data.SparQLQueries.Value[property];
            int index = 0;
            foreach (var (name, query) in queries)
            {
                string suffix = queries.Count == 1 ? "" : $".{index++}";
                File.WriteAllText(Path.Combine(directory, $"BaseStar.{property.Name}{suffix}.sparql"), query.SparQL!.TrimEnd() + "\n", Utf8);
                documentation.WriteLine($"\n### {name}\n");
                documentation.WriteLine("```sparql");
                documentation.WriteLine(query.SparQL!.TrimEnd());
                documentation.WriteLine("```");
            }
        }
        Console.WriteLine($"Generated {data.Manifests.Value.Count} manifests and their discovery queries in {directory}.");
    }

    private static string FindProjectDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DWIS.DAQBridge.BaseStar.GenerateSparqlAndManifest.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Specify an output directory when running outside the source tree.");
    }
}
