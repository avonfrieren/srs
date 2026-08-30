using System;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

public class TimeFormatTests {
    [Fact]
    public void UnderOneMinuteHasNoLeadingZero() {
        Assert.Equal("2.550", TimeFormat.FromTicks(TimeSpan.FromSeconds(2.55).Ticks));
    }

    [Fact]
    public void UnderOneMinuteKeepsThreeDecimals() {
        Assert.Equal("27.999", TimeFormat.FromTicks(TimeSpan.FromSeconds(27.999).Ticks));
    }

    [Fact]
    public void OneMinuteSwitchesToMinuteFormat() {
        Assert.Equal("1:00.000", TimeFormat.FromTicks(TimeSpan.FromSeconds(60).Ticks));
    }

    [Fact]
    public void AboveOneMinutePadsSecondsToTwoDigits() {
        Assert.Equal("1:07.915", TimeFormat.FromTicks(TimeSpan.FromSeconds(67.915).Ticks));
    }

    [Fact]
    public void MillisecondsAreTruncatedNotRounded() {
        // 59.9995 s reste sous la minute et ne doit pas devenir "1:00.000"
        Assert.Equal("59.999", TimeFormat.FromTicks(TimeSpan.FromSeconds(59.9995).Ticks));
    }

    [Fact]
    public void ZeroIsFormattedNotBlank() {
        Assert.Equal("0.000", TimeFormat.FromTicks(0));
    }

    [Fact]
    public void DeltaIsSignedSecondsEvenPastAMinute() {
        Assert.Equal("-73.500", TimeFormat.Delta(-TimeSpan.FromSeconds(73.5).Ticks));
        Assert.Equal("+0.499", TimeFormat.Delta(TimeSpan.FromSeconds(0.499).Ticks));
    }
}
