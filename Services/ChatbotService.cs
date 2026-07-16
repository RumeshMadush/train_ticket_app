using TrainTicketApp.Models;

namespace TrainTicketApp.Services;

public sealed class ChatbotService(DataStore store, ForecastService forecast)
{
    public string Reply(string message)
    {
        var text = message.ToLowerInvariant().Trim();
        if (text.Contains("all routes") || text.Contains("show routes") || text.Contains("available routes"))
            return "🚆 Available routes:\n" + string.Join("\n", store.GetAllRoutes().Select(route => $"• {route}"));

        if (text.Contains("busiest") || text.Contains("busy day") || text.Contains("popular day"))
        {
            var patterns = forecast.AnalysePatterns().GroupBy(item => item.DayOfWeek).Select(group => new { Day=group.Key,Count=group.Sum(item => item.BookingCount) }).OrderByDescending(item => item.Count).ToList();
            if (patterns.Count == 0) return "Not enough booking data to identify a busiest day.";
            return "📊 Reserved-seat frequency:\n" + string.Join("\n", patterns.Select(item => $"• {item.Day}: {item.Count} seat occurrence(s)")) + $"\n\n🔥 Busiest day: {patterns[0].Day}.";
        }

        if (text.Contains("price") || text.Contains("cost") || text.Contains("how much"))
        {
            var (from,to,date) = ExtractRouteAndDate(text);
            if (from is null || to is null) return "Please specify a route, for example: price from Colombo Fort to Kandy on Friday.";
            var result = forecast.PredictPrice(from,to,date);
            return $"💰 {result.Route}\n{result.TargetDate:dddd, d MMMM}: Rs {result.PredictedPrice:F2}\nTrend: {result.PriceTrend} · Confidence: {result.Confidence:P0}\n{result.Explanation}";
        }

        if (text.Contains("seat") || text.Contains("available") || text.Contains("availability") || text.Contains("space"))
        {
            var (from,to,date) = ExtractRouteAndDate(text);
            if (from is null || to is null) return "Please specify a route, for example: seats from Colombo Fort to Galle on Monday.";
            var result = forecast.PredictAvailability(from,to,date);
            return $"🚦 {result.Route}\n{result.TargetDate:dddd, d MMMM}: {result.PredictedSeats} predicted seat(s)\nStatus: {result.AvailabilityStatus} · Confidence: {result.Confidence:P0}\n{result.Explanation}";
        }

        if (text.Contains("trend") || text.Contains("forecast") || text.Contains("predict") || text.Contains("future"))
        {
            var patterns = forecast.AnalysePatterns();
            if (patterns.Count == 0) return "There is not enough occurrence data for a trend summary yet.";
            var busiest = patterns.MaxBy(item => item.BookingCount)!;
            var expensive = patterns.MaxBy(item => item.AveragePrice)!;
            return $"📈 Booking trend summary:\n• Strongest pattern: {busiest.Route} on {busiest.DayOfWeek} ({busiest.BookingCount} seats)\n• Highest recorded average: {expensive.Route} (Rs {expensive.AveragePrice:F2})";
        }

        if (text.Contains("help") || text.Contains("what can you") || text.Contains('?')) return "🤖 Ask me about route prices, occurrence-level seat availability, busiest days, routes or booking trends.";
        if (text.Contains("hello") || text.Contains("hi") || text.Contains("hey")) return "👋 Hello! Ask me about prices, seat availability or booking trends.";
        return "I didn't understand that. Try asking about prices, availability, routes or trends.";
    }

    private (string? from, string? to, DateTime date) ExtractRouteAndDate(string text)
    {
        var date = DateTime.Today.AddDays(1);
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            if (!text.Contains(day.ToString().ToLowerInvariant())) continue;
            date = DateTime.Today;
            while (date.DayOfWeek != day) date = date.AddDays(1);
            break;
        }
        string? from = null, to = null;
        var stations = store.GetAllSchedules().SelectMany(item => new[]{item.DepartureStation,item.ArrivalStation}).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(item => item.Length);
        foreach (var station in stations.Where(station => text.Contains(station.ToLowerInvariant()))) { if (from is null) from=station; else { to=station; break; } }
        return (from,to,date);
    }
}
