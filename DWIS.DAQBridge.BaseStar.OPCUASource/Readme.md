# Helper Program to Generate Live Signals on OPC-UA for the Base Star Mechanical Sub
This project provides an OPC-UA source that exposes live Base Star mechanical sub signals, primarily for integration testing and end-to-end validation of the DAQ Bridge.

## Purpose
- Publish Base Star signal values over OPC-UA in a consistent, testable way.
- Allow the bridge and downstream consumers to validate subscriptions and data flow.
- Provide a controllable signal source without the physical sub connected.

## What it does
- Emits random values for each signal on an OPC-UA server.
- Use the OPC-UA Node ID defintion defined alongside the signal semantic descriptions.

## When to use it
- During development to validate OPC-UA connectivity.
- For integration tests that require predictable signal publishing.
- When troubleshooting data flow into the D-WIS Blackboard.

