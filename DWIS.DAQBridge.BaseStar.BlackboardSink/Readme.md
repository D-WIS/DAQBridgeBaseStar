# BaseStar Blackboard Sink
This project provides DWIS Blackboard sink that reads Base Star mechanical sub signals after they have been published to the D-WIS Blackboard.

## Purpose
- Validate that Base Star signals arrive on the Blackboard as expected.
- Provide a convenient way to inspect or consume published data.
- Support troubleshooting of the bridge and publication pipeline.

## What it does
- Connects to D-WIS to read the Base Star signal set.
- Subscribes to updates and surfaces the latest values.
- Uses the Base Star semantic model to interpret signal metadata.

## When to use it
- During integration testing to confirm Blackboard publication.
- When diagnosing signal mapping or data quality issues.
- As a lightweight consumer for Base Star signal verification.


## Run
From the solution directory, run
`dotnet run --project DWIS.DAQBridge.BaseStar.BlackboardSink/DWIS.DAQBridge.BaseStar.BlackboardSink.csproj`.
The project, assembly, and namespace are `DWIS.DAQBridge.BaseStar.BlackboardSink`.


## Downhole contract runtime
This BlackboardSink host runs `RuntimeHost` for BaseStar. It consumes `DWIS.RigOS.DownholeMechanical.Bridge` NuGet 1.0.4. See the repository README
and the [shared runtime documentation](https://github.com/D-WIS/Common/blob/main/DWIS.RigOS.DownholeMechanical.Bridge/README.md) for `/home/config.json`, explicit identity/node/unit mappings, telemetry,
reconnection, persistent logging and Docker build contexts. Existing legacy workers are not
registered by this host.
