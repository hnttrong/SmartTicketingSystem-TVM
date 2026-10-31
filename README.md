# Smart Ticketing System – TVM

Software Engineering Lab Assignment 1: a Ticket Vendor Machine (TVM) prototype for metro and bus ticket purchases.

The repository contains UML models, Visual Studio Class Designer work, UI mockups, and a C# WinForms demonstration with Microsoft SQL Server persistence.

## Repository Contents

| Path | Description |
|---|---|
| `diagrams/` | UML source files and exported diagram images |
| `diagrams/m4/` | Visual Studio engineering evidence |
| `src/SmartTicketing/` | C# model skeleton and Class Designer diagram |
| `src/TvmDemo.sln` | WinForms demo solution |
| `src/TvmDemo/` | Demo application source code |
| `db/TvmDemo_Setup.sql` | Database creation, sample data, and reporting queries |
| `ui/tvm_screens_1280x800.html` | Browser-based UI mockups |
| `ui/figma_svg_import/` | SVG screens and design assets |

## System Modeling

The UML models describe the Buy a Ticket use case:

- Sequence diagrams covering destination selection, payment, ticket printing, failures, and cancellation.
- A state chart describing the transaction flow.
- Core and supporting class diagrams.
- Architecture and deployment diagrams.

PlantUML source files and exported images are available in `diagrams/`.

## Visual Studio Engineering

### Requirements

- Visual Studio 2026 with the .NET desktop development workload.
- .NET Framework 4.8.1 targeting pack.
- The Visual Studio Class Designer component.

### Open and Build

1. Open `src/SmartTicketing/SmartTicketing.slnx`.
2. Select **Build > Build Solution**.
3. Open `src/SmartTicketing/SmartTicketing.TVM/TVM_Core.cd` to inspect the class diagram.

### Model-to-Code Work

The class model was recreated in Visual Studio Class Designer to produce C# class, interface, and enum skeletons.

This project is an engineering skeleton. Most operations intentionally contain `NotImplementedException`; use the separate WinForms project to run the demo.

### Code-to-Model Work

The C# skeleton was extended with:

- `Transaction.LastFailureReason`, with an updated `MarkFailed` method to record the failure reason, increment the retry count, and set the failed state.
- `Ticket.IsValid(DateTime checkedAt)` to check whether a time falls within the ticket validity interval.
- `TicketPrinter.HasEnoughPaper(int quantity)` to check paper capacity, assuming one paper unit per ticket.

These additions are reflected in the updated Class Designer model.

### Code Map

Code Map was not completed because the Visual Studio Enterprise feature was unavailable in the development environment.

## WinForms Demo

The demo uses C# Windows Forms, .NET 8, and Microsoft SQL Server.

### Features

- Metro and bus destination selection.
- Ticket quantities from 1 to 5.
- Fare calculation.
- Simulated card, bank QR, and digital wallet payments.
- Simulated successful, declined, and timed-out payments.
- Ticket preview with a ticket number and barcode-style image.
- Transaction storage in SQL Server.
- A transaction report showing payment status, transaction status, issued tickets, and completed sales totals.
- Vietnamese and English interface options.
- Large text and high contrast options.

## Demo Requirements

- Windows.
- Visual Studio with the .NET desktop development workload and .NET 8 SDK.
- Microsoft SQL Server, SQL Server Express, or SQL Server LocalDB.
- SQL Server Management Studio or another SQL query tool.

## Database Setup

1. Connect to your SQL Server instance.
2. Open `db/TvmDemo_Setup.sql`.
3. Execute the script to create and populate the `TvmDemo` database.
4. Confirm that the database was created on the same instance used by the application.

## Connection Configuration

Edit `src/TvmDemo/appsettings.json` before running the application.

The `ActiveProfile` value selects a connection string from `ConnectionStrings`.

For the default LocalDB instance:

```json
{
  "ActiveProfile": "LocalDb",
  "ConnectionStrings": {
    "LocalDb": "Server=(localdb)\\MSSQLLocalDB;Database=TvmDemo;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=4;"
  }
}
```

Update the relevant values in the existing configuration file and retain the other settings.

Examples of server names:

| SQL instance | Server value in JSON |
|---|---|
| Default LocalDB | `(localdb)\\MSSQLLocalDB` |
| Custom LocalDB instance named SQLEXPRESS | `(localdb)\\SQLEXPRESS` |
| SQL Server Express | `.\\SQLEXPRESS` |
| Default local SQL Server instance | `localhost` |

A custom LocalDB instance named SQLEXPRESS and SQL Server Express are different instances. Use the exact instance where you executed the setup script.

## Run the Demo

1. Open `src/TvmDemo.sln` in Visual Studio.
2. Restore NuGet packages if prompted.
3. Set `TvmDemo` as the startup project.
4. Build and run the application.
5. Select the matching SQL Server profile in the simulator panel.
6. Click **Connect DB**.
7. Confirm that the application displays **DB OK**.

Alternatively, from the repository root:

```powershell
dotnet restore src/TvmDemo/TvmDemo.csproj
dotnet run --project src/TvmDemo/TvmDemo.csproj
```

## Demonstration Scenarios

### Successful Purchase

1. Set **Gateway** to **Success**.
2. Leave **Printer Fault** unchecked.
3. Start a purchase and select a destination and quantity.
4. Select a payment method and continue.
5. Wait for the simulated payment result.
6. Inspect the ticket preview and transaction report.

### Declined Payment

1. Set **Gateway** to **Declined**.
2. Start a new purchase.
3. Continue to payment.
4. Inspect the declined message.
5. Choose another payment method or cancel the transaction.

### Payment Timeout

1. Set **Gateway** to **Timeout**.
2. Start a new purchase and continue to payment.
3. Inspect the timeout message.
4. Retry to create a new payment session or return to the home screen.

The simulator can return a timeout result before the displayed countdown reaches zero. This is a simulated gateway response.

### Transaction Report

Click **Output Report (DB)** to inspect stored transactions.

The report includes destination, quantity, total amount, payment method, payment status, transaction status, and ticket numbers. It also provides a status filter and summary totals.

## UI Mockups

Open `ui/tvm_screens_1280x800.html` in a browser to view the UI mockups.

Figma design:

https://www.figma.com/design/chjKyWaJin2KMWQ9iaUzuP/TVM

## Known Limitations

- Payments use a mock gateway; no real bank or wallet transaction is performed.
- Ticket printing and hardware faults are simulated.
- Barcode and QR images are demonstration visuals; standards-compliant scanning has not been verified.
- Production payment callbacks and automatic refunds are not implemented.
- The Class Designer skeleton targets .NET Framework 4.8.1, while the demo targets .NET 8.
- The modeling skeleton and demo are separate projects; changes are not automatically synchronized.
- Code Map evidence is unavailable.

## Collaboration

Work is organized into feature branches and integrated into `main` through pull requests.

Commit history and pull requests provide evidence of contributions and reviews. The team workbook records contribution details, artifact progress, and AI prompt usage.

The final report and workbook are maintained by the team member responsible for submission.
