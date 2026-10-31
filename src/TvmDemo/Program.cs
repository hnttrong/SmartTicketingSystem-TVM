using TvmDemo.Controllers;
using TvmDemo.Data;
using TvmDemo.Models;
using TvmDemo.Services;
using TvmDemo.Views;

namespace TvmDemo;

/// <summary>
/// Application entry point wiring MVC dependencies (<see cref="TicketVendorMachine"/>,
/// <see cref="SqlTvmRepository"/>, <see cref="MockPaymentGateway"/>, <see cref="PaymentService"/>,
/// <see cref="TicketPrinter"/>, <see cref="TicketController"/>, and <see cref="MainForm"/>).
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();

        AppSettingsConfig config = AppSettingsConfig.Load();

        var machine = new TicketVendorMachine
        {
            MachineCode = config.MachineCode,
            OriginStationEn = config.OriginStationEn,
            OriginStationVi = config.OriginStationVi
        };

        var repository = new SqlTvmRepository(config.GetActiveConnectionString());
        var mockGateway = new MockPaymentGateway();
        var paymentService = new PaymentService(mockGateway);
        var ticketPrinter = new TicketPrinter();

        var controller = new TicketController(
            machine,
            repository,
            paymentService,
            mockGateway,
            ticketPrinter);

        Application.Run(new MainForm(controller, config));
    }
}
