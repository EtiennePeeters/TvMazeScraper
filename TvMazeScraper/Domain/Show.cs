namespace TvMazeScraper.Domain
{
    public class Show
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Cast> Cast { get; set; }
    }
}
