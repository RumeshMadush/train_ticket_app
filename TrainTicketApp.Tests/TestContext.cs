using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TrainTicketApp.Models;
using TrainTicketApp.Services;

namespace TrainTicketApp.Tests;

internal sealed class TestContext
{
    public DataStore Store { get; } = new();
    public BookingOccurrenceService Occurrences { get; }
    public BookingService Bookings { get; }
    public WeeklyReportService Reports { get; }
    public ForecastService Forecasts { get; }
    public ChatbotService Chatbot { get; }

    public TestContext()
    {
        Occurrences = new BookingOccurrenceService(Store);
        Bookings = new BookingService(Store, Occurrences);
        Reports = new WeeklyReportService(Store, Occurrences);
        Forecasts = new ForecastService(Store, Occurrences);
        Chatbot = new ChatbotService(Store, Forecasts);
    }

    public Schedule AddSchedule(
        string from = "Colombo Fort",
        string to = "Kandy",
        int standardSeats = 12,
        int firstClassSeats = 4,
        decimal standardPrice = 190m,
        decimal firstClassPrice = 380m,
        IReadOnlyCollection<DayOfWeek>? days = null,
        string trainNumber = "TEST-100")
    {
        return Store.AddSchedule(new Schedule
        {
            TrainNumber = trainNumber,
            DepartureStation = from,
            ArrivalStation = to,
            DepartureTime = new TimeSpan(8, 0, 0),
            ArrivalTime = new TimeSpan(10, 30, 0),
            DaysRunning = (days ?? Enum.GetValues<DayOfWeek>()).ToList(),
            StandardSeatCount = standardSeats,
            FirstClassSeatCount = firstClassSeats,
            StandardPrice = standardPrice,
            FirstClassPrice = firstClassPrice,
            IsActive = true
        });
    }

    public ScheduleSeat Seat(Schedule schedule, SeatClass seatClass, int ordinal) =>
        schedule.Seats.Single(item => item.SeatClass == seatClass && item.ClassOrdinal == ordinal);

    public static SeatReservation Reservation(ScheduleSeat seat, string passenger, decimal price) => new()
    {
        PassengerName = passenger,
        ScheduleSeatId = seat.Id,
        ReservedPrice = price,
        Status = ReservationStatus.Active
    };

    public static OneOffBooking OneOff(Schedule schedule, DateTime date, params SeatReservation[] seats) => new()
    {
        PassengerName = seats.FirstOrDefault()?.PassengerName ?? "Test Passenger",
        ScheduleTemplateId = schedule.Id,
        DepartureStation = schedule.DepartureStation,
        ArrivalStation = schedule.ArrivalStation,
        DepartureTime = schedule.DepartureTime,
        TravelDate = date.Date,
        Status = BookingStatus.Confirmed,
        SeatSelections = seats.ToList()
    };

    public static RecurringBooking Recurring(
        Schedule schedule,
        DateTime date,
        RecurrenceFrequency frequency,
        int interval,
        IReadOnlyCollection<DayOfWeek> days,
        DateTime? endDate,
        params SeatReservation[] seats) => new()
    {
        PassengerName = seats.FirstOrDefault()?.PassengerName ?? "Test Passenger",
        ScheduleTemplateId = schedule.Id,
        DepartureStation = schedule.DepartureStation,
        ArrivalStation = schedule.ArrivalStation,
        DepartureTime = schedule.DepartureTime,
        TravelDate = date.Date,
        Status = BookingStatus.Confirmed,
        SeatSelections = seats.ToList(),
        Rule = new RecurrenceRule
        {
            Frequency = frequency,
            Interval = interval,
            DaysOfWeek = days.ToList(),
            EndDate = endDate?.Date
        }
    };

    public static DateTime Next(DayOfWeek day, int minimumDaysAhead = 1)
    {
        var date = DateTime.Today.AddDays(minimumDaysAhead).Date;
        while (date.DayOfWeek != day) date = date.AddDays(1);
        return date;
    }

    public static TempDataDictionary EmptyTempData() =>
        new(new DefaultHttpContext(), new DictionaryTempDataProvider());

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> _values = new();
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(_values);
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) =>
            _values = new Dictionary<string, object>(values);
    }
}
