using TrainTicketApp.Models;
using TrainTicketApp.Services;
using Xunit;

namespace TrainTicketApp.Tests;

public sealed class ReportingForecastAndChatbotTests
{
    [Fact]
    public void WeeklyReport_AlwaysHasSevenDaysAndCorrectCombinedTotals()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = TestContext.Next(DayOfWeek.Wednesday);
        var booking = context.Bookings.Create(TestContext.OneOff(
            schedule,
            date,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m),
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 2), "Nimal", 190m))).Value!;
        context.Store.AddRequest(new SpecialRequest
        {
            BookingId = booking.Id,
            RequestType = RequestType.SpecialMeal,
            Description = "Vegetarian meal",
            DateNeeded = date,
            AdditionalCost = 75m
        });

        var report = context.Reports.Generate(date);

        Assert.Equal(7, report.Days.Count);
        Assert.Equal(date.StartOfWeek(), report.WeekStart);
        Assert.Equal(report.WeekStart.AddDays(6), report.WeekEnd);
        Assert.Equal(1, report.TotalBookings);
        Assert.Equal(2, report.TotalSeats);
        Assert.Equal(1, report.TotalRequests);
        Assert.Equal(380m, report.TotalFare);
        Assert.Equal(75m, report.RequestCosts);
        Assert.Equal(455m, report.TotalCost);
        Assert.Equal(Enumerable.Range(0, 7).Select(offset => report.WeekStart.AddDays(offset)),
            report.Days.Select(day => day.Date));
    }

    [Fact]
    public void AvailabilityForecast_SubtractsActiveSeatReservationsNotBookingCount()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule(standardSeats: 4, firstClassSeats: 2);
        var date = DateTime.Today.AddDays(5);
        Assert.True(context.Bookings.Create(TestContext.OneOff(
            schedule,
            date,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m),
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 2), "Nimal", 190m))).IsSuccess);

        var forecast = context.Forecasts.PredictAvailability("Colombo Fort", "Kandy", date);

        Assert.Equal(4, forecast.PredictedSeats);
        Assert.Equal("Good availability", forecast.AvailabilityStatus);
        Assert.Contains("6 catalogue seats and 2 active occurrence reservation(s)", forecast.Explanation);
    }

    [Fact]
    public void BookingPatternsAndPriceForecast_UseGeneratedSeatOccurrences()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule(standardPrice: 190m);
        var date = TestContext.Next(DayOfWeek.Wednesday);
        Assert.True(context.Bookings.Create(TestContext.OneOff(
            schedule,
            date,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 180m),
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 2), "Nimal", 220m))).IsSuccess);

        var pattern = Assert.Single(context.Forecasts.AnalysePatterns());
        var forecast = context.Forecasts.PredictPrice("Colombo Fort", "Kandy", date);

        Assert.Equal(2, pattern.BookingCount);
        Assert.Equal(200m, pattern.AveragePrice);
        Assert.Equal("Low", pattern.DemandLevel);
        Assert.Equal(190m, forecast.PredictedPrice);
        Assert.Contains("2 reserved-seat occurrence(s)", forecast.Explanation);
    }

    [Fact]
    public void Chatbot_ReturnsRoutesAvailabilityPriceAndFallbackResponses()
    {
        var context = new TestContext();
        var schedule = context.AddSchedule();
        var date = TestContext.Next(DayOfWeek.Wednesday);
        Assert.True(context.Bookings.Create(TestContext.OneOff(
            schedule, date,
            TestContext.Reservation(context.Seat(schedule, SeatClass.Standard, 1), "Asha", 190m))).IsSuccess);

        var routes = context.Chatbot.Reply("show all routes");
        var availability = context.Chatbot.Reply($"seats from Colombo Fort to Kandy on {date:dddd}");
        var price = context.Chatbot.Reply($"price from Colombo Fort to Kandy on {date:dddd}");
        var fallback = context.Chatbot.Reply("tell me a joke");

        Assert.Contains("Colombo Fort → Kandy", routes);
        Assert.Contains("predicted seat(s)", availability);
        Assert.Contains("Colombo Fort → Kandy", availability);
        Assert.Contains("Rs 190.00", price);
        Assert.Contains("I didn't understand", fallback);
    }

    [Fact]
    public void ForecastForNonRunningRoute_ReturnsNoService()
    {
        var context = new TestContext();
        context.AddSchedule(days: [DayOfWeek.Monday]);
        var date = TestContext.Next(DayOfWeek.Tuesday);

        var forecast = context.Forecasts.PredictAvailability("Colombo Fort", "Kandy", date);

        Assert.Equal(0, forecast.PredictedSeats);
        Assert.Equal("No scheduled service", forecast.AvailabilityStatus);
    }
}
