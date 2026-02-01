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
