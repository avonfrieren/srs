using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool.Message;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeedrunSheet;

// Phase 4bis: while playing, the checkpoint being practiced drives the
// selection instead of the Mod Options sliders. The current checkpoint is the
// last checkpoint room entered (or the session's own start), tracked across
// transitions and registered with SpeedrunTool's save states, so loading a
// savestate restores the checkpoint of the moment of the save — exactly the
// practice workflow.
public static partial class SegmentAutoDetect {
    private static SrsSettings Settings => SrsModule.Settings;

    // room name of the last checkpoint room entered in the current session;
    // null = the session started here (FirstLevel, or StartCheckpoint when a
    // checkpoint was picked on the chapter panel). Mutated during gameplay ⇒
    // registered with SpeedrunTool's save states
    private static string checkpointRoom;

    private static object saveLoadAction;

    // fields are filled at runtime by ModInterop()
#pragma warning disable CS0649
    [ModImportName("SpeedrunTool.SaveLoad")]
    private static class SaveLoadImports {
        public static Func<Type, string[], object> RegisterStaticTypes;
        public static Action<object> Unregister;
    }
#pragma warning restore CS0649

    // vanilla (AreaKey.ID, side) -> sheet chapter, plus the side name inside
    // the folded chapters (the sheet routes 5 as 5B only, 6 as both sides).
    // internal: RunWatcher anchors segments to game checkpoints through it
    internal static readonly Dictionary<(int Id, AreaMode Mode), (string Chapter, string Side)> ChapterMap = new() {
        [(0, AreaMode.Normal)] = ("Prologue", null),
        [(1, AreaMode.Normal)] = ("1a", null),
        [(2, AreaMode.Normal)] = ("2a", null),
        [(3, AreaMode.Normal)] = ("3a", null),
        [(4, AreaMode.Normal)] = ("4a", null),
        [(5, AreaMode.Normal)] = ("5a/b", "5a"),
        [(5, AreaMode.BSide)] = ("5a/b", "5b"),
        [(6, AreaMode.Normal)] = ("6a/b", "6a"),
        [(6, AreaMode.BSide)] = ("6a/b", "6b"),
        [(7, AreaMode.Normal)] = ("7a", null),
        // vanilla numbering skips the Epilogue (area 8): Core is 9, Farewell
        // is 10. The sheet gives Farewell a tab of its own, and the mod a
        // chapter of its own — named after the tab rather than "9a"
        [(9, AreaMode.Normal)] = ("8a", null),
        [(10, AreaMode.Normal)] = ("Farewell", null),
    };

    public static void Load() {
        Everest.Events.Level.OnEnter += OnLevelEnter;
        // subscribed after TierComparison's hook ⇒ outermost: on a completion
        // frame the capture still sees the selection the run was started with,
        // the detection only moves it afterwards
        On.Celeste.Level.Update += LevelOnUpdate;

        typeof(SaveLoadImports).ModInterop();
        saveLoadAction = SaveLoadImports.RegisterStaticTypes?.Invoke(typeof(SegmentAutoDetect),
            [nameof(checkpointRoom)]);
    }

    public static void Unload() {
        Everest.Events.Level.OnEnter -= OnLevelEnter;
        On.Celeste.Level.Update -= LevelOnUpdate;

        if (saveLoadAction != null) {
            SaveLoadImports.Unregister?.Invoke(saveLoadAction);
            saveLoadAction = null;
        }
    }

    private static void OnLevelEnter(Session session, bool fromSaveData) {
        // null on a fresh start ⇒ resolved as "Start" against FirstLevel
        checkpointRoom = session.StartCheckpoint;
    }

    // rooms that carry a game checkpoint, plus (v3.2.0) the rooms a segment is
    // timed from when the sheet does not start it at its checkpoint: being in
    // 2A's end_0 is being in the Awake segment, three rooms before the game
    // says so.
    // Polled on Session.Level rather than caught on transition (v3.5.1): the
    // game enters some of these rooms without one. Every "wake up" of the run
    // is a cutscene assigning Session.Level and reloading the level — 2A's
    // dream into end_0, 5A's mirror into c-00 (Unraveling), 5B's into c-00
    // (Through The Mirror) — and no transition is raised for any of them, so
    // the selection used to sit on the previous checkpoint until the *next*
    // real transition. This is also what RunWatcher watches, so both ends of
    // a segment now react to the same thing
    private static void TrackCheckpointRoom(Session session) {
        string room = session.Level;
        if (room == null || room == checkpointRoom) {
            return;
        }

        if (session.LevelData?.HasCheckpoint == true
            || OverriddenCheckpointAt(ScopeOf(session), room) != null) {
            checkpointRoom = room;
        }
    }

    // the scope a chapter's name tables are keyed by: the side for the folded
    // chapters (5a/b, 6a/b), the chapter itself otherwise. Null outside the
    // chapters the sheet covers
    internal static string ScopeOf(Session session) =>
        ChapterMap.TryGetValue((session.Area.ID, session.Area.Mode), out (string Chapter, string Side) chapter)
            ? chapter.Side ?? chapter.Chapter
            : null;

    // applied every frame rather than on discrete events: this is what folds
    // savestate loads back into the selection (RegisterStaticTypes restores
    // checkpointRoom silently, there is no callback to react to)
    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // above the gate, unlike everything else here: this is a side-effect-free
        // read of Session.Level, and letting it go stale while the mod is off
        // arms the wrong segment when the switch comes back on mid-chapter
        // (OnLevelEnter has seeded it with the chapter's start checkpoint)
        TrackCheckpointRoom(self.Session);

        if (!Settings.Enabled) {
            return;
        }

        // hotkey (v3.1.0): cycle the practiced category without leaving the
        // game — the natural gesture between an any% run of a checkpoint and
        // the cassette variant of the same one. Handled here rather than in
        // TierComparison because the category only feeds the detection right
        // below, which picks the new variant up on this very frame
        if (Hotkeys.CycleCategory.Pressed) {
            Settings.Category = SegmentCategories.Next(Settings.Category);
            SrsModule.Instance.SaveSettings();
            PopupMessageUtils.ShowOptionState(Dialog.Clean("SRS_CATEGORY"),
                SegmentCategories.NameOf(Settings.Category));
        }

        // suspended while a completed run's tier is displayed: the completion
        // usually transitions into the next checkpoint's room, and moving the
        // selection there would re-point the tier comparison at the wrong
        // segment and hide the result. checkpointRoom keeps tracking meanwhile,
        // so detection catches up as soon as the timer is reset (savestate
        // load, timer clear)
        if (Settings.AutoDetect && !RunWatcher.Completed) {
            Apply(self.Session);
        }
    }

    private static void Apply(Session session) {
        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        if (block == null || !ChapterMap.TryGetValue((session.Area.ID, session.Area.Mode), out (string Chapter, string Side) chapter)) {
            return;
        }

        string gameName = GameCheckpointName(session);
        if (gameName == null
            || !CheckpointMap.TryGetValue((chapter.Side ?? chapter.Chapter, gameName), out string sheetName)) {
            return;
        }

        // the active category's variant of this checkpoint takes precedence
        // (Cassette: Hollows -> Hollows Tape); the plain row is the fallback,
        // so checkpoints without a variant keep detecting. Only checkpoints
        // the imported sheet actually has are selectable
        SheetSegment target = null;
        if (CategoryVariants.TryGetValue((Settings.Category, chapter.Chapter, sheetName), out string variant)) {
            target = Find(block, chapter.Chapter, variant);
        }

        target ??= Find(block, chapter.Chapter, sheetName);
        if (target == null
            || (Settings.SelectedChapter == chapter.Chapter && Settings.SelectedCheckpoint == target.Name)) {
            return;
        }

        Settings.SelectedChapter = chapter.Chapter;
        Settings.SelectedCheckpoint = target.Name;
    }

    private static SheetSegment Find(SheetBlock block, string chapter, string name) {
        foreach (SheetSegment segment in block.Checkpoints(chapter)) {
            if (segment.Name == name) {
                return segment;
            }
        }

        return null;
    }

    // resolve the tracked checkpoint room to the game's checkpoint name; null
    // means the session started from the beginning ("Start" — StartCheckpoint
    // is only set when a checkpoint was picked on the chapter panel). Always
    // the english names (CheckpointData.Name is a dialog key, the map must not
    // depend on the player's language)
    private static string GameCheckpointName(Session session) {
        if (checkpointRoom == null) {
            return "Start";
        }

        // an override room stands for its checkpoint even though the game has
        // none there; checked first, since such a room carries no CheckpointData
        if (OverriddenCheckpointAt(ScopeOf(session), checkpointRoom) is { } overridden) {
            return overridden;
        }

        CheckpointData[] checkpoints = AreaData.Get(session.Area)?.Mode[(int)session.Area.Mode]?.Checkpoints;
        if (checkpoints != null) {
            foreach (CheckpointData checkpoint in checkpoints) {
                if (checkpoint.Level == checkpointRoom) {
                    return Dialog.Clean(checkpoint.Name, Dialog.Languages["english"]);
                }
            }
        }

        return null;
    }
}
