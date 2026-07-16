using TrainTicketApp.Models;

namespace TrainTicketApp.Services;

public sealed class WeeklyReportService(DataStore store, BookingOccurrenceService occurrences)
{
    public WeeklyReport Generate(DateTime weekStart)
    {
        var start = weekStart.StartOfWeek();
        var generated = occurrences.GenerateAll(start, start.AddDays(6));
        var requests = store.GetAllRequests().Where(item => item.DateNeeded.Date >= start && item.DateNeeded.Date <= start.AddDays(6)).ToList();
        return new WeeklyReport
        {
            WeekStart = start,
            Days = Enumerable.Range(0, 7).Select(offset =>
            {
                var date = start.AddDays(offset);
                return new DaySummary
                {
                    Date = date,
                    Occurrences = generated.Where(item => item.TravelDate == date).ToList(),
                    SpecialRequests = requests.Where(item => item.DateNeeded.Date == date).ToList()
                };
            }).ToList()
        };
    }
}
