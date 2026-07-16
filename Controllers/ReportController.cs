using Microsoft.AspNetCore.Mvc;
using TrainTicketApp.Services;

namespace TrainTicketApp.Controllers;

public sealed class ReportController(WeeklyReportService reports) : Controller
{
    public IActionResult Index(string? weekOf){var selected=DateTime.TryParse(weekOf,out var parsed)?parsed:DateTime.Today;return View(reports.Generate(selected));}
}
