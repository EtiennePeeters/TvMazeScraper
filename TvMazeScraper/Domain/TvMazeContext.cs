using Microsoft.EntityFrameworkCore;

namespace TvMazeScraper.Domain
{
    public class TvMazeContext : DbContext
    {
        public TvMazeContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Show> Shows { get; set; }
        public DbSet<Cast> Cast { get; set; }
    }
}
