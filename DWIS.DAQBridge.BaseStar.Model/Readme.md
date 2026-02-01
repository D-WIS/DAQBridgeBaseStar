# Semantic Descriptions of the Signals used by the BaseStar Mechanical Sub Bridge to D-WIS
This project defines the semantic model for BaseStar mechanical sub signals as used by the DAQ Bridge to publish into D-WIS.

## Purpose
- Provide a single, authoritative description of BaseStar signal meaning.
- Keep signal names and identifiers consistent across the solution.
- Enable downstream tooling to interpret BaseStar signals correctly.

## What it contains
- Semantic definitions for each BaseStar signal exposed by the bridge.
- The metadata required to map signals to D-WIS concepts.
- Shared structures that other projects use to generate manifests and publish data.

## When to update
- When a BaseStar signal is added, removed, or renamed.
- When units, ranges, or identifiers change.
- Before regenerating manifests or releasing updates that depend on signal metadata.
