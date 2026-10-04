using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

// The row under Speed Run Tool's timer: the latest record's time, and the tier
// it reached in the tier's color, like srta's delta row.
public static class TierComparison {
    private static SrsSettings Settings => SrsModule.Settings;

    // recomputed every frame from RunWatcher.Latest, so a sheet re-import shows
    // at once; session display, not registered with save states
    private static string rowText = "";
    private static Color tierColor = Color.White;

    // drop the row below srta's delta row when srta is present; resolved on
    // first render (mod load order between srs and srta is not guaranteed)
    private static bool? srtaLoaded;

    public static void Load() {
        // hook order: see SrsModule.Load
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

        // flip the toggle and confirm with Speed Run Tool's popup; Hotkeys
        // answers false while paused
        if (Hotkeys.Pressed(Hotkeys.ToggleShowTier)) {
            Settings.ShowTier = !Settings.ShowTier;
            SrsModule.TrySaveSettings("Show Tier");
            PopupMessageUtils.ShowOptionState(Dialog.Clean("MODOPTIONS_SRS_SHOWTIER"),
                Dialog.Clean(Settings.ShowTier ? DialogIds.On : DialogIds.Off));
        }

        ComputeTier();
    }

    // the latest record's row, looked up by name (see SheetBlock.Find), and the
    // tier its time reached (SheetData.TierOf)
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

    // tier colors, copied from the sheet: each tier's cell fill, keyed by the
    // full column name (the rank suffix is significant). Gold takes its ink,
    // since its fill is near black; WR is white; Unranked is grey, as on the
    // sheet. Hidden never matches (its thresholds are zero) and, like any
    // unknown column, falls back white
    private static readonly Dictionary<string, Color> TierColors =
        new(StringComparer.OrdinalIgnoreCase) {
            ["WR"] = Calc.HexToColor("ffffff"),
            ["Gold"] = Calc.HexToColor("ffbf00"),
            ["Pink"] = Calc.HexToColor("a64d79"),
            ["Purple 1"] = Calc.HexToColor("351c75"),
            ["Purple 2"] = Calc.HexToColor("674ea7"),
            ["Purple 3"] = Calc.HexToColor("8e7cc3"),
            ["Indigo 1"] = Calc.HexToColor("4d31bf"),
            ["Indigo 2"] = Calc.HexToColor("7980f7"),
            ["Indigo 3"] = Calc.HexToColor("a2a7fe"),
            ["Blue 1"] = Calc.HexToColor("073763"),
            ["Blue 2"] = Calc.HexToColor("0b5394"),
            ["Blue 3"] = Calc.HexToColor("3d85c6"),
            ["Cyan 1"] = Calc.HexToColor("003d3b"),
            ["Cyan 2"] = Calc.HexToColor("105755"),
            ["Cyan 3"] = Calc.HexToColor("458e8c"),
            ["Green 1"] = Calc.HexToColor("274e13"),
            ["Green 2"] = Calc.HexToColor("38761d"),
            ["Green 3"] = Calc.HexToColor("6aa84f"),
            ["Olive 1"] = Calc.HexToColor("88995f"),
            ["Olive 2"] = Calc.HexToColor("a0b275"),
            ["Olive 3"] = Calc.HexToColor("bacc85"),
            ["Yellow 1"] = Calc.HexToColor("bf9000"),
            ["Yellow 2"] = Calc.HexToColor("dcac18"),
            ["Yellow 3"] = Calc.HexToColor("f1c232"),
            ["Orange 1"] = Calc.HexToColor("b45f06"),
            ["Orange 2"] = Calc.HexToColor("d17618"),
            ["Orange 3"] = Calc.HexToColor("e69138"),
            ["Red 1"] = Calc.HexToColor("990000"),
            ["Red 2"] = Calc.HexToColor("cc0000"),
            ["Red 3"] = Calc.HexToColor("e06666"),
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

        // Speed Run Tool's PB-row width formula (3.27.17), not the text's width:
        // the 288 px background fades out to the right, and each row of the
        // stack ends inside its text, the tail over the fade. Nothing checks
        // the formula against a newer Speed Run Tool
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
