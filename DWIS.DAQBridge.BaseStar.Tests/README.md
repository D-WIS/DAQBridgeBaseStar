# BaseStar worker tests
Run `dotnet test DWIS.DAQBridge.BaseStar.Tests/DWIS.DAQBridge.BaseStar.Tests.csproj`
from the solution directory. Tests use simulated OPC-UA clients and do not write to
live Blackboard or OPC-UA servers. See the root README for live recovery verification.

`MaxRawWeightTests` exercises the actual source and server worker methods with simulated
OPC-UA clients, including initialization, the double contract, registration, unit
conversion to SI, and Blackboard publication.
