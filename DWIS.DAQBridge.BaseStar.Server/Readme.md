# Bridge Between an OPC-UA Server and the D-WIS Blackboard for the Base Star Mechanical Sub
This project implements the runtime bridge that reads Base Star mechanical sub data from an OPC-UA server and publishes it to the D-WIS Blackboard.

## Purpose
- Provide the core data path between OPC-UA and D-WIS for Base Star signals.
- Normalize signal values and metadata before publication.
- Ensure the Blackboard receives a complete, consistent signal set.

## What it does
- Connects to a configured OPC-UA endpoint and subscribes to Base Star signals.
- Maps incoming values to the Base Star semantic model.
- Publishes signal updates into the D-WIS Blackboard.

## When to use it
- In deployments where Base Star live data must be bridged into D-WIS.
- During system integration to validate end-to-end data flow.
- Before releasing updates that affect signal mappings or publication behavior.

## How to use it
To install and run a replicated DWIS Blackboard. Here is the installation command:
```sh
docker run  -dit --name blackboard -P -p 48030:48030/tcp --hostname localhost  digiwells/ddhubserver:latest --useHub --hubURL https://dwis.digiwells.no/blackboard/applications
```

To install and run the BaseStar to DWIS bridge run the following command:
```sh
docker run -dit --name BaseStar -v c:\Volumes\DWISDAQBridgeBaseStarServer:/home digiwells/dwisdaqbridgebasestarserver:stable
```

Here is an example config.json file for a docker based configuration. The OPCUA server runs inside the same docker environment on port 48031.
The DWIS blackboard runs inside the same docker environment on port 48030. The loop duration for accessing the
OPCUA signals is 10 ms and the frames have a length of 1s. The OPC-UA namespace is identical to the internal one.
The OPC-UA node ids are identical to the internal ones and the unit conversion for each of the signal is the identity.

```json
{
  "LoopDuration": "00:00:01",
  "GeneralBlackboard": "opc.tcp://host.docker.internal:48030",
  "OPCUAURL": "opc.tcp://host.docker.internal:48031",
  "OPCUALoopDuration": "00:00:01",
  "NameSpace": "http://ddhub.no/BaseStarDataManifest/Variables/",
  "NodeIDPredix" : "BaseStarDataManifest.",
  "OPCUAIDs": {
    "BaseStarDataManifest.AverageRotationalSpeed": "BaseStarDataManifest.AverageRotationalSpeed",
    "BaseStarDataManifest.MaxRotationalSpeed": "BaseStarDataManifest.MaxRotationalSpeed",
    "BaseStarDataManifest.StickSlipIndicator": "BaseStarDataManifest.StickSlipIndicator",
    "BaseStarDataManifest.PeakRadialAcceleration": "BaseStarDataManifest.PeakRadialAcceleration",
    "BaseStarDataManifest.AverageRadialAcceleration": "BaseStarDataManifest.AverageRadialAcceleration",
    "BaseStarDataManifest.PeakAxialAcceleration": "BaseStarDataManifest.PeakAxialAcceleration",
    "BaseStarDataManifest.AverageRawWeight": "BaseStarDataManifest.AverageRawWeight",
    "BaseStarDataManifest.MinRawWeight": "BaseStarDataManifest.MinRawWeight",
    "BaseStarDataManifest.MaxRawWeight": "BaseStarDataManifest.MaxRawWeight",
    "BaseStarDataManifest.TareBitWeight": "BaseStarDataManifest.TareBitWeight",
    "BaseStarDataManifest.AverageBitWeight": "BaseStarDataManifest.AverageBitWeight",
    "BaseStarDataManifest.MinBitWeight": "BaseStarDataManifest.MinBitWeight",
    "BaseStarDataManifest.MaxBitWeight": "BaseStarDataManifest.MaxBitWeight",
    "BaseStarDataManifest.AverageRawTorque": "BaseStarDataManifest.AverageRawTorque",
    "BaseStarDataManifest.MinRawTorque": "BaseStarDataManifest.MinRawTorque",
    "BaseStarDataManifest.MaxRawTorque": "BaseStarDataManifest.MaxRawTorque",
    "BaseStarDataManifest.TorqueBias": "BaseStarDataManifest.TorqueBias",
    "BaseStarDataManifest.AverageCorrectedTorque": "BaseStarDataManifest.AverageCorrectedTorque",
    "BaseStarDataManifest.MinCorrectedTorque": "BaseStarDataManifest.MinCorrectedTorque",
    "BaseStarDataManifest.MaxCorrectedTorque": "BaseStarDataManifest.MaxCorrectedTorque",
    "BaseStarDataManifest.AverageBitTorque": "BaseStarDataManifest.AverageBitTorque",
    "BaseStarDataManifest.MinBitTorque": "BaseStarDataManifest.MinBitTorque",
    "BaseStarDataManifest.MaxBitTorque": "BaseStarDataManifest.MaxBitTorque",
    "BaseStarDataManifest.ElectronicTemperature": "BaseStarDataManifest.ElectronicTemperature",
    "BaseStarDataManifest.StringPressure": "BaseStarDataManifest.StringPressure",
    "BaseStarDataManifest.AnnulusPressure": "BaseStarDataManifest.AnnulusPressure"
 },
  "UnitConversions": {
    "BaseStarDataManifest.AverageRotationalSpeed": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MaxRotationalSpeed": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.StickSlipIndicator": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.PeakRadialAcceleration": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.AverageRadialAcceleration": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.PeakAxialAcceleration": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.AverageRawWeight": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MinRawWeight": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MaxRawWeight": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.TareBitWeight": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.AverageBitWeight": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MinBitWeight": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MaxBitWeight": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.AverageRawTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MinRawTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MaxRawTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.TorqueBias": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MinCorrectedTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MaxCorrectedTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.AverageBitTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MinBitTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.MaxBitTorque": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.ElectronicTemperature": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.StringPressure": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    },
    "BaseStarDataManifest.AnnulusPressure": {
      "ConversionFactor": 1.0,
      "ConversionOffset": 0.0
    }
  }
}

```