namespace TrainTicketApp.Models;

public sealed class WeeklyReport
{
    public required DateTime WeekStart { get; init; }
    public DateTime WeekEnd => WeekStart.AddDays(6);
    public required IReadOnlyList<DaySummary> Days { get; init; }
    public int TotalBookings => Days.Sum(day => day.Occurrences.Count);
    public int TotalSeats => Days.Sum(day => day.ReservedSeats);
    public int TotalRequests => Days.Sum(day => day.SpecialRequests.Count);
    public decimal TotalFare => Days.Sum(day => day.FareTotal);
    public decimal RequestCosts => Days.Sum(day => day.RequestCost);
    public decimal TotalCost => TotalFare + RequestCosts;
}

public sealed class DaySummary
{
    public required DateTime Date { get; init; }
    public required IReadOnlyList<BookingOccurrence> Occurrences { get; init; }
    public required IReadOnlyList<SpecialRequest> SpecialRequests { get; init; }
    public int ReservedSeats => Occurrences.Sum(item => item.SeatCount);
    public decimal FareTotal => Occurrences.Sum(item => item.TotalPrice);
    public decimal RequestCost => SpecialRequests.Sum(item => item.AdditionalCost);
}
