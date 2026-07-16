using Microsoft.AspNetCore.Mvc;
using TrainTicketApp.Models;
using TrainTicketApp.Services;

namespace TrainTicketApp.Controllers;

public class ScheduleController : Controller
{
    private readonly DataStore _store;

    public ScheduleController(DataStore store)
    {
        _store = store;
    }

    // GET: /Schedule
    public IActionResult Index(string? search)
    {
        var schedules = _store.GetAllSchedules().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
            schedules = schedules.Where(s =>
                s.DepartureStation.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.ArrivalStation.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.TrainNumber.Contains(search, StringComparison.OrdinalIgnoreCase));

        ViewBag.Search = search;
        return View(schedules.OrderBy(s => s.DepartureTime).ToList());
    }

    // GET: /Schedule/Details/5
    public IActionResult Details(int id)
    {
        var schedule = _store.GetScheduleById(id);
        if (schedule is null) return NotFound();
        return View(schedule);
    }

    // GET: /Schedule/Create
    public IActionResult Create()
    {
        return View(new Schedule
        {
            DepartureTime = new TimeSpan(8, 0, 0),
            ArrivalTime = new TimeSpan(10, 0, 0),
            StandardSeatCount = 80,
            FirstClassSeatCount = 20,
            StandardPrice = 45.00m,
            FirstClassPrice = 90.00m,
            DaysRunning = new List<DayOfWeek>()
        });
    }

    // POST: /Schedule/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Schedule schedule, List<DayOfWeek>? daysRunning)
    {
        schedule.DaysRunning = daysRunning ?? [];
        ValidateSchedule(schedule);
        if (!ModelState.IsValid) return View(schedule);

        _store.AddSchedule(schedule);
        TempData["Success"] = "Schedule created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Schedule/Edit/5
    public IActionResult Edit(int id)
    {
        var schedule = _store.GetScheduleById(id);
        if (schedule is null) return NotFound();
        return View(schedule);
    }

    // POST: /Schedule/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Schedule schedule, List<DayOfWeek>? daysRunning)
    {
        if (id != schedule.Id) return BadRequest();
        schedule.DaysRunning = daysRunning ?? [];
        ValidateSchedule(schedule);
        ValidateExistingReservations(schedule);
        if (!ModelState.IsValid) return View(schedule);

        _store.UpdateSchedule(schedule);
        TempData["Success"] = "Schedule updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Schedule/Delete/5
    public IActionResult Delete(int id)
    {
        var schedule = _store.GetScheduleById(id);
        if (schedule is null) return NotFound();
        ViewBag.HasBookings = _store.HasBookingsForSchedule(id);
        return View(schedule);
    }

    // POST: /Schedule/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        if (!_store.DeleteSchedule(id))
        {
            TempData["Error"] = "This schedule is linked to one or more bookings. Deactivate it instead of deleting it.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["Success"] = "Schedule deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateSchedule(Schedule schedule)
    {
        AddRequiredTextError(schedule.TrainNumber, nameof(schedule.TrainNumber), "Enter a train number.");
        AddRequiredTextError(schedule.DepartureStation, nameof(schedule.DepartureStation), "Enter a departure station.");
        AddRequiredTextError(schedule.ArrivalStation, nameof(schedule.ArrivalStation), "Enter an arrival station.");

        schedule.TrainNumber = schedule.TrainNumber?.Trim() ?? string.Empty;
        schedule.DepartureStation = schedule.DepartureStation?.Trim() ?? string.Empty;
        schedule.ArrivalStation = schedule.ArrivalStation?.Trim() ?? string.Empty;
        schedule.DaysRunning ??= [];

        if (schedule.DepartureStation.Length > 0 &&
            schedule.ArrivalStation.Length > 0 &&
            string.Equals(schedule.DepartureStation, schedule.ArrivalStation, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(schedule.ArrivalStation), "Arrival station must be different from the departure station.");
        if (schedule.DaysRunning.Count == 0)
            ModelState.AddModelError(nameof(schedule.DaysRunning), "Select at least one running day.");
        if (schedule.StandardSeatCount <= 0)
            ModelState.AddModelError(nameof(schedule.StandardSeatCount), "At least one standard seat is required.");
        if (schedule.FirstClassPrice < schedule.StandardPrice)
            ModelState.AddModelError(nameof(schedule.FirstClassPrice), "First class price cannot be lower than the standard price.");
    }

    private void AddRequiredTextError(string? value, string key, string message)
    {
        if (!string.IsNullOrWhiteSpace(value)) return;
        if (ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0) return;
        ModelState.AddModelError(key, message);
    }

    private void ValidateExistingReservations(Schedule schedule)
    {
        var bookings = _store.GetAllBookings().Where(item => item.ScheduleTemplateId == schedule.Id && item.Status != BookingStatus.Cancelled).ToList();
        foreach (var booking in bookings)
        {
            var invalidRecurring = booking is RecurringBooking recurring && (recurring.Rule.Frequency switch
            {
                RecurrenceFrequency.Daily => schedule.DaysRunning.Count != 7,
                RecurrenceFrequency.Weekdays => new[]{DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday}.Any(day=>!schedule.DaysRunning.Contains(day)),
                _ => recurring.Rule.DaysOfWeek.Any(day => !schedule.DaysRunning.Contains(day))
            });
            if (!schedule.DaysRunning.Contains(booking.TravelDate.DayOfWeek) || invalidRecurring)
            {
                ModelState.AddModelError(nameof(schedule.DaysRunning), $"The running days would invalidate booking {booking.ReferenceNumber}.");
                break;
            }
            var existing = _store.GetScheduleById(schedule.Id)!;
            foreach (var reservation in booking.SeatSelections)
            {
                var seat = existing.Seats.First(item => item.Id == reservation.ScheduleSeatId);
                var newLimit = seat.SeatClass == SeatClass.FirstClass ? schedule.FirstClassSeatCount : schedule.StandardSeatCount;
                if (seat.ClassOrdinal > newLimit) ModelState.AddModelError(string.Empty, $"Capacity cannot remove reserved seat {seat.SeatNumber}.");
            }
        }
    }
}
