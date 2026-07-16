using TrainTicketApp.Models;

namespace TrainTicketApp.Services;

public sealed class BookingService(DataStore store, BookingOccurrenceService occurrences)
{
    public OperationResult<Booking> Create(Booking booking)
    {
        lock (store.SyncRoot)
        {
            var errors = Validate(booking, null);
            if (errors.Count > 0) return OperationResult<Booking>.Failure(errors);
            return OperationResult<Booking>.Success(store.AddBookingUnsafe(booking));
        }
    }

    public OperationResult<Booking> Update(Booking booking)
    {
        lock (store.SyncRoot)
        {
            if (store.GetBookingById(booking.Id) is null) return OperationResult<Booking>.Failure(["Booking not found."]);
            var errors = Validate(booking, booking.Id);
            if (errors.Count > 0) return OperationResult<Booking>.Failure(errors);
            store.UpdateBookingUnsafe(booking);
            return OperationResult<Booking>.Success(booking);
        }
    }

    private List<string> Validate(Booking booking, int? excludeId)
    {
        var errors = new List<string>();
        var schedule = store.GetScheduleById(booking.ScheduleTemplateId);
        if (schedule is null || (!schedule.IsActive && !excludeId.HasValue)) errors.Add("Select an active train schedule.");
        if (booking.TravelDate.Date < DateTime.Today && !excludeId.HasValue) errors.Add("The first travel date cannot be in the past.");
        if (schedule is not null && !schedule.DaysRunning.Contains(booking.TravelDate.DayOfWeek)) errors.Add($"{schedule.TrainNumber} does not run on {booking.TravelDate:dddd}.");
        if (booking.SeatSelections.Count == 0) errors.Add("Add at least one passenger and seat reservation.");
        if (booking.SeatSelections.Any(item => string.IsNullOrWhiteSpace(item.PassengerName))) errors.Add("Every seat reservation needs a passenger name.");
        if (booking.SeatSelections.Any(item => item.ReservedPrice <= 0)) errors.Add("Every active reservation needs a positive recorded price.");
        if (schedule is not null && booking.SeatSelections.Any(item => schedule.Seats.All(seat => seat.Id != item.ScheduleSeatId))) errors.Add("One or more selected seats do not belong to this schedule.");

        if (booking is RecurringBooking recurring)
        {
            if (recurring.Rule.EndDate.HasValue && recurring.Rule.EndDate.Value.Date < booking.TravelDate.Date) errors.Add("The repeat-until date must be after the first journey.");
            if (recurring.Rule.Frequency == RecurrenceFrequency.Weekly && recurring.Rule.DaysOfWeek.Count == 0) errors.Add("Choose at least one weekly travel day.");
            if (schedule is not null && recurring.Rule.DaysOfWeek.Any(day => !schedule.DaysRunning.Contains(day))) errors.Add("The selected schedule does not run on every recurrence day.");
            if (schedule is not null && recurring.Rule.Frequency == RecurrenceFrequency.Weekdays && Weekdays.Any(day => !schedule.DaysRunning.Contains(day))) errors.Add("A weekday series requires a schedule that runs Monday to Friday.");
            if (schedule is not null && recurring.Rule.Frequency == RecurrenceFrequency.Daily && schedule.DaysRunning.Count != 7) errors.Add("A daily series requires a schedule that runs every day.");
        }

        var conflict = schedule is null ? null : occurrences.FindConflict(booking, excludeId);
        if (conflict is not null) errors.Add(conflict);
        return errors;
    }

    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday];
}

public sealed record OperationResult<T>(bool IsSuccess, T? Value, IReadOnlyList<string> Errors)
{
    public static OperationResult<T> Success(T value) => new(true, value, []);
    public static OperationResult<T> Failure(IReadOnlyList<string> errors) => new(false, default, errors);
}
