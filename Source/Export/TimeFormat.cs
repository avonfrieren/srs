using System;
using System.Globalization;

namespace Celeste.Mod.SpeedrunSheet;

/// Mirrors RoomTimerData.FormatTime so exported times are byte-identical to what
/// SpeedrunTool displayed during the run. TimeSpan custom formats truncate, they
/// do not round — that is the intended behaviour here.
public static class TimeFormat {
    public static string FromTicks(long ticks) {
        TimeSpan span = TimeSpan.FromTicks(ticks);
        string format = span.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff";
        return span.ToString(format, CultureInfo.InvariantCulture);
    }
}
