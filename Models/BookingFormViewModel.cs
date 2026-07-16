using System.ComponentModel.DataAnnotations;

namespace TrainTicketApp.Models;

public sealed class BookingFormViewModel
{
    public int Id { get; set; }
    [Required, Display(Name = "Lead passenger")]
    public string PassengerName { get; set; } = string.Empty;
    [Range(1, int.MaxValue), Display(Name = "Train schedule")]
    public int ScheduleId { get; set; }
    [DataType(DataType.Date), Display(Name = "First travel date")]
    public DateTime TravelDate { get; set; } = DateTime.Today.AddDays(1);
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    [Display(Name = "Booking type")]
    public BookingKind BookingType { get; set; } = BookingKind.OneOff;
    public RecurrenceFrequency RecurrenceFrequency { get; set; } = RecurrenceFrequency.Weekly;
    [Range(1, 52)] public int RecurrenceInterval { get; set; } = 1;
    public List<DayOfWeek> RecurrenceDays { get; set; } = new();
    [DataType(DataType.Date)] public DateTime? RecurrenceEndDate { get; set; }
    public List<SeatReservationInputModel> SeatReservations { get; set; } = new();
    [Display(Name = "Add a special request after saving")]
    public bool AddSpecialRequestAfterBooking { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public static BookingFormViewModel FromBooking(Booking booking) => new()
    {
        Id = booking.Id,
        PassengerName = booking.PassengerName,
        ScheduleId = booking.ScheduleTemplateId,
        TravelDate = booking.TravelDate,
        Status = booking.Status,
        BookingType = booking.BookingType,
        CreatedAt = booking.CreatedAt,
        RecurrenceFrequency = booking is RecurringBooking recurring ? recurring.Rule.Frequency : RecurrenceFrequency.Weekly,
        RecurrenceInterval = booking is RecurringBooking recurringInterval ? recurringInterval.Rule.Interval : 1,
        RecurrenceDays = booking is RecurringBooking recurringDays ? recurringDays.Rule.DaysOfWeek.ToList() : new(),
        RecurrenceEndDate = booking is RecurringBooking recurringEnd ? recurringEnd.Rule.EndDate : null,
        SeatReservations = booking.SeatSelections.Select(item => new SeatReservationInputModel
        {
            PassengerName = item.PassengerName,
            ScheduleSeatId = item.ScheduleSeatId,
            ReservedPrice = item.ReservedPrice,
            Status = item.Status
        }).ToList()
    };
}

public sealed class SeatReservationInputModel
{
    [Required, Display(Name = "Passenger")]
    public string PassengerName { get; set; } = string.Empty;
    [Range(1, int.MaxValue), Display(Name = "Seat")]
    public int ScheduleSeatId { get; set; }
    [Range(0.01, 100000), Display(Name = "Reserved price")]
    public decimal ReservedPrice { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
}
