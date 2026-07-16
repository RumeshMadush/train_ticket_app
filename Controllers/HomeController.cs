using Microsoft.AspNetCore.Mvc;
using TrainTicketApp.Models;
using TrainTicketApp.Services;

namespace TrainTicketApp.Controllers;

public sealed class HomeController(DataStore store,BookingOccurrenceService occurrences,WeeklyReportService reports) : Controller
{
    public IActionResult Index()
    {
        var bookings=store.GetAllBookings(); var report=reports.Generate(DateTime.Today); var upcoming=occurrences.GenerateAll(DateTime.Today,DateTime.Today.AddDays(21)).Take(5).ToList();
        ViewBag.TotalBookings=bookings.Count;ViewBag.RecurringBookings=bookings.Count(item=>item.BookingType==BookingKind.Recurring);ViewBag.TotalSchedules=store.GetAllSchedules().Count(item=>item.IsActive);ViewBag.TotalRequests=store.GetAllRequests().Count;ViewBag.PendingRequests=store.GetAllRequests().Count(item=>item.Status==RequestStatus.Pending);ViewBag.ThisWeekBookings=report.TotalBookings;ViewBag.ThisWeekSeats=report.TotalSeats;ViewBag.ThisWeekRevenue=report.TotalCost;ViewBag.UpcomingOccurrences=upcoming;ViewBag.NextTrip=upcoming.FirstOrDefault();
        return View();
    }
}
