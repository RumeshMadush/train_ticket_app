using Microsoft.AspNetCore.Mvc;
using TrainTicketApp.Models;
using TrainTicketApp.Services;

namespace TrainTicketApp.Controllers;

public class SpecialRequestController : Controller
{
    private readonly DataStore _store;

    public SpecialRequestController(DataStore store)
    {
        _store = store;
    }

    // GET: /SpecialRequest
    public IActionResult Index(string? status)
    {
        var requests = _store.GetAllRequests().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, out var statusEnum))
            requests = requests.Where(r => r.Status == statusEnum);

        ViewBag.Status = status;
        ViewBag.Bookings = _store.GetAllBookings().ToList();
        return View(requests.OrderByDescending(r => r.CreatedAt).ToList());
    }

    // GET: /SpecialRequest/Create
    public IActionResult Create(int? bookingId)
    {
        PopulateBookings();
        var booking = bookingId.HasValue ? _store.GetBookingById(bookingId.Value) : null;
        var date = DateTime.Today;
        if (booking is not null)
        {
            while (!booking.OccursOn(date) && date < DateTime.Today.AddYears(1)) date = date.AddDays(1);
        }
        return View(new SpecialRequest
        {
            BookingId = bookingId ?? 0,
            DateNeeded = booking is null ? DateTime.Today.AddDays(1) : date
        });
    }

    // POST: /SpecialRequest/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(SpecialRequest request)
    {
        ValidateBooking(request.BookingId);
        ValidateRequestDate(request);
        if (!ModelState.IsValid)
        {
            PopulateBookings();
            return View(request);
        }

        _store.AddRequest(request);
        TempData["Success"] = "Special request submitted.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /SpecialRequest/Edit/5
    public IActionResult Edit(int id)
    {
        var request = _store.GetRequestById(id);
        if (request is null) return NotFound();
        PopulateBookings();
        return View(request);
    }

    // POST: /SpecialRequest/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, SpecialRequest request)
    {
        if (id != request.Id) return BadRequest();
        ValidateBooking(request.BookingId);
        ValidateRequestDate(request);
        if (!ModelState.IsValid)
        {
            PopulateBookings();
            return View(request);
        }

        _store.UpdateRequest(request);
        TempData["Success"] = "Request updated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /SpecialRequest/Delete/5
    public IActionResult Delete(int id)
    {
        var request = _store.GetRequestById(id);
        if (request is null) return NotFound();
        return View(request);
    }

    // POST: /SpecialRequest/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        _store.DeleteRequest(id);
        TempData["Success"] = "Request deleted.";
        return RedirectToAction(nameof(Index));
    }

    private void PopulateBookings() => ViewBag.Bookings = _store.GetAllBookings()
        .Where(booking => booking.Status != BookingStatus.Cancelled)
        .OrderBy(booking => booking.TravelDate)
        .ToList();

    private void ValidateBooking(int bookingId)
    {
        if (_store.GetBookingById(bookingId) is null)
            ModelState.AddModelError(nameof(SpecialRequest.BookingId), "Select a valid booking.");
    }

    private void ValidateRequestDate(SpecialRequest request)
    {
        var booking = _store.GetBookingById(request.BookingId);
        if (booking is not null && !booking.OccursOn(request.DateNeeded))
            ModelState.AddModelError(nameof(request.DateNeeded), "The requested date must match a journey occurrence.");
    }
}
