using System;
using System.Reflection;
using Celeste.Mod.SpeedrunTool;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeedrunSheet;

// Phase 4: when SpeedrunTool's room timer completes, compare the final time
// against the selected checkpoint's sheet tiers and draw the reached tier's
// name in its color under the timer, like srta's delta row.
public static class TierComparison {
    private static SrsSettings Settings => SrsModule.Settings;

    // final time captured on the frame the timer completes: GetRoomTime() keeps
    // running in the background afterwards, only SpeedrunTool's display freezes.
    // hasCapture is only set when that completion was a full run of the
    // selected checkpoint (see IsFullRun); mutated during gameplay ⇒ registered
    // with SpeedrunTool's save states, so loading a savestate restores the
    // tier row of the moment of the save
    private static bool wasCompleted;
    private static bool hasCapture;
    private static long capturedTicks;

    // SegmentAutoDetect suspends itself while this is true: finishing a run
    // transitions into the next checkpoint's room, and re-targeting Number of
    // Rooms there would un-complete the timer and throw the result away
    public static bool TimerCompleted => wasCompleted;

    // recomputed every frame from capturedTicks (srta-style), so the row reacts
    // instantly to selection changes and sheet re-imports; derived state only,
    // deliberately not registered with save states
    private static string tierText = "";
    private static Color tierColor = Color.White;

    // drop the row below srta's delta row when srta is present; resolved on
    // first render (mod load order between srs and srta is not guaranteed)
    private static bool? srtaLoaded;

    private static object saveLoadAction;

    // fields are filled at runtime by ModInterop()
#pragma warning disable CS0649
    [ModImportName("SpeedrunTool.RoomTimer")]
    private static class RoomTimerImports {
        public static Func<bool> RoomTimerIsCompleted;
        public static Func<long> GetRoomTime;
    }

    [ModImportName("SpeedrunTool.SaveLoad")]
    private static class SaveLoadImports {
        public static Func<Type, string[], object> RegisterStaticTypes;
        public static Action<object> Unregister;
    }
#pragma warning restore CS0649

    public static void Load() {
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.SpeedrunTimerDisplay.Render += SpeedrunTimerDisplayOnRender;

        typeof(RoomTimerImports).ModInterop();
        typeof(SaveLoadImports).ModInterop();
        saveLoadAction = SaveLoadImports.RegisterStaticTypes?.Invoke(typeof(TierComparison),
            [nameof(wasCompleted), nameof(hasCapture), nameof(capturedTicks)]);
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.SpeedrunTimerDisplay.Render -= SpeedrunTimerDisplayOnRender;

        if (saveLoadAction != null) {
            SaveLoadImports.Unregister?.Invoke(saveLoadAction);
            saveLoadAction = null;
        }
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        // srs loads after SpeedrunTool (dependency), so this hook is outermost
        // and the timer data of this frame is final after orig
        orig(self);

        // srta-style hotkey: flip the toggle and confirm with SpeedrunTool's
        // popup, so the row can be hidden without leaving the game
        if (!self.Paused && Settings.ToggleShowTier.Pressed) {
            Settings.ShowTier = !Settings.ShowTier;
            SrsModule.Instance.SaveSettings();
            PopupMessageUtils.ShowOptionState(Dialog.Clean("MODOPTIONS_SRS_SHOWTIER"),
                Dialog.Clean(Settings.ShowTier ? DialogIds.On : DialogIds.Off));
        }

        if (RoomTimerImports.RoomTimerIsCompleted == null) {
            return;
        }

        bool completed = RoomTimerImports.RoomTimerIsCompleted();
        if (!completed) {
            hasCapture = false;
        } else if (!wasCompleted && IsFullRun()) {
            hasCapture = true;
            capturedTicks = RoomTimerImports.GetRoomTime();
        }

        wasCompleted = completed;
        ComputeTier();
    }

    // the timer completing only means a finished run of the selected checkpoint
    // when it stopped at the checkpoint's room count — SegmentSelector pushes
    // it into SpeedrunTool's Number of Rooms on selection, so a mismatch is a
    // deliberate manual override (partial-segment practice): no tier then
    private static bool IsFullRun() {
        SheetSegment segment = SegmentSelector.Current;
        return segment != null
            && SpeedrunToolSettings.Instance is { } settings
            && settings.NumberOfRooms == RoomCounts.TargetFor(segment);
    }

    // first tier column whose threshold is >= the captured time wins; slower
    // than every threshold (i.e. beyond Red 3) is Unranked. The Hidden column
    // is 0:00.000 everywhere and never matches; empty/unparseable cells (null)
    // are skipped
    private static void ComputeTier() {
        tierText = "";
        if (!hasCapture) {
            return;
        }

        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        SheetSegment segment = SegmentSelector.Current;
        if (block == null || segment == null) {
            return;
        }

        TimeSpan time = TimeSpan.FromTicks(capturedTicks);
        for (int i = 0; i < segment.Times.Count && i < block.Columns.Count; i++) {
            if (segment.Times[i] is { } threshold && threshold > TimeSpan.Zero && time <= threshold) {
                SetTier(block.Columns[i]);
                return;
            }
        }

        SetTier("Unranked");
    }

    // tier colors come from the column names themselves: Gold, Pink, Purple,
    // Indigo, Blue, Cyan, Green, Olive, Yellow, Orange and Red are all XNA
    // named colors; the "1"-"3" rank suffix is dropped for the lookup only
    private static void SetTier(string column) {
        tierText = column;
        string name = column;
        int space = name.LastIndexOf(' ');
        if (space > 0 && int.TryParse(name[(space + 1)..], out _)) {
            name = name[..space];
        }

        tierColor = name switch {
            "WR" or "Hidden" => Color.White,
            "Unranked" => Color.Gray,
            _ => NamedColor(name),
        };
    }

    private static Color NamedColor(string name) {
        PropertyInfo property = typeof(Color).GetProperty(name,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        return property != null && property.PropertyType == typeof(Color)
            ? (Color)property.GetValue(null)
            : Color.White;
    }

    private static void SpeedrunTimerDisplayOnRender(On.Celeste.SpeedrunTimerDisplay.orig_Render orig, SpeedrunTimerDisplay self) {
        orig(self);

        if (!Settings.ShowTier || tierText.Length == 0) {
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
        float textWidth = font.Get(fontFaceSize).Measure(tierText).X * scale;

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

        font.DrawOutline(fontFaceSize, tierText, new Vector2(x + timeMarginLeft, y + 28.4f),
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
