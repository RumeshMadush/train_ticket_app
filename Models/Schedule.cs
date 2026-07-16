using System.ComponentModel.DataAnnotations;

namespace TrainTicketApp.Models;

public sealed class PersonalUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class Station
{
    public int Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class Route
{
    public int Id { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public required Station Origin { get; set; }
    public required Station Destination { get; set; }
}

public class Schedule
{
    public int Id { get; set; }
    public int RouteId { get; set; }

    [Required, Display(Name = "Train Number")]
    public string TrainNumber { get; set; } = string.Empty;
    [Required, Display(Name = "From")]
    public string DepartureStation { get; set; } = string.Empty;
    [Required, Display(Name = "To")]
    public string ArrivalStation { get; set; } = string.Empty;
    [DataType(DataType.Time), Display(Name = "Departure Time")]
    public TimeSpan DepartureTime { get; set; }
    [DataType(DataType.Time), Display(Name = "Arrival Time")]
    public TimeSpan ArrivalTime { get; set; }
    public List<DayOfWeek> DaysRunning { get; set; } = new();

    [Range(1, 1000), Display(Name = "Standard seats")]
    public int StandardSeatCount { get; set; } = 80;
    [Range(0, 1000), Display(Name = "First class seats")]
    public int FirstClassSeatCount { get; set; } = 20;
    [Range(0.01, 100000), Display(Name = "Standard price (Rs)")]
    public decimal StandardPrice { get; set; }
    [Range(0.01, 100000), Display(Name = "First class price (Rs)")]
    public decimal FirstClassPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ScheduleSeat> Seats { get; set; } = new();

    public int TotalSeats => StandardSeatCount + FirstClassSeatCount;
    public TimeSpan Duration => ArrivalTime > DepartureTime ? ArrivalTime - DepartureTime : ArrivalTime + TimeSpan.FromHours(24) - DepartureTime;
    public string DaysRunningDisplay => DaysRunning.Count == 7 ? "Daily" : string.Join(", ", DaysRunning.Select(day => day.ToString()[..3]));
}

public sealed class ScheduleSeat
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public string CoachNumber { get; set; } = string.Empty;
    public SeatClass SeatClass { get; set; }
    public int ClassOrdinal { get; set; }
}
