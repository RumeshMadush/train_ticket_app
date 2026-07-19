using TrainTicketApp.Models;
using TrainTicketApp.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace TrainTicketApp.Tests;

public sealed class BookingFunctionalTests
{
    [Fact]
    public void BookingCreate_BlankSubmissionReturnsValidationInsteadOfThrowing()
    {
        var context = new TestContext();
        context.Store.Seed();
        var schedule = context.Store.GetAllSchedules().First();
        var originalCount = context.Store.GetAllBookings().Count;
        var controller = new BookingController(context.Store, context.Bookings, context.Occurrences);
        var blankModel = new BookingFormViewModel
        {
            PassengerName = null!,
            ScheduleId = schedule.Id,
            TravelDate = DateTime.Today.AddDays(1),
            RecurrenceDays = null!,
            SeatReservations = null!
        };

        var result = controller.Create(blankModel);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(blankModel.RecurrenceDays);
        Assert.Empty(blankModel.SeatReservations);
        Assert.Contains(controller.ModelState[nameof(BookingFormViewModel.PassengerName)]!.Errors,
            error => error.ErrorMessage == "Enter a lead passenger name.");
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Add at least one passenger and seat reservation.");
        Assert.Equal(originalCount, context.Store.GetAllBookings().Count);
    }

    [Fact]
    public void BookingCreate_BlankReservationPassengerReturnsFieldValidation()
    {
        var context = new TestContext();
        context.Store.Seed();
        var schedule = context.Store.GetAllSchedules().First();
        var seat = schedule.Seats.First();
        var originalCount = context.Store.GetAllBookings().Count;
        var controller = new BookingController(context.Store, context.Bookings, context.Occurrences);
        var model = new BookingFormViewModel
        {
            PassengerName = "Lead Passenger",
            ScheduleId = schedule.Id,
            TravelDate = TestContext.Next(DayOfWeek.Monday),
            SeatReservations =
            [
                new SeatReservationInputModel
                {
                    PassengerName = null!,
                    ScheduleSeatId = seat.Id,
                    ReservedPrice = schedule.FirstClassPrice
                }
            ]
        };

        var result = controller.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(string.Empty, model.SeatReservations[0].PassengerName);
        Assert.Contains(controller.ModelState["SeatReservations[0].PassengerName"]!.Errors,
            error => error.ErrorMessage == "Enter a passenger name for every reservation.");
        Assert.Equal(originalCount, context.Store.GetAllBookings().Count);
    }

    [Fact]
    public void CreateOneOff_MultipleSeats_GeneratesReferencesAndCorrectTotals()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = DateTime.Today.AddDays(7);
        var first = TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m);
        var second = TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 2), "Nimal", 210m);

        var result = context.Bookings.Create(TestContext.OneOff(schedule, date, first, second));

        Assert.True(result.IsSuccess);
        var booking = Assert.IsType<OneOffBooking>(result.Value);
        Assert.Matches(@"^RF-\d{8}-0001$", booking.ReferenceNumber);
        Assert.Equal(2, booking.SeatCount);
        Assert.Equal(400m, booking.TotalPrice);

        var occurrence = Assert.Single(context.Occurrences.Generate(booking, date, date));
        Assert.Equal($"{booking.ReferenceNumber}-{date:yyyyMMdd}", occurrence.OccurrenceId);
        Assert.Equal(400m, occurrence.TotalPrice);
        Assert.All(occurrence.SeatReservations, reservation =>
            Assert.StartsWith(occurrence.OccurrenceId, reservation.ReservationId));
    }

    [Fact]
    public void BoundedRecurring_GeneratesExpectedOccurrencesAndEstimatedTotal()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var start = TestContext.Next(DayOfWeek.Monday);
        var seat = TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m);
        var booking = TestContext.Recurring(
            schedule, start, RecurrenceFrequency.Weekly, 1,
            [DayOfWeek.Monday, DayOfWeek.Thursday], start.AddDays(14), seat);

        var result = context.Bookings.Create(booking);
        var generated = context.Occurrences.Generate(booking, start, start.AddDays(14));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, generated.Count);
        Assert.Equal(950m, booking.EstimatedSeriesTotal);
        Assert.Equal(
            [start, start.AddDays(3), start.AddDays(7), start.AddDays(10), start.AddDays(14)],
            generated.Select(item => item.TravelDate));
    }

    [Fact]
    public void OpenEndedRecurring_IsProjectedOnlyForRequestedDateRange()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var start = TestContext.Next(DayOfWeek.Monday);
        var booking = TestContext.Recurring(
            schedule, start, RecurrenceFrequency.Weekly, 1,
            [DayOfWeek.Monday], null,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m));

        var result = context.Bookings.Create(booking);
        var generated = context.Occurrences.Generate(booking, start, start.AddDays(28));

        Assert.True(result.IsSuccess);
        Assert.Null(booking.EstimatedSeriesTotal);
        Assert.Equal(5, generated.Count);
        Assert.All(generated, occurrence => Assert.Equal(DayOfWeek.Monday, occurrence.TravelDate.DayOfWeek));
    }

    [Fact]
    public void DuplicateSeatWithinBooking_IsRejected()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var seat = context.Seat(schedule, SeatClass.Standard, 1);
        var booking = TestContext.OneOff(
            schedule,
            DateTime.Today.AddDays(5),
            TestContext.Reservation(seat, "Asha", 190m),
            TestContext.Reservation(seat, "Nimal", 190m));

        var result = context.Bookings.Create(booking);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("only be selected once", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(context.Store.GetAllBookings());
    }

    [Fact]
    public void SameSeatOnSameOccurrence_IsRejectedAcrossBookings()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = DateTime.Today.AddDays(5);
        var seat = context.Seat(schedule, SeatClass.Standard, 1);
        Assert.True(context.Bookings.Create(TestContext.OneOff(
            schedule, date, TestContext.Reservation(seat, "Asha", 190m))).IsSuccess);

        var result = context.Bookings.Create(TestContext.OneOff(
            schedule, date, TestContext.Reservation(seat, "Nimal", 190m)));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("already reserved", StringComparison.OrdinalIgnoreCase));
        Assert.Single(context.Store.GetAllBookings());
    }

    [Fact]
    public void SameScheduleSeatOnDifferentDates_IsAllowed()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var firstDate = DateTime.Today.AddDays(5);
        var seat = context.Seat(schedule, SeatClass.Standard, 1);

        var first = context.Bookings.Create(TestContext.OneOff(
            schedule, firstDate, TestContext.Reservation(seat, "Asha", 190m)));
        var second = context.Bookings.Create(TestContext.OneOff(
            schedule, firstDate.AddDays(1), TestContext.Reservation(seat, "Nimal", 190m)));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, context.Store.GetAllBookings().Count);
    }

    [Fact]
    public void OpenEndedPeriodicRules_FindConflictBeyondShortForecastHorizon()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var start = TestContext.Next(DayOfWeek.Monday);
        var seat = context.Seat(schedule, SeatClass.Standard, 1);
        var everyEightWeeks = TestContext.Recurring(
            schedule, start, RecurrenceFrequency.Weekly, 8, [DayOfWeek.Monday], null,
            TestContext.Reservation(seat, "Asha", 190m));
        var everyNineWeeks = TestContext.Recurring(
            schedule, start.AddDays(7), RecurrenceFrequency.Weekly, 9, [DayOfWeek.Monday], null,
            TestContext.Reservation(seat, "Nimal", 190m));

        Assert.True(context.Bookings.Create(everyEightWeeks).IsSuccess);
        var result = context.Bookings.Create(everyNineWeeks);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains(start.AddDays(64 * 7).ToString("d MMM yyyy")));
    }

    [Fact]
    public async Task ConcurrentConflictingCreates_AreAtomicAndOnlyOneSucceeds()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = DateTime.Today.AddDays(5);
        var seat = context.Seat(schedule, SeatClass.Standard, 1);
        using var startGate = new ManualResetEventSlim(false);
        var firstBooking = TestContext.OneOff(schedule, date, TestContext.Reservation(seat, "Asha", 190m));
        var secondBooking = TestContext.OneOff(schedule, date, TestContext.Reservation(seat, "Nimal", 190m));

        var first = Task.Run(() => { startGate.Wait(); return context.Bookings.Create(firstBooking); });
        var second = Task.Run(() => { startGate.Wait(); return context.Bookings.Create(secondBooking); });
        startGate.Set();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => !result.IsSuccess);
        Assert.Single(context.Store.GetAllBookings());
    }
}
