# Bridge for the Base Star Mechanical Sub

The active hosts use the shared downhole contract runtime described below. Earlier scalar-worker
sections document the retained model/library implementation, which is not registered by the hosts.
A .NET solution that bridges Halliburton's Base Star mechanical sub signals to the DWIS Blackboard, including shared semantic models, manifest generation, and OPC-UA integration helpers.

## Solution overview
The solution is organized around a single goal: reliably map Base Star signals into DWIS with consistent semantics and verifiable data flow.

## Projects
- `DWIS.DAQBridge.BaseStar.Model` - The authoritative semantic model for Base Star signals.
- `DWIS.DAQBridge.BaseStar.ExportDataStructures` - Helper tool to generate signal manifests for DWIS.
- `DWIS.DAQBridge.BaseStar.OPCUASource` - OPC-UA source to publish Base Star signals for testing and integration.
- `DWIS.DAQBridge.BaseStar.BlackboardSink` - Blackboard sink to read signals back from the DWIS Blackboard.
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

## Retained scalar worker connection recovery
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

## Retained scalar worker logging and history
The Server compares immutable numeric snapshots after each OPC-UA poll. The 26
measurements are represented as 52 signals: one mud-pulse signal and one wired-pipe
signal for every measurement. Source
timestamps, queries, manifests, and other semantic metadata do not participate in
change detection. The first available sample establishes a baseline. Later polls
with identical values create no file records, history updates, or ordinary signal
writes. When publication is due, only changed finite signal values are sent to the
Blackboard; intermediate samples are retained in the raw log while publication
uses independent `MudPulsePublishInterval` (30 seconds) and
`WiredPipePublishInterval` (2.5 seconds) cadences. Missing/non-finite values are represented
as `null` in logs and are not written to double-valued signal tags.

Configuration defaults (also shown in `home/config.json`):

```json
{
  "EnableDataLogging": true,
  "DataLogDirectory": "/home",
  "DataLogSegmentDuration": "06:00:00",
  "MaximumDataLogStorageBytes": 1073741824,
  "EnableBlackboardHistory": true,
  "MudPulsePublishInterval": "00:00:30",
  "WiredPipePublishInterval": "00:00:02.500"
}
```

Files named `basestar-raw-v2-<UTC start>.jsonl` and
`basestar-published-v2-<UTC start>.jsonl` contain a schema line (`format`, `units`,
ordered `fields`) followed by records containing only `t` (UTC observation/publication
time) and `v` (an array of canonical SI values in that field order). “Raw” means
received values **after** input unit conversion to SI. These are compact complete
snapshots, not serialized domain objects. Each stream appends only when its values
change. Six-hour default boundaries are 00:00, 06:00, 12:00, and 18:00 UTC.
The v2 name separates the 52-field layout from retained 26-field v1 chunks. The
storage cap maintains both generations. A v1 Blackboard history slot is replaced
with a v2 payload when that slot first receives a new sample, preventing mixed schemas.

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

## Semantic model and generated contracts
The annotations in `MechanicalSubOutputData` are authoritative.
Each telemetry pair identifies the instrument as the Halliburton BaseStar model and
the same BaseStar instrument instance. Its delivered measurement is separately named
and linked to either `MudPulseTelemetry` or `WiredPipeTelemetry`; the instrument
declares support for both paths. This distinguishes BaseStar signals from equivalent
measurements supplied by DrillView, BlackStream, and other downhole instruments.
Measurement-principle facts are intentionally absent where the available BaseStar
specification does not establish a principle for that sensor.
The shared raw rotational-speed node is consistently `DerivedMeasurement`, matching
its established classification in the maximum-speed and stick-slip descriptions.
Maximum bit torque's correction uses `BaseStarMeasuredMaxRawTorque`, not average
raw torque. Signal node identities and the scalar double wire contract are unchanged.

Both export tools now consume the same `Manifests` and `SparQLQueries` used by the
Server and BlackboardSink. Their provider name is `BaseStarDataManifestDWISBridge`.
Run these commands from the solution directory after an annotation/package change:

```powershell
dotnet run --project .\DWIS.DAQBridge.BaseStar.GenerateSparqlAndManifest\DWIS.DAQBridge.BaseStar.GenerateSparqlAndManifest.csproj
dotnet run --project .\DWIS.DAQBridge.BaseStar.ExportDataStructures\DWIS.DAQBridge.BaseStar.ExportDataStructures.csproj
```

The first regenerates `BaseStar.<signal>.json`, `BaseStar.<signal>.sparql`, and
`MechanicalSubOutputData.md` in its project directory. The second
regenerates `<signal>Manifest.json` in `DWIS.DAQBridge.BaseStar.Schemas`. Each tool
also accepts an explicit output directory after `--`. Do not hand-edit generated
artifacts. The scalar runtime manifests provide one primary double per signal;
obsolete exported `Sigma_` variables are no longer advertised as current outputs.

Existing Blackboard imports are additive: injecting a corrected manifest does not
retract an old type, relationship, or unused uncertainty tag. Live verification can
use a separate provider namespace on the same Blackboard, requiring each query to
return that provider's expected node and checking the absence of obsolete facts.
Migrating an already populated graph requires a separately planned, backed-up
Blackboard maintenance operation; these source fixes do not erase deployed data.

## Verification
Run `dotnet build DWIS.DAQBridge.BaseStar.sln` and
`dotnet test DWIS.DAQBridge.BaseStar.Tests/DWIS.DAQBridge.BaseStar.Tests.csproj`.
The tests use simulated clients to cover unavailable connections, reconnect exceptions,
connection loss during reads, exchange exceptions, and cancellation during retry waits.
They also check MaxRawWeight source initialization/publication, semantic registration,
input unit conversion, and Blackboard transfer. `DataLoggingTests` covers compact
serialization, change detection, per-signal publication, rotation/compression,
storage limits and file ownership, history restoration/expiry, write rejection,
and continued publication during file logging failures. `SemanticContractTests`
checks the corrected annotations and all 52 exported manifests/query files against
the runtime contract.
Live recovery can be checked with isolated OPC-UA and Blackboard instances by starting
the Server before either dependency, then stopping and restarting each dependency while
observing retry and resumed-exchange messages.

## Reproducible simulator and container releases
The OPCUASource now generates plausible scalar SI samples with a configurable seed
and independent schedules. Mud-pulse dynamics default to 30 seconds and wired-pipe
values default to 2.5 seconds. Only changed OPC-UA tags are written. Settings are
`SimulationSeed`, `SimulationConditionGenerationPeriod`,
`SimulationDataGenerationPeriod`, `SimulationWiredPipeDataGenerationPeriod`, and
`SimulationOPCUAPublishPeriod`; restart the
simulator after editing them. See `config/config.example.json` and the OPCUASource
README for signal conventions and limitations. Generation is reproducible across
polling cadences and skipped polls. These synthetic values are not vendor calibration.

Both Dockerfiles build from the solution root and restore their Model dependency
explicitly. They run as the .NET non-root user (UID 1654). Mount a persistent, writable
`/home` volume, and place `config.json` there; for a bind mount, grant UID 1654 access.
Do not change or delete existing persisted data when granting access. Runtime files
are excluded from the image build context. Container-specific OPC-UA configuration
stores logs under `/home/logs` and certificate/cache state under
`/home/pki/basestar-server` or `/home/pki/basestar-source`; unsecured connections may
not create certificate stores until needed. Local development XML remains separate.

.github/workflows/docker-build-push.yml runs verification on main pushes and pull requests.
Manual dispatch publishes all three images: digiwells/dwisdaqbridgebasestarserver,
digiwells/dwisdaqbridgebasestaropcuasource and digiwells/dwisdaqbridgebasestarblackboardsink.
Each receives stable and sha-<full commit SHA> tags. DOCKERHUB_USERNAME and
DOCKERHUB_PASSWORD repository secrets are required. Deployment is a separate step.

## DWIS package versions
DWIS dependencies are selected in the Model project: RigOS Common Worker
1.0.7 and OPC Foundation client 2.2.54, resolving Blackboard packages
2.2.34 and vocabulary 1.0.176 through their package dependencies. DrillingProperties
10.2.11 is pinned so semantic attributes are emitted in manifests and SPARQL.


## Shared downhole mechanical contract

The active Server, OPCUASource and BlackboardSink hosts now use the shared runtime in
`DWIS.RigOS.DownholeMechanical.Bridge` NuGet (1.0.5) and contract version v1 in
`DWIS.RigOS.Contracts.DownholeMechanical` (package 1.1.0). See that runtime README for the
identity/placement algorithm, callbacks, telemetry, compact six-hour logs, retention and tests.
The earlier source/model classes and generated scalar artifacts remain preserved; their old
workers are not registered alongside the new hosts. Existing flat configuration must be migrated
explicitly to the instrument/channel mappings; no existing volume/configuration is overwritten.

Copy `config/downhole.config.example.json` to the host's `/home/config.json`. Sample IDs are
simulation fixtures, not discovered production equipment. The paired
`config/downhole.drillstring.example.json` illustrates the required current DrillString JSON.
Map real EquipmentIdentity.EquipmentInstanceID, SensorID, ComponentID and SensorTypeInt explicitly.
Namespace URI, string/full OPC-UA NodeID and source unit are configurable for every scalar tag.
Use `DWIS_CONFIG_PATH` and `--validate-config` for local checks. Unsupported channels are absent;
missing or stale configured values are null. Manufacturer, brand/model and serial/asset values
are read from current context, not guessed from tag names. No WebApp is added.

The simulator is an OPC-UA server exposing individual double values and source timestamps.
It listens on OPCUAURL (example port 48131). The bridge connects to that endpoint;
the sink subscribes to compound Blackboard outputs. In Docker, give each host a separate bind
volume under `C:/Volumes` mounted at `/home`; use routable addresses such as
`host.docker.internal` in bridge/sink configurations. Bind the simulator to `0.0.0.0` inside
its container and map the configured port. ContractsPublicBaseUrl must be reachable by viewers,
for example `http://host.docker.internal:54321/` for the local contract service. Its image must
be rebuilt/published to serve the new DownholeMechanical routes. The contextual DrillString
bridge must also include the refreshed generated DTOs so it does not discard identity fields.

Docker builds use this repository root as their context; no sibling Common checkout is needed.
The contract is pinned to NuGet 1.1.0 and runtime to 1.0.5. For example:
`docker build -f DWIS.DAQBridge.BaseStar.Server/Dockerfile .`
GitHub Actions builds and validates on pushes and pull requests. Only a manual workflow dispatch
publishes Server, OPCUASource and BlackboardSink images as `stable` plus immutable commit-SHA tags.
No `common_ref` input or cross-repository checkout is used.

BaseStar accepts mud pulse only, or mud pulse plus wired pipe; every configured instrument
must have a mud-pulse path. The example preserves the 26 inspected legacy channel meanings
and now illustrates one physical unit with both telemetry paths. Additional accelerometer/gyro/
magnetometer/bending channels can be mapped explicitly without inventing their statistics.

## One BaseStar with independent telemetry schedules
The example has one physical equipment UUID, with 26 individual OPC-UA nodes per telemetry path.
Matching channel meanings on the two paths share the same SensorID and equipment identity; their
NodeIDs remain distinct. The paired DrillString fixture contains only that physical instrument.

SimulatorProductionInterval is `00:00:02.540`: all 26 wired-pipe values share a production time
and are updated together every 2.54 seconds. SimulatorMudPulseCycleDuration is `00:01:00`: the
26 mud-pulse channels are transmitted one at a time in config order, about 2.3076923 seconds
apart. The complete set spans the 60-second cycle and each individual channel repeats after 60
seconds. Only that channel gets a new source timestamp; the others keep their prior timestamps.
No synchronized mud-pulse dataset is synthesized. Values not sent yet remain unavailable.

SimulatorPhaseOffset is zero in the example. Both schedules restart from the source's startup
origin. Every changed wired-pipe frame is published without an output throttle. Mud-pulse
publication retains its one-second minimum interval, allowing each arrival through. MaximumInputAge is 120 seconds, longer than a full
mud-pulse cycle, so ordinary interleaving does not blank healthy slow channels.

Runtime 1.0.5 is published from Common through NuGet Trusted Publishing. Restore from nuget.org;
no local feed or ProjectReference to Common is needed. Pushes run verification; Docker image
publication and deployment remain separate release steps.

## Runtime 1.0.5 delivery and durability
OPC-UA subscription callbacks preserve every source sample, including multiple queued changes
for the same node. The bridge force-flushes each received batch to its compact input journal
before processing it; six-hour rotation and the shared storage limit still apply. Placement
resolution is cached by current DrillString revision. The simulator uses a dedicated production
clock, and transient subscription timeouts have a bounded recovery grace period.

The current DrillString must be advertised on the Blackboard after a Blackboard restart; starting
a measurement source alone does not restore contextual semantics. Equipment identity and sensor
positions are resolved from that description. WiredPipePublishInterval is retained as a simulator
fallback when SimulatorProductionInterval is omitted; it no longer drops wired-pipe arrivals.

## Polar bending support
Runtime 1.0.5 uses mechanical contract 1.1.0. Configured polar inputs use
`BendingMomentMagnitude` (N.m) and `BendingMomentAngle` (SI radians), both with
`Axis=None`. Source angle units may be `rad` or `deg`. Cartesian `BendingMoment`
X/Y inputs remain supported. Schema and metadata explicitly identify both forms;
the runtime does not invent counterpart channels. Existing configurations, physical
UUIDs, telemetry timing and mounted histories are preserved during upgrades.

Runtime 1.0.5 independently advertises configured manufacturer/model and sensor ownership even
without a current DrillString. DAQDisplay can label each model row while positions and readings
remain explicitly unavailable until context validates. No configuration or contract schema change is required.

The query lifecycle dependency release uses OPC Foundation client 2.2.55 (directly or through mechanical bridge 1.0.6) and Common.Worker 1.0.8 where applicable. Repeated query registrations are deduplicated and released when their session closes. Existing configuration and persistent volumes are preserved.
