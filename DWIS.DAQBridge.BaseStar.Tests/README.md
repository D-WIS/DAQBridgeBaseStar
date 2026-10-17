# BaseStar worker tests
Run `dotnet test DWIS.DAQBridge.BaseStar.Tests/DWIS.DAQBridge.BaseStar.Tests.csproj`
from the solution directory. Tests use simulated OPC-UA clients and do not write to
live Blackboard or OPC-UA servers. See the root README for live recovery verification.

`MaxRawWeightTests` exercises the actual source and server worker methods with simulated
OPC-UA clients, including initialization, the double contract, registration, unit
conversion to SI, and Blackboard publication.

`DataLoggingTests` checks compact numeric snapshots, per-signal change detection,
raw/published logging, UTC rotation, ZIP archives, storage quotas, safe file ownership,
late appends, history restoration and expiration during idle periods, failed-write
retries, and continued publication when file logging fails. The 48-hour tests use
explicit timestamps rather than waiting in wall-clock time.
