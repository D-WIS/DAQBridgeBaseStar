# Semantic Descriptions of the Signals used by the BaseStar Mechanical Sub Bridge to D-WIS
This project retains the scalar BaseStar model and its generators for source compatibility. Active provider hosts use the shared DownholeMechanical contract and runtime in the sibling Common repository.

## Purpose
- Provide a single, authoritative description of BaseStar signal meaning.
- Keep signal names and identifiers consistent across the solution.
- Enable downstream tooling to interpret BaseStar signals correctly.

## What it contains
- Semantic definitions for each BaseStar signal exposed by the bridge.
- The metadata required to map signals to D-WIS concepts.
- Shared structures that other projects use to generate manifests and publish data.

In the retained scalar model, every physical measurement has a mud-pulse property and a `WiredPipe` property. Active compound hosts instead use explicit channel mappings for mud pulse alone or mud pulse plus wired pipe.
Their semantic outputs and telemetry relationships are distinct, while both identify
the Halliburton BaseStar model and instrument instance. Their default OPC-UA IDs and
namespaces are also distinct and can be overridden per signal.

## When to update
- When a BaseStar signal is added, removed, or renamed.
- When units, ranges, or identifiers change.
- Before regenerating manifests or releasing updates that depend on signal metadata.

## DWIS package versions
DWIS dependencies are selected in the Model project: RigOS Common Worker
1.0.7 and OPC Foundation client 2.2.54, resolving Blackboard packages
2.2.34 and vocabulary 1.0.176 through their package dependencies. DrillingProperties
10.2.11 is explicit because its manifest and SPARQL generators preserve semantic
attribute fields such as the BaseStar model identifier and model name.


New compound output uses DWIS.RigOS.Contracts.DownholeMechanical; existing scalar model classes
and generated artifacts are retained for source compatibility and existing regression tests.
The contract owns the wire shape; provider hosts do not redefine its measurement DTOs.
