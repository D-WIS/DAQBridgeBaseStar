# BaseStar OPC-UA simulator

This helper publishes 52 scalar BaseStar signals (26 measurements on each of the
mud-pulse and wired-pipe paths) to an existing OPC-UA server. It
is a reproducible integration-test source for the bridge, without physical hardware.

The simulator starts with a complete sample. Pressure, temperature and rotational
conditions refresh every 10 seconds; mud-pulse mechanical/dynamic measurements refresh
every 30 seconds and wired-pipe measurements every 2.5 seconds. Only changed tags are
written to the OPC-UA server.
Generation and publication are independent of the polling cadence. Missed polls
advance directly to the latest scheduled generation.

Set `SimulationSeed` (default `20261017`),
`SimulationConditionGenerationPeriod`, `SimulationDataGenerationPeriod`,
`SimulationWiredPipeDataGenerationPeriod`, and
`SimulationOPCUAPublishPeriod` in `/home/config.json` (Windows: `../home/config.json`).
Restart the source to apply these settings. Non-positive periods use their defaults.
The publication period controls evaluation; an unchanged signal is not rewritten.
See `../config/config.example.json` for a complete example. The same seed and elapsed
schedule reproduce the same values, even with different polling intervals.

Values use canonical SI units: rad/s, m/s², N, N·m, Pa and K. Synthetic conditions
include roughly 120 rpm, 24 MPa annular pressure and 343 K electronics temperature.
Minimum/average/maximum measurements are ordered; corrected weight/torque use the
configured synthetic tare/bias relationships. These are plausible test scenarios,
not vendor calibration or a physical model. No uncertainty distributions are invented.

`OPCUAURL` selects the existing server. `OPCUANameSpaces` and `OPCUAIDs` are keyed by
the default signal ID (for example `MaxRawWeight` or `MaxRawWeightWiredPipe`) and can
map every signal to an unrelated namespace and NodeID. `NameSpace` and `NodeIDPrefix`
remain fallbacks. Source writes are one checked batch; `UnitConversions`
convert SI samples back to wire values, while the bridge converts them to SI.
Rejected writes fail visibly. Automatic connection retry remains a Server feature;
restart this helper after an unavailable endpoint or source connection failure.

Build the Dockerfile from the solution root. The image runs as UID 1654 and expects
a writable persistent `/home` mount containing `config.json`. Its OPC-UA logs and
certificate paths are under `/home/logs` and `/home/pki/basestar-source`.


## Downhole contract runtime
This OPCUASource host runs `RuntimeHost` for BaseStar. It consumes `DWIS.RigOS.DownholeMechanical.Bridge` NuGet 1.0.5. See the repository README
and the [shared runtime documentation](https://github.com/D-WIS/Common/blob/main/DWIS.RigOS.DownholeMechanical.Bridge/README.md) for `/home/config.json`, explicit identity/node/unit mappings, telemetry,
reconnection, persistent logging and Docker build contexts. Existing legacy workers are not
registered by this host.

The current example simulates one BaseStar: all 26 wired-pipe values every 2.54 seconds,
and 26 mud-pulse values one at a time across a 60-second cycle. Configure the instrument's
SimulatorProductionInterval and SimulatorMudPulseCycleDuration; see the root README for
ordering, timestamps, freshness and local preview-package verification.
