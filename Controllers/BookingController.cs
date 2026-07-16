using Microsoft.AspNetCore.Mvc;
using TrainTicketApp.Models;
using TrainTicketApp.Services;

namespace TrainTicketApp.Controllers;

public sealed class BookingController(DataStore store, BookingService bookingService, BookingOccurrenceService occurrences) : Controller
{
    public IActionResult Index(string? search, string? status, string? type, string? sort)
    {
        var bookings = store.GetAllBookings().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search)) bookings = bookings.Where(item => item.PassengerName.Contains(search,StringComparison.OrdinalIgnoreCase) || item.ReferenceNumber.Contains(search,StringComparison.OrdinalIgnoreCase) || item.DepartureStation.Contains(search,StringComparison.OrdinalIgnoreCase) || item.ArrivalStation.Contains(search,StringComparison.OrdinalIgnoreCase));
        if (Enum.TryParse<BookingStatus>(status,out var parsedStatus)) bookings = bookings.Where(item => item.Status == parsedStatus);
        if (Enum.TryParse<BookingKind>(type,out var parsedType)) bookings = bookings.Where(item => item.BookingType == parsedType);
        bookings = sort switch { "date_asc"=>bookings.OrderBy(item=>item.TravelDate),"date_desc"=>bookings.OrderByDescending(item=>item.TravelDate),"price_asc"=>bookings.OrderBy(item=>item.TotalPrice),"price_desc"=>bookings.OrderByDescending(item=>item.TotalPrice),_=>bookings.OrderByDescending(item=>item.CreatedAt) };
        ViewBag.Search=search; ViewBag.Status=status; ViewBag.Type=type; ViewBag.Sort=sort;
        return View(bookings.ToList());
    }

    public IActionResult Details(int id)
    {
        var booking = store.GetBookingById(id); if (booking is null) return NotFound();
        ViewBag.Schedule = store.GetScheduleById(booking.ScheduleTemplateId);
        ViewBag.Requests = store.GetAllRequests().Where(item=>item.BookingId==id).ToList();
        ViewBag.Upcoming = occurrences.Generate(booking,DateTime.Today,DateTime.Today.AddDays(60)).Take(8).ToList();
        return View(booking);
    }

    public IActionResult Create(int? scheduleId)
    {
        var schedule = scheduleId.HasValue ? store.GetScheduleById(scheduleId.Value) : store.GetAllSchedules().FirstOrDefault(item=>item.IsActive);
        var model = new BookingFormViewModel { ScheduleId=schedule?.Id??0,TravelDate=NextRunningDate(schedule,DateTime.Today.AddDays(1)),RecurrenceEndDate=DateTime.Today.AddMonths(1) };
        if (schedule is not null)
        {
            var seat = schedule.Seats.First(item=>item.SeatClass==SeatClass.Standard);
            model.SeatReservations.Add(new SeatReservationInputModel { PassengerName=store.CurrentUser.Name,ScheduleSeatId=seat.Id,ReservedPrice=schedule.StandardPrice });
        }
        PopulateForm(model.ScheduleId,model.TravelDate,null);
        return View(model);
    }

    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult Create(BookingFormViewModel model)
    {
        NormalizeFormModel(model);
        var booking = BuildBooking(model);
        if (!ModelState.IsValid || booking is null) { PopulateForm(model.ScheduleId,model.TravelDate,null); return View(model); }
        var result = bookingService.Create(booking);
        if (!result.IsSuccess) { foreach(var error in result.Errors) ModelState.AddModelError(string.Empty,error); PopulateForm(model.ScheduleId,model.TravelDate,null); return View(model); }
        TempData["Success"] = booking.BookingType==BookingKind.Recurring ? "Recurring multi-seat booking series created." : "One-off multi-seat booking created.";
        return model.AddSpecialRequestAfterBooking ? RedirectToAction("Create","SpecialRequest",new{bookingId=result.Value!.Id}) : RedirectToAction(nameof(Details),new{id=result.Value!.Id});
    }

    public IActionResult Edit(int id)
    {
        var booking=store.GetBookingById(id); if(booking is null) return NotFound();
        var model=BookingFormViewModel.FromBooking(booking); PopulateForm(model.ScheduleId,model.TravelDate,id); return View(model);
    }

    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult Edit(int id,BookingFormViewModel model)
    {
        if(id!=model.Id) return BadRequest(); var existing=store.GetBookingById(id); if(existing is null) return NotFound();
        NormalizeFormModel(model);
        var booking=BuildBooking(model); if(!ModelState.IsValid||booking is null){PopulateForm(model.ScheduleId,model.TravelDate,id);return View(model);}
        booking.Id=id; booking.ReferenceNumber=existing.ReferenceNumber; booking.CreatedAt=existing.CreatedAt;
        var result=bookingService.Update(booking); if(!result.IsSuccess){foreach(var error in result.Errors)ModelState.AddModelError(string.Empty,error);PopulateForm(model.ScheduleId,model.TravelDate,id);return View(model);}
        TempData["Success"]="Booking updated and all occurrence reservations revalidated."; return RedirectToAction(nameof(Details),new{id});
    }

    public IActionResult Delete(int id){var booking=store.GetBookingById(id);if(booking is null)return NotFound();ViewBag.RequestCount=store.GetAllRequests().Count(item=>item.BookingId==id);return View(booking);}
    [HttpPost,ActionName("Delete"),ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id){if(!store.DeleteBooking(id))return NotFound();TempData["Success"]="Booking, generated occurrences and linked requests were deleted.";return RedirectToAction(nameof(Index));}

    [HttpGet]
    public IActionResult SeatOptions(int scheduleId,DateTime travelDate,int? excludeBookingId)
    {
        var schedule=store.GetScheduleById(scheduleId); if(schedule is null)return NotFound();
        return Json(schedule.Seats.Select(seat=>new{seat.Id,seat.SeatNumber,seat.CoachNumber,seatClass=seat.SeatClass.ToString(),price=seat.SeatClass==SeatClass.FirstClass?schedule.FirstClassPrice:schedule.StandardPrice,isAvailable=occurrences.IsSeatAvailable(scheduleId,seat.Id,travelDate,excludeBookingId)}));
    }

    private Booking? BuildBooking(BookingFormViewModel model)
    {
        var schedule=store.GetScheduleById(model.ScheduleId); if(schedule is null){ModelState.AddModelError(nameof(model.ScheduleId),"Select a valid schedule.");return null;}
        Booking booking=model.BookingType==BookingKind.Recurring?new RecurringBooking{Rule=new RecurrenceRule{Frequency=model.RecurrenceFrequency,Interval=model.RecurrenceInterval,DaysOfWeek=model.RecurrenceFrequency==RecurrenceFrequency.Weekly?(model.RecurrenceDays??[]).Distinct().ToList():[],EndDate=model.RecurrenceEndDate}}:new OneOffBooking();
        booking.PersonalUserId=store.CurrentUser.Id; booking.PassengerName=(model.PassengerName??string.Empty).Trim(); booking.ScheduleTemplateId=schedule.Id; booking.DepartureStation=schedule.DepartureStation; booking.ArrivalStation=schedule.ArrivalStation; booking.DepartureTime=schedule.DepartureTime; booking.TravelDate=model.TravelDate.Date; booking.Status=model.Status; booking.CreatedAt=model.CreatedAt;
        booking.SeatSelections=(model.SeatReservations??[]).Where(item=>item is not null).Select(item=>new SeatReservation{PassengerName=(item.PassengerName??string.Empty).Trim(),ScheduleSeatId=item.ScheduleSeatId,ReservedPrice=item.ReservedPrice,Status=item.Status}).ToList();
        return booking;
    }

    private void NormalizeFormModel(BookingFormViewModel model)
    {
        AddRequiredTextError(model.PassengerName, nameof(model.PassengerName), "Enter a lead passenger name.");
        model.PassengerName = model.PassengerName?.Trim() ?? string.Empty;
        model.RecurrenceDays ??= [];
        model.SeatReservations ??= [];

        if (model.SeatReservations.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one passenger and seat reservation.");

        for (var index = 0; index < model.SeatReservations.Count; index++)
        {
            model.SeatReservations[index] ??= new SeatReservationInputModel();
            var reservation = model.SeatReservations[index];
            var key = $"{nameof(model.SeatReservations)}[{index}].{nameof(reservation.PassengerName)}";
            AddRequiredTextError(reservation.PassengerName, key, "Enter a passenger name for every reservation.");
            reservation.PassengerName = reservation.PassengerName?.Trim() ?? string.Empty;
        }
    }

    private void AddRequiredTextError(string? value, string key, string message)
    {
        if (!string.IsNullOrWhiteSpace(value)) return;
        if (ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0) return;
        ModelState.AddModelError(key, message);
    }

    private void PopulateForm(int scheduleId,DateTime travelDate,int? excludeBookingId){ViewBag.Schedules=store.GetAllSchedules().Where(item=>item.IsActive||item.Id==scheduleId).ToList();var schedule=store.GetScheduleById(scheduleId);ViewBag.Seats=schedule?.Seats??[];ViewBag.ExcludeBookingId=excludeBookingId;}
    private static DateTime NextRunningDate(Schedule? schedule,DateTime from){if(schedule is null)return from.Date;for(var i=0;i<14;i++){var date=from.Date.AddDays(i);if(schedule.DaysRunning.Contains(date.DayOfWeek))return date;}return from.Date;}
}
