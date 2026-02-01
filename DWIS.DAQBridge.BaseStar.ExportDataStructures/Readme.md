# Helper Program to Generate Signals' Manifests
This project provides a small, focused export tool that generates the manifest entries describing each Base Star mechanical sub signal published to the D-WIS Blackboard.

## Purpose
- Produce consistent, machine-readable signal definitions.
- Ensure the Base Star signal set is represented accurately for downstream consumers.
- Keep manifest generation repeatable and easy to audit.

## What it does
- Reads the Base Star signal model used in the DAQ Bridge.
- Produces manifest records for every published signal.
- Outputs the manifests in a format suitable for ingestion by D-WIS tooling.

## When to use it
- After adding or modifying Base Star signal definitions.
- Before publishing updates to the Blackboard so manifests stay in sync.
