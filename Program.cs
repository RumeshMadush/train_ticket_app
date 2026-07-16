using TrainTicketApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Session for chatbot history
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register the in-memory data store as a singleton
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<BookingOccurrenceService>();
builder.Services.AddSingleton<BookingService>();
builder.Services.AddSingleton<WeeklyReportService>();
builder.Services.AddSingleton<ForecastService>();
builder.Services.AddSingleton<ChatbotService>();

var app = builder.Build();

// Seed initial data
var dataStore = app.Services.GetRequiredService<DataStore>();
dataStore.Seed();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
