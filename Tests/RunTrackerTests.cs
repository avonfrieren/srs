using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the engine on synthetic rules: every situation the engine must handle, event by event.
// A segment ending in a room closes with a time only on an entry from the
// entry room of the segment starting there, so most tests give the segment
// after the one they time
public class RunTrackerTests {
    private sealed class Rooms : IRoomMap {
        public readonly Dictionary<string, (string Start, string End)> ByName = [];
        public readonly Dictionary<string, string[]> Berries = [];
        public readonly Dictionary<string, string> Entries = [];
        public string StartRoomOf(SegmentRule rule) => ByName.TryGetValue(rule.Name, out var r) ? r.Start : null;
        public string EndRoomOf(SegmentRule rule) => ByName.TryGetValue(rule.Name, out var r) ? r.End : null;
        public string EntryRoomOf(SegmentRule rule) => Entries.GetValueOrDefault(rule.Name);
        public IReadOnlyCollection<string> BerriesOf(SegmentRule rule) => Berries.GetValueOrDefault(rule.Name, []);
    }

    private readonly Rooms rooms = new();
    private readonly List<SegmentRule> rules = [];

    private SegmentRule Add(string name, string start, string end, Collectibles requires = Collectibles.None,
        EndKind endKind = EndKind.NextStart, Collectibles endsOn = Collectibles.None, int? dashes = null,
        StartKind startKind = StartKind.Room, StartSetup setup = StartSetup.NextRoom, long head = 0, long tail = 0,
        bool berries = false, string scope = "1a", string entry = null) {
        SegmentRule rule = new(scope, scope, name, name, startKind, setup, endKind, endsOn, requires, berries, dashes,
            head, tail, rules.Count);
        rules.Add(rule);
        rooms.ByName[name] = (start, end);
        if (entry != null) {
            rooms.Entries[name] = entry;
        }

        return rule;
    }

    private RunTracker Tracker() => new(rules, rooms);

    private static readonly EndState One = EndState.With(1);

    private static List<(string, long)> Of(List<SegmentRecord> records) =>
        records.Select(r => (r.Rule.Name, r.Ticks)).ToList();

    [Fact]
    public void ABacktrackIntoAStartRoomOpensNothing() {
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        // a state saved in room 7, inside Crossing, then back into its first room
        t.Restart("1a", "7", 0, true, false, _ => true);
        Assert.Empty(t.Open);
        Assert.Empty(t.RoomEntered("1a", "7", "6", 30, true, false, One));

        Assert.Empty(t.Open);
    }

    [Fact]
    public void ReturningToTheFirstRoomDoesNotReopenStart() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.RoomEntered("1a", "1", "5", 20, true, false, One);
        t.RoomEntered("1a", "5", "6", 100, true, false, One);
        t.RoomEntered("1a", "6", "5", 120, true, false, One);
        t.RoomEntered("1a", "5", "1", 140, true, false, One);

        Assert.Equal(["Crossing"], t.Open.Select(r => r.Name));
    }

    [Fact]
    public void AChainRecordsEverySegment() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        Add("Chasm", "9b", null, entry: "9");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        Assert.Equal([("Start", 100L)], Of(t.RoomEntered("1a", "5", "6", 100, true, false, One)));
        Assert.Equal([("Crossing", 150L)], Of(t.RoomEntered("1a", "9", "9b", 250, true, false, One)));
        Assert.Equal([("Chasm", 150L)], Of(t.ChapterTimeStopped(400, One)));
    }

    // so a chapter's segments add up to its whole chapter
    [Fact]
    public void AChaptersLastSegmentAndItsWholeChapterEndOnTheSameReading() {
        Add("Chasm", "9b", null, setup: StartSetup.CurrentRoom);
        Add("IL", "9b", null, endKind: EndKind.ChapterEnd, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "9b", 0, true, false, _ => true);
        Assert.Equal([("Chasm", 400L), ("IL", 400L)], Of(t.ChapterTimeStopped(400, One)));
    }

    [Fact]
    public void AnIlStaysOpenAcrossCheckpoints() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("IL", "1", null, endKind: EndKind.ChapterEnd, setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        Assert.Equal([("Start", 100L)], Of(t.RoomEntered("1a", "5", "6", 100, true, false, One)));
        Assert.Equal([("IL", 400L)], Of(t.ChapterTimeStopped(400, One)));
    }

    [Fact]
    public void AHeartRunCountsForBothRows() {
        Add("Huge Mess", "a", "b", setup: StartSetup.CurrentRoom);
        Add("Huge Mess Heart", "a", "b", requires: Collectibles.Heart, setup: StartSetup.CurrentRoom);
        Add("Elevator Shaft", "b", null, entry: "a9");
        RunTracker t = Tracker();

        t.Restart("1a", "a", 0, true, false, _ => true);
        Assert.Empty(t.Collected(Collectibles.Heart, 50, One));

        Assert.Equal([("Huge Mess", 100L), ("Huge Mess Heart", 100L)],
            Of(t.RoomEntered("1a", "a9", "b", 100, true, false, One)));
    }

    [Fact]
    public void WithoutTheHeartOnlyThePlainRowIsRecorded() {
        Add("Huge Mess", "a", "b", setup: StartSetup.CurrentRoom);
        Add("Huge Mess Heart", "a", "b", requires: Collectibles.Heart, setup: StartSetup.CurrentRoom);
        Add("Elevator Shaft", "b", null, entry: "a9");
        RunTracker t = Tracker();

        t.Restart("1a", "a", 0, true, false, _ => true);

        Assert.Equal([("Huge Mess", 100L)], Of(t.RoomEntered("1a", "a9", "b", 100, true, false, One)));
    }

    [Fact]
    public void ACollectBeforeTheOpeningDoesNotCount() {
        Add("Start", "1", "a", setup: StartSetup.CurrentRoom);
        Add("Huge Mess Heart", "a", "b", requires: Collectibles.Heart, entry: "1z");
        Add("Elevator Shaft", "b", null, entry: "a9");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.Collected(Collectibles.Heart, 20, One);
        t.RoomEntered("1a", "1z", "a", 50, true, false, One);

        Assert.Empty(t.RoomEntered("1a", "a9", "b", 100, true, false, One));
    }

    [Fact]
    public void ACollectAndStopRowClosesOnTheCollect() {
        Add("Hollows", "04", "b-00", setup: StartSetup.CurrentRoom);
        Add("Hollows Tape", "04", null, requires: Collectibles.Cassette, endKind: EndKind.Collect, endsOn: Collectibles.Cassette,
            setup: StartSetup.CurrentRoom);
        Add("Reflection", "b-00", null, entry: "20");
        RunTracker t = Tracker();

        t.Restart("1a", "04", 0, true, false, _ => true);
        Assert.Equal([("Hollows Tape", 40L)], Of(t.Collected(Collectibles.Cassette, 40, One)));
        Assert.Equal([("Hollows", 90L)], Of(t.RoomEntered("1a", "20", "b-00", 90, true, false, One)));
    }

    [Fact]
    public void TwoCollectsCloseOnTheLater() {
        Collectibles both = Collectibles.Heart | Collectibles.Cassette;
        Add("Depths Both", "b-00", null, requires: both, endKind: EndKind.Collect, endsOn: both,
            setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "b-00", 0, true, false, _ => true);
        Assert.Empty(t.Collected(Collectibles.Heart, 30, One));
        Assert.Equal([("Depths Both", 60L)], Of(t.Collected(Collectibles.Cassette, 60, One)));
    }

    [Theory]
    [InlineData(2, "Start DTS")]
    [InlineData(1, "Start")]
    public void DtsTwinsAreExclusive(int dashes, string recorded) {
        Add("Start", "intro", "a-00", dashes: 1, setup: StartSetup.CurrentRoom);
        Add("Start DTS", "intro", "a-00", dashes: 2, setup: StartSetup.CurrentRoom);
        Add("Singular", "a-00", null, entry: "intro-03-space");
        RunTracker t = Tracker();

        t.Restart("1a", "intro", 0, true, false, _ => true);

        Assert.Equal([(recorded, 100L)],
            Of(t.RoomEntered("1a", "intro-03-space", "a-00", 100, true, false, EndState.With(dashes))));
    }

    // by the end of Farewell both routes carry the same dashes: the IL is
    // decided at the first checkpoint crossed
    [Theory]
    [InlineData(2, "DTS IL")]
    [InlineData(1, "No DTS IL")]
    public void AWholeChapterReadsItsDashesAtTheFirstCheckpoint(int dashes, string recorded) {
        Add("DTS IL", "intro", null, dashes: 2, endKind: EndKind.ChapterEnd, setup: StartSetup.CurrentRoom);
        Add("No DTS IL", "intro", null, dashes: 1, endKind: EndKind.ChapterEnd, setup: StartSetup.CurrentRoom);
        Add("Singular", "a-00", "c-00", entry: "intro-03-space");
        Add("Power Source", "c-00", null, entry: "b-07");
        RunTracker t = Tracker();

        t.Restart("1a", "intro", 0, true, false, _ => true);
        // a room that starts no checkpoint is not read: the dash is lost after it
        t.RoomEntered("1a", "intro", "intro-01", 50, true, false, EndState.With(3 - dashes));
        t.RoomEntered("1a", "intro-03-space", "a-00", 100, true, false, EndState.With(dashes));
        t.RoomEntered("1a", "b-07", "c-00", 200, true, false, EndState.With(3 - dashes));

        Assert.Equal([(recorded, 400L), ("Power Source", 200L)],
            Of(t.ChapterTimeStopped(400, EndState.With(2))));
    }

    // without a checkpoint crossed, nothing says which IL it was
    [Fact]
    public void AWholeChapterWithADashRuleNeedsACheckpoint() {
        Add("DTS IL", "intro", null, dashes: 2, endKind: EndKind.ChapterEnd, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "intro", 0, true, false, _ => true);

        Assert.Empty(t.ChapterTimeStopped(400, EndState.With(2)));
    }

    [Fact]
    public void DropForgetsEverything() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.Drop();

        Assert.Empty(t.Open);
        Assert.Empty(t.RoomEntered("1a", "5", "6", 100, true, false, One));
    }

    [Fact]
    public void NeverRecordsANonPositiveTime() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 100, true, false, _ => true);

        Assert.Empty(t.RoomEntered("1a", "5", "6", 100, true, false, One));
    }

    // Speed Run Tool's format has no hours: a time of an hour or more would be
    // exported an hour short
    [Fact]
    public void NeverRecordsAnHourOrMore() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();
        long hour = TimeSpan.FromHours(1).Ticks;

        t.Restart("1a", "1", 0, true, false, _ => true);
        Assert.Empty(t.RoomEntered("1a", "5", "6", hour, true, false, One));

        t.Restart("1a", "1", 0, true, false, _ => true);
        Assert.Equal([("Start", hour - 1)], Of(t.RoomEntered("1a", "5", "6", hour - 1, true, false, One)));
    }

    [Fact]
    public void ReEnteringTheStartRoomKeepsTheFirstOpening() {
        Add("Huge Mess", "08-a", "09-d", entry: "07-a");
        Add("Elevator Shaft", "09-d", null, entry: "09-b");
        RunTracker t = Tracker();

        t.RoomEntered("1a", "07-a", "08-a", 0, true, false, One);
        // a route that steps back into the entry room and in again
        t.RoomEntered("1a", "08-a", "07-a", 30, true, false, One);
        t.RoomEntered("1a", "07-a", "08-a", 50, true, false, One);

        Assert.Equal([("Huge Mess", 100L)], Of(t.RoomEntered("1a", "09-b", "09-d", 100, true, false, One)));
    }

    [Fact]
    public void AStartReachedWithoutControlOpensWhenControlReturns() {
        Add("Intervention", "3", "end_0", entry: "3x");
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom, entry: "13");
        RunTracker t = Tracker();

        t.RoomEntered("1a", "3x", "3", 0, true, false, One);
        // the flip into end_0 happens in the wake-up: the close counts, the opening waits
        Assert.Equal([("Intervention", 100L)], Of(t.RoomEntered("1a", "13", "end_0", 100, false, false, One)));
        Assert.Empty(t.Open);
        // a start reached by an entry is not tested where the player appears
        t.ControlReturned("end_0", 160, _ => false);

        Assert.Equal([("Awake", 240L)], Of(t.ChapterTimeStopped(400, One)));
    }

    // Awake reached by its wake-up, without control: it waits for control in end_0
    private RunTracker AwakeWaitingForControl() {
        Add("Before", "3", "end_0", setup: StartSetup.CurrentRoom);
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom, entry: "13");
        RunTracker t = Tracker();
        t.Restart("1a", "3", 0, true, false, _ => true);
        t.RoomEntered("1a", "13", "end_0", 100, false, false, One);
        return t;
    }

    [Fact]
    public void ControlReturningInThePendingRoomOpensTheStart() {
        RunTracker t = AwakeWaitingForControl();
        t.ControlReturned("end_0", 130, _ => true);

        Assert.Equal(["Awake"], t.Open.Select(r => r.Name));
    }

    [Fact]
    public void ControlReturningInAnotherRoomOpensNothing() {
        RunTracker t = AwakeWaitingForControl();
        t.ControlReturned("end_1", 130, _ => true);

        Assert.Empty(t.Open);
    }

    [Fact]
    public void ALaunchRowOpensOnlyWhenTheLaunchEnds() {
        Add("7a Start", "a-00", "b-00", startKind: StartKind.AfterLaunch, head: 1000,
            setup: StartSetup.CurrentRoom);
        Add("500m", "b-00", null, entry: "a-06");
        RunTracker t = Tracker();

        t.Restart("1a", "a-00-intro", 0, true, false, _ => true);
        t.RoomEntered("1a", "a-00-intro", "a-00", 10, true, true, One);
        Assert.Empty(t.Open);
        t.LaunchEnded("1a", "a-00", 40);

        Assert.Equal([("7a Start", 60L + 1000)], Of(t.RoomEntered("1a", "a-06", "b-00", 100, true, false, One)));
    }

    [Fact]
    public void ALaunchRowOpensOnALoadAfterTheLanding() {
        Add("7a Start", "a-00", "b-00", startKind: StartKind.AfterLaunch, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "a-00", 0, true, true, _ => true);
        Assert.Empty(t.Open);
        t.Drop();
        t.Restart("1a", "a-00", 0, true, false, _ => true);

        Assert.Equal(["7a Start"], t.Open.Select(r => r.Name));
    }

    [Fact]
    public void HeadAndTailAreAdded() {
        Add("Granny", "0", null, head: 1037, tail: 544, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "0", 0, true, false, _ => true);

        Assert.Equal([("Granny", 1000L + 1037 + 544)], Of(t.ChapterTimeStopped(1000, One)));
    }

    [Fact]
    public void AFollowingBerryCounts() {
        Add("ARB Crossing", "6", "9b", berries: true, setup: StartSetup.CurrentRoom);
        Add("Chasm", "9b", null, entry: "9");
        rooms.Berries["ARB Crossing"] = ["b1", "b2"];
        RunTracker t = Tracker();

        t.Restart("1a", "6", 0, true, false, _ => true);
        t.BerryCollected("b1");

        Assert.Empty(t.RoomEntered("1a", "9", "9b", 100, true, false, One));
        t.Drop();
        t.Restart("1a", "6", 0, true, false, _ => true);
        t.BerryCollected("b1");
        Assert.Equal([("ARB Crossing", 100L)],
            Of(t.RoomEntered("1a", "9", "9b", 100, true, false, new EndState(1, ["b2"]))));
    }

    [Fact]
    public void ClosesComeBeforeOpens() {
        Add("A", "1", "2", setup: StartSetup.CurrentRoom);
        Add("B", "2", "3", entry: "1");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);

        Assert.Equal([("A", 100L)], Of(t.RoomEntered("1a", "1", "2", 100, true, false, One)));
        Assert.Equal(["B"], t.Open.Select(r => r.Name));
    }

    [Fact]
    public void OnlyTheScopesRulesOpen() {
        SegmentRule other = new("6b", "6a/b", "Elsewhere", "Start", StartKind.Room, StartSetup.CurrentRoom, EndKind.NextStart,
            Collectibles.None, Collectibles.None, false, null, 0, 0, 0);
        rules.Add(other);
        rooms.ByName["Elsewhere"] = ("1", "6");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);

        Assert.Empty(t.Open);
    }

    [Fact]
    public void ASegmentEndingWhereItStartsClosesThenReopens() {
        Add("Loop", "2", "2", setup: StartSetup.CurrentRoom, entry: "x");
        RunTracker t = Tracker();

        t.Restart("1a", "2", 0, true, false, _ => true);
        t.RoomEntered("1a", "2", "x", 30, true, false, One);

        Assert.Equal([("Loop", 100L)], Of(t.RoomEntered("1a", "x", "2", 100, true, false, One)));
        Assert.Equal(["Loop"], t.Open.Select(r => r.Name));
        t.RoomEntered("1a", "2", "x", 120, true, false, One);
        Assert.Equal([("Loop", 50L)], Of(t.RoomEntered("1a", "x", "2", 150, true, false, One)));
    }

    [Fact]
    public void ChapterTimeStoppedLeavesASegmentWhoseEndRoomWasNotReached() {
        Add("A", "1", "6", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);

        Assert.Empty(t.ChapterTimeStopped(400, One));
        Assert.Equal(["A"], t.Open.Select(r => r.Name));
    }

    [Fact]
    public void DropForgetsAPendingStart() {
        RunTracker t = AwakeWaitingForControl();
        t.Drop();
        t.ControlReturned("end_0", 160, _ => true);

        Assert.Empty(t.Open);
    }

    [Fact]
    public void ARestartForgetsAPendingStartFromAnotherRoom() {
        Add("Before", "w", "y", setup: StartSetup.CurrentRoom);
        Add("FromY", "y", null, entry: "w");
        Add("FromX", "x", null, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        // FromY is pending in y when a restart lands in x
        t.Restart("1a", "w", 0, true, false, _ => true);
        t.RoomEntered("1a", "w", "y", 100, false, false, One);
        t.Restart("1a", "x", 0, false, false, _ => true);
        t.ControlReturned("x", 60, _ => true);

        Assert.Equal(["FromX"], t.Open.Select(r => r.Name));
    }

    // 6A's watched fall: Madeline falls from start through 04, 02b, 02 and 01
    // into 00 in the fall's own state, so every room of it is entered from start
    [Fact]
    public void ADisqualifiedSegmentRecordsNothingButStillChains() {
        Add("6a Start", "start", "00", setup: StartSetup.CurrentRoom);
        Add("Lake", "00", "04", entry: "start");
        Add("Hollows", "04", "b-00", entry: "02b");
        RunTracker t = Tracker();

        t.Restart("1a", "start", 0, true, false, _ => true);
        t.Disqualify();
        // the fall passes Hollows' room: nothing closes, nothing opens
        Assert.Empty(t.RoomEntered("1a", "start", "04", 50, false, false, One));
        Assert.Equal(["6a Start"], t.Open.Select(r => r.Name));
        Assert.Empty(t.RoomEntered("1a", "start", "02b", 60, false, false, One));
        Assert.Empty(t.RoomEntered("1a", "start", "02", 70, false, false, One));
        Assert.Empty(t.RoomEntered("1a", "start", "01", 75, false, false, One));
        // 6a Start closes unrecorded, and Lake waits for control
        Assert.Empty(t.RoomEntered("1a", "start", "00", 80, false, false, One));
        Assert.Empty(t.Open);
        t.ControlReturned("00", 100, _ => false);

        Assert.Equal([("Lake", 200L)], Of(t.RoomEntered("1a", "02b", "04", 300, true, false, One)));
    }

    [Fact]
    public void ADisqualifiedSegmentRecordsNothingOnACollectOrAStop() {
        Add("Hollows Tape", "04", null, requires: Collectibles.Cassette, endKind: EndKind.Collect,
            endsOn: Collectibles.Cassette, setup: StartSetup.CurrentRoom);
        Add("Final", "04", null, endKind: EndKind.ChapterEnd, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "04", 0, true, false, _ => true);
        t.Disqualify();
        Assert.Empty(t.Collected(Collectibles.Cassette, 40, One));
        Assert.Equal(["Final"], t.Open.Select(r => r.Name));
        Assert.Empty(t.ChapterTimeStopped(90, One));
        Assert.Empty(t.Open);
    }

    [Fact]
    public void DisqualifyForgetsPendingStarts() {
        RunTracker t = AwakeWaitingForControl();
        t.Disqualify();
        t.ControlReturned("end_0", 160, _ => true);

        Assert.Empty(t.Open);
    }

    [Fact]
    public void AMidSegmentSavestateRecordsTheNextSegment() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        Add("Chasm", "9b", null, entry: "9");
        RunTracker t = Tracker();

        // a state saved in room 7, mid-Crossing
        t.Restart("1a", "7", 1000, true, false, _ => true);
        Assert.Empty(t.Open);

        Assert.Empty(t.RoomEntered("1a", "9", "9b", 1500, true, false, One));
        Assert.Equal([("Chasm", 500L)], Of(t.ChapterTimeStopped(2000, One)));
    }

    [Fact]
    public void ALoadAwayFromTheSpawnOpensNothing() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => false);
        Assert.Empty(t.Open);
    }

    [Fact]
    public void ALoadIntoANextRoomStartOpensNothing() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        t.Restart("1a", "6", 0, true, false, _ => true);
        Assert.Empty(t.Open);
    }

    [Fact]
    public void ALoadDropsWhatWasOpen() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        Add("Chasm", "9b", null, entry: "9");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.RoomEntered("1a", "5", "6", 100, true, false, One);
        t.Restart("1a", "7", 50, true, false, _ => true);

        Assert.Empty(t.Open);
        Assert.Empty(t.RoomEntered("1a", "9", "9b", 300, true, false, One));
    }

    [Fact]
    public void ARestartWithoutControlWaitsForIt() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, false, false, _ => true);
        Assert.Empty(t.Open);
        t.ControlReturned("1", 40, _ => false);

        Assert.Equal([("Start", 60L)], Of(t.RoomEntered("1a", "5", "6", 100, true, false, One)));
    }

    // a state saved mid-respawn or mid-wake-up: where the player stood at the
    // save says nothing, where they appear does
    [Fact]
    public void ALoadWithoutControlTestsWhereThePlayerAppears() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");

        RunTracker t = Tracker();
        t.RestartAtAppearance("1a", "1");
        Assert.Empty(t.Open);
        t.ControlReturned("1", 40, _ => true);
        Assert.Equal([("Start", 60L)], Of(t.RoomEntered("1a", "5", "6", 100, true, false, One)));

        RunTracker refused = Tracker();
        refused.RestartAtAppearance("1a", "1");
        refused.ControlReturned("1", 40, _ => false);
        Assert.Empty(refused.Open);
    }

    [Fact]
    public void ALoadWithoutControlInANextRoomStartWaitsForNothing() {
        Add("Crossing", "6", "9b", entry: "5");
        RunTracker t = Tracker();

        t.RestartAtAppearance("1a", "6");
        t.ControlReturned("6", 40, _ => true);

        Assert.Empty(t.Open);
    }

    // the debug map into 2A's 13, then the phone: Awake opens from its entry
    // room however the run reached it
    [Fact]
    public void AWakeUpOpensOnceControlReturns() {
        Add("Intervention", "3", "end_0", entry: "3x", scope: "2a");
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom, scope: "2a", entry: "13");
        RunTracker t = Tracker();

        t.Restart("2a", "13", 0, true, false, _ => true);
        // the wake-up changes rooms without control
        Assert.Empty(t.RoomEntered("2a", "13", "end_0", 100, false, false, One));
        Assert.Empty(t.Open);
        t.ControlReturned("end_0", 300, _ => false);

        Assert.Equal([("Awake", 200L)], Of(t.ChapterTimeStopped(500, One)));
    }

    // the debug map into 5A's b-16, then the mirror: void, and the wake-up in c-00
    [Fact]
    public void AnEntryFromTheEntryRoomOpensHoweverTheRunGotThere() {
        Add("Unravelling", "c-00", "d-00", setup: StartSetup.CurrentRoom, scope: "5a", entry: "void");
        Add("Search", "d-00", "e-00", scope: "5a", entry: "c-13");
        RunTracker t = Tracker();

        t.Restart("5a", "b-16", 0, true, false, _ => true);
        t.RoomEntered("5a", "b-16", "void", 50, false, false, One);
        t.RoomEntered("5a", "void", "c-00", 100, false, false, One);
        t.ControlReturned("c-00", 150, _ => false);

        Assert.Equal([("Unravelling", 250L)], Of(t.RoomEntered("5a", "c-13", "d-00", 400, true, false, One)));
    }

    // Speed Run Tool's PageDown into the room before Crossing's, then walking in
    [Fact]
    public void ATeleportIntoTheEntryRoomThenAnEntryRecords() {
        Add("Crossing", "6", "9b", entry: "5");
        Add("Chasm", "9b", null, entry: "9");
        RunTracker t = Tracker();

        t.Restart("1a", "5", 0, true, false, _ => true);
        t.RoomEntered("1a", "5", "6", 100, true, false, One);

        Assert.Equal([("Crossing", 200L)], Of(t.RoomEntered("1a", "9", "9b", 300, true, false, One)));
    }

    // 4A's d-00 is reached from c-08, and from c-10, the berry room beside it
    [Fact]
    public void AnEntryFromAnotherRoomOfTheSegmentBeforeDropsItAndOpensNothing() {
        Add("Old Trail", "c-00", "d-00", scope: "4a", entry: "b-08");
        Add("Cliff Face", "d-00", null, scope: "4a", entry: "c-08");

        RunTracker control = Tracker();
        control.RoomEntered("4a", "b-08", "c-00", 0, true, false, One);
        Assert.Equal([("Old Trail", 500L)], Of(control.RoomEntered("4a", "c-08", "d-00", 500, true, false, One)));
        Assert.Equal(["Cliff Face"], control.Open.Select(r => r.Name));

        RunTracker t = Tracker();
        t.RoomEntered("4a", "b-08", "c-00", 0, true, false, One);
        Assert.Empty(t.RoomEntered("4a", "c-10", "d-00", 500, true, false, One));
        Assert.Empty(t.Open);
    }

    [Fact]
    public void PassingBackThroughTheLastCheckpointsRoomStillChains() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b", entry: "5");
        Add("Chasm", "9b", null, entry: "9");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.RoomEntered("1a", "5", "6", 100, true, false, One);
        // a route that loops back through Start's room inside Crossing
        t.RoomEntered("1a", "6x", "1", 150, true, false, One);
        Assert.Equal([("Crossing", 300L)], Of(t.RoomEntered("1a", "9", "9b", 400, true, false, One)));
        Assert.Equal(["Chasm"], t.Open.Select(r => r.Name));
    }

    // 2A's heart and Restart Chapter: the RC row is timed to the moment the old
    // level is left, with the old session's chapter time
    [Fact]
    public void TheRcRowClosesAtTheRestartAndTheRestIsDropped() {
        Add("Start", "start", "3", setup: StartSetup.CurrentRoom, scope: "2a");
        Add("Start Heart RC", "start", "3", requires: Collectibles.Heart, endKind: EndKind.Restart,
            setup: StartSetup.CurrentRoom, scope: "2a");
        Add("Intervention", "3", "end_0", scope: "2a", entry: "3x");
        RunTracker t = Tracker();

        t.Restart("2a", "start", 0, true, false, _ => true);
        Assert.Empty(t.Collected(Collectibles.Heart, 1322, One));
        Assert.Equal([("Start Heart RC", 1380L)], Of(t.ChapterRestarted(1380, One)));
        Assert.Empty(t.Open);

        // the new level opens the chapter's start again, and plain Start ranks
        t.Restart("2a", "start", 0, true, false, _ => true);
        Assert.Equal([("Start", 270L)], Of(t.RoomEntered("2a", "3x", "3", 270, true, false, One)));
    }

    [Fact]
    public void ARestartWithoutTheCollectibleRecordsNothing() {
        Add("Start Heart RC", "start", "3", requires: Collectibles.Heart, endKind: EndKind.Restart,
            setup: StartSetup.CurrentRoom, scope: "2a");
        RunTracker t = Tracker();

        t.Restart("2a", "start", 0, true, false, _ => true);

        Assert.Empty(t.ChapterRestarted(1380, One));
        Assert.Empty(t.Open);
    }

    // the heart, then on into Intervention, then Restart Chapter much later:
    // that run is not one of the row
    [Fact]
    public void TheRcRowDoesNotOutliveItsSegment() {
        Add("Start", "start", "3", setup: StartSetup.CurrentRoom, scope: "2a");
        Add("Start Heart RC", "start", "3", requires: Collectibles.Heart, endKind: EndKind.Restart,
            setup: StartSetup.CurrentRoom, scope: "2a");
        Add("Intervention", "3", "end_0", scope: "2a", entry: "3x");

        // control case: walking between the segment's own rooms keeps it open
        RunTracker control = Tracker();
        control.Restart("2a", "start", 0, true, false, _ => true);
        control.Collected(Collectibles.Heart, 100, One);
        Assert.Empty(control.RoomEntered("2a", "start", "s1", 150, true, false, One));
        Assert.Equal([("Start Heart RC", 200L)], Of(control.ChapterRestarted(200, One)));

        // walking out of its segment drops it unrecorded
        RunTracker t = Tracker();
        t.Restart("2a", "start", 0, true, false, _ => true);
        t.Collected(Collectibles.Heart, 100, One);
        Assert.Equal([("Start", 270L)], Of(t.RoomEntered("2a", "3x", "3", 270, true, false, One)));
        Assert.Equal(["Intervention"], t.Open.Select(r => r.Name));

        Assert.Empty(t.ChapterRestarted(9000, One));
    }
}
