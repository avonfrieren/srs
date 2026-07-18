using System;
using System.Collections.Generic;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeedrunSheet;

// Phase 4bis: while playing, the checkpoint being practiced drives the
// selection instead of the Mod Options sliders. The current checkpoint is the
// last checkpoint room entered (or the session's own start), tracked across
// transitions and registered with SpeedrunTool's save states, so loading a
// savestate restores the checkpoint of the moment of the save — exactly the
// practice workflow.
public static class SegmentAutoDetect {
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
    // the folded chapters (the sheet routes 5 as 5B only, 6 as both sides)
    private static readonly Dictionary<(int Id, AreaMode Mode), (string Chapter, string Side)> ChapterMap = new() {
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
    };

    // (side or chapter, game checkpoint name) -> sheet checkpoint name.
    // Deliberately a hardcoded table, no name normalization (owner decision
    // 2026-07-18): the current sheet is a prototype and its successor will
    // need a new parser anyway. "Start" stands for the session's first room
    // (which has no CheckpointData). Game checkpoints absent from the sheet
    // (the full-5A route past Depths) are simply not listed — reaching them
    // leaves the selection where it was. "Hollows Tape" stays manual-only:
    // it starts at 6A's Hollows checkpoint, indistinguishable from "Hollows"
    private static readonly Dictionary<(string Scope, string GameName), string> CheckpointMap = new() {
        [("Prologue", "Start")] = "Granny",
        [("1a", "Start")] = "Start",
        [("1a", "Crossing")] = "Crossing",
        [("1a", "Chasm")] = "Chasm",
        [("2a", "Start")] = "Start",
        [("2a", "Intervention")] = "Intervention",
        [("2a", "Awake")] = "Awake",
        [("3a", "Start")] = "Start",
        [("3a", "Huge Mess")] = "Huge Mess",
        [("3a", "Elevator Shaft")] = "Elevator Shaft",
        [("3a", "Presidential Suite")] = "Presidential Suite",
        [("4a", "Start")] = "Start",
        [("4a", "Shrine")] = "Shrine",
        [("4a", "Old Trail")] = "Old Trail",
        [("4a", "Cliff Face")] = "Cliff Face",
        [("5a", "Start")] = "5a Start",
        [("5a", "Depths")] = "Depths",
        [("5b", "Start")] = "5b Start",
        [("5b", "Central Chamber")] = "Central Chamber",
        [("5b", "Through the Mirror")] = "Through The Mirror",
        [("5b", "Mix Master")] = "Mix Master",
        [("6a", "Start")] = "6a Start",
        [("6a", "Lake")] = "Lake",
        [("6a", "Hollows")] = "Hollows",
        [("6a", "Reflection")] = "Reflection",
        [("6a", "Rock Bottom")] = "6a Rock Bottom",
        [("6a", "Resolution")] = "Resolution",
        [("6b", "Start")] = "6b Start",
        [("6b", "Reflection")] = "Falling", // the sheet's name for 6B Reflection
        [("6b", "Rock Bottom")] = "6b Rock Bottom",
        [("6b", "Reprieve")] = "Reprieve",
        [("7a", "Start")] = "Start",
        [("7a", "500 M")] = "500m",
        [("7a", "1000 M")] = "1000m",
        [("7a", "1500 M")] = "1500m",
        [("7a", "2000 M")] = "2000m",
        [("7a", "2500 M")] = "2500m",
        [("7a", "3000 M")] = "3000m",
    };

    public static void Load() {
        Everest.Events.Level.OnEnter += OnLevelEnter;
        Everest.Events.Level.OnTransitionTo += OnTransitionTo;
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
        Everest.Events.Level.OnTransitionTo -= OnTransitionTo;
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

    private static void OnTransitionTo(Level level, LevelData next, Microsoft.Xna.Framework.Vector2 direction) {
        if (next.HasCheckpoint) {
            checkpointRoom = next.Name;
        }
    }

    // applied every frame rather than on discrete events: this is what folds
    // savestate loads back into the selection (RegisterStaticTypes restores
    // checkpointRoom silently, there is no callback to react to)
    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // suspended while a completed run's tier is displayed: the completion
        // transition lands in the next checkpoint's room, and moving the
        // selection there would re-target Number of Rooms, un-complete the
        // timer and hide the result. checkpointRoom keeps tracking meanwhile,
        // so detection catches up as soon as the timer is reset (savestate
        // load, timer clear)
        if (Settings.AutoDetect && !TierComparison.TimerCompleted) {
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
            || !CheckpointMap.TryGetValue((chapter.Side ?? chapter.Chapter, gameName), out string sheetName)
            || (Settings.SelectedChapter == chapter.Chapter && Settings.SelectedCheckpoint == sheetName)) {
            return;
        }

        // only select checkpoints the imported sheet actually has
        foreach (SheetSegment segment in block.Checkpoints(chapter.Chapter)) {
            if (segment.Name == sheetName) {
                Settings.SelectedChapter = chapter.Chapter;
                Settings.SelectedCheckpoint = sheetName;
                RoomCounts.Apply(segment);
                return;
            }
        }
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
