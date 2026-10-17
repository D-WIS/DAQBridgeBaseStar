using System.Text.Json;
using System.Text.Json.Nodes;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.Tests;

public class SemanticContractTests
{
    [Fact]
    public void SharedRawRotationalSpeedIsConsistentlyDerived()
    {
        var data = new MechanicalSubOutputDataMudPulseTelemetry();
        foreach (string name in new[] { "AverageRotationalSpeed", "MaxRotationalSpeed", "StickSlipIndicator" })
        {
            var property = BaseStarSnapshot.Properties.Single(p => p.Name == name);
            var raw = Assert.Single(data.Manifests.Value[property].InjectedNodes, n => n.UniqueName == "BaseStarRotationalSpeed#01");
            Assert.Equal("DerivedMeasurement", raw.TypeDictionaryURI);
            string query = Assert.Single(data.SparQLQueries.Value[property]).Value.SparQL!;
            Assert.Contains("?BaseStarRotationalSpeed_01 rdf:type ddhub:DerivedMeasurement", query);
            Assert.DoesNotContain("rdf:type ddhub:DirectMeasurement", query);
        }
    }

    [Fact]
    public void MaxBitTorqueCorrectionUsesMaximumRawTorque()
    {
        var data = new MechanicalSubOutputDataMudPulseTelemetry();
        var property = BaseStarSnapshot.Properties.Single(p => p.Name == "MaxBitTorque");
        var manifest = data.Manifests.Value[property];
        Assert.Contains(manifest.InjectedReferences, r => r.Subject.ID == "BaseStarMeasuredMaxRawTorque#01" &&
            r.VerbURI == "http://ddhub.no/IsTransformationInput" && r.Object.ID == "taringTransformationMaxBitTorque");
        Assert.DoesNotContain(manifest.InjectedNodes, n => n.UniqueName.Contains("AverageRawTorque"));
        string query = Assert.Single(data.SparQLQueries.Value[property]).Value.SparQL!;
        Assert.Contains("?BaseStarMeasuredMaxRawTorque_01 ddhub:IsTransformationInput ?taringTransformationMaxBitTorque", query);
        Assert.DoesNotContain("AverageRawTorque", query);
    }

    [Fact]
    public void CheckedInExportsAndQueriesMatchRuntimeContractForAll26Signals()
    {
        var root = FindRepository();
        var data = new MechanicalSubOutputDataMudPulseTelemetry();
        Assert.Equal(26, data.Manifests.Value.Count);
        foreach (var (property, manifest) in data.Manifests.Value)
        {
            Assert.Equal("BaseStarDataManifestDWISBridge", manifest.Provider.Name);
            Assert.Equal("BaseStarDataManifest", manifest.ManifestName);
            Assert.Single(manifest.ProvidedVariables);
            Assert.Equal("double", manifest.ProvidedVariables[0].DataType);
            foreach (string path in new[]
            {
                Path.Combine(root, "DWIS.DAQBridge.BaseStar.Schemas", property.Name + "Manifest.json"),
                Path.Combine(root, "DWIS.DAQBridge.BaseStar.GenerateSparqlAndManifest", "BaseStar." + property.Name + ".json")
            })
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(manifest), JsonNode.Parse(File.ReadAllText(path))), path);
            string queryPath = Path.Combine(root, "DWIS.DAQBridge.BaseStar.GenerateSparqlAndManifest", "BaseStar." + property.Name + ".sparql");
            Assert.Equal(Assert.Single(data.SparQLQueries.Value[property]).Value.SparQL!.Trim(), File.ReadAllText(queryPath).Trim());
        }
    }

    private static string FindRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DWIS.DAQBridge.BaseStar.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("The semantic artifact tests require the source checkout.");
    }
}
