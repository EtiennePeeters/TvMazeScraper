namespace TvMazeScraper.Domain
{
    /// <summary>
    /// This class solely exists to deserialize the TvMaze "cast" object,
    /// without the need for a custom deserializer.
    /// </summary>
    public class CastDto
    {
        public PersonDto Person { get; set; }

        public class PersonDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime? BirthDay { get; set; }
        }
    }
}
