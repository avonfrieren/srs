using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the three tabs as they were last exported; refreshing them is how a
// change in the sheet becomes a failing test (see SheetConsistencyTests)
internal static class Fixtures {
    public static string ASides { get; } = Read("asides.csv");
    public static string BSides { get; } = Read("bsides.csv");
    public static string Farewell { get; } = Read("farewell.csv");

    public static SheetData Parsed { get; } = SheetData.Parse(ASides, BSides, Farewell);

    public static List<SheetSegment> Imported => Parsed.CheckpointBlock.Segments;

    private static string Read(string name) =>
        System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
