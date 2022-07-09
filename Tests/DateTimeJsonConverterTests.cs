using System.Text.Json;

namespace Tests
{
    public class DateTimeJsonConverterTests
    {
        [Fact]
        public void ConvertsDateTimeToCustomDateFormat()
        {
            var stringDate = "1979-07-17";
            var expected = $"\"{stringDate}\"";
            var date = DateTime.Parse(stringDate);

            var jsonSerializerOptions = new JsonSerializerOptions()
            {
                Converters =
                {
                    new DateTimeJsonConverter()
                }
            };
            var json = JsonSerializer.Serialize(date, jsonSerializerOptions);

            Assert.Equal(expected, json);
        }
    }
}