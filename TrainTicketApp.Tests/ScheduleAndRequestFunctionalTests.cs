using Microsoft.AspNetCore.Mvc;
using TrainTicketApp.Controllers;
using TrainTicketApp.Models;
using Xunit;

namespace TrainTicketApp.Tests;

public sealed class ScheduleAndRequestFunctionalTests
{
    [Fact]
    public void ScheduleCreate_BlankSubmissionReturnsValidationInsteadOfThrowing()
    {
        var context = new TestContext();
        var controller = new ScheduleController(context.Store);
        var blankSchedule = new Schedule
        {
            TrainNumber = null!,
            DepartureStation = null!,
            ArrivalStation = null!,
            DaysRunning = null!,
            StandardSeatCount = 0,
            FirstClassSeatCount = 0,
            StandardPrice = 0,
            FirstClassPrice = 0
        };

        var result = controller.Create(blankSchedule, null);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(blankSchedule.DaysRunning);
        Assert.Contains(controller.ModelState[nameof(Schedule.TrainNumber)]!.Errors,
            error => error.ErrorMessage == "Enter a train number.");
        Assert.Contains(controller.ModelState[nameof(Schedule.DepartureStation)]!.Errors,
            error => error.ErrorMessage == "Enter a departure station.");
        Assert.Contains(controller.ModelState[nameof(Schedule.ArrivalStation)]!.Errors,
            error => error.ErrorMessage == "Enter an arrival station.");
        Assert.Contains(controller.ModelState[nameof(Schedule.DaysRunning)]!.Errors,
            error => error.ErrorMessage == "Select at least one running day.");
        Assert.Empty(context.Store.GetAllSchedules());
    }

    [Fact]
    public void ScheduleCatalogue_UsesStableClassCodesAndDerivedCapacity()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule(standardSeats: 42, firstClassSeats: 41);

        Assert.Equal(83, schedule.TotalSeats);
        Assert.Equal("F1-01A", context.Seat(schedule, SeatClass.FirstClass, 1).SeatNumber);
        Assert.Equal("F2-01A", context.Seat(schedule, SeatClass.FirstClass, 41).SeatNumber);
        Assert.Equal("S1-01A", context.Seat(schedule, SeatClass.Standard, 1).SeatNumber);
        Assert.Equal("S2-01A", context.Seat(schedule, SeatClass.Standard, 41).SeatNumber);
        Assert.Equal(schedule.Id * 10000 + 1001, context.Seat(schedule, SeatClass.FirstClass, 1).Id);
        Assert.Equal(schedule.Id * 10000 + 5001, context.Seat(schedule, SeatClass.Standard, 1).Id);
    }

    [Fact]
    public void ScheduleSave_NormalizesAndReusesStationsAndRoutes()
    {
        var context = new TestContext();
        var first = context.AddSchedule(from: " Colombo Fort ", to: "Kandy", trainNumber: "T-1");
        var second = context.AddSchedule(from: "colombo fort", to: "KANDY", trainNumber: "T-2");

        Assert.Equal("Colombo Fort", first.DepartureStation);
        Assert.Equal("Colombo Fort", second.DepartureStation);
        Assert.Equal(2, context.Store.GetAllStations().Count);
        Assert.Single(context.Store.GetAllRouteEntities());
        Assert.Equal(first.RouteId, second.RouteId);
    }

    [Fact]
    public void ReferencedSchedule_CannotBeDeleted()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var booking = TestContext.OneOff(
            schedule,
            DateTime.Today.AddDays(5),
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m));
        Assert.True(context.Bookings.Create(booking).IsSuccess);

        Assert.False(context.Store.DeleteSchedule(schedule.Id));
        Assert.NotNull(context.Store.GetScheduleById(schedule.Id));
    }

    [Fact]
    public void ScheduleEdit_CannotRemoveAReservedSeat()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule(standardSeats: 12);
        var reservedSeat = context.Seat(schedule, SeatClass.Standard, 12);
        Assert.True(context.Bookings.Create(TestContext.OneOff(
            schedule,
            DateTime.Today.AddDays(5),
            TestContext.Reservation(reservedSeat, "Asha", 190m))).IsSuccess);
        var edited = CopySchedule(schedule, standardSeats: 8, schedule.DaysRunning);
        var controller = new ScheduleController(context.Store);

        var result = controller.Edit(schedule.Id, edited, edited.DaysRunning);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage.Contains("Capacity cannot remove reserved seat S1-03D"));
        Assert.Equal(12, context.Store.GetScheduleById(schedule.Id)!.StandardSeatCount);
    }

    [Fact]
    public void ScheduleEdit_CannotRemoveARecurringRunningDay()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var start = TestContext.Next(DayOfWeek.Monday);
        var recurring = TestContext.Recurring(
            schedule, start, RecurrenceFrequency.Weekly, 1,
            [DayOfWeek.Monday, DayOfWeek.Thursday], null,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m));
        Assert.True(context.Bookings.Create(recurring).IsSuccess);
        var reducedDays = schedule.DaysRunning.Where(day => day != DayOfWeek.Thursday).ToList();
        var edited = CopySchedule(schedule, schedule.StandardSeatCount, reducedDays);
        var controller = new ScheduleController(context.Store);

        var result = controller.Edit(schedule.Id, edited, reducedDays);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(Schedule.DaysRunning)]!.Errors,
            error => error.ErrorMessage.Contains("would invalidate booking"));
    }

    [Fact]
    public void SpecialRequest_DateMatchingOccurrence_IsAccepted()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = DateTime.Today.AddDays(5);
        var booking = context.Bookings.Create(TestContext.OneOff(
            schedule, date,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m))).Value!;
        var controller = new SpecialRequestController(context.Store) { TempData = TestContext.EmptyTempData() };
        var request = RequestFor(booking.Id, date);

        var result = controller.Create(request);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(controller.ModelState.IsValid);
        Assert.Single(context.Store.GetAllRequests());
    }

    [Fact]
    public void SpecialRequest_DateWithoutOccurrence_IsRejected()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = DateTime.Today.AddDays(5);
        var booking = context.Bookings.Create(TestContext.OneOff(
            schedule, date,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m))).Value!;
        var controller = new SpecialRequestController(context.Store) { TempData = TestContext.EmptyTempData() };
        var request = RequestFor(booking.Id, date.AddDays(1));

        var result = controller.Create(request);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(SpecialRequest.DateNeeded)]!.Errors,
            error => error.ErrorMessage.Contains("must match a journey occurrence"));
        Assert.Empty(context.Store.GetAllRequests());
    }

    [Fact]
    public void DeleteBooking_CascadesItsSpecialRequests()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = DateTime.Today.AddDays(5);
        var booking = context.Bookings.Create(TestContext.OneOff(
            schedule, date,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m))).Value!;
        context.Store.AddRequest(RequestFor(booking.Id, date));

        Assert.True(context.Store.DeleteBooking(booking.Id));
        Assert.Empty(context.Store.GetAllBookings());
        Assert.Empty(context.Store.GetAllRequests());
    }

    private static Schedule CopySchedule(Schedule source, int standardSeats, List<DayOfWeek> days) => new()
    {
        Id = source.Id,
        TrainNumber = source.TrainNumber,
        DepartureStation = source.DepartureStation,
        ArrivalStation = source.ArrivalStation,
        DepartureTime = source.DepartureTime,
        ArrivalTime = source.ArrivalTime,
        DaysRunning = days,
        StandardSeatCount = standardSeats,
        FirstClassSeatCount = source.FirstClassSeatCount,
        StandardPrice = source.StandardPrice,
        FirstClassPrice = source.FirstClassPrice,
        IsActive = source.IsActive
    };

    private static SpecialRequest RequestFor(int bookingId, DateTime date) => new()
    {
        BookingId = bookingId,
        RequestType = RequestType.SpecialMeal,
        Description = "Vegetarian meal",
        DateNeeded = date,
        AdditionalCost = 75m,
        Status = RequestStatus.Pending
    };
}
