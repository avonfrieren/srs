using System;
using Monocle;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeedrunSheet;

// Phase 6 (v3.0.0): srs decides itself when a run of the selected segment is
// finished, from the segment's declarative EndCondition — entering the next
// checkpoint's room, completing the chapter, or collecting the cassette/heart.
// SpeedrunTool is only the stopwatch: GetRoomTime() is captured on the frame
// the condition fires, and its Number of Rooms setting is never touched (the
// old RoomCounts table is gone — room counts were route-fragile and blind to
// categories; conditions are neither).
public static class RunWatcher {
    // the room the timer was last seen armed in (time == 0). A Next Room
    // timer starts on the first transition, so on the 0 -> >0 edge the room
    // the run started from is the one recorded while the timer was still 0.
    // Mutated during gameplay ⇒ registered with SpeedrunTool's save states:
    // loading a savestate restores the run in progress, capture included
    private static string armRoom;
    private static string startRoom;
    private static bool completed;
    private static bool hasCapture;
    private static long capturedTicks;

    private static object saveLoadAction;

    // SegmentAutoDetect suspends itself while this is true: a finished run
    // usually walks into the next checkpoint's room, and moving the selection
    // there would re-target the tier comparison and discard the shown result
    public static bool Completed => completed;

    // the run's final time — only set when the run also started at the
    // selected segment's start room (start guard): a run from a savestate
    // planted mid-segment completes but gets no tier
    public static bool HasCapture => hasCapture;
    public static long CapturedTicks => capturedTicks;

    // fields are filled at runtime by ModInterop()
#pragma warning disable CS0649
    [ModImportName("SpeedrunTool.RoomTimer")]
    private static class RoomTimerImports {
        public static Func<long> GetRoomTime;
    }

    [ModImportName("SpeedrunTool.SaveLoad")]
    private static class SaveLoadImports {
        public static Func<Type, string[], object> RegisterStaticTypes;
        public static Action<object> Unregister;
    }
#pragma warning restore CS0649

    public static void Load() {
        // loaded before TierComparison and SegmentAutoDetect: this Level.Update
        // hook must stay innermost so the frame order after orig is capture →
        // tier computation → selection move
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.SaveData.RegisterCassette += OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected += OnRegisterHeart;

        typeof(RoomTimerImports).ModInterop();
        typeof(SaveLoadImports).ModInterop();
        saveLoadAction = SaveLoadImports.RegisterStaticTypes?.Invoke(typeof(RunWatcher),
            [nameof(armRoom), nameof(startRoom), nameof(completed), nameof(hasCapture), nameof(capturedTicks)]);
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.SaveData.RegisterCassette -= OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected -= OnRegisterHeart;

        if (saveLoadAction != null) {
            SaveLoadImports.Unregister?.Invoke(saveLoadAction);
            saveLoadAction = null;
        }
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        // srs loads after SpeedrunTool (dependency), so the timer data of this
        // frame is final after orig
        orig(self);

        long time = RoomTimerImports.GetRoomTime?.Invoke() ?? 0;
        if (time == 0) {
            // armed (or reset — timer clear, savestate load back to zero):
            // whatever run was tracked is discarded, detection resumes
            armRoom = self.Session.Level;
            startRoom = null;
            completed = false;
            hasCapture = false;
            return;
        }

        // 0 -> >0 edge: the run just started from the room recorded while the
        // timer was armed (a savestate load restores startRoom directly)
        startRoom ??= armRoom;

        if (!completed) {
            CheckRoomConditions(self, time);
        }
    }

    // the two room-shaped conditions, polled like SpeedrunTool polls its own
    // room count: entering the end room (Session.Level flips on the same frame
    // SpeedrunTool counts the room) and the chapter's completion
    private static void CheckRoomConditions(Level level, long time) {
        SheetSegment segment = SelectedSegmentFor(level.Session);
        if (segment == null) {
            return;
        }

        switch (segment.End) {
            case EndCondition.Checkpoint:
                string endRoom = EndRoomOf(segment, level.Session);
                // no next checkpoint (chapter finals, Granny) ⇒ the chapter
                // completion ends the run, exactly like ChapterComplete rows
                if (endRoom == null ? level.Completed : level.Session.Level == endRoom) {
                    Complete(level.Session, segment, time);
                }

                break;
            case EndCondition.ChapterComplete:
                if (level.Completed) {
                    Complete(level.Session, segment, time);
                }

                break;
        }
    }

    private static void OnRegisterCassette(On.Celeste.SaveData.orig_RegisterCassette orig, SaveData self, AreaKey area) {
        orig(self, area);
        OnCollect(EndCondition.Cassette);
    }

    private static void OnRegisterHeart(On.Celeste.HeartGem.orig_RegisterAsCollected orig, HeartGem self, Level level, string poemId) {
        orig(self, level, poemId);
        OnCollect(EndCondition.Heart);
    }

    // collect events land between updates; the capture happens right here so
    // the time is the collect frame's, not the next update's
    private static void OnCollect(EndCondition condition) {
        if (completed || Engine.Scene is not Level level) {
            return;
        }

        long time = RoomTimerImports.GetRoomTime?.Invoke() ?? 0;
        if (time == 0) {
            return;
        }

        SheetSegment segment = SelectedSegmentFor(level.Session);
        if (segment != null && segment.End == condition) {
            Complete(level.Session, segment, time);
        }
    }

    private static void Complete(Session session, SheetSegment segment, long time) {
        completed = true;

        // start guard: a tier only makes sense for a run of the whole segment
        if (startRoom != null && startRoom == ExpectedStartRoom(segment, session)) {
            hasCapture = true;
            capturedTicks = time;
        }
    }

    // the selected segment, but only while the session is actually playing its
    // chapter — a cassette grabbed in 3A must not complete a Hollows Tape run
    private static SheetSegment SelectedSegmentFor(Session session) {
        SheetSegment segment = SegmentSelector.Current;
        if (segment == null
            || !SegmentAutoDetect.ChapterMap.TryGetValue((session.Area.ID, session.Area.Mode),
                out (string Chapter, string Side) chapter)
            || chapter.Chapter != segment.Chapter) {
            return null;
        }

        return segment;
    }

    // the room a full run of the segment starts in: the game checkpoint the
    // segment is anchored to (variants inherit their plain sibling's — that is
    // the whole point of the Category selector), "Start" being the map's own
    // first room
    private static string ExpectedStartRoom(SheetSegment segment, Session session) {
        string gameName = GameAnchorOf(segment, session);
        if (gameName == null) {
            return null;
        }

        if (gameName == "Start") {
            return session.MapData.StartLevel()?.Name;
        }

        CheckpointData[] checkpoints = Checkpoints(session);
        if (checkpoints != null) {
            foreach (CheckpointData checkpoint in checkpoints) {
                if (EnglishName(checkpoint) == gameName) {
                    return checkpoint.Level;
                }
            }
        }

        return null;
    }

    // where a Checkpoint segment ends: the next game checkpoint's start room,
    // resolved from AreaData at runtime — zero hand-entered data. Null = no
    // next checkpoint (the chapter ends the run instead)
    private static string EndRoomOf(SheetSegment segment, Session session) {
        string gameName = GameAnchorOf(segment, session);
        CheckpointData[] checkpoints = Checkpoints(session);
        if (gameName == null || checkpoints == null || checkpoints.Length == 0) {
            return null;
        }

        // CheckpointData only lists the non-start checkpoints, so the segment
        // starting at "Start" ends where the first of them begins
        if (gameName == "Start") {
            return checkpoints[0].Level;
        }

        for (int i = 0; i < checkpoints.Length - 1; i++) {
            if (EnglishName(checkpoints[i]) == gameName) {
                return checkpoints[i + 1].Level;
            }
        }

        return null;
    }

    private static string GameAnchorOf(SheetSegment segment, Session session) {
        if (!SegmentAutoDetect.ChapterMap.TryGetValue((session.Area.ID, session.Area.Mode),
                out (string Chapter, string Side) chapter)) {
            return null;
        }

        return SegmentAutoDetect.GameNameOf(chapter.Side ?? chapter.Chapter, segment.Name);
    }

    private static CheckpointData[] Checkpoints(Session session) =>
        AreaData.Get(session.Area)?.Mode[(int)session.Area.Mode]?.Checkpoints;

    // CheckpointData.Name is a dialog key, translated — the tables must not
    // depend on the player's language (same rule as SegmentAutoDetect)
    private static string EnglishName(CheckpointData checkpoint) =>
        Dialog.Clean(checkpoint.Name, Dialog.Languages["english"]);
}
