SMART TICKETING SYSTEM – TVM
Software Engineering Lab Group Assignment 1 – Sem 1_2627

GITHUB
https://github.com/hnttrong/SmartTicketingSystem-TVM

FIGMA PROTOTYPE
https://www.figma.com/design/chjKyWaJin2KMWQ9iaUzuP/TVM

REQUIREMENTS
- Windows
- Microsoft Visual Studio 2022 (v17.8 or later) with the ".NET desktop development" workload
- .NET 8 SDK (C# 12, net8.0-windows, Windows Forms)
- Microsoft SQL Server 2022 Express or LocalDB
- SQL Server Management Studio (optional; sqlcmd can be used instead)
- Microsoft.Data.SqlClient 5.2.2 (restored automatically by NuGet)

DATABASE SETUP
1. Open SQL Server Management Studio and connect with Windows Authentication.
2. Open db/TvmDemo_Setup.sql and press F5.
3. Confirm that the TvmDemo database has been created.

CONNECTION STRING
Default profile is LocalDb: Server=(localdb)\MSSQLLocalDB;Database=TvmDemo;Integrated Security=True;TrustServerCertificate=True;
If you use SQL Server Express instead, select the SqlExpress profile on the Simulator Bar or edit src/TvmDemo/appsettings.json.

RUN THE DEMO
1. Run db/TvmDemo_Setup.sql once on LocalDB or SQLEXPRESS (see DATABASE SETUP).
2. Open src/TvmDemo.sln in Visual Studio 2022 and press F5
   (or run: dotnet run --project src/TvmDemo/TvmDemo.csproj).
3. On the Simulator Bar, select the database profile and click Connect DB. Confirm that DB OK appears.
4. Buy a ticket: S01 Start -> S02 destination and quantity (1-5) -> S03 payment mode -> S04a card / S04b QR -> S05a ticket issued.

SIMULATION (Instructor Simulator Bar)
- Gateway Result: Success (payment approved, tickets generated), Declined (payment rejected, no ticket), Timeout (payment expires, no ticket).
- Printer Fault: tick to simulate a printer fault after payment (S08 Out of Service, refund record created).
- SQL Server profile: switch the database connection (an invalid profile shows S08 Out of Service).
- Output Report (DB): DataGridView with the saved transactions and tickets.

KNOWN LIMITATIONS
- Payment gateways (CardAcquirerAdapter, VietQRAdapter, MomoAdapter, ZaloPayAdapter, VNPayAdapter) are simulated in memory with about 1.8 s of async latency; no live merchant credentials are used.
- Refunds are recorded in memory/DB only; no real interbank settlement.
- No offline store-and-forward queue: if SQL Server is unreachable the kiosk locks into S08 Out of Service.
- The demo connects directly to local SQL Server Express/LocalDB through SqlClient (no separate ASP.NET Core API server).
- Barcode and QR codes are drawn on screen (GDI+); no physical thermal printer or card reader is driven.
- The Class Designer skeleton (Visual Studio, section 5.1) targets .NET Framework 4.8.1; the runnable demo targets .NET 8.

TEAM CONTRIBUTIONS
Student ID | Full Name | Contribution (%) | Main Tasks
524H0168 | Dang Hieu Minh | 20% | Requirements and report integration: Clarification Q&A, requirements specification, sections 1, 2, 6, 7 and 8, final report
524H0161 | Pham Cong Quoc Huy | 20% | Use case modeling and activity diagrams
524H0166 | Nguyen Trong Hoang Linh | 20% | Sequence, state chart and class diagrams; architecture diagram
524H0200 | Hua Nguyen Thanh Trong | 20% | Visual Studio engineering, GitHub repository, Copilot prompt documentation, readme.txt
524H0199 | Le Hoang Tri | 20% | UI/UX touchscreen mockups (4.1); UML deployment diagram (4.2.2); C# .NET 8 WinForms and MSSQL demo (5.3)
Total: 100%
