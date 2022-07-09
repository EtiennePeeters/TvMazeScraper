using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;
using TvMazeScraper;
using TvMazeScraper.Domain;
using TvMazeScraper.Scraper;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(nameof(ApiOptions)));
builder.Services.Configure<ScraperOptions>(builder.Configuration.GetSection(nameof(ScraperOptions)));

builder.Services.AddDbContext<TvMazeContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddHttpClient();

builder.Services.AddHostedService<Scraper>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

// Map endpoints.

app.MapGet("/shows", async (TvMazeContext db, IOptions<ApiOptions> options, CancellationToken cancellationToken, int? page) =>
{
    var pageSize = options.Value.PageSize;

    var shows = await db.Shows.OrderBy(s => s.Id)
        .Skip(pageSize * (page ?? 0)).Take(pageSize)
        .Include(s => s.Cast.OrderByDescending(c => c.BirthDay))
        .ToListAsync(cancellationToken);

    var jsonSerializerOptions = new JsonSerializerOptions() 
    { 
        WriteIndented = true, 
        Converters = { new DateTimeJsonConverter() } 
    };

    return JsonSerializer.Serialize(shows, jsonSerializerOptions);
});

app.Run();