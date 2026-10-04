using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Monocle;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeedrunSheet;

// Feeds RunTracker from the game: SpeedrunTool's room-timer readings, the room
// the session is in, the player's control, the collects, and the frame chapter
// time stops. SpeedrunTool is only the stopwatch: where a segment starts and
// ends is srs's (SegmentRules), and every row whose requirements a run met
// gets the time.
public static class RunWatcher {
    // the player has no control in these states (Player.cs:384-412): a start
    // reached in one waits for control in that room. Provisional, checked in
    // game. ⚠️ StIntroJump must stay out: 7A's launches land in it (Dummy in the
    // old room, IntroJump from the room change, Normal ~78 frames later), and
    // 500m to 3000m are entered in it and open on the entry
    private static readonly HashSet<int> NoControlStates = [
        Player.StDummy, Player.StIntroWalk, Player.StIntroRespawn,
        Player.StIntroWakeUp, Player.StBirdDashTutorial, Player.StFrozen, Player.StReflectionFall,
        Player.StTempleFall, Player.StIntroMoonJump, Player.StIntroThinkForABit,
    ];

    private static RunTracker tracker;

    // the last frame as polled, to turn polls into events. Not registered with
    // SpeedrunTool's save states: a load resets the room timer, which drops
    // the chain, and is what the reading going down detects
    private static long lastReading;
    private static string lastRoom;
    private static int lastState = -1;
    private static bool lastControl = true;
    private static bool lastStopped;
    private static RoomTimerType lastTimerType;
    private static bool loadedFromLoader;
    private static bool fedLastFrame;

    /// The last segment closed with a time, the most specific of its frame;
    /// null after a timer reset. The HUD's tier row.
    internal static SegmentRecord? Latest { get; private set; }

    // fields are filled at runtime by ModInterop()
#pragma warning disable CS0649
    [ModImportName("SpeedrunTool.RoomTimer")]
    private static class RoomTimerImports {
        public static Func<long> GetRoomTime;
    }
#pragma warning restore CS0649

    public static void Load() {
        // loaded right after Hotkeys: this hook stays inside TierComparison's,
        // so after orig the frame's records are settled when the tier computes
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.Level.LoadLevel += LevelOnLoadLevel;
        On.Celeste.SaveData.RegisterCassette += OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected += OnRegisterHeart;

        typeof(RoomTimerImports).ModInterop();
        tracker = new RunTracker(SegmentRules.All, null);
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.Level.LoadLevel -= LevelOnLoadLevel;
        On.Celeste.SaveData.RegisterCassette -= OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected -= OnRegisterHeart;
    }

    private static long Reading() => RoomTimerImports.GetRoomTime?.Invoke() ?? 0;

    // a new Level from the loader: entering a chapter, a restart, a console
    // load. A respawn after a death is a LoadLevel too, without isFromLoader
    private static void LevelOnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self,
        Player.IntroTypes playerIntro, bool isFromLoader) {
        orig(self, playerIntro, isFromLoader);
        if (isFromLoader) {
            loadedFromLoader = true;
        }
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        // switched off: nothing is fed, and the next frame fed drops what was
        // open, since the events missed meanwhile would close it wrongly
        if (!SrsModule.Settings.Enabled) {
            fedLastFrame = false;
            orig(self);
            return;
        }

        // the reading before this frame's delta: on the frame an end fires,
        // SpeedrunTool freezes its display first, then adds the delta
        long before = Reading();
        RoomTimerType typeBefore = SpeedrunToolSettings.Instance?.RoomTimerType ?? RoomTimerType.Off;
        orig(self);
        long after = Reading();

        Session session = self.Session;
        string scope = SegmentAutoDetect.ScopeOf(session);
        string room = session.Level;
        int state = self.Tracker.GetEntity<Player>()?.StateMachine.State ?? -1;
        bool control = !self.InCutscene && !NoControlStates.Contains(state);
        bool launching = state == Player.StIntroJump;
        bool stopped = self.TimerStopped || self.Completed;
        RoomTimerType timerType = SpeedrunToolSettings.Instance?.RoomTimerType ?? RoomTimerType.Off;
        tracker.Rooms = RoomMap.For(session);
        EndState end = EndState.With(session.Inventory.Dashes);

        // a savestate load resets the timer, and it can count again before srs
        // reads its 0: a reading that went down is a reset
        bool wentDown = after < lastReading;
        bool reset = after == 0 || wentDown || !fedLastFrame || loadedFromLoader
                     || timerType != lastTimerType || timerType == RoomTimerType.Off;
        if (reset) {
            tracker.Drop();
            Latest = null;
        } else if (state == Player.StReflectionFall && lastState != Player.StReflectionFall) {
            // 6A's watched fall is not a run of 6a Start (owner, 2026-10-03):
            // disqualified, not dropped, so it still closes in Lake's start
            // room and Lake opens there
            tracker.Disqualify();
        }

        // a timer moving from 0 within this frame needs no history; a restart
        // after a load is read off two readings, which must be of one clock:
        // a re-enabled switch or a timer-type change leaves lastReading from
        // another frame or another accumulator
        bool sameClock = fedLastFrame && typeBefore == timerType && timerType == lastTimerType;

        if (scope != null && after > 0 && timerType != RoomTimerType.Off) {
            if (typeBefore == timerType && (before == 0 || (sameClock && wentDown))) {
                // the timer starts on this frame, or restarted after a load: a
                // standalone run, timed from the timer's own 0
                tracker.TimerStarted(scope, room, wentDown ? 0 : before, control, launching);
            } else if (!reset) {
                // closes before opens: the stop edge, then the room entry
                // (which closes before it opens), then the starts waiting
                if (stopped && !lastStopped) {
                    Emit(session, tracker.ChapterTimeStopped(before, end));
                }

                if (room != lastRoom) {
                    Emit(session, tracker.RoomEntered(scope, room, before, control, launching, end));
                }

                // the state changes during this frame's update, and a
                // savestate saved at the end of this frame times from the next
                // frame: the start reading is the one after it
                if (lastState == Player.StIntroJump && !launching) {
                    tracker.LaunchEnded(scope, room, after);
                }

                if (!lastControl && control) {
                    tracker.ControlReturned(room, after);
                }
            }
        }

        lastReading = after;
        lastRoom = room;
        lastState = state;
        lastControl = control;
        lastStopped = stopped;
        lastTimerType = timerType;
        loadedFromLoader = false;
        fedLastFrame = true;
    }

    private static void OnRegisterCassette(On.Celeste.SaveData.orig_RegisterCassette orig, SaveData self, AreaKey area) {
        orig(self, area);
        OnCollect(Collectibles.Cassette);
    }

    private static void OnRegisterHeart(On.Celeste.HeartGem.orig_RegisterAsCollected orig, HeartGem self, Level level, string poemId) {
        orig(self, level, poemId);
        OnCollect(Collectibles.Heart);
    }

    // collects land between updates; the reading is taken right here, so a
    // segment ending on one gets the collect frame's time
    private static void OnCollect(Collectibles kind) {
        if (!SrsModule.Settings.Enabled || !fedLastFrame || lastReading == 0 || Engine.Scene is not Level level
            || (SpeedrunToolSettings.Instance?.RoomTimerType ?? RoomTimerType.Off) != lastTimerType) {
            return;
        }

        long reading = Reading();
        if (reading == 0) {
            return;
        }

        tracker.Rooms = RoomMap.For(level.Session);
        Emit(level.Session, tracker.Collected(kind, reading, EndState.With(level.Session.Inventory.Dashes)));
    }

    private static void Emit(Session session, List<SegmentRecord> records) {
        if (records.Count == 0) {
            return;
        }

        SessionBests.Record(records, session);
        Latest = records[Specificity.MostSpecific(records.ConvertAll(record => record.Rule))];
    }
}
