# BaseStar OPC-UA simulator

This helper publishes 26 scalar BaseStar signals to an existing OPC-UA server. It
is a reproducible integration-test source for the bridge, without physical hardware.

The simulator starts with a complete sample. Pressure, temperature and rotational
conditions refresh every 10 seconds; mechanical/dynamic measurements refresh every
30 seconds. The current sample is republished every 5 seconds, including unchanged
values, so source timestamps can advance without producing new bridge records.
Generation and publication are independent of the polling cadence. Missed polls
advance directly to the latest scheduled generation.

Set `SimulationSeed` (default `20261017`),
`SimulationConditionGenerationPeriod`, `SimulationDataGenerationPeriod`, and
`SimulationOPCUAPublishPeriod` in `/home/config.json` (Windows: `../home/config.json`).
Restart the source to apply these settings. Non-positive periods use their defaults.
See `../config/config.example.json` for a complete example. The same seed and elapsed
schedule reproduce the same values, even with different polling intervals.

Values use canonical SI units: rad/s, m/s², N, N·m, Pa and K. Synthetic conditions
include roughly 120 rpm, 24 MPa annular pressure and 343 K electronics temperature.
Minimum/average/maximum measurements are ordered; corrected weight/torque use the
configured synthetic tare/bias relationships. These are plausible test scenarios,
not vendor calibration or a physical model. No uncertainty distributions are invented.

`OPCUAURL` selects the existing server. The established manifest namespace and tag
mapping settings apply. Source writes are one checked batch; `UnitConversions`
convert SI samples back to wire values, while the bridge converts them to SI.
Rejected writes fail visibly. Automatic connection retry remains a Server feature;
restart this helper after an unavailable endpoint or source connection failure.

Build the Dockerfile from the solution root. The image runs as UID 1654 and expects
a writable persistent `/home` mount containing `config.json`. Its OPC-UA logs and
certificate paths are under `/home/logs` and `/home/pki/basestar-source`.
