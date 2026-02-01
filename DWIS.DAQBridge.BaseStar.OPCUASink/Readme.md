# Helper Program to Read from the DWIS Blackboard Signals Transferred from the Base Star Mechanical Sub
This project provides DWIS Blackboard sink that reads Base Star mechanical sub signals after they have been published to the D-WIS Blackboard.

## Purpose
- Validate that Base Star signals arrive on the Blackboard as expected.
- Provide a convenient way to inspect or consume published data.
- Support troubleshooting of the bridge and publication pipeline.

## What it does
- Connects to D-WIS to read the Base Star signal set.
- Subscribes to updates and surfaces the latest values.
- Uses the Base Star semantic model to interpret signal metadata.

## When to use it
- During integration testing to confirm Blackboard publication.
- When diagnosing signal mapping or data quality issues.
- As a lightweight consumer for Base Star signal verification.

