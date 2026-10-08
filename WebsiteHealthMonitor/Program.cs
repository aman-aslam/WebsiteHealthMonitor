using WebsiteHealthMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<PageLoadService>();
builder.Services.AddScoped<WebsiteHealthService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("HealthCheck", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0 Safari/537.36 WebsiteHealthMonitor/1.0");
});

var app = builder.Build();

// Swagger should also work in Production on Vercel
app.UseSwagger();
app.UseSwaggerUI();

// Simple root endpoint
app.MapGet("/", () => "Website Health Monitor API is running!");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
