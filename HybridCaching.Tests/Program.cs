using System.Text.Json.Serialization;
using HybridRedisCache;
using Microsoft.Extensions.Caching.Hybrid;
using HybridCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApiDocument();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddMemoryCache();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis");
    options.InstanceName = "hybridcachingtests_";
});

#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018

builder.Services.AddHttpClient<CalenderApiService>(options =>
{
    options.BaseAddress = new Uri("https://holidayapi.ir");
});

builder.Services.AddHybridRedisCaching(options =>
{
    options.AllowAdmin = true;
    options.EnableTracing = true;
    options.TracingActivitySourceName = "HybridRedisCache";
    options.InstancesSharedName = "HybridRedisCachingTests";
    options.DefaultDistributedExpirationTime = TimeSpan.FromHours(1);
    options.DefaultLocalExpirationTime = TimeSpan.FromMinutes(45);
    options.KeepAlive = 30;
    options.RedisConnectionString = builder.Configuration.GetConnectionString("redis");

});


var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.MapGet("/GetEventsNoCache", async (int year, int month, int day, CalenderApiService service) =>
{
    return await service.GetGeorgianDate(year, month, day);
});

app.MapGet("/GetEventsHybridCache", async (int year, int month, int day, CalenderApiService service,HybridCache cache,ILogger<Program> logger) =>
{
    var cacheKey=$"{year}-{month}-{day}";

    return await cache.GetOrCreateAsync(cacheKey, async _ =>
    {
        logger.LogWarning("Cache Miss for key {key}",cacheKey);
        var result=await service.GetGeorgianDate(year, month, day);

        return result;
    },new HybridCacheEntryOptions()
    {
        Expiration = TimeSpan.FromHours(1)
    });
});


app.MapGet("/GetEventsHybridRedisCache", async (int year, int month, int day, CalenderApiService service,IHybridCache cache,ILogger<Program> logger) =>
{
    var cacheKey=$"{year}-{month}-{day}";

    return await cache.GetAsync(cacheKey, async _ =>
    {
        logger.LogWarning("Cache Miss for key {key}",cacheKey);
        var result=await service.GetGeorgianDate(year, month, day);

        return result;
    });
});


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class CalenderApiService(HttpClient client)
{
    public record Event(
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("additional_description")] string AdditionalDescription,
        [property: JsonPropertyName("is_holiday")] bool IsHoliday,
        [property: JsonPropertyName("is_religious")] bool IsReligious
    );

    public record CalendarApiResponseModel(
        [property: JsonPropertyName("is_holiday")] bool IsHoliday,
        [property: JsonPropertyName("events")] IReadOnlyList<Event> Events
    );

    public async Task<CalendarApiResponseModel?> GetGeorgianDate(int year, int month, int day)
    {
       return await client.GetFromJsonAsync<CalendarApiResponseModel>($"/jalali/{year}/{month}/{day}");
       
    }
}