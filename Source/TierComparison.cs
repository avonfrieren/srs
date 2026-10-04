using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

// The row shows the latest record's time and tier: the time of the segment
// RunWatcher last closed, compared against the segment's sheet tiers, drawn
// with the reached tier's name in the tier's color under the timer, like
// srta's delta row. srs is the holder of the reference time —
// SpeedrunTool's own display keeps obeying its Number of Rooms setting, which
// srs no longer touches.
public static class TierComparison {
    private static SrsSettings Settings => SrsModule.Settings;

    // recomputed every frame from RunWatcher.Latest (srta-style), so the row
    // reacts instantly to sheet re-imports; session display only, not
    // registered with save states
    private static string rowText = "";
    private static Color tierColor = Color.White;

    // drop the row below srta's delta row when srta is present; resolved on
    // first render (mod load order between srs and srta is not guaranteed)
    private static bool? srtaLoaded;

    public static void Load() {
        // after RunWatcher's Level.Update hook: this one wraps it, so after
        // orig the frame's records are already settled when the tier computes
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.SpeedrunTimerDisplay.Render += SpeedrunTimerDisplayOnRender;
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.SpeedrunTimerDisplay.Render -= SpeedrunTimerDisplayOnRender;
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        if (!Settings.Enabled) {
            return;
        }

        // srta-style hotkey: flip the toggle and confirm with SpeedrunTool's
        // popup, so the row can be hidden without leaving the game. Hotkeys
        // already answers false while paused, so the row cannot be toggled
        // from behind the pause menu
        if (Hotkeys.Pressed(Hotkeys.ToggleShowTier)) {
            Settings.ShowTier = !Settings.ShowTier;
            SrsModule.Instance.SaveSettings();
            PopupMessageUtils.ShowOptionState(Dialog.Clean("MODOPTIONS_SRS_SHOWTIER"),
                Dialog.Clean(Settings.ShowTier ? DialogIds.On : DialogIds.Off));
        }

        ComputeTier();
    }

    // first tier column whose threshold is >= the time wins; past every one,
    // Unranked. The row is the latest record's, looked up by name in the sheet
    // as it is now, since SheetImporter.Data is reassigned from a worker
    private static void ComputeTier() {
        rowText = "";
        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        if (RunWatcher.Latest is not { } record || block == null
            || block.Find(record.Rule.Chapter, record.Rule.Name) is not { } segment) {
            return;
        }

        TimeSpan time = TimeSpan.FromTicks(record.Ticks);
        SetTier(time, SheetData.TierOf(block.Columns, segment.Times, time));
    }

    // Format matches what SpeedrunTool displayed during the run (see TimeFormat.FromTicks).
    private static string FormatTime(TimeSpan time) =>
        TimeFormat.FromTicks(time.Ticks);

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

        // hidden along with the room timer itself, and with the whole mod
        if (!Settings.Enabled
            || SpeedrunToolSettings.Instance is not { Enabled: true } settings
            || settings.RoomTimerType == RoomTimerType.Off || self.DrawLerp <= 0f) {
            return;
        }

        if (Settings.ShowTier && rowText.Length > 0) {
            DrawRow(self, 0, rowText, tierColor);
        }
    }

    // row below SpeedrunTool's time + PB rows (below srta's delta row when srta
    // is installed), same background and sliding animation; row 0 is the only
    // slot srs owns, each further one would sit a row lower
    private static void DrawRow(SpeedrunTimerDisplay self, int row, string text, Color color) {
        const float topTimeHeight = 38f;
        const float timeMarginLeft = 32f;
        const float scale = 0.6f;

        srtaLoaded ??= IsSrtaLoaded();

        PixelFont font = Dialog.Languages["english"].Font;
        float fontFaceSize = Dialog.Languages["english"].FontFaceSize;

        MTexture bg = GFX.Gui["strawberryCountBG"];
        float rowHeight = bg.Height * scale + 1f;
        float x = -300f * Ease.CubeIn(1f - self.DrawLerp);
        float y = self.Y + topTimeHeight + rowHeight + row * (rowHeight + 1f);
        if (srtaLoaded.Value) {
            y += rowHeight + 1f;
        }

        // SpeedrunTool's own PB-row width heuristic, read off 3.27.17, not the
        // measured text width: the background is a
        // 288px strip that fades out to the right, and every row of the stack is
        // meant to end *inside* the text, letting the tail sit over the fade.
        // Measuring the text put the whole fade past the last character instead,
        // making this row visibly longer than the ones above it. Nothing rechecks
        // the formula against SpeedrunTool, so a release that changes it brings
        // that back with no test failing
        float width = 60f + Math.Max(0f, 18f * (text.Length - 8));
        Draw.Rect(x, y - 1f, width + bg.Width * scale, 1f, Color.Black);
        Draw.Rect(x, y, width + 2f, rowHeight, Color.Black);
        bg.Draw(new Vector2(x + width, y), Vector2.Zero, Color.White, scale);

        font.DrawOutline(fontFaceSize, text, new Vector2(x + timeMarginLeft, y + 28.4f),
            new Vector2(0f, 1f), Vector2.One * scale, color, 2f, Color.Black);
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
