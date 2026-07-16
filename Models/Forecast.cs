namespace TrainTicketApp.Models;

public abstract class Forecast
{
    public string Route { get; init; } = string.Empty;
    public DateTime TargetDate { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public decimal Confidence { get; init; }
    public string Explanation { get; init; } = string.Empty;
}

public sealed class PriceForecast : Forecast
{
    public decimal PredictedPrice { get; init; }
    public string PriceTrend { get; init; } = string.Empty;
}

public sealed class AvailabilityForecast : Forecast
{
    public int PredictedSeats { get; init; }
    public string AvailabilityStatus { get; init; } = string.Empty;
}

public sealed class BookingPattern
{
    public string Route { get; init; } = string.Empty;
    public DayOfWeek DayOfWeek { get; init; }
    public int BookingCount { get; init; }
    public decimal AveragePrice { get; init; }
    public string DemandLevel { get; init; } = string.Empty;
}
