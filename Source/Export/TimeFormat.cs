using System;
using System.Globalization;

namespace Celeste.Mod.SpeedrunSheet;

/// The one place a tick count becomes a string a player sees or the sheet
/// receives.
public static class TimeFormat {
    /// SpeedrunTool's own formatter, installed by SrsModule.Load: it measured
    /// the segment, so what it prints is what the player watched.
    ///
    /// A delegate rather than a call, for the reason ExportProtocol.Localize is
    /// one — this file is compiled into the test project, which references
    /// neither Celeste nor SpeedrunTool and installs its own stand-in.
    public static Func<long, string> Format;

    public static string FromTicks(long ticks) =>
        Format is { } format
            ? format(ticks)
            // SrsModule fails at load rather than reach here, so this is the
            // test project having forgotten to install its stand-in
            : throw new InvalidOperationException(
                "TimeFormat.Format was never set; SpeedrunTool's formatter is the only one srs has");

    /// A signed difference, always in seconds: "+1.250", "-73.000". Ours, not
    /// SpeedrunTool's -- it has no such format. Deltas are read against each
    /// other and against a threshold, where seconds stay comparable at a glance
    /// and m:ss stops being so.
    public static string Delta(long ticks) {
        double seconds = TimeSpan.FromTicks(ticks).TotalSeconds;
        return (seconds < 0 ? "-" : "+")
             + Math.Abs(seconds).ToString("0.000", CultureInfo.InvariantCulture);
    }
}
