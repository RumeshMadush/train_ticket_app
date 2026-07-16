using TrainTicketApp.Models;

namespace TrainTicketApp.Services;

public sealed class ForecastService(DataStore store, BookingOccurrenceService occurrences)
{
    public IReadOnlyList<BookingPattern> AnalysePatterns()
    {
        var generated = occurrences.GenerateAll(DateTime.Today.AddDays(-84), DateTime.Today.AddDays(84));
        return generated.GroupBy(item => new { Route = RouteKey(item.Schedule), item.TravelDate.DayOfWeek })
            .Select(group => new BookingPattern
            {
                Route = group.Key.Route,
                DayOfWeek = group.Key.DayOfWeek,
                BookingCount = group.Sum(item => item.SeatCount),
                AveragePrice = group.SelectMany(item => item.SeatReservations).Where(item => item.Status == ReservationStatus.Active).Select(item => item.ReservedPrice).DefaultIfEmpty().Average(),
                DemandLevel = group.Sum(item => item.SeatCount) >= 12 ? "High" : group.Sum(item => item.SeatCount) >= 5 ? "Moderate" : "Low"
            }).OrderByDescending(item => item.BookingCount).ToList();
    }

    public PriceForecast PredictPrice(string from, string to, DateTime date)
    {
        var schedule = FindSchedule(from, to, date);
        var pattern = AnalysePatterns().FirstOrDefault(item => item.Route.Equals($"{from} → {to}", StringComparison.OrdinalIgnoreCase) && item.DayOfWeek == date.DayOfWeek);
        var basePrice = schedule?.StandardPrice ?? 200m;
        var demandMultiplier = pattern?.DemandLevel == "High" ? 1.20m : pattern?.DemandLevel == "Moderate" ? 1.10m : 1m;
        var dayMultiplier = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Sunday ? 1.15m : 1m;
        var predicted = Math.Round(basePrice * demandMultiplier * dayMultiplier, 2);
        return new PriceForecast
        {
            Route = $"{from} → {to}", TargetDate = date.Date, PredictedPrice = predicted,
            PriceTrend = demandMultiplier * dayMultiplier > 1.2m ? "Rising" : demandMultiplier * dayMultiplier > 1m ? "Slightly above average" : "Stable",
            Confidence = schedule is null ? .55m : Math.Min(.94m, .72m + (pattern?.BookingCount ?? 0) * .01m),
            Explanation = $"Based on the standard fare and {(pattern?.BookingCount ?? 0)} reserved-seat occurrence(s) for {date:dddd}."
        };
    }

    public AvailabilityForecast PredictAvailability(string from, string to, DateTime date)
    {
        var schedule = FindSchedule(from, to, date);
        if (schedule is null) return new AvailabilityForecast { Route=$"{from} → {to}",TargetDate=date.Date,PredictedSeats=0,AvailabilityStatus="No scheduled service",Confidence=.95m,Explanation="No active schedule runs on the selected day." };
        var booked = schedule.Seats.Count(seat => !occurrences.IsSeatAvailable(schedule.Id, seat.Id, date));
        var available = Math.Max(0, schedule.TotalSeats - booked);
        var status = available > schedule.TotalSeats * .4 ? "Good availability" : available > schedule.TotalSeats * .1 ? "Limited availability" : available > 0 ? "Almost full" : "Sold out";
        return new AvailabilityForecast { Route=RouteKey(schedule),TargetDate=date.Date,PredictedSeats=available,AvailabilityStatus=status,Confidence=.90m,Explanation=$"Calculated from {schedule.TotalSeats} catalogue seats and {booked} active occurrence reservation(s)." };
    }

    private Schedule? FindSchedule(string from, string to, DateTime date) => store.GetAllSchedules().FirstOrDefault(item => item.IsActive && item.DepartureStation.Equals(from,StringComparison.OrdinalIgnoreCase) && item.ArrivalStation.Equals(to,StringComparison.OrdinalIgnoreCase) && item.DaysRunning.Contains(date.DayOfWeek));
    private static string RouteKey(Schedule schedule) => $"{schedule.DepartureStation} → {schedule.ArrivalStation}";
}
