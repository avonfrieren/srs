using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeedrunSheet;

// Feeds RunTracker from the game: chapter time, the room the session is in,
// the player's control, the collects, and the frame chapter time stops. Where
// a segment starts and ends is srs's (SegmentRules), and every row whose
// requirements a run met gets the time.
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

    // with Speed Run Tool's "freeze after load" Off, its timer counts the
    // frame the load wipe ends on, which the level does not simulate and
    // Session.Time does not count (measured 2026-10-03: 1A 80 frames against
    // 81). With it On, the default, and on a TAS load, both resume together.
    // The wipe is what tells them apart: a player load sets Level.Wipe in the
    // same call, a TAS load sets none
    private static readonly long WipeEndFrame = TimeSpan.FromMilliseconds(17).Ticks;

    private static RunTracker tracker;

    // game state, saved and restored with each savestate: the checkpoint the
    // player is in, and a stamp written on every fed frame. A load puts back an
    // older stamp, which is how a load is seen
    private static class Saved {
        public static long Stamp;
        public static string Checkpoint;
    }

    private static object saveLoadAction;

    // never saved: what a load is compared against. stamp only grows, so a
    // saved stamp differs from it once a frame has been fed since the save
    private static long stamp;
    private static string lastRoom;
    private static AreaKey lastArea;
    private static int lastState = -1;
    private static bool lastControl = true;
    private static bool lastStopped;
    private static bool loadedFromLoader;
    private static bool teleported;
    private static bool fedLastFrame;

    // the frame being fed: a collect inside it is timed from its start, as
    // every other end is (Session.Time already counts the frame by then)
    private static bool inUpdate;
    private static long frameStart;

    /// The last segment closed with a time, the most specific of its frame;
    /// null after a load. The HUD's tier row.
    internal static SegmentRecord? Latest { get; private set; }

    // fields are filled at runtime by ModInterop()
#pragma warning disable CS0649
    [ModImportName("SpeedrunTool.SaveLoad")]
    private static class SaveLoadImports {
        public static Func<Type, string[], object> RegisterStaticTypes;
        public static Action<object> Unregister;
    }
#pragma warning restore CS0649

    public static void Load() {
        // loaded right after Hotkeys: this hook stays inside TierComparison's,
        // so after orig the frame's records are settled when the tier computes
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.Level.LoadLevel += LevelOnLoadLevel;
        On.Celeste.SaveData.RegisterCassette += OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected += OnRegisterHeart;

        typeof(SaveLoadImports).ModInterop();
        saveLoadAction = SaveLoadImports.RegisterStaticTypes?.Invoke(typeof(Saved),
            [nameof(Saved.Stamp), nameof(Saved.Checkpoint)]);
        if (saveLoadAction == null) {
            // loads would go unseen, and chapter time keeps running across a
            // load, so a segment open across one would be mistimed: nothing is fed
            Logger.Log(LogLevel.Warn, "srs", "Speed Run Tool's SaveLoad did not bind: savestate loads cannot be seen, so srs records nothing");
        }

        tracker = new RunTracker(SegmentRules.All, null);
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.Level.LoadLevel -= LevelOnLoadLevel;
        On.Celeste.SaveData.RegisterCassette -= OnRegisterCassette;
        On.Celeste.HeartGem.RegisterAsCollected -= OnRegisterHeart;

        if (saveLoadAction != null) {
            SaveLoadImports.Unregister?.Invoke(saveLoadAction);
            saveLoadAction = null;
        }
    }

    // a new Level from the loader: entering a chapter, a restart, a console
    // load. A respawn after a death is a LoadLevel too, without isFromLoader,
    // and always in the room of the death. A Respawn into another room is
    // Speed Run Tool's room teleport (TeleportRoomUtils.TeleportTo), which then
    // updates the level itself: the vanilla room changes use other intro types
    private static void LevelOnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self,
        Player.IntroTypes playerIntro, bool isFromLoader) {
        orig(self, playerIntro, isFromLoader);
        if (isFromLoader) {
            loadedFromLoader = true;
        } else if (playerIntro == Player.IntroTypes.Respawn && self.Session.Level != lastRoom) {
            teleported = true;
        }
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        // switched off: nothing is fed, and the next frame fed drops what was
        // open, since the events missed meanwhile would close it wrongly. A
        // chapter entered meanwhile is not a start any more. A savestate loaded
        // meanwhile is seen as a load on the first frame back, and opens only
        // if the player stands on a start spawn then. Without Speed Run Tool's
        // SaveLoad nothing is ever fed: a load could not be seen
        if (!SrsModule.Settings.Enabled || saveLoadAction == null) {
            // cleared after orig too: a LoadLevel inside it would set them
            fedLastFrame = false;
            orig(self);
            loadedFromLoader = false;
            teleported = false;
            return;
        }

        // before orig: the reading an end is timed from (Speed Run Tool freezes
        // its display before it adds the frame), and the level as a load left it
        bool loaded = Saved.Stamp != stamp;
        long before = self.Session.Time;
        Player playerBefore = self.Tracker.GetEntity<Player>();
        Vector2? positionBefore = playerBefore?.Position;
        LevelData dataBefore = self.Session.LevelData;
        string roomBefore = self.Session.Level;
        int stateBefore = playerBefore?.StateMachine.State ?? -1;
        bool controlBefore = !self.InCutscene && !NoControlStates.Contains(stateBefore);
        bool stoppedBefore = self.TimerStopped || self.Completed;
        bool wipingBefore = self.Wipe != null;

        frameStart = before;
        inUpdate = true;
        orig(self);
        inUpdate = false;

        Session session = self.Session;
        long after = session.Time;
        string scope = SegmentAutoDetect.ScopeOf(session);
        string room = session.Level;
        int state = self.Tracker.GetEntity<Player>()?.StateMachine.State ?? -1;
        bool control = !self.InCutscene && !NoControlStates.Contains(state);
        bool launching = state == Player.StIntroJump;
        bool stopped = self.TimerStopped || self.Completed;
        tracker.Rooms = RoomMap.For(session);
        EndState end = EndState.With(session.Inventory.Dashes);

        bool feed = false;
        if (!loaded && Saved.Stamp != stamp) {
            // a load inside orig, which no Speed Run Tool version does today:
            // this frame's events would mix two timelines
            tracker.Checkpoint = Saved.Checkpoint;
            tracker.Drop();
            Latest = null;
        } else if (loaded) {
            // every load is a new attempt; the checkpoint comes back with the state
            tracker.Checkpoint = Saved.Checkpoint;
            bool wipeEnd = wipingBefore
                           && SpeedrunToolSettings.Instance?.FreezeAfterLoadStateType == FreezeAfterLoadStateType.Off;
            tracker.Restart(scope, roomBefore, before - (wipeEnd ? WipeEndFrame : 0), controlBefore,
                stateBefore == Player.StIntroJump, AtStartSpawn(dataBefore, positionBefore));
            Latest = null;
            // this frame's events start from the level the load left
            lastRoom = roomBefore;
            lastState = stateBefore;
            lastControl = controlBefore;
            lastStopped = stoppedBefore;
            feed = scope != null;
        } else if (loadedFromLoader) {
            // entering a chapter, a restart, a console load, the debug map: the
            // spawn tested is the one the player appears at (an intro starts
            // off screen), the chapter's own for a chapter start and the
            // checkpoint's for a checkpoint entered from chapter select, which
            // counts without its wake-up (owner, 2026-10-03). Timed from before
            // the frame, as any start
            tracker.Checkpoint = null;
            tracker.Restart(scope, room, before, control, launching, AtStartSpawn(session.LevelData, session.RespawnPoint));
            Latest = null;
        } else if (teleported) {
            // Speed Run Tool's room teleport skips rooms without lowering chapter
            // time: a new attempt from the spawn it put the player on. The
            // checkpoint is unknown mid-segment, and a stale one would open the
            // next segment from its far side
            tracker.Checkpoint = null;
            tracker.Restart(scope, room, before, control, launching, AtStartSpawn(session.LevelData, session.RespawnPoint));
            Latest = null;
        } else if (!fedLastFrame) {
            // switched back on: the checkpoint stays within the room it was left
            // in, and is unknown anywhere else, so the next segment is missed,
            // never mistimed
            if (room != lastRoom || session.Area != lastArea) {
                tracker.Checkpoint = null;
            }

            tracker.Drop();
            Latest = null;
        } else if (after < before) {
            // Level.Reload zeroes chapter time on a death in the first room
            // with nothing collected: a new attempt from the spawn, which
            // without control waits for ControlReturned
            tracker.Restart(scope, room, after, control, launching, AtStartSpawn(session.LevelData, session.RespawnPoint));
            Latest = null;
        } else {
            feed = scope != null;
        }

        if (feed) {
            if (state == Player.StReflectionFall && lastState != Player.StReflectionFall) {
                // 6A's watched fall is not a run of 6a Start (owner, 2026-10-03):
                // disqualified, not dropped, so it still closes in Lake's start
                // room and Lake opens there
                tracker.Disqualify();
            }

            // closes before opens: the stop edge, then the room entry (which
            // closes before it opens), then the starts waiting
            if (stopped && !lastStopped) {
                Emit(session, tracker.ChapterTimeStopped(before, end));
            }

            if (room != lastRoom) {
                Emit(session, tracker.RoomEntered(scope, room, before, control, launching, end));
            }

            // the state changes during this frame's update, and a savestate
            // saved at the end of this frame times from the next frame: the
            // start reading is the one after it
            if (lastState == Player.StIntroJump && !launching) {
                tracker.LaunchEnded(scope, room, after);
            }

            if (!lastControl && control) {
                tracker.ControlReturned(room, after);
            }
        }

        Saved.Stamp = ++stamp;
        Saved.Checkpoint = tracker.Checkpoint;
        lastRoom = room;
        lastArea = session.Area;
        lastState = state;
        lastControl = control;
        lastStopped = stopped;
        loadedFromLoader = false;
        teleported = false;
        fedLastFrame = true;
    }

    // a savestate further into the room does not play the room (owner): the
    // point must be on a start spawn of the room, or at the measured offset
    // from one. A "Start" row's are the room's default spawn (the game's own,
    // nearest its bottom-left corner, Level.DefaultSpawnPoint) and the spawn
    // beside each checkpoint; a wake-up row's is its WakeUpSpawns entry, and a
    // row without one opens nothing. Not on any: a far spawn is reached by
    // backtracking into the room, or by the debug map
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
    // one, the frame's start reading, as for every other end
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
        Latest = records[Specificity.MostSpecific(records.ConvertAll(record => record.Rule))];
    }
}
