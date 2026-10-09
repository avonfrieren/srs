using System;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// The time format is not pinned here: SpeedrunTool's own formatter is the
// authority, and this project cannot reference it. TimeFormat owns only the
// delta.
public class TimeFormatTests {
    [Fact]
    public void DeltaIsSignedSecondsEvenPastAMinute() {
        Assert.Equal("-73.500", TimeFormat.Delta(-TimeSpan.FromSeconds(73.5).Ticks));
        Assert.Equal("+0.499", TimeFormat.Delta(TimeSpan.FromSeconds(0.499).Ticks));
    }

    [Fact]
    public void DeltaKeepsThreeDecimalsAndSignsZeroPositive() {
        Assert.Equal("+0.000", TimeFormat.Delta(0));
    }
}
