# SmartTicketingSystem-TVM
Software Engineering Lab Assignment 1 - Smart Ticketing System

## M4: Visual Studio engineering

The current project is a C# class library for the TVM model, built using
Visual Studio 2026 and .NET Framework 4.8.1. Install the Class Designer
component to open the class diagram.

### Files

- `src/SmartTicketing/SmartTicketing.slnx`: Visual Studio solution.
- `src/SmartTicketing/SmartTicketing.TVM/TVM_Core.cd`: Class Designer diagram.
- `src/SmartTicketing/SmartTicketing.TVM/`: C# classes, interfaces and enums.
- `diagrams/m4/`: exported diagram images.

### Build

Open the solution in Visual Studio 2026 and select **Build > Build Solution**.
The .NET Framework 4.8.1 targeting pack is required.

### Code-to-model changes

- `Transaction.LastFailureReason` and the updated `MarkFailed` method record
  the failure reason, increment the retry count and set the failed state.
- `Ticket.IsValid(DateTime checkedAt)` checks the ticket validity interval.
- `TicketPrinter.HasEnoughPaper(int quantity)` checks paper capacity, assuming
  one paper unit per ticket.

These additions require review against the team's shared M3 design.

### Current status

This is a model/code engineering skeleton, not a runnable ticket-purchase demo.
Most methods intentionally throw `NotImplementedException`. No live payment
gateway, hardware integration, MSSQL persistence or user interface is implemented
in this project yet. M5 will implement the demo using the agreed model.

Code Map evidence and the team's report, commit log and AI prompt log are pending.
