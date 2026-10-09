using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeedrunSheet;

// Feeds RunTracker from the game: chapter time, how each room was reached, the
// player's control, the collects, the frame chapter time stops and Restart
// Chapter.
public static class RunWatcher {
    // the player has no control in these states: a start reached in one waits
    // for control in that room. ⚠️ StIntroJump must stay out: 7A's 500m to
    // 3000m are entered in it, from the launch, and open on the entry
    private static readonly HashSet<int> NoControlStates = [
        Player.StDummy, Player.StIntroWalk, Player.StIntroRespawn,
        Player.StIntroWakeUp, Player.StBirdDashTutorial, Player.StFrozen, Player.StReflectionFall,
        Player.StTempleFall, Player.StIntroMoonJump, Player.StIntroThinkForABit,
    ];

    // with Speed Run Tool's "freeze after load" Off, a player load counts the
    // frame its own wipe ends on, which Session.Time does not. A TAS load
    // starts no wipe, and restores the one its state was saved under
    private static readonly long WipeEndFrame = TimeSpan.FromMilliseconds(17).Ticks;

    // how the room was loaded since the last fed frame, read from LoadLevel;
    // when several loads land in one gap, the strongest wins
    private enum RoomLoad {
        None,
        // Respawn from Level.Reload: a death
        Respawn,
        // any intro but Respawn: a transition, or a room change the game makes
        // in a cutscene
        WalkIn,
        // any other Respawn: Speed Run Tool's teleport, into another room or to
        // a summit flag of this one
        Teleport,
        // a level from the loader: entering a chapter, a restart, console load,
        // the debug map
        Loader,
    }

    private static RunTracker tracker;

    // game state, saved and restored with each savestate: a stamp written on
    // every fed frame (a load puts back an older one, which is how a load is
    // seen), the room the next walk-in comes from, and where the player last
    // appeared, exactly, with whether they have moved since
    private static class Saved {
        public static long Stamp;
        public static string From;
        public static Vector2 SpawnAt;
        public static bool Moved = true;
    }

    private static object saveLoadAction;
    private static object wipeAction;
    // the wipe the last load restored with the level, before a player load
    // starts its own; read and cleared on the next frame
    private static ScreenWipe restoredWipe;

    // never saved: what a load is compared against. stamp only grows, so a
    // saved stamp differs from it once a frame has been fed since the save
    private static long stamp;
    private static string lastRoom;
    private static int lastState = -1;
    private static bool lastControl = true;
    private static bool lastStopped;
    private static bool fedLastFrame;
    private static RoomLoad roomLoad;
    // inside Level.Reload, the game's only LoadLevel(Respawn)
    private static bool reloading;

    // Restart Chapter, seen when the new session is made: the old session's
    // last chapter time, for the rows that end on it
    private static bool chapterRestarted;
    private static long restartedTime;
    private static AreaKey restartedArea;

    // the frame being fed: a collect inside it is timed from its start, as
    // a room entry is (Session.Time already counts the frame by then)
    private static bool inUpdate;
    private static long frameStart;

    /// The segments closed with a time since the attempt started, oldest
    /// first, the most specific segment of each frame then the most specific
    /// chapter run, each with its serial; empty after a load. The HUD's tier rows show the last and step back from it.
    internal static IReadOnlyList<(SegmentRecord Record, int Serial)> Attempt => attempt;
    private static readonly List<(SegmentRecord Record, int Serial)> attempt = [];

    /// The room the last one closed in: the rows show until the player leaves it.
    internal static string LatestRoom { get; private set; }

    /// Counts every record added, so a reader tells a new one from the same one.
    internal static int LatestSerial { get; private set; }

    // fields are filled at runtime by ModInterop()
#pragma warning disable CS0649
    [ModImportName("SpeedrunTool.SaveLoad")]
    private static class SaveLoadImports {
        public static Func<Type, string[], object> RegisterStaticTypes;

        // saveState, loadState, clearState, beforeSaveState, beforeLoadState, preCloneEntities
        public static Func<Action<Dictionary<Type, Dictionary<string, object>>, Level>,
            Action<Dictionary<Type, Dictionary<string, object>>, Level>, Action, Action<Level>, Action<Level>,
            Action, object> RegisterSaveLoadAction;

        public static Action<object> Unregister;
    }
#pragma warning restore CS0649

    public static void Load() {
        // hook order: see SrsModule.Load
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.Level.LoadLevel += LevelOnLoadLevel;
        On.Celeste.Level.Reload += LevelOnReload;
        On.Celeste.Session.Restart += SessionOnRestart;
        On.Celeste.SaveData.RegisterCassette += OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected += OnRegisterHeart;

        typeof(SaveLoadImports).ModInterop();
        saveLoadAction = SaveLoadImports.RegisterStaticTypes?.Invoke(typeof(Saved),
            [nameof(Saved.Stamp), nameof(Saved.From), nameof(Saved.SpawnAt), nameof(Saved.Moved)]);
        if (saveLoadAction == null) {
            // loads would go unseen, and chapter time keeps running across a
            // load, so a segment open across one would be mistimed: nothing is fed
            Logger.Log(LogLevel.Warn, "srs", "Speed Run Tool's SaveLoad did not bind: savestate loads cannot be seen, so srs records nothing");
        }

        // loadState runs once the level is restored, before a player load's
        // wipe starts. clearState stays unwired: clearing a slot rewinds
        // nothing, and a TAS may clear the slots as it starts
        wipeAction = SaveLoadImports.RegisterSaveLoadAction?.Invoke(null,
            (_, level) => restoredWipe = level.Wipe, null, null, null, null);

        tracker = new RunTracker(SegmentRules.All, null);
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.Level.LoadLevel -= LevelOnLoadLevel;
        On.Celeste.Level.Reload -= LevelOnReload;
        On.Celeste.Session.Restart -= SessionOnRestart;
        On.Celeste.SaveData.RegisterCassette -= OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected -= OnRegisterHeart;

        if (saveLoadAction != null) {
            SaveLoadImports.Unregister?.Invoke(saveLoadAction);
            saveLoadAction = null;
        }

        if (wipeAction != null) {
            SaveLoadImports.Unregister?.Invoke(wipeAction);
            wipeAction = null;
        }

        restoredWipe = null;
    }

    // every room load passes here; the kinds are RoomLoad's. A cutscene's room
    // change (2A's dream, the mirrors, 6A's fall, Farewell's intro) is a
    // walk-in. Four of them run in OnEndOfFrame, after the update hook: the
    // next fed frame reads the kind
    private static void LevelOnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self,
        Player.IntroTypes playerIntro, bool isFromLoader) {
        orig(self, playerIntro, isFromLoader);
        RoomLoad kind = isFromLoader ? RoomLoad.Loader
            : playerIntro != Player.IntroTypes.Respawn ? RoomLoad.WalkIn
            : reloading ? RoomLoad.Respawn
            : RoomLoad.Teleport;
        if (kind > roomLoad) {
            roomLoad = kind;
        }
    }

    private static void LevelOnReload(On.Celeste.Level.orig_Reload orig, Level self) {
        reloading = true;
        try {
            orig(self);
        } finally {
            reloading = false;
        }
    }

    // Restart Chapter makes the new session with no room, with or without the
    // wipe; a golden berry restart names its room. Nothing updates the old
    // session afterwards, so its time is the run's last chapter time
    private static Session SessionOnRestart(On.Celeste.Session.orig_Restart orig, Session self, string intoLevel) {
        Session restarted = orig(self, intoLevel);
        if (intoLevel == null) {
            chapterRestarted = true;
            restartedTime = self.Time;
            restartedArea = self.Area;
        }

        return restarted;
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        // switched off, or without Speed Run Tool's SaveLoad: nothing is fed.
        // Moved is set on every frame off, so a savestate made or loaded
        // meanwhile opens nothing
        if (!SrsModule.Settings.Enabled || saveLoadAction == null) {
            // cleared after orig too: a LoadLevel or a restart inside it would set them
            fedLastFrame = false;
            restoredWipe = null;
            orig(self);
            roomLoad = RoomLoad.None;
            chapterRestarted = false;
            Saved.Moved = true;
            if (self.Tracker.GetEntity<Player>()?.StateMachine.State != Player.StReflectionFall) {
                Saved.From = self.Session.Level;
            }

            return;
        }

        // before orig: the reading a room entry is timed from (the chapter's
        // end takes the one after, as chapter time does; Speed Run Tool's
        // display stops a frame short there), and the level and the Saved
        // values as a load left them, before this frame can move the player
        bool loaded = Saved.Stamp != stamp;
        if (loaded) {
            // a room loaded before the load belongs to the timeline it left
            roomLoad = RoomLoad.None;
        }

        long before = self.Session.Time;
        Player playerBefore = self.Tracker.GetEntity<Player>();
        LevelData dataBefore = self.Session.LevelData;
        string roomBefore = self.Session.Level;
        int stateBefore = playerBefore?.StateMachine.State ?? -1;
        Vector2? exactBefore = playerBefore?.ExactPosition;
        bool controlBefore = !self.InCutscene && !NoControlStates.Contains(stateBefore);
        bool stoppedBefore = self.TimerStopped || self.Completed;
        // a player load's own wipe
        bool loadWiping = self.Wipe != null && self.Wipe != restoredWipe;
        restoredWipe = null;
        string from = Saved.From;
        bool movedAtLoad = Saved.Moved;
        Vector2 spawnAtLoad = Saved.SpawnAt;

        frameStart = before;
        inUpdate = true;
        orig(self);
        inUpdate = false;

        Session session = self.Session;
        long after = session.Time;
        string scope = SegmentAutoDetect.ScopeOf(session);
        string room = session.Level;
        Player player = self.Tracker.GetEntity<Player>();
        int state = player?.StateMachine.State ?? -1;
        bool control = !self.InCutscene && !NoControlStates.Contains(state);
        bool launching = state == Player.StIntroJump;
        bool stopped = self.TimerStopped || self.Completed;
        tracker.Rooms = RoomMap.For(session);
        EndState end = EndState.With(session.Inventory.Dashes);
        RoomLoad load = roomLoad;
        roomLoad = RoomLoad.None;
        // a room change no LoadLevel walked into is a teleport too. Not on a
        // load: the state's room is not a room change
        bool teleported = Saved.Stamp == stamp
                          && (load == RoomLoad.Teleport
                              || (fedLastFrame && room != lastRoom && load < RoomLoad.WalkIn));

        bool feed = false;
        // a new attempt landed this frame, so the player appears now if they
        // have control
        bool restarted = false;
        // this frame's control and state edges are the player's own: not on a
        // load inside orig, nor on the frame srs is switched back on
        bool watched = true;
        if (!loaded && Saved.Stamp != stamp) {
            // a load inside orig: this frame's events would mix two timelines
            tracker.Drop();
            attempt.Clear();
            watched = false;
        } else if (loaded) {
            // every load is a new attempt. With control, the room's Current Room
            // segment opens if the player has not moved since appearing on its
            // start spawn; without, it waits for the appearance that follows
            bool wipeEnd = loadWiping
                           && SpeedrunToolSettings.Instance?.FreezeAfterLoadStateType == FreezeAfterLoadStateType.Off;
            if (controlBefore) {
                tracker.Restart(scope, roomBefore, before - (wipeEnd ? WipeEndFrame : 0), true,
                    stateBefore == Player.StIntroJump,
                    movedAtLoad ? _ => false : AtStartSpawn(dataBefore, spawnAtLoad));
            } else {
                tracker.RestartAtAppearance(scope, roomBefore);
            }

            attempt.Clear();
            // this frame's events start from the level the load left
            lastRoom = roomBefore;
            lastState = stateBefore;
            lastControl = controlBefore;
            lastStopped = stoppedBefore;
            feed = scope != null;
        } else if (load == RoomLoad.Loader) {
            // the spawn tested is the one the player appears at (an intro
            // starts off screen). A checkpoint entered from chapter select
            // counts without its wake-up
            attempt.Clear();
            // == and not Equals: AreaKey.Equals(object) always returns false
            if (chapterRestarted && restartedArea == session.Area) {
                // the rows ending on Restart Chapter close first, on the old
                // session's time; the restart then drops the rest unrecorded
                Emit(session, tracker.ChapterRestarted(restartedTime, end));
            }

            tracker.Restart(scope, room, before, control, launching, AtStartSpawn(session.LevelData, session.RespawnPoint));
            restarted = true;
        } else if (teleported) {
            // Speed Run Tool's room teleport skips rooms without lowering chapter
            // time: a new attempt from the spawn it put the player on
            tracker.Restart(scope, room, before, control, launching, AtStartSpawn(session.LevelData, session.RespawnPoint));
            attempt.Clear();
            restarted = true;
        } else if (!fedLastFrame) {
            // switched back on: whatever was open missed events
            tracker.Drop();
            attempt.Clear();
            watched = false;
        } else if (after < before) {
            // Level.Reload zeroes chapter time on a death in the first room
            // with nothing collected: a new attempt from the spawn, which
            // without control waits for ControlReturned
            tracker.Restart(scope, room, after, control, launching, AtStartSpawn(session.LevelData, session.RespawnPoint));
            attempt.Clear();
            restarted = true;
        } else {
            feed = scope != null;
        }

        if (feed) {
            if (state == Player.StReflectionFall && lastState != Player.StReflectionFall) {
                // 6A's watched fall is not a run of 6a Start: disqualified, not
                // dropped, so it still closes in Lake's start room and Lake
                // opens there
                tracker.Disqualify();
            }

            // closes before opens: the stop edge, then the room entry (which
            // closes before it opens), then the starts waiting
            if (stopped && !lastStopped) {
                Emit(session, tracker.ChapterTimeStopped(after, end));
            }

            if (room != lastRoom && load == RoomLoad.WalkIn) {
                Emit(session, tracker.RoomEntered(scope, from, room, before, control, launching, end));
            }

            // the state changes during this frame's update, and a savestate
            // saved at the end of this frame times from the next frame: the
            // start reading is the one after it
            if (lastState == Player.StIntroJump && !launching) {
                tracker.LaunchEnded(scope, room, after);
            }

            if (!lastControl && control) {
                tracker.ControlReturned(room, after, AtStartSpawn(session.LevelData, player?.ExactPosition));
            }
        }

        // the player appears when control returns, when the intro jump ends
        // (1A's too), and when a restart lands with control; a load restores
        // the last appearance
        bool appeared = restarted
            ? control
            : watched && ((!lastControl && control) || (lastState == Player.StIntroJump && !launching));
        bool disturbed = load is RoomLoad.Respawn or RoomLoad.Teleport or RoomLoad.Loader || teleported || !fedLastFrame;
        (float X, float Y)? at = player == null ? null : (player.ExactPosition.X, player.ExactPosition.Y);
        // a restart that has control already appears where the frame began, so
        // a direction held into its first frame is a move
        (float X, float Y)? appearedAt = restarted && control && exactBefore is { } start ? (start.X, start.Y) : at;
        Stillness still = new Stillness(Saved.SpawnAt.X, Saved.SpawnAt.Y, Saved.Moved)
            .After(disturbed, appeared, appearedAt)
            .After(false, false, at);
        Saved.SpawnAt = new Vector2(still.X, still.Y);
        Saved.Moved = still.Moved;

        Saved.Stamp = ++stamp;
        // during 6A's watched fall the rooms passed through are not where the
        // run comes from: 00 is entered from start, as after either skip
        if (restarted || !watched || state != Player.StReflectionFall) {
            Saved.From = room;
        }

        lastRoom = room;
        lastState = state;
        lastControl = control;
        lastStopped = stopped;
        chapterRestarted = false;
        fedLastFrame = true;
    }

    // a savestate further into the room does not play it: the point must be on
    // a start spawn, or at its SpawnOffsets offset. A "Start" row's are the
    // room's default spawn (the game's own, Level.DefaultSpawnPoint) and the
    // one beside each checkpoint
    private static Func<SegmentRule, bool> AtStartSpawn(LevelData data, Vector2? point) =>
        rule => point is { } at && AtStartSpawn(data, at, rule);

    private static bool AtStartSpawn(LevelData data, Vector2 point, SegmentRule rule) {
        if (data == null || data.Spawns.Count == 0) {
            return false;
        }

        (int x, int y) = SegmentAutoDetect.SpawnOffsets.GetValueOrDefault((rule.Scope, rule.Anchor));
        Vector2 offset = new(x, y);
        bool On(Vector2 spawn) => Near(point, spawn) || Near(point, spawn + offset);

        if (rule.Anchor != "Start") {
            return SegmentAutoDetect.WakeUpSpawns.TryGetValue((rule.Scope, rule.Anchor), out (int X, int Y) wakeUp)
                   && On(new Vector2(wakeUp.X, wakeUp.Y));
        }

        if (On(data.Spawns.ClosestTo(new Vector2(data.Bounds.Left, data.Bounds.Bottom)))) {
            return true;
        }

        foreach (EntityData entity in data.Entities) {
            if (entity.Name == "checkpoint" && On(data.Spawns.ClosestTo(data.Position + entity.Position))) {
                return true;
            }
        }

        return false;
    }

    private static bool Near(Vector2 a, Vector2 b) => Math.Abs(a.X - b.X) <= 1f && Math.Abs(a.Y - b.Y) <= 1f;

    private static void OnRegisterCassette(On.Celeste.SaveData.orig_RegisterCassette orig, SaveData self, AreaKey area) {
        orig(self, area);
        OnCollect(Collectibles.Cassette);
    }

    private static void OnRegisterHeart(On.Celeste.HeartGem.orig_RegisterAsCollected orig, HeartGem self, Level level, string poemId) {
        orig(self, level, poemId);
        OnCollect(Collectibles.Heart);
    }

    // collects land inside an update (entity updates) or between two; inside
    // one, the frame's start reading, as for a room entry
    private static void OnCollect(Collectibles kind) {
        if (!SrsModule.Settings.Enabled || saveLoadAction == null || !fedLastFrame || Saved.Stamp != stamp
            || Engine.Scene is not Level level) {
            return;
        }

        tracker.Rooms = RoomMap.For(level.Session);
        long reading = inUpdate ? frameStart : level.Session.Time;
        Emit(level.Session, tracker.Collected(kind, reading, EndState.With(level.Session.Inventory.Dashes)));
    }

    private static void Emit(Session session, List<SegmentRecord> records) {
        if (records.Count == 0) {
            return;
        }

        SessionBests.Record(records, session);
        LatestRoom = session.Level;
        // a chapter run closes with a segment: the segment first, so the rows
        // show the chapter run and step back to the segment
        List<SegmentRule> rules = records.ConvertAll(record => record.Rule);
        foreach (bool chapterRun in (bool[])[false, true]) {
            if (Specificity.MostSpecific(rules, chapterRun) is >= 0 and int shown) {
                attempt.Add((records[shown], ++LatestSerial));
            }
        }
    }
}
