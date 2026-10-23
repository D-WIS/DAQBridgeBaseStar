using System.Text.Json;
using System.Text.Json.Nodes;
using DWIS.DAQBridge.BaseStar.Model;

namespace DWIS.DAQBridge.BaseStar.Tests;

public class SemanticContractTests
{
    [Fact]
    public void SharedRawRotationalSpeedIsConsistentlyDerived()
    {
        var data = new MechanicalSubOutputData();
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
        var data = new MechanicalSubOutputData();
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
    public void TelemetryPairsHaveDistinctTransportAndSharedBaseStarIdentity()
    {
        var data = new MechanicalSubOutputData();
        string[] mudPulseNames = BaseStarSnapshot.Properties.Select(p => p.Name)
            .Where(n => !n.EndsWith("WiredPipe", StringComparison.Ordinal)).ToArray();
        Assert.Equal(26, mudPulseNames.Length);
        foreach (string mudPulseName in mudPulseNames)
        {
            var mudPulseProperty = BaseStarSnapshot.Properties.Single(p => p.Name == mudPulseName);
            var wiredPipeProperty = BaseStarSnapshot.Properties.Single(p => p.Name == mudPulseName + "WiredPipe");
            AssertTransportAndIdentity(data, mudPulseProperty, "MudPulseTelemetry", "mudPulseTelemetry");
            AssertTransportAndIdentity(data, wiredPipeProperty, "WiredPipeTelemetry", "wiredPipeTelemetry");
            Assert.NotEqual(data.Manifests.Value[mudPulseProperty].ProvidedVariables.Single().VariableID,
                data.Manifests.Value[wiredPipeProperty].ProvidedVariables.Single().VariableID);

            var mudNode = data.OPCUANodes.Value[mudPulseProperty];
            var wiredNode = data.OPCUANodes.Value[wiredPipeProperty];
            Assert.NotEqual(mudNode.NameSpace, wiredNode.NameSpace);
            Assert.NotEqual(mudNode.ID, wiredNode.ID);
        }
    }

    [Fact]
    public void CheckedInExportsAndQueriesMatchRuntimeContractForAll52Signals()
    {
        var root = FindRepository();
        var data = new MechanicalSubOutputData();
        Assert.Equal(52, data.Manifests.Value.Count);
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

    private static void AssertTransportAndIdentity(MechanicalSubOutputData data,
        System.Reflection.PropertyInfo property, string telemetryType, string telemetryName)
    {
        var manifest = data.Manifests.Value[property];
        var model = Assert.Single(manifest.InjectedNodes,
            n => n.UniqueName == "baseStarModel" && n.TypeDictionaryURI == "InstrumentModel");
        Assert.Contains(model.Fields!, field => field.FieldName == "ModelIdentifier" &&
            field.FieldValue == "Halliburton/BaseStar");
        Assert.Contains(model.Fields!, field => field.FieldName == "ModelName" && field.FieldValue == "BaseStar");
        Assert.Contains(manifest.InjectedNodes, n => n.UniqueName == "baseStarUnit" && n.TypeDictionaryURI == "InstrumentInstance");
        Assert.Contains(manifest.InjectedNodes, n => n.UniqueName == telemetryName && n.TypeDictionaryURI == telemetryType);
        Assert.Contains(manifest.InjectedReferences, r => r.Subject.ID == "baseStarUnit" &&
            r.VerbURI == "http://ddhub.no/IsOfInstrumentModel" && r.Object.ID == "baseStarModel");
        Assert.Contains(manifest.InjectedReferences, r => r.VerbURI == "http://ddhub.no/IsTransmittedBy" &&
            r.Object.ID == telemetryName);
        Assert.Contains(manifest.InjectedReferences, r => r.VerbURI == "http://ddhub.no/IsLocatedAtEquipment" &&
            r.Object.ID == "baseStarUnit");
        Assert.Contains(manifest.InjectedReferences, r => r.Subject.ID == "baseStarUnit" &&
            r.VerbURI == "http://ddhub.no/SupportsTelemetry" && r.Object.ID == "mudPulseTelemetry");
        Assert.Contains(manifest.InjectedReferences, r => r.Subject.ID == "baseStarUnit" &&
            r.VerbURI == "http://ddhub.no/SupportsTelemetry" && r.Object.ID == "wiredPipeTelemetry");
        string query = Assert.Single(data.SparQLQueries.Value[property]).Value.SparQL!;
        Assert.Contains("?baseStarModel rdf:type ddhub:InstrumentModel", query);
        Assert.Contains("?baseStarModel ddhub:ModelIdentifier \"Halliburton/BaseStar\"^^<http://www.w3.org/2001/XMLSchema#string>", query);
        Assert.Contains($"ddhub:IsTransmittedBy ?{telemetryName}", query);
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
