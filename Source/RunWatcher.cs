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
    // the room the run started in: Session.Level on the 0 -> >0 edge of the
    // timer. That is the room whose entry started the timing — a Next Room
    // timer armed one room before the segment (the standard practice setup)
    // starts on the transition *into* the segment's first room, a Current
    // Room timer starts right where it was reset; in both cases the frame
    // the time first moves, Session.Level is the room the run is timed from.
    // Mutated during gameplay ⇒ registered with SpeedrunTool's save states:
    // loading a savestate restores the run in progress, capture included
    private static string startRoom;
    private static bool completed;
    private static bool hasCapture;
    private static long capturedTicks;

    private static object saveLoadAction;

    // SegmentAutoDetect suspends itself while this is true: a finished run
    // usually walks into the next checkpoint's room, and moving the selection
    // there would re-target the tier comparison and discard the shown result
    public static bool Completed => completed;

    // the final time is frozen on every completion; HasCapture tells whether
    // the run also started at the selected segment's start room (start
    // guard) — only then does it earn a tier. A run from a savestate planted
    // mid-segment still shows its frozen time, greyed, so the end of the run
    // is always visible
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
            [nameof(startRoom), nameof(completed), nameof(hasCapture), nameof(capturedTicks)]);
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
        // switched off: no capture, and the run in progress is simply left
        // where it is — turning the mod back on resumes from the timer's own
        // state, which is SpeedrunTool's, not ours
        if (!SrsModule.Settings.Enabled) {
            orig(self);
            return;
        }

        // the room timer as it stands *before* this frame is added to it
        // (v3.2.0). SpeedrunTool runs inside orig — srs loads after it, so its
        // hook is the inner one — and on the frame a room condition fires it
        // does exactly this: RoomTimerManager.Timing() freezes the displayed
        // time first (UpdateTimerState), then adds the frame's delta. Reading
        // GetRoomTime() after orig therefore handed srs one frame more than
        // SpeedrunTool shows, and than the sheet's references, which are all
        // recorded with SpeedrunTool. It is also the right time on its own
        // terms: the frame Session.Level has already flipped is a frame spent
        // in the *next* room, and does not belong to the segment
        long timeBeforeUpdate = RoomTimerImports.GetRoomTime?.Invoke() ?? 0;

        orig(self);

        long time = RoomTimerImports.GetRoomTime?.Invoke() ?? 0;
        if (time == 0) {
            // armed (or reset — timer clear, savestate load back to zero):
            // whatever run was tracked is discarded, detection resumes
            startRoom = null;
            completed = false;
            hasCapture = false;
            return;
        }

        // 0 -> >0 edge: the timing just started, and it started from the room
        // the session is in on this very frame (SpeedrunTool starts a Next
        // Room timer on the same frame Session.Level flips to the new room).
        // A savestate load restores startRoom directly instead
        startRoom ??= self.Session.Level;

        // timeBeforeUpdate == 0 is that very edge frame: the timing starts
        // here, so there is no run to finish yet
        if (!completed && timeBeforeUpdate > 0) {
            CheckRoomConditions(self, timeBeforeUpdate);
        }
    }

    // the room-shaped condition, polled like SpeedrunTool polls its own room
    // count: entering the end room (Session.Level flips on the same frame
    // SpeedrunTool counts the room), or the chapter's completion when the
    // segment has no next checkpoint to end at
    private static void CheckRoomConditions(Level level, long time) {
        SheetSegment segment = SelectedSegmentFor(level.Session);
        if (segment == null) {
            return;
        }

        switch (segment.End) {
            case EndCondition.Checkpoint:
                string endRoom = EndRoomOf(segment, level.Session);
                // no next checkpoint (chapter finals, Granny) ⇒ the chapter's
                // completion ends the run instead.
                // A run never ends in the room its own timing started from:
                // on the frame a Next Room timer starts (the transition into
                // the segment's first room), the selection can still be the
                // previous segment — whose end room is exactly the room being
                // entered. That entry is the start of this run, not the end
                // of the previous one
                if (endRoom == null ? level.Completed
                        : level.Session.Level == endRoom && endRoom != startRoom) {
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
        // gated here as well: this runs from the collect hooks, not from the
        // update one, so a switched-off mod would still freeze a capture that
        // nothing cleared before the switch came back on
        if (!SrsModule.Settings.Enabled || completed || Engine.Scene is not Level level) {
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

        // start guard: a tier only makes sense for a run of the whole
        // segment. The time freezes either way — the greyed row is the
        // visible cue that the run ended but did not start at the segment's
        // first room
        hasCapture = startRoom != null && startRoom == ExpectedStartRoom(segment, session);

        // add back the head of the segment srs does not time (7A's 0m), so
        // the frozen time is the one the sheet's thresholds describe. Only
        // for a run that did start at the segment's start room: a time that
        // earns no tier is not a segment time, and padding it would only
        // make the greyed row lie about what the timer showed
        capturedTicks = time + (hasCapture ? UntimedHeadOf(segment, session).Ticks : 0L);

        // only a run that started at the segment's first room is a segment
        // time, so only that one is worth exporting
        if (hasCapture) {
            SessionBests.Record(segment, capturedTicks);
        }
    }

    private static TimeSpan UntimedHeadOf(SheetSegment segment, Session session) {
        string gameName = GameAnchorOf(segment, session);
        return gameName != null
               && SegmentAutoDetect.UntimedSegmentHead.TryGetValue(
                   (SegmentAutoDetect.ScopeOf(session), gameName), out TimeSpan head)
            ? head
            : TimeSpan.Zero;
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
    // the whole point of the Category selector)
    private static string ExpectedStartRoom(SheetSegment segment, Session session) {
        string gameName = GameAnchorOf(segment, session);
        return gameName == null ? null : StartRoomOf(gameName, session);
    }

    // the room a run anchored at this game checkpoint is timed from: the
    // override when the sheet does not start the segment at the checkpoint's
    // own room (2A Awake, 7A 0m), the map's first room for "Start" — which is
    // the only anchor with no CheckpointData — the checkpoint's room otherwise
    private static string StartRoomOf(string gameName, Session session) {
        if (SegmentAutoDetect.StartRoomOverrides.TryGetValue(
                (SegmentAutoDetect.ScopeOf(session), gameName), out string overridden)) {
            return overridden;
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

    // where a Checkpoint segment ends: exactly where the next segment starts,
    // resolved from AreaData at runtime — zero hand-entered data beyond the
    // overrides, which both ends read, so the finish line moves with the start
    // line and the two segments never overlap. Null = no next checkpoint (the
    // chapter ends the run instead)
    private static string EndRoomOf(SheetSegment segment, Session session) {
        string gameName = GameAnchorOf(segment, session);
        CheckpointData[] checkpoints = Checkpoints(session);
        if (gameName == null || checkpoints == null || checkpoints.Length == 0) {
            return null;
        }

        // a checkpoint the sheet cuts in two ends where its own second half
        // starts (8A's Heart of the Mountain), not at the next game checkpoint
        if (SegmentAutoDetect.SplitCheckpoints.TryGetValue(
                (SegmentAutoDetect.ScopeOf(session), gameName), out string secondHalf)) {
            return StartRoomOf(secondHalf, session);
        }

        // CheckpointData only lists the non-start checkpoints, so the segment
        // starting at "Start" ends where the first of them begins
        if (gameName == "Start") {
            return StartRoomOf(EnglishName(checkpoints[0]), session);
        }

        for (int i = 0; i < checkpoints.Length - 1; i++) {
            if (EnglishName(checkpoints[i]) == gameName) {
                return StartRoomOf(EnglishName(checkpoints[i + 1]), session);
            }
        }

        return null;
    }

    private static string GameAnchorOf(SheetSegment segment, Session session) {
        string scope = SegmentAutoDetect.ScopeOf(session);
        return scope == null ? null : SegmentAutoDetect.GameNameOf(scope, segment.Name);
    }

    private static CheckpointData[] Checkpoints(Session session) =>
        AreaData.Get(session.Area)?.Mode[(int)session.Area.Mode]?.Checkpoints;

    // CheckpointData.Name is a dialog key, translated — the tables must not
    // depend on the player's language (same rule as SegmentAutoDetect)
    private static string EnglishName(CheckpointData checkpoint) =>
        Dialog.Clean(checkpoint.Name, Dialog.Languages["english"]);
}
