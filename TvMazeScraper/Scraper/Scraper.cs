using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Serilog;
using System.Net;
using System.Text.Json;
using TvMazeScraper.Domain;

namespace TvMazeScraper.Scraper
{
    /// <summary>
    /// This background service scrapes and stores all shows and their cast from the TvMaze API.
    /// </summary>
    /// <remarks>
    /// It only scrapes shows once based on their ID. Will continue where left off.
    /// Can be enabled or disabled with the ScraperOptions.Enabled setting in appsettings.json.
    /// The constant delay duration after each request can be changed with the ScraperOptions.RequestDelayMs setting in appsettings.json.
    /// </remarks>
    public class Scraper : BackgroundService
    {
        private readonly TvMazeContext db;
        private readonly IOptions<ScraperOptions> options;
        private readonly HttpClient client;

        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // Set up a retry policy, because of possible rate-limiting. See: https://www.tvmaze.com/api#rate-limiting
        private readonly AsyncPolicy retry = Policy.Handle<HttpRequestException>(ex => ex.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryForeverAsync(retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            (ex, timespan) =>
            {
                Log.Information("Request failed because of rate limiting, waiting for {timespan}...", timespan);
            });

        public Scraper(IServiceProvider services, IOptions<ScraperOptions> options, HttpClient client)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            var scope = services.CreateAsyncScope();
            db = scope.ServiceProvider.GetRequiredService<TvMazeContext>();

            this.options = options ?? throw new ArgumentNullException(nameof(options));

            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.client.BaseAddress = new Uri("https://api.tvmaze.com/");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!options.Value.Enabled) return;

            // Find the last show's id added to the database.
            var lastShowId = (await db.Shows.OrderByDescending(i => i.Id).FirstOrDefaultAsync(stoppingToken))?.Id ?? 0;
            // Use the last show's id to get the pagenumer of the api's shows index page, to continue scraping.
            var page = lastShowId / 250;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var shows = await GetShowsByIndexPageAsync(page, stoppingToken);
                    if (shows is null) return;

                    // Get cast for each show, and add to the database.
                    // Skip all shows that are already in the database.
                    foreach (var show in shows.Where(s => s.Id > lastShowId))
                    {
                        db.Shows.Add(show);

                        // Get all cast for this show.
                        var cast = await GetCastByShowIdAsync(show.Id, stoppingToken);
                        if (cast is not null)
                        {
                            // Get cast members which are already in the database.
                            var castIds = cast.Select(c => c.Id).ToArray();
                            var castFromDb = await db.Cast
                                .Where(c => castIds.Contains(c.Id))
                                .Include(c => c.Shows)
                                .ToListAsync(stoppingToken);

                            foreach (var castMember in cast)
                            {
                                // Check if cast member is already in the database.
                                var castMemberFromDb = castFromDb.SingleOrDefault(c => c.Id == castMember.Id);
                                if (castMemberFromDb is not null)
                                {
                                    // Add show to existing cast member.
                                    if (castMemberFromDb.Shows is null)
                                    {
                                        castMemberFromDb.Shows = new List<Show>();
                                    }
                                    castMemberFromDb.Shows.Add(show);
                                    db.Cast.Update(castMemberFromDb);
                                }
                                else
                                {
                                    // Add show to new cast member.
                                    castMember.Shows = new List<Show> { show };
                                    db.Cast.Add(castMember);
                                }
                            }
                        }

                        await db.SaveChangesAsync(stoppingToken);

                        await Task.Delay(options.Value.RequestDelayMs, stoppingToken);
                    }
                    
                    page++;
                }
            }
            catch (Exception ex) when (ex is DbUpdateException || ex is DbUpdateConcurrencyException)
            {
                Log.Error(ex, "Error writing to the database.");
                throw;
            }
            catch (Exception ex)
            {
                // Some other exception occured. Because we do not know what happened, just stop scraping to be sure.
                // (This should be diagnosed and possibly added as a seperate flow. Maybe scraping can be retried in some cases.)
                Log.Fatal(ex, "Scraping stopped due to a fatal exception.");
                throw;
            }

        }

        private async Task<List<Show>> GetShowsByIndexPageAsync(int page, CancellationToken stoppingToken)
        {
            try
            {
                var showsResponse = await retry.ExecuteAsync(async () => (await client.GetAsync($"shows?page={page}")).EnsureSuccessStatusCode());
                var showsContent = await showsResponse.Content.ReadAsStreamAsync(stoppingToken);
                var shows = await JsonSerializer.DeserializeAsync<List<Show>>(showsContent, jsonOptions, stoppingToken);
                return shows;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Information("Stopped scraping because last page of shows index is reached.");
                return null;
            }
        }

        private async Task<List<Cast>> GetCastByShowIdAsync(int showId, CancellationToken stoppingToken)
        {
            var castResponse = await retry.ExecuteAsync(async () => (await client.GetAsync($"shows/{showId}/cast")).EnsureSuccessStatusCode());
            var castContent = await castResponse.Content.ReadAsStreamAsync(stoppingToken);
            var castDtos = await JsonSerializer.DeserializeAsync<List<CastDto>>(castContent, jsonOptions, stoppingToken);
            if (castDtos is null) return null;

            // Using distinct here because there are some duplicate cast members in some shows.
            var cast = castDtos.DistinctBy(c => c.Person.Id)
                .Select(c => new Cast
                {
                    Id = c.Person.Id,
                    Name = c.Person.Name,
                    BirthDay = c.Person.BirthDay
                }).ToList();

            return cast;
        }
    }
}
