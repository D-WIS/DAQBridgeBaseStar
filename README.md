# Bridge for the Base Star Mechanical Sub
A .NET solution that bridges Halliburton's Base Star mechanical sub signals to the DWIS Blackboard, including shared semantic models, manifest generation, and OPC-UA integration helpers.

## Solution overview
The solution is organized around a single goal: reliably map Base Star signals into DWIS with consistent semantics and verifiable data flow.

## Projects
- `DWIS.DAQBridge.BaseStar.Model` - The authoritative semantic model for Base Star signals.
- `DWIS.DAQBridge.BaseStar.ExportDataStructures` - Helper tool to generate signal manifests for DWIS.
- `DWIS.DAQBridge.BaseStar.OPCUASource` - OPC-UA source to publish Base Star signals for testing and integration.
- `DWIS.DAQBridge.BaseStar.OPCUASink` - OPC-UA sink to read signals back from the DWIS Blackboard.
- `DWIS.DAQBridge.BaseStar.Server` - Runtime bridge that connects OPC-UA data to the DWIS Blackboard.
- `DWIS.DAQBridge.BaseStar.Schemas` - Shared schemas supporting signal definitions and exchange.

## Data flow (high level)
1. Base Star signals are defined in the semantic model.
2. Manifests are generated to describe the signal set to DWIS.
3. The OPC-UA source can publish live or simulated signals for validation.
4. The server bridge subscribes to OPC-UA and publishes to the Blackboard.
5. The OPC-UA sink can read back published values for verification.

## When to use this solution
- Integrating Base Star live data into DWIS deployments.
- Validating end-to-end signal mapping and publication workflows.
- Updating the Base Star signal set and regenerating manifests.

## Notes
- Keep the semantic model updated before regenerating manifests.
- Verify signal publication using the sink after any mapping changes.

## Server connection recovery
The Server waits until both the configured OPC-UA endpoint and DWIS Blackboard are
connected before registering signals and starting data exchange. Unavailable endpoints
at startup are retried without stopping the hosted worker. A disconnected client or an
exception during exchange returns the Server to connection retry mode. Exchange
exceptions force replacement sessions even when a client's `Connected` flag is stale.
After reconnection, Blackboard targets are cleared and resolved again; optional input
OPC-UA registration (`InitializeInputOPCUAVariables`) is repeated for the new session.

Set `ConnectionRetryInterval` in `home/config.json` (for example, `"00:00:05"`).
It defaults to five seconds; non-positive values also use five seconds. An invalid
non-positive `OPCUALoopDuration` uses one second. Retry waits honor host shutdown.
The retry interval is a delay between attempts; connection attempts may also take time.
The bridge resumes live exchange after recovery; it does not buffer outage samples.

## MaxRawWeight exchange
`MaxRawWeight` uses the shared `ScalarProperty` wrapper, like the other weight
signals. OPCUASource initializes and publishes its numeric value to
`BaseStarDataManifest.MaxRawWeight` in
`http://ddhub.no/BaseStarDataManifest/Variables/`. The Server registers and
publishes it to `BaseStarDataManifestDWISBridge.BaseStarMeasuredMaxRawWeight` in
`http://ddhub.no/BaseStarDataManifestDWISBridge/Variables/`.

The OPC-UA and Blackboard values remain `double`; configured input unit
conversion produces the canonical SI force value in newtons before publication.
This corrects the previous unsupported `GaussianDrillingProperty` member; C#
consumers should now use `MaxRawWeight.Value`. It does not introduce a standard
deviation input or populate a separate uncertainty signal. Existing Blackboard
node identities and stored values are retained; no data migration is required.

## Verification
Run `dotnet build DWIS.DAQBridge.BaseStar.sln` and
`dotnet test DWIS.DAQBridge.BaseStar.Tests/DWIS.DAQBridge.BaseStar.Tests.csproj`.
The tests use simulated clients to cover unavailable connections, reconnect exceptions,
connection loss during reads, exchange exceptions, and cancellation during retry waits.
They also check MaxRawWeight source initialization/publication, semantic registration,
input unit conversion, and Blackboard transfer.
Live recovery can be checked with isolated OPC-UA and Blackboard instances by starting
the Server before either dependency, then stopping and restarting each dependency while
observing retry and resumed-exchange messages.
