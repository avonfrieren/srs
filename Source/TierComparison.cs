using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

// Phase 4 (reshaped by phase 6): when RunWatcher captures a finished run of
// the selected segment, compare the final time against the segment's sheet
// tiers and draw the time + the reached tier's name in the tier's color under
// the timer, like srta's delta row. srs is the holder of the reference time —
// SpeedrunTool's own display keeps obeying its Number of Rooms setting, which
// srs no longer touches.
public static class TierComparison {
    private static SrsSettings Settings => SrsModule.Settings;

    // recomputed every frame from RunWatcher's capture (srta-style), so the
    // row reacts instantly to selection changes and sheet re-imports; derived
    // state only, deliberately not registered with save states (the capture
    // itself is, in RunWatcher)
    private static string rowText = "";
    private static Color tierColor = Color.White;

    // drop the row below srta's delta row when srta is present; resolved on
    // first render (mod load order between srs and srta is not guaranteed)
    private static bool? srtaLoaded;

    public static void Load() {
        // after RunWatcher's Level.Update hook: this one wraps it, so after
        // orig the frame's capture is already settled when the tier computes
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.SpeedrunTimerDisplay.Render += SpeedrunTimerDisplayOnRender;
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.SpeedrunTimerDisplay.Render -= SpeedrunTimerDisplayOnRender;
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // srta-style hotkey: flip the toggle and confirm with SpeedrunTool's
        // popup, so the row can be hidden without leaving the game
        if (!self.Paused && Settings.ToggleShowTier.Pressed) {
            Settings.ShowTier = !Settings.ShowTier;
            SrsModule.Instance.SaveSettings();
            PopupMessageUtils.ShowOptionState(Dialog.Clean("MODOPTIONS_SRS_SHOWTIER"),
                Dialog.Clean(Settings.ShowTier ? DialogIds.On : DialogIds.Off));
        }

        ComputeTier();
    }

    // first tier column whose threshold is >= the captured time wins; slower
    // than every threshold (i.e. beyond Red 3) is Unranked. The Hidden column
    // is 0:00.000 everywhere and never matches; empty/unparseable cells (null)
    // are skipped. The captured time is drawn left of the tier name, in the
    // tier's color — srs's frozen reference time, since SpeedrunTool's own
    // display no longer freezes on the segment's real end. A completed run
    // that failed the start guard (savestate planted mid-segment) shows its
    // frozen time alone, greyed: the end of the run stays visible, the grey
    // says it earned no tier
    private static void ComputeTier() {
        rowText = "";
        if (!RunWatcher.Completed) {
            return;
        }

        TimeSpan time = TimeSpan.FromTicks(RunWatcher.CapturedTicks);
        if (!RunWatcher.HasCapture) {
            rowText = FormatTime(time);
            tierColor = Color.Gray;
            return;
        }

        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        SheetSegment segment = SegmentSelector.Current;
        if (block == null || segment == null) {
            return;
        }

        for (int i = 0; i < segment.Times.Count && i < block.Columns.Count; i++) {
            if (segment.Times[i] is { } threshold && threshold > TimeSpan.Zero && time <= threshold) {
                SetTier(time, block.Columns[i]);
                return;
            }
        }

        SetTier(time, "Unranked");
    }

    // SpeedrunTool's own time format ("s.fff" under a minute, "m:ss.fff" above)
    private static string FormatTime(TimeSpan time) =>
        time.ToString(time.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");

    // tier colors: each sheet column name maps to its exact palette hex. Unlike
    // XNA's named colors, the "1"-"3" rank suffix is significant here, so
    // Purple 1/2/3 (and the other ranked tiers) are three distinct shades.
    // WR/Hidden are white; Unranked stays grey; unknown columns fall back white
    private static readonly Dictionary<string, Color> TierColors =
        new(StringComparer.OrdinalIgnoreCase) {
            ["WR"] = Calc.HexToColor("ffffff"),
            ["Hidden"] = Calc.HexToColor("ffffff"),
            ["Gold"] = Calc.HexToColor("ffbf00"),
            ["Pink"] = Calc.HexToColor("c27ba0"),
            ["Purple 1"] = Calc.HexToColor("8e7cc3"),
            ["Purple 2"] = Calc.HexToColor("b4a7d6"),
            ["Purple 3"] = Calc.HexToColor("d9d2e9"),
            ["Indigo 1"] = Calc.HexToColor("7980f7"),
            ["Indigo 2"] = Calc.HexToColor("a2a7fe"),
            ["Indigo 3"] = Calc.HexToColor("bbbfff"),
            ["Blue 1"] = Calc.HexToColor("6fa8dc"),
            ["Blue 2"] = Calc.HexToColor("9fc5e8"),
            ["Blue 3"] = Calc.HexToColor("cfe2f3"),
            ["Cyan 1"] = Calc.HexToColor("76a5af"),
            ["Cyan 2"] = Calc.HexToColor("a2c4c9"),
            ["Cyan 3"] = Calc.HexToColor("d0e0e3"),
            ["Green 1"] = Calc.HexToColor("93c47d"),
            ["Green 2"] = Calc.HexToColor("b6d7a8"),
            ["Green 3"] = Calc.HexToColor("d9ead3"),
            ["Olive 1"] = Calc.HexToColor("afc47d"),
            ["Olive 2"] = Calc.HexToColor("cbde9e"),
            ["Olive 3"] = Calc.HexToColor("e6f9ba"),
            ["Yellow 1"] = Calc.HexToColor("ffd966"),
            ["Yellow 2"] = Calc.HexToColor("ffe599"),
            ["Yellow 3"] = Calc.HexToColor("fff2cc"),
            ["Orange 1"] = Calc.HexToColor("f6b26b"),
            ["Orange 2"] = Calc.HexToColor("f9cb9c"),
            ["Orange 3"] = Calc.HexToColor("fce5cd"),
            ["Red 1"] = Calc.HexToColor("e06666"),
            ["Red 2"] = Calc.HexToColor("ea9999"),
            ["Red 3"] = Calc.HexToColor("f4cccc"),
        };

    private static void SetTier(TimeSpan time, string column) {
        rowText = $"{FormatTime(time)} {column}";
        tierColor = column.Trim().Equals("Unranked", StringComparison.OrdinalIgnoreCase)
            ? Color.Gray
            : TierColors.GetValueOrDefault(column.Trim(), Color.White);
    }

    private static void SpeedrunTimerDisplayOnRender(On.Celeste.SpeedrunTimerDisplay.orig_Render orig, SpeedrunTimerDisplay self) {
        orig(self);

        if (!Settings.ShowTier || rowText.Length == 0) {
            return;
        }

        // hidden along with the room timer itself
        if (SpeedrunToolSettings.Instance is not { Enabled: true } settings
            || settings.RoomTimerType == RoomTimerType.Off || self.DrawLerp <= 0f) {
            return;
        }

        DrawTier(self);
    }

    // row below SpeedrunTool's time + PB rows (below srta's delta row when srta
    // is installed), same background and sliding animation
    private static void DrawTier(SpeedrunTimerDisplay self) {
        const float topTimeHeight = 38f;
        const float timeMarginLeft = 32f;
        const float scale = 0.6f;

        srtaLoaded ??= IsSrtaLoaded();

        PixelFont font = Dialog.Languages["english"].Font;
        float fontFaceSize = Dialog.Languages["english"].FontFaceSize;
        float textWidth = font.Get(fontFaceSize).Measure(rowText).X * scale;

        MTexture bg = GFX.Gui["strawberryCountBG"];
        float rowHeight = bg.Height * scale + 1f;
        float x = -300f * Ease.CubeIn(1f - self.DrawLerp);
        float y = self.Y + topTimeHeight + rowHeight;
        if (srtaLoaded.Value) {
            y += rowHeight + 1f;
        }

        float width = timeMarginLeft + textWidth;
        Draw.Rect(x, y - 1f, width + bg.Width * scale, 1f, Color.Black);
        Draw.Rect(x, y, width + 2f, rowHeight, Color.Black);
        bg.Draw(new Vector2(x + width, y), Vector2.Zero, Color.White, scale);

        font.DrawOutline(fontFaceSize, rowText, new Vector2(x + timeMarginLeft, y + 28.4f),
            new Vector2(0f, 1f), Vector2.One * scale, tierColor, 2f, Color.Black);
    }

    private static bool IsSrtaLoaded() {
        foreach (EverestModule module in Everest.Modules) {
            if (module.Metadata?.Name == "srta") {
                return true;
            }
        }

        return false;
    }
}
