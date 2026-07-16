using TrainTicketApp.Models;

namespace TrainTicketApp.Services;

public sealed class BookingOccurrenceService(DataStore store)
{
    public IReadOnlyList<BookingOccurrence> Generate(Booking booking, DateTime from, DateTime to)
    {
        var schedule = store.GetScheduleById(booking.ScheduleTemplateId);
        if (schedule is null || to.Date < from.Date) return [];
        return Enumerable.Range(0, (to.Date - from.Date).Days + 1)
            .Select(offset => from.Date.AddDays(offset))
            .Where(booking.OccursOn)
            .Select(date => Build(booking, schedule, date))
            .ToList();
    }

    public IReadOnlyList<BookingOccurrence> GenerateAll(DateTime from, DateTime to) => store.GetAllBookings()
        .Where(item => item.Status != BookingStatus.Cancelled)
        .SelectMany(item => Generate(item, from, to))
        .OrderBy(item => item.TravelDate).ThenBy(item => item.Schedule.DepartureTime).ToList();

    public bool IsSeatAvailable(int scheduleId, int seatId, DateTime date, int? excludeBookingId = null) => !store.GetAllBookings()
        .Where(item => item.Id != excludeBookingId && item.Status != BookingStatus.Cancelled && item.ScheduleTemplateId == scheduleId && item.OccursOn(date))
        .SelectMany(item => item.SeatSelections)
        .Any(item => item.Status == ReservationStatus.Active && item.ScheduleSeatId == seatId);

    public string? FindConflict(Booking candidate, int? excludeBookingId = null)
    {
        var activeSeats = candidate.SeatSelections.Where(item => item.Status == ReservationStatus.Active).Select(item => item.ScheduleSeatId).ToList();
        if (activeSeats.Count != activeSeats.Distinct().Count()) return "Each seat can only be selected once in a booking.";

        foreach (var existing in store.GetAllBookings().Where(item => item.Id != excludeBookingId && item.Status != BookingStatus.Cancelled && item.ScheduleTemplateId == candidate.ScheduleTemplateId))
        {
            var commonSeats = activeSeats.Intersect(existing.SeatSelections.Where(item => item.Status == ReservationStatus.Active).Select(item => item.ScheduleSeatId)).ToList();
            if (commonSeats.Count == 0) continue;
            var conflictDate = FirstOverlap(candidate, existing);
            if (!conflictDate.HasValue) continue;
            var schedule = store.GetScheduleById(candidate.ScheduleTemplateId)!;
            var seatCodes = schedule.Seats.Where(item => commonSeats.Contains(item.Id)).Select(item => item.SeatNumber);
            return $"Seat(s) {string.Join(", ", seatCodes)} are already reserved on {conflictDate.Value:d MMM yyyy}.";
        }
        return null;
    }

    private static DateTime? FirstOverlap(Booking left, Booking right)
    {
        var start = left.TravelDate.Date > right.TravelDate.Date ? left.TravelDate.Date : right.TravelDate.Date;
        var finiteEnd = MinEnd(left.SeriesEndDate, right.SeriesEndDate);
        var end = finiteEnd ?? start.AddDays(LeastCommonMultiple(PeriodDays(left), PeriodDays(right)) + 7);
        if (end < start) return null;
        for (var date = start; date <= end; date = date.AddDays(1)) if (left.OccursOn(date) && right.OccursOn(date)) return date;
        return null;
    }

    private static DateTime? MinEnd(DateTime? left, DateTime? right) => left.HasValue && right.HasValue ? (left < right ? left : right) : left ?? right;
    private static int PeriodDays(Booking booking) => booking is not RecurringBooking recurring ? 1 : recurring.Rule.Frequency == RecurrenceFrequency.Daily ? recurring.Rule.Interval : recurring.Rule.Interval * 7;
    private static int LeastCommonMultiple(int left, int right) => Math.Abs(left * right) / GreatestCommonDivisor(left, right);
    private static int GreatestCommonDivisor(int left, int right) { while (right != 0) (left, right) = (right, left % right); return Math.Max(1, left); }

    private static BookingOccurrence Build(Booking booking, Schedule schedule, DateTime date)
    {
        var occurrenceId = $"{booking.ReferenceNumber}-{date:yyyyMMdd}";
        var reservations = booking.SeatSelections.Select(item =>
        {
            var seat = schedule.Seats.Single(seat => seat.Id == item.ScheduleSeatId);
            return new OccurrenceSeatReservation { ReservationId=$"{occurrenceId}-{seat.SeatNumber}",PassengerName=item.PassengerName,Seat=seat,ReservedPrice=item.ReservedPrice,Status=item.Status };
        }).ToList();
        return new BookingOccurrence { OccurrenceId=occurrenceId,Booking=booking,Schedule=schedule,TravelDate=date,SeatReservations=reservations };
    }
}
