using DWIS.DAQBridge.BaseStar.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace DWIS.DAQBridge.BaseStar.ExportDataStructures
{
    class Program
    {
        static void Main()
        {
            GenerateManifests();
        }

        static void GenerateManifests()
        {
            string rootDir = ".\\";
            bool found = false;
            do
            {
                DirectoryInfo? info = Directory.GetParent(rootDir);
                if (info != null && "DWIS.DAQBridge.BaseStar.ExportDataStructures".Equals(info.Name))
                {
                    found = true;
                }
                else
                {
                    rootDir += "..\\";
                }
            } while (!found);
            rootDir += "..\\DWIS.DAQBridge.BaseStar.Schemas\\";
            MechanicalSubOutputDataMudPulseTelemetry data = new MechanicalSubOutputDataMudPulseTelemetry();
            if (data.Manifests is not null)
            {
                foreach (var kpv in data.Manifests.Value)
                {
                    string name = kpv.Key.Name;
                    using (StreamWriter writer = new StreamWriter(rootDir + name + "Manifest" + ".json"))
                    {
                        string json = System.Text.Json.JsonSerializer.Serialize(kpv.Value);
                        writer.WriteLine(json);
                    }
                }
            }
        }
    }
}