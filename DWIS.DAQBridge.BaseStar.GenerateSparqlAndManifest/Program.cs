using DWIS.API.DTO;
using DWIS.DAQBridge.BaseStar.Model;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;
using System.Reflection;

namespace DWIS.DAQBridge.BaseStar.GenerateSparqlAndManifest
{

    class Progam
    {
        static void GenerateSparQLForMD(StreamWriter writer, Dictionary<string, QuerySpecification>? queries, string procedureClassName)
        {
            if (writer != null && queries != null)
            {
                writer.WriteLine("# Semantic Queries for the class `" + procedureClassName + "`");
                foreach (var query in queries)
                {
                    if (query.Value != null)
                    {
                        writer.WriteLine("## " + query.Key);
                        writer.WriteLine("```sparql");
                        writer.WriteLine(query.Value.SparQL);
                        writer.WriteLine("```");
                    }
                }
            }
            else
            {
                bool glups = true;
            }
        }
        static void GenerateSparQLForMD(StreamWriter writer, string propertyName, string? query)
        {
            if (writer != null && !string.IsNullOrEmpty(query))
            {
                writer.WriteLine("# Alternate Semantic Query for `" + propertyName + "`");
                writer.WriteLine("```sparql");
                writer.WriteLine(query);
                writer.WriteLine("```");
            }
            else
            {
                bool glups = true;
            }
        }
        static void GenerateSparQLForMD(StreamWriter writer, string propertyName, Dictionary<string, QuerySpecification>? queries)
        {
            if (writer != null && !string.IsNullOrEmpty(propertyName) && queries != null)
            {
                writer.WriteLine("# Semantic Queries for `" + propertyName + "`");
                foreach (var query in queries)
                {
                    if (query.Value != null)
                    {
                        writer.WriteLine("## " + query.Key);
                        writer.WriteLine("```sparql");
                        writer.WriteLine(query.Value.SparQL);
                        writer.WriteLine("```");
                    }
                }
            }
            else
            {
                bool glups = true;
            }
        }

        static void GenerateMermaidForMD(StreamWriter writer, string propertyName, string? mermaid)
        {
            if (writer != null && !string.IsNullOrEmpty(propertyName) && !string.IsNullOrEmpty(mermaid))
            {
                writer.WriteLine("# Semantic Graph for `" + propertyName + "`");
                writer.WriteLine(mermaid);
            }
            else
            {
                bool glups = true;
            }
        }

        static void DumpToJson(ManifestFile manifestFile, string filename)
        {
            string tempPath = Directory.GetCurrentDirectory();
            DirectoryInfo? dir = new DirectoryInfo(tempPath);
            dir = dir?.Parent?.Parent?.Parent;
            if (dir != null)
            {
                string tempFile = Path.Combine(dir.FullName, filename + ".json");
                using (StreamWriter writer = new StreamWriter(tempFile))
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(manifestFile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    writer.WriteLine(json);
                }
            }
        }
        static void Main()
        {
            MechanicalSubOutputDataMudPulseTelemetry testClass = new MechanicalSubOutputDataMudPulseTelemetry();
            Assembly? assembly = Assembly.GetAssembly(typeof(MechanicalSubOutputDataMudPulseTelemetry));
            if (assembly != null)
            {
                string tempPath = Directory.GetCurrentDirectory();
                DirectoryInfo? dir = new DirectoryInfo(tempPath);
                dir = dir?.Parent?.Parent?.Parent;
                if (dir != null)
                {
                    string tempFile = Path.Combine(dir.FullName, "MechanicalSubOutputDataMudPulseTelemetry.md");
                    using (StreamWriter writer = new StreamWriter(tempFile))
                    {
                        var manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AverageRotationalSpeed", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AverageRotationalSpeed", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AverageRotationalSpeed");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MaxRotationalSpeed", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MaxRotationalSpeed", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MaxRotationalSpeed");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "StickSlipIndicator", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "StickSlipIndicator", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.StickSlipIndicator");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "PeakRadialAcceleration", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "PeakRadialAcceleration", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.PeakRadialAcceleration");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AverageRadialAcceleration", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AverageRadialAcceleration", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AverageRadialAcceleration");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "PeakAxialAcceleration", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "PeakAxialAcceleration", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.PeakAxialAcceleration");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AverageRawWeight", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AverageRawWeight", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AverageRawWeight");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MinRawWeight", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MinRawWeight", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MinRawWeight");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MaxRawWeight", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MaxRawWeight", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MaxRawWeight");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "TareBitWeight", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "TareBitWeight", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.TareBitWeight");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AverageBitWeight", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AverageBitWeight", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AverageBitWeight");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MinBitWeight", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MinBitWeight", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MinBitWeight");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MaxBitWeight", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MaxBitWeight", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MaxBitWeight");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AverageRawTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AverageRawTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AverageRawTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MinRawTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MinRawTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MinRawTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MaxRawTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MaxRawTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MaxRawTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "TorqueBias", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "TorqueBias", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.TorqueBias");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AverageCorrectedTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AverageCorrectedTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AverageCorrectedTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MinCorrectedTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MinCorrectedTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MinCorrectedTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MaxCorrectedTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MaxCorrectedTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MaxCorrectedTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AverageBitTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AverageBitTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AverageBitTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MinBitTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MinBitTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MinBitTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "MaxBitTorque", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "MaxBitTorque", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.MaxBitTorque");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "ElectronicTemperature", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "ElectronicTemperature", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.ElectronicTemperature");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "StringPressure", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "StringPressure", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.StringPressure");
                        }
                        manifest = GeneratorSparQLManifestFile.GetManifestFile(assembly, typeof(MechanicalSubOutputDataMudPulseTelemetry).FullName, "AnnulusPressure", "BaseStarDataManifest", "Halliburton", "DWIS");
                        if (manifest != null)
                        {
                            GenerateMermaidForMD(writer, "AnnulusPressure", GeneratorSparQLManifestFile.GetMermaid(manifest));
                            DumpToJson(manifest, "BaseStar.AnnulusPressure");
                        }
                    }
                }
            }
        }
    }
}


