using System.Globalization;
using SalaReservas.Api;

namespace SalaReservas.Api.Tests;

public class InstantParserTests
{
    [Theory]
    [InlineData("2030-06-01T10:00:00Z")]
    [InlineData("2030-06-01T10:00:00+00:00")]
    [InlineData("2030-06-01T12:00:00+02:00")]
    [InlineData("2030-06-01T04:30:00-05:30")]
    [InlineData("2030-06-02T00:00:00+14:00")]
    [InlineData("2030-05-31T20:00:00-14:00")]
    public void R02_EquivalentOffsetsProduceTheSameUtcInstant(string input)
    {
        var success = InstantParser.TryParse(input, out var instant);

        Assert.True(success);
        Assert.Equal(new DateTimeOffset(2030, 6, 1, 10, 0, 0, TimeSpan.Zero), instant);
        Assert.Equal(TimeSpan.Zero, instant.Offset);
    }

    [Theory]
    [InlineData("1", 1000000)]
    [InlineData("12", 1200000)]
    [InlineData("123", 1230000)]
    [InlineData("1234", 1234000)]
    [InlineData("12345", 1234500)]
    [InlineData("123456", 1234560)]
    [InlineData("1234567", 1234567)]
    [InlineData("0000001", 1)]
    public void R02_FractionsPreserveTickPrecision(string fraction, long ticks)
    {
        var expected = new DateTimeOffset(2030, 6, 1, 10, 0, 0, TimeSpan.Zero).AddTicks(ticks);

        Assert.True(InstantParser.TryParse($"2030-06-01T10:00:00.{fraction}Z", out var utc));
        Assert.True(InstantParser.TryParse($"2030-06-01T12:00:00.{fraction}+02:00", out var offset));

        Assert.Equal(expected, utc);
        Assert.Equal(expected, offset);
        Assert.Equal(TimeSpan.Zero, utc.Offset);
        Assert.Equal(TimeSpan.Zero, offset.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("2030-06-01T10:00:00")]
    [InlineData("2030-06-01T10:00:00.1234567")]
    [InlineData("2030-06-01")]
    [InlineData("2030-06-01T10:00Z")]
    [InlineData("2030-06-01T10:00:00.Z")]
    [InlineData("2030-06-01T10:00:00.12345678Z")]
    [InlineData("2030-06-01T10:00:00,123Z")]
    [InlineData("2030-06-01 10:00:00Z")]
    [InlineData("2030-06-01t10:00:00z")]
    [InlineData("2030-6-1T10:00:00Z")]
    [InlineData(" 2030-06-01T10:00:00Z")]
    [InlineData("2030-06-01T10:00:00Z\n")]
    [InlineData("2030-06-01T10:00:00Zjunk")]
    [InlineData("2030-06-01T10:00:00+02")]
    [InlineData("2030-06-01T10:00:00+0200")]
    [InlineData("2030-06-01T10:00:00+14:01")]
    [InlineData("2030-06-01T10:00:00-14:01")]
    [InlineData("2030-06-01T10:00:00+02:60")]
    [InlineData("2030-02-29T10:00:00Z")]
    [InlineData("2030-13-01T10:00:00Z")]
    [InlineData("2030-06-31T10:00:00Z")]
    [InlineData("2030-06-01T24:00:00Z")]
    [InlineData("2030-06-01T10:60:00Z")]
    [InlineData("2030-06-01T10:00:60Z")]
    [InlineData("0001-01-01T00:00:00+00:01")]
    [InlineData("9999-12-31T23:59:59-00:01")]
    public void R02_A09_InvalidOrAmbiguousInputIsRejected(string? input)
    {
        Assert.False(InstantParser.TryParse(input, out var instant));
        Assert.Equal(default(DateTimeOffset), instant);
    }

    [Theory]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    [InlineData("ar-SA")]
    public void R02_ParsingDoesNotDependOnCurrentCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            Assert.True(InstantParser.TryParse("2030-06-01T12:00:00+02:00", out var instant));
            Assert.Equal(new DateTimeOffset(2030, 6, 1, 10, 0, 0, TimeSpan.Zero), instant);
            Assert.Equal(TimeSpan.Zero, instant.Offset);
            Assert.False(InstantParser.TryParse("2030-06-01T12:00:00", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("0001-01-01T00:00:00Z", 1, 1, 1)]
    [InlineData("2000-02-29T00:00:00Z", 2000, 2, 29)]
    [InlineData("9999-12-31T00:00:00Z", 9999, 12, 31)]
    public void R02_ValidCalendarDatesAreNotRestrictedByBusinessTimeRules(
        string input, int year, int month, int day)
    {
        Assert.True(InstantParser.TryParse(input, out var instant));
        Assert.Equal(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero), instant);
    }
}
