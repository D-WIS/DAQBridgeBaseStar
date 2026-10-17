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

## Compact data logging and Blackboard history
The Server compares immutable numeric snapshots after each OPC-UA poll. Source
timestamps, queries, manifests, and other semantic metadata do not participate in
change detection. The first available sample establishes a baseline. Later polls
with identical values create no file records, history updates, or ordinary signal
writes. When publication is due, only changed finite signal values are sent to the
Blackboard; intermediate samples are retained in the raw log while publication
keeps the existing `LoopDuration` cadence. Missing/non-finite values are represented
as `null` in logs and are not written to double-valued signal tags.

Configuration defaults (also shown in `home/config.json`):

```json
{
  "EnableDataLogging": true,
  "DataLogDirectory": "/home",
  "DataLogSegmentDuration": "06:00:00",
  "MaximumDataLogStorageBytes": 1073741824,
  "EnableBlackboardHistory": true
}
```

Files named `basestar-raw-<UTC start>.jsonl` and
`basestar-published-<UTC start>.jsonl` contain a schema line (`format`, `units`,
ordered `fields`) followed by records containing only `t` (UTC observation/publication
time) and `v` (an array of canonical SI values in that field order). “Raw” means
received values **after** input unit conversion to SI. These are compact complete
snapshots, not serialized domain objects. Each stream appends only when its values
change. Six-hour default boundaries are 00:00, 06:00, 12:00, and 18:00 UTC.

Completed segments are ZIP-compressed through a temporary file and atomic rename.
Maintenance also runs during quiet polls and connection retries. The combined size
of owned raw/published JSONL and ZIP files is capped at 1 GiB by default, deleting
the oldest segments first. A cap smaller than one active segment can delete that
active file too. Configuration, unrelated files, and older legacy dumps are never
deleted by this policy. Non-positive duration/storage settings use the defaults.
File failures are logged and retried without stopping signal publication.
On Windows, the default `/home` resolves to the worker's `../home` directory;
Linux/container deployments use `/home`, which must be mounted persistently and
writable. Changing the directory applies the cap to the new directory; it does not
move or delete logs in the old location. No historic files are migrated automatically.

The `BaseStarHistory` manifest defines **16 pure OPC-UA string tags**, without
semantic nodes or relationships: logical IDs `BaseStar.Raw.History.0` through `.7`
and `BaseStar.Published.History.0` through `.7`. Use the manifest injection result
for the server-assigned namespace and node IDs. Each value is a JSON envelope with
`BucketStartUtc` and `JsonLines`, using the same compact schema/record format as the
files. Raw and published history each use eight fixed six-hour UTC slots; the slot
is the absolute six-hour bucket number modulo eight. Slots are reused after 48 hours.
Expiry maintenance clears stale payloads even without new samples or while the
source is offline and the Blackboard remains reachable, so retention is
at most 48 hours (42–48 hours at bucket granularity). History consumers must also
honor bucket timestamps while the bridge is offline, since an offline bridge cannot
clear server tags. This history window is fixed and independent of file size limits.

Existing tag payloads are loaded on connection, preserving in-window records.
Reconnect synchronizes current live signal values once, while restored history and
in-memory file snapshots suppress duplicate records for unchanged data. A process
restart emits a baseline in the files; retained Blackboard history is reused.
Failed signal/history writes trigger connection recovery and are not marked as
successful; partial signal batches can be retried. History is bounded by duration,
not bytes, so tag payload sizes still depend on the actual change rate. No offline
sample queue or standalone OPCUASource recovery behavior is added.

## Verification
Run `dotnet build DWIS.DAQBridge.BaseStar.sln` and
`dotnet test DWIS.DAQBridge.BaseStar.Tests/DWIS.DAQBridge.BaseStar.Tests.csproj`.
The tests use simulated clients to cover unavailable connections, reconnect exceptions,
connection loss during reads, exchange exceptions, and cancellation during retry waits.
They also check MaxRawWeight source initialization/publication, semantic registration,
input unit conversion, and Blackboard transfer. `DataLoggingTests` covers compact
serialization, change detection, per-signal publication, rotation/compression,
storage limits and file ownership, history restoration/expiry, write rejection,
and continued publication during file logging failures.
Live recovery can be checked with isolated OPC-UA and Blackboard instances by starting
the Server before either dependency, then stopping and restarting each dependency while
observing retry and resumed-exchange messages.
