using System.Text.Json.Serialization;

namespace TvMazeScraper.Domain
{
    public class Cast
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime? BirthDay { get; set; }
        [JsonIgnore]
        public virtual ICollection<Show> Shows { get; set; }
    }
}
