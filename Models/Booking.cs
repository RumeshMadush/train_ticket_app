using System.ComponentModel.DataAnnotations;

namespace TrainTicketApp.Models;

public abstract class Booking
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public int PersonalUserId { get; set; } = 1;

    [Required, Display(Name = "Lead passenger")]
    public string PassengerName { get; set; } = string.Empty;

    [Range(1, int.MaxValue), Display(Name = "Train schedule")]
    public int ScheduleTemplateId { get; set; }

    public string DepartureStation { get; set; } = string.Empty;
    public string ArrivalStation { get; set; } = string.Empty;
    public TimeSpan DepartureTime { get; set; }

    [DataType(DataType.Date), Display(Name = "First travel date")]
    public DateTime TravelDate { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<SeatReservation> SeatSelections { get; set; } = new();

    public decimal TotalPrice => SeatSelections
        .Where(item => item.Status == ReservationStatus.Active)
        .Sum(item => item.ReservedPrice);
    public int SeatCount => SeatSelections.Count(item => item.Status == ReservationStatus.Active);
    public DayOfWeek DayOfWeek => TravelDate.DayOfWeek;
    public abstract BookingKind BookingType { get; }
    public abstract string RecurrenceSummary { get; }
    public virtual DateTime? SeriesEndDate => TravelDate.Date;
    public virtual bool OccursOn(DateTime date) => TravelDate.Date == date.Date;
}

public sealed class OneOffBooking : Booking
{
    public override BookingKind BookingType => BookingKind.OneOff;
    public override string RecurrenceSummary => "One-off journey";
}

public sealed class RecurringBooking : Booking
{
    public RecurrenceRule Rule { get; set; } = new();
    public override BookingKind BookingType => BookingKind.Recurring;
    public override string RecurrenceSummary => Rule.ToDisplayText(TravelDate);
    public override DateTime? SeriesEndDate => Rule.EndDate?.Date;
    public override bool OccursOn(DateTime date) => Rule.OccursOn(TravelDate, date);

    public decimal? EstimatedSeriesTotal
    {
        get
        {
            if (!Rule.EndDate.HasValue) return null;
            var count = Enumerable.Range(0, (Rule.EndDate.Value.Date - TravelDate.Date).Days + 1)
                .Count(offset => OccursOn(TravelDate.Date.AddDays(offset)));
            return count * TotalPrice;
        }
    }
}

public sealed class RecurrenceRule
{
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Weekly;
    [Range(1, 52)] public int Interval { get; set; } = 1;
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();
    [DataType(DataType.Date)] public DateTime? EndDate { get; set; }

    public bool OccursOn(DateTime seriesStart, DateTime candidate)
    {
        var start = seriesStart.Date;
        var date = candidate.Date;
        if (date < start || (EndDate.HasValue && date > EndDate.Value.Date)) return false;

        var interval = Math.Max(1, Interval);
        var elapsedDays = (date - start).Days;
        return Frequency switch
        {
            RecurrenceFrequency.Daily => elapsedDays % interval == 0,
            RecurrenceFrequency.Weekdays => date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
                                               && (elapsedDays / 7) % interval == 0,
            _ => (elapsedDays / 7) % interval == 0
                 && (DaysOfWeek.Count == 0
                     ? date.DayOfWeek == start.DayOfWeek
                     : DaysOfWeek.Contains(date.DayOfWeek))
        };
    }

    public string ToDisplayText(DateTime seriesStart)
    {
        var every = Interval > 1 ? $" every {Interval} cycles" : string.Empty;
        var days = DaysOfWeek.Count > 0
            ? $" on {string.Join(", ", DaysOfWeek.Select(day => day.ToString()[..3]))}"
            : Frequency == RecurrenceFrequency.Weekly ? $" on {seriesStart:ddd}" : string.Empty;
        var end = EndDate.HasValue ? $" until {EndDate.Value:d MMM yyyy}" : " · open-ended";
        return $"{Frequency}{every}{days}{end}";
    }
}

public sealed class SeatReservation
{
    public int Id { get; set; }
    [Required] public string PassengerName { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int ScheduleSeatId { get; set; }
    [Range(0.01, 100000)] public decimal ReservedPrice { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
}

public sealed class BookingOccurrence
{
    public required string OccurrenceId { get; init; }
    public required Booking Booking { get; init; }
    public required Schedule Schedule { get; init; }
    public required DateTime TravelDate { get; init; }
    public required IReadOnlyList<OccurrenceSeatReservation> SeatReservations { get; init; }
    public decimal TotalPrice => SeatReservations.Where(item => item.Status == ReservationStatus.Active).Sum(item => item.ReservedPrice);
    public int SeatCount => SeatReservations.Count(item => item.Status == ReservationStatus.Active);
}

public sealed class OccurrenceSeatReservation
{
    public required string ReservationId { get; init; }
    public required string PassengerName { get; init; }
    public required ScheduleSeat Seat { get; init; }
    public required decimal ReservedPrice { get; init; }
    public required ReservationStatus Status { get; init; }
}

public enum BookingKind { OneOff, Recurring }
public enum RecurrenceFrequency { Daily, Weekly, Weekdays }
public enum SeatClass { Standard, FirstClass }
public enum BookingStatus { Confirmed, Pending, Cancelled }
public enum ReservationStatus { Active, Cancelled }
