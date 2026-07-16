using TrainTicketApp.Models;
using DomainRoute = TrainTicketApp.Models.Route;

namespace TrainTicketApp.Services;

public sealed class DataStore
{
    internal object SyncRoot { get; } = new();
    private readonly List<PersonalUser> _users = new();
    private readonly List<Station> _stations = new();
    private readonly List<DomainRoute> _routes = new();
    private readonly List<Booking> _bookings = new();
    private readonly List<Schedule> _schedules = new();
    private readonly List<SpecialRequest> _specialRequests = new();
    private int _bookingId = 1, _scheduleId = 1, _requestId = 1, _stationId = 1, _routeId = 1;

    public PersonalUser CurrentUser => _users.Single();
    public IReadOnlyList<Booking> GetAllBookings() { lock (SyncRoot) return _bookings.ToList(); }
    public Booking? GetBookingById(int id) { lock (SyncRoot) return _bookings.FirstOrDefault(item => item.Id == id); }
    public IReadOnlyList<Schedule> GetAllSchedules() { lock (SyncRoot) return _schedules.ToList(); }
    public Schedule? GetScheduleById(int id) { lock (SyncRoot) return _schedules.FirstOrDefault(item => item.Id == id); }
    public IReadOnlyList<SpecialRequest> GetAllRequests() { lock (SyncRoot) return _specialRequests.ToList(); }
    public SpecialRequest? GetRequestById(int id) { lock (SyncRoot) return _specialRequests.FirstOrDefault(item => item.Id == id); }
    public IReadOnlyList<Station> GetAllStations() { lock (SyncRoot) return _stations.ToList(); }
    public IReadOnlyList<DomainRoute> GetAllRouteEntities() { lock (SyncRoot) return _routes.ToList(); }

    internal Booking AddBookingUnsafe(Booking booking)
    {
        booking.Id = _bookingId++;
        booking.ReferenceNumber = $"RF-{DateTime.Today:yyyyMMdd}-{booking.Id:D4}";
        booking.CreatedAt = DateTime.Now;
        for (var i = 0; i < booking.SeatSelections.Count; i++) booking.SeatSelections[i].Id = booking.Id * 100 + i + 1;
        _bookings.Add(booking);
        return booking;
    }

    internal bool UpdateBookingUnsafe(Booking booking)
    {
        var index = _bookings.FindIndex(item => item.Id == booking.Id);
        if (index < 0) return false;
        booking.ReferenceNumber = _bookings[index].ReferenceNumber;
        for (var i = 0; i < booking.SeatSelections.Count; i++) booking.SeatSelections[i].Id = booking.Id * 100 + i + 1;
        _bookings[index] = booking;
        return true;
    }

    public bool DeleteBooking(int id)
    {
        lock (SyncRoot)
        {
            var booking = _bookings.FirstOrDefault(item => item.Id == id);
            if (booking is null) return false;
            _bookings.Remove(booking);
            _specialRequests.RemoveAll(item => item.BookingId == id);
            return true;
        }
    }

    public Schedule AddSchedule(Schedule schedule)
    {
        lock (SyncRoot)
        {
            schedule.Id = _scheduleId++;
            NormalizeRoute(schedule);
            schedule.Seats = BuildSeatCatalogue(schedule);
            _schedules.Add(schedule);
            return schedule;
        }
    }

    public bool UpdateSchedule(Schedule schedule)
    {
        lock (SyncRoot)
        {
            var index = _schedules.FindIndex(item => item.Id == schedule.Id);
            if (index < 0) return false;
            NormalizeRoute(schedule);
            schedule.Seats = BuildSeatCatalogue(schedule);
            _schedules[index] = schedule;
            return true;
        }
    }

    public bool DeleteSchedule(int id)
    {
        lock (SyncRoot)
        {
            if (_bookings.Any(item => item.ScheduleTemplateId == id)) return false;
            var schedule = _schedules.FirstOrDefault(item => item.Id == id);
            return schedule is not null && _schedules.Remove(schedule);
        }
    }

    public bool HasBookingsForSchedule(int id) { lock (SyncRoot) return _bookings.Any(item => item.ScheduleTemplateId == id); }
    public ScheduleSeat? GetScheduleSeat(int scheduleId, int seatId) => GetScheduleById(scheduleId)?.Seats.FirstOrDefault(item => item.Id == seatId);

    public SpecialRequest AddRequest(SpecialRequest request)
    {
        lock (SyncRoot)
        {
            request.Id = _requestId++;
            request.CreatedAt = DateTime.Now;
            request.PassengerName = _bookings.FirstOrDefault(item => item.Id == request.BookingId)?.PassengerName ?? "Unknown";
            _specialRequests.Add(request);
            return request;
        }
    }

    public bool UpdateRequest(SpecialRequest request)
    {
        lock (SyncRoot)
        {
            var index = _specialRequests.FindIndex(item => item.Id == request.Id);
            if (index < 0) return false;
            request.PassengerName = _bookings.FirstOrDefault(item => item.Id == request.BookingId)?.PassengerName ?? "Unknown";
            _specialRequests[index] = request;
            return true;
        }
    }

    public bool DeleteRequest(int id)
    {
        lock (SyncRoot)
        {
            var request = _specialRequests.FirstOrDefault(item => item.Id == id);
            return request is not null && _specialRequests.Remove(request);
        }
    }

    public List<string> GetAllRoutes() => GetAllSchedules()
        .Select(item => $"{item.DepartureStation} → {item.ArrivalStation}")
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();

    private void NormalizeRoute(Schedule schedule)
    {
        var origin = GetOrCreateStation(schedule.DepartureStation);
        var destination = GetOrCreateStation(schedule.ArrivalStation);
        var route = _routes.FirstOrDefault(item => item.Origin.Id == origin.Id && item.Destination.Id == destination.Id);
        if (route is null)
        {
            route = new DomainRoute { Id = _routeId++, Origin = origin, Destination = destination, RouteName = $"{origin.Name} → {destination.Name}" };
            _routes.Add(route);
        }
        schedule.RouteId = route.Id;
        schedule.DepartureStation = origin.Name;
        schedule.ArrivalStation = destination.Name;
    }

    private Station GetOrCreateStation(string name)
    {
        var normalized = name.Trim();
        var station = _stations.FirstOrDefault(item => item.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (station is not null) return station;
        var code = new string(normalized.Where(char.IsLetterOrDigit).Take(3).Select(char.ToUpperInvariant).ToArray());
        station = new Station { Id = _stationId++, Name = normalized, StationCode = code.PadRight(3, 'X') };
        _stations.Add(station);
        return station;
    }

    private static List<ScheduleSeat> BuildSeatCatalogue(Schedule schedule)
    {
        var seats = new List<ScheduleSeat>();
        AddClass(SeatClass.FirstClass, schedule.FirstClassSeatCount, "F", 1000);
        AddClass(SeatClass.Standard, schedule.StandardSeatCount, "S", 5000);
        return seats;

        void AddClass(SeatClass seatClass, int count, string prefix, int idOffset)
        {
            for (var ordinal = 1; ordinal <= count; ordinal++)
            {
                var coach = (ordinal - 1) / 40 + 1;
                var withinCoach = (ordinal - 1) % 40;
                var row = withinCoach / 4 + 1;
                var letter = (char)('A' + withinCoach % 4);
                seats.Add(new ScheduleSeat
                {
                    Id = schedule.Id * 10000 + idOffset + ordinal,
                    ScheduleId = schedule.Id,
                    SeatClass = seatClass,
                    ClassOrdinal = ordinal,
                    CoachNumber = $"{prefix}{coach}",
                    SeatNumber = $"{prefix}{coach}-{row:D2}{letter}"
                });
            }
        }
    }

    public void Seed()
    {
        if (_users.Any()) return;
        _users.Add(new PersonalUser { Id = 1, Name = "Rumesh Madushanka" });
        AddSchedule(new Schedule { TrainNumber = "SLR-101", DepartureStation = "Colombo Fort", ArrivalStation = "Kandy", DepartureTime = new(7,0,0), ArrivalTime = new(9,45,0), DaysRunning = [DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday], StandardSeatCount = 96, FirstClassSeatCount = 24, StandardPrice = 190m, FirstClassPrice = 550m });
        AddSchedule(new Schedule { TrainNumber = "SLR-202", DepartureStation = "Colombo Fort", ArrivalStation = "Galle", DepartureTime = new(8,30,0), ArrivalTime = new(11,0,0), DaysRunning = Enum.GetValues<DayOfWeek>().ToList(), StandardSeatCount = 160, FirstClassSeatCount = 40, StandardPrice = 130m, FirstClassPrice = 380m });
        AddSchedule(new Schedule { TrainNumber = "SLR-303", DepartureStation = "Colombo Fort", ArrivalStation = "Jaffna", DepartureTime = new(6,0,0), ArrivalTime = new(13,30,0), DaysRunning = Enum.GetValues<DayOfWeek>().ToList(), StandardSeatCount = 80, FirstClassSeatCount = 20, StandardPrice = 500m, FirstClassPrice = 1200m });
        AddSchedule(new Schedule { TrainNumber = "SLR-404", DepartureStation = "Kandy", ArrivalStation = "Badulla", DepartureTime = new(9,0,0), ArrivalTime = new(14,30,0), DaysRunning = [DayOfWeek.Monday,DayOfWeek.Wednesday,DayOfWeek.Friday,DayOfWeek.Sunday], StandardSeatCount = 144, FirstClassSeatCount = 36, StandardPrice = 310m, FirstClassPrice = 850m });

        var week = DateTime.Today.StartOfWeek();
        SeedBooking(new RecurringBooking { PassengerName="Rumesh Madushanka", ScheduleTemplateId=1, TravelDate=week, Status=BookingStatus.Confirmed, Rule=new(){Frequency=RecurrenceFrequency.Weekly,DaysOfWeek=[DayOfWeek.Monday,DayOfWeek.Thursday],EndDate=week.AddDays(42)}, SeatSelections=[Selection(1,"Rumesh Madushanka",SeatClass.Standard,12,190m)] });
        SeedBooking(new OneOffBooking { PassengerName="Kasun Perera", ScheduleTemplateId=2, TravelDate=week.AddDays(1), Status=BookingStatus.Confirmed, SeatSelections=[Selection(2,"Kasun Perera",SeatClass.FirstClass,4,380m),Selection(2,"Malini Perera",SeatClass.FirstClass,5,380m)] });
        SeedBooking(new RecurringBooking { PassengerName="Ayesha Fernando", ScheduleTemplateId=3, TravelDate=week.AddDays(2), Status=BookingStatus.Confirmed, Rule=new(){Frequency=RecurrenceFrequency.Weekdays,EndDate=week.AddDays(28)}, SeatSelections=[Selection(3,"Ayesha Fernando",SeatClass.Standard,5,500m)] });
        SeedBooking(new OneOffBooking { PassengerName="Nimal Silva", ScheduleTemplateId=4, TravelDate=week.AddDays(2), Status=BookingStatus.Pending, SeatSelections=[Selection(4,"Nimal Silva",SeatClass.Standard,31,310m)] });
        SeedBooking(new OneOffBooking { PassengerName="Sanduni Wickramasinghe", ScheduleTemplateId=2, TravelDate=week.AddDays(4), Status=BookingStatus.Confirmed, SeatSelections=[Selection(2,"Sanduni Wickramasinghe",SeatClass.Standard,18,130m)] });
        SeedBooking(new OneOffBooking { PassengerName="Dilshan Jayawardena", ScheduleTemplateId=4, TravelDate=week.AddDays(5), Status=BookingStatus.Confirmed, SeatSelections=[Selection(4,"Dilshan Jayawardena",SeatClass.Standard,9,310m)] });

        AddRequest(new SpecialRequest { BookingId=1,RequestType=RequestType.SeatingPreference,Description="Window seat preferred near the scenic hill route",Status=RequestStatus.Approved,DateNeeded=week,AdditionalCost=0 });
        AddRequest(new SpecialRequest { BookingId=2,RequestType=RequestType.SpecialMeal,Description="Vegetarian meal required for the journey",Status=RequestStatus.Pending,DateNeeded=week.AddDays(1),AdditionalCost=75m });
        AddRequest(new SpecialRequest { BookingId=4,RequestType=RequestType.WheelchairAssistance,Description="Wheelchair assistance required at Badulla station",Status=RequestStatus.Approved,DateNeeded=week.AddDays(2),AdditionalCost=0 });
        AddRequest(new SpecialRequest { BookingId=5,RequestType=RequestType.BaggageAssistance,Description="Porter assistance required at Galle station",Status=RequestStatus.Pending,DateNeeded=week.AddDays(4),AdditionalCost=150m });

        SeatReservation Selection(int scheduleId, string passenger, SeatClass seatClass, int ordinal, decimal price)
        {
            var seat = GetScheduleById(scheduleId)!.Seats.Single(item => item.SeatClass == seatClass && item.ClassOrdinal == ordinal);
            return new SeatReservation { PassengerName=passenger,ScheduleSeatId=seat.Id,ReservedPrice=price };
        }
        void SeedBooking(Booking booking)
        {
            var schedule = GetScheduleById(booking.ScheduleTemplateId)!;
            booking.DepartureStation = schedule.DepartureStation;
            booking.ArrivalStation = schedule.ArrivalStation;
            booking.DepartureTime = schedule.DepartureTime;
            lock (SyncRoot) AddBookingUnsafe(booking);
        }
    }
}

public static class DateTimeExtensions
{
    public static DateTime StartOfWeek(this DateTime date, DayOfWeek startOfWeek = DayOfWeek.Monday)
    {
        var difference = (7 + (date.DayOfWeek - startOfWeek)) % 7;
        return date.AddDays(-difference).Date;
    }
}
