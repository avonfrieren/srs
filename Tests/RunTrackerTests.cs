using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the engine on synthetic rules: every situation the engine must handle, event by event
public class RunTrackerTests {
    private sealed class Rooms : IRoomMap {
        public readonly Dictionary<string, (string Start, string End)> ByName = [];
        public readonly Dictionary<string, string[]> Berries = [];
        public string StartRoomOf(SegmentRule rule) => ByName.TryGetValue(rule.Name, out var r) ? r.Start : null;
        public string EndRoomOf(SegmentRule rule) => ByName.TryGetValue(rule.Name, out var r) ? r.End : null;
        public IReadOnlyCollection<string> BerriesOf(SegmentRule rule) => Berries.GetValueOrDefault(rule.Name, []);
    }

    private readonly Rooms rooms = new();
    private readonly List<SegmentRule> rules = [];

    private SegmentRule Add(string name, string start, string end, Collectibles requires = Collectibles.None,
        EndKind endKind = EndKind.NextStart, Collectibles endsOn = Collectibles.None, int? dashes = null,
        StartKind startKind = StartKind.Room, StartSetup setup = StartSetup.NextRoom, long head = 0, long tail = 0,
        bool berries = false, string scope = "1a") {
        SegmentRule rule = new(scope, scope, name, name, startKind, setup, endKind, endsOn, requires, berries, dashes,
            head, tail, rules.Count);
        rules.Add(rule);
        rooms.ByName[name] = (start, end);
        return rule;
    }

    private RunTracker Tracker() => new(rules, rooms);

    private static readonly EndState One = EndState.With(1);

    private static List<(string, long)> Of(List<SegmentRecord> records) =>
        records.Select(r => (r.Rule.Name, r.Ticks)).ToList();

    [Fact]
    public void ABacktrackIntoAStartRoomOpensNothing() {
        Add("Crossing", "6", "9b");
        RunTracker t = Tracker();

        // a state saved in room 7, inside Crossing: it knows its checkpoint
        t.Checkpoint = "6";
        t.Restart("1a", "7", 0, true, false, _ => true);
        Assert.Empty(t.RoomEntered("1a", "6", 30, true, false, One));

        Assert.Empty(t.RoomEntered("1a", "9b", 100, true, false, One));
        Assert.Empty(t.Open);
    }

    [Fact]
    public void ReturningToTheFirstRoomDoesNotReopenStart() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.RoomEntered("1a", "2", 20, true, false, One);
        t.RoomEntered("1a", "6", 100, true, false, One);
        t.RoomEntered("1a", "2", 120, true, false, One);
        t.RoomEntered("1a", "1", 140, true, false, One);

        Assert.Equal(["Crossing"], t.Open.Select(r => r.Name));
    }

    [Fact]
    public void AChainRecordsEverySegment() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b");
        Add("Chasm", "9b", null);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        Assert.Equal([("Start", 100L)], Of(t.RoomEntered("1a", "6", 100, true, false, One)));
        Assert.Equal([("Crossing", 150L)], Of(t.RoomEntered("1a", "9b", 250, true, false, One)));
        Assert.Equal([("Chasm", 150L)], Of(t.ChapterTimeStopped(400, One)));
    }

    [Fact]
    public void AnIlStaysOpenAcrossCheckpoints() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("IL", "1", null, endKind: EndKind.ChapterEnd, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        Assert.Equal([("Start", 100L)], Of(t.RoomEntered("1a", "6", 100, true, false, One)));
        Assert.Equal([("IL", 400L)], Of(t.ChapterTimeStopped(400, One)));
    }

    [Fact]
    public void AHeartRunCountsForBothRows() {
        Add("Huge Mess", "a", "b", setup: StartSetup.CurrentRoom);
        Add("Huge Mess Heart", "a", "b", requires: Collectibles.Heart, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "a", 0, true, false, _ => true);
        Assert.Empty(t.Collected(Collectibles.Heart, 50, One));

        Assert.Equal([("Huge Mess", 100L), ("Huge Mess Heart", 100L)], Of(t.RoomEntered("1a", "b", 100, true, false, One)));
    }

    [Fact]
    public void WithoutTheHeartOnlyThePlainRowIsRecorded() {
        Add("Huge Mess", "a", "b", setup: StartSetup.CurrentRoom);
        Add("Huge Mess Heart", "a", "b", requires: Collectibles.Heart, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "a", 0, true, false, _ => true);

        Assert.Equal([("Huge Mess", 100L)], Of(t.RoomEntered("1a", "b", 100, true, false, One)));
    }

    [Fact]
    public void ACollectBeforeTheOpeningDoesNotCount() {
        Add("Start", "1", "a", setup: StartSetup.CurrentRoom);
        Add("Huge Mess Heart", "a", "b", requires: Collectibles.Heart, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.Collected(Collectibles.Heart, 20, One);
        t.RoomEntered("1a", "a", 50, true, false, One);

        Assert.Empty(t.RoomEntered("1a", "b", 100, true, false, One));
    }

    [Fact]
    public void ACollectAndStopRowClosesOnTheCollect() {
        Add("Hollows", "04", "b-00", setup: StartSetup.CurrentRoom);
        Add("Hollows Tape", "04", null, requires: Collectibles.Cassette, endKind: EndKind.Collect, endsOn: Collectibles.Cassette,
            setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "04", 0, true, false, _ => true);
        Assert.Equal([("Hollows Tape", 40L)], Of(t.Collected(Collectibles.Cassette, 40, One)));
        Assert.Equal([("Hollows", 90L)], Of(t.RoomEntered("1a", "b-00", 90, true, false, One)));
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
        RunTracker t = Tracker();

        t.Restart("1a", "intro", 0, true, false, _ => true);

        Assert.Equal([(recorded, 100L)], Of(t.RoomEntered("1a", "a-00", 100, true, false, EndState.With(dashes))));
    }

    [Fact]
    public void APlantedSavestateOpensNothing() {
        Add("Crossing", "6", "9b");
        RunTracker t = Tracker();

        t.Restart("1a", "7", 0, true, false, _ => true);

        Assert.Empty(t.Open);
        Assert.Empty(t.RoomEntered("1a", "6", 50, true, false, One));
        Assert.Empty(t.Open);
    }

    [Fact]
    public void DropForgetsEverything() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.Drop();

        Assert.Empty(t.Open);
        Assert.Empty(t.RoomEntered("1a", "6", 100, true, false, One));
    }

    [Fact]
    public void NeverRecordsANonPositiveTime() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 100, true, false, _ => true);

        Assert.Empty(t.RoomEntered("1a", "6", 100, true, false, One));
    }

    [Fact]
    public void ReEnteringTheStartRoomKeepsTheFirstOpening() {
        Add("Huge Mess", "08-a", "09-d", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "08-a", 0, true, false, _ => true);
        t.RoomEntered("1a", "08-x", 30, true, false, One);
        t.RoomEntered("1a", "08-a", 50, true, false, One);

        Assert.Equal([("Huge Mess", 100L)], Of(t.RoomEntered("1a", "09-d", 100, true, false, One)));
    }

    [Fact]
    public void AStartReachedWithoutControlOpensWhenControlReturns() {
        Add("Intervention", "3", "end_0", setup: StartSetup.CurrentRoom);
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "3", 0, true, false, _ => true);
        // the flip into end_0 happens in the wake-up: the close counts, the opening waits
        Assert.Equal([("Intervention", 100L)], Of(t.RoomEntered("1a", "end_0", 100, false, false, One)));
        Assert.Empty(t.Open);
        t.ControlReturned("end_0", 160);

        Assert.Equal([("Awake", 240L)], Of(t.ChapterTimeStopped(400, One)));
    }

    [Fact]
    public void ControlReturningInAnotherRoomOpensNothing() {
        Add("Before", "3", "end_0", setup: StartSetup.CurrentRoom);
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom);

        // control case: the same steps, control back in the pending room, open it
        RunTracker control = Tracker();
        control.Restart("1a", "3", 0, true, false, _ => true);
        control.RoomEntered("1a", "end_0", 100, false, false, One);
        control.ControlReturned("end_0", 130);
        Assert.Equal(["Awake"], control.Open.Select(r => r.Name));

        RunTracker t = Tracker();
        t.Restart("1a", "3", 0, true, false, _ => true);
        t.RoomEntered("1a", "end_0", 100, false, false, One);
        t.ControlReturned("end_1", 130);

        Assert.Empty(t.Open);
    }

    [Fact]
    public void ALoadWithoutControlWaitsToo() {
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        // a Next Room timer starts on the flip itself, inside the animation
        t.Restart("1a", "end_0", 0, false, false, _ => true);
        Assert.Empty(t.Open);
        t.ControlReturned("end_0", 60);

        Assert.Equal([("Awake", 340L)], Of(t.ChapterTimeStopped(400, One)));
    }

    [Fact]
    public void ALaunchRowOpensOnlyWhenTheLaunchEnds() {
        Add("7a Start", "a-00", "b-00", startKind: StartKind.AfterLaunch, head: 1000,
            setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "a-00-intro", 0, true, false, _ => true);
        t.RoomEntered("1a", "a-00", 10, true, true, One);
        Assert.Empty(t.Open);
        t.LaunchEnded("1a", "a-00", 40);

        Assert.Equal([("7a Start", 60L + 1000)], Of(t.RoomEntered("1a", "b-00", 100, true, false, One)));
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
        Add("Granny", "0", null, head: 1037, tail: 561, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "0", 0, true, false, _ => true);

        Assert.Equal([("Granny", 1000L + 1037 + 561)], Of(t.ChapterTimeStopped(1000, One)));
    }

    [Fact]
    public void AFollowingBerryCounts() {
        Add("ARB Crossing", "6", "9b", berries: true, setup: StartSetup.CurrentRoom);
        rooms.Berries["ARB Crossing"] = ["b1", "b2"];
        RunTracker t = Tracker();

        t.Restart("1a", "6", 0, true, false, _ => true);
        t.BerryCollected("b1");

        Assert.Empty(t.RoomEntered("1a", "9b", 100, true, false, One));
        t.Drop();
        t.Restart("1a", "6", 0, true, false, _ => true);
        t.BerryCollected("b1");
        Assert.Equal([("ARB Crossing", 100L)],
            Of(t.RoomEntered("1a", "9b", 100, true, false, new EndState(1, ["b2"]))));
    }

    [Fact]
    public void ClosesComeBeforeOpens() {
        Add("A", "1", "2", setup: StartSetup.CurrentRoom);
        Add("B", "2", "3");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);

        Assert.Equal([("A", 100L)], Of(t.RoomEntered("1a", "2", 100, true, false, One)));
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
        Add("Loop", "2", "2", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "2", 0, true, false, _ => true);
        t.RoomEntered("1a", "x", 30, true, false, One);

        Assert.Equal([("Loop", 100L)], Of(t.RoomEntered("1a", "2", 100, true, false, One)));
        Assert.Equal(["Loop"], t.Open.Select(r => r.Name));
        Assert.Equal([("Loop", 50L)], Of(t.RoomEntered("1a", "2", 150, true, false, One)));
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
        Add("Before", "3", "end_0", setup: StartSetup.CurrentRoom);
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom);

        // control case: without the drop, control returning opens it
        RunTracker control = Tracker();
        control.Restart("1a", "3", 0, true, false, _ => true);
        control.RoomEntered("1a", "end_0", 100, false, false, One);
        control.ControlReturned("end_0", 160);
        Assert.Equal(["Awake"], control.Open.Select(r => r.Name));

        RunTracker t = Tracker();
        t.Restart("1a", "3", 0, true, false, _ => true);
        t.RoomEntered("1a", "end_0", 100, false, false, One);
        t.Drop();
        t.ControlReturned("end_0", 160);

        Assert.Empty(t.Open);
    }

    [Fact]
    public void ARestartForgetsAPendingStartFromAnotherRoom() {
        Add("Before", "w", "y", setup: StartSetup.CurrentRoom);
        Add("FromY", "y", null);
        Add("FromX", "x", null, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        // FromY is pending in y when the timer starts in x
        t.Restart("1a", "w", 0, true, false, _ => true);
        t.RoomEntered("1a", "y", 100, false, false, One);
        t.Restart("1a", "x", 0, false, false, _ => true);
        t.ControlReturned("x", 60);

        Assert.Equal(["FromX"], t.Open.Select(r => r.Name));
    }

    [Fact]
    public void ADisqualifiedSegmentRecordsNothingButStillChains() {
        Add("6a Start", "start", "00", setup: StartSetup.CurrentRoom);
        Add("Lake", "00", "04");
        Add("Hollows", "04", "x");
        RunTracker t = Tracker();

        t.Restart("1a", "start", 0, true, false, _ => true);
        t.Disqualify();
        // the fall passes Hollows' room: nothing closes, nothing opens
        Assert.Empty(t.RoomEntered("1a", "04", 50, false, false, One));
        Assert.Equal(["6a Start"], t.Open.Select(r => r.Name));
        Assert.Empty(t.RoomEntered("1a", "00", 80, false, false, One));
        t.ControlReturned("00", 100);

        Assert.Equal([("Lake", 200L)], Of(t.RoomEntered("1a", "04", 300, true, false, One)));
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
        Add("Before", "3", "end_0", setup: StartSetup.CurrentRoom);
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "3", 0, true, false, _ => true);
        t.RoomEntered("1a", "end_0", 100, false, false, One);
        t.Disqualify();
        t.ControlReturned("end_0", 160);

        Assert.Empty(t.Open);
    }

    [Fact]
    public void AMidSegmentSavestateRecordsTheNextSegment() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b");
        Add("Chasm", "9b", null);
        RunTracker t = Tracker();

        // a state saved in room 7, mid-Crossing: it knows its checkpoint
        t.Checkpoint = "6";
        t.Restart("1a", "7", 1000, true, false, _ => true);
        Assert.Empty(t.Open);

        Assert.Empty(t.RoomEntered("1a", "9b", 1500, true, false, One));
        Assert.Equal([("Chasm", 500L)], Of(t.ChapterTimeStopped(2000, One)));
    }

    [Fact]
    public void ALoadAtTheSpawnOpensTheCurrentRoomSegment() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        Assert.Equal([("Start", 100L)], Of(t.RoomEntered("1a", "6", 100, true, false, One)));
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
        Add("Crossing", "6", "9b");
        RunTracker t = Tracker();

        t.Restart("1a", "6", 0, true, false, _ => true);
        Assert.Empty(t.Open);
        Assert.Equal("6", t.Checkpoint);
    }

    [Fact]
    public void ALoadDropsWhatWasOpen() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b");
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.RoomEntered("1a", "6", 100, true, false, One);
        t.Restart("1a", "7", 50, true, false, _ => true);

        Assert.Empty(t.Open);
        Assert.Empty(t.RoomEntered("1a", "9b", 300, true, false, One));
    }

    [Fact]
    public void ALoadWithoutControlWaitsForIt() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, false, false, _ => true);
        Assert.Empty(t.Open);
        t.ControlReturned("1", 40);

        Assert.Equal([("Start", 60L)], Of(t.RoomEntered("1a", "6", 100, true, false, One)));
    }

    [Fact]
    public void AWakeUpOpensOnceControlReturns() {
        Add("Intervention", "a", "end_0", scope: "2a");
        Add("Awake", "end_0", null, setup: StartSetup.CurrentRoom, scope: "2a");
        RunTracker t = Tracker();

        t.Checkpoint = "a";
        t.Restart("2a", "b", 0, true, false, _ => true);
        // the wake-up changes rooms without control
        Assert.Empty(t.RoomEntered("2a", "end_0", 100, false, false, One));
        Assert.Empty(t.Open);
        t.ControlReturned("end_0", 300);

        Assert.Equal([("Awake", 200L)], Of(t.ChapterTimeStopped(500, One)));
    }

    [Fact]
    public void AnEntryFromAnotherCheckpointOpensNothing() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b");
        Add("Chasm", "9b", null);
        RunTracker t = Tracker();

        // in Chasm, walked back through Crossing to its first room
        t.Checkpoint = "9b";
        t.Restart("1a", "10", 0, true, false, _ => true);
        t.RoomEntered("1a", "6", 100, true, false, One);

        Assert.Empty(t.Open);
        Assert.Equal("6", t.Checkpoint);
    }

    [Fact]
    public void PassingBackThroughTheLastCheckpointsRoomStillChains() {
        Add("Start", "1", "6", setup: StartSetup.CurrentRoom);
        Add("Crossing", "6", "9b");
        Add("Chasm", "9b", null);
        RunTracker t = Tracker();

        t.Restart("1a", "1", 0, true, false, _ => true);
        t.RoomEntered("1a", "6", 100, true, false, One);
        // a route that loops back through Start's room inside Crossing
        t.RoomEntered("1a", "1", 150, true, false, One);
        Assert.Equal([("Crossing", 300L)], Of(t.RoomEntered("1a", "9b", 400, true, false, One)));
        Assert.Equal(["Chasm"], t.Open.Select(r => r.Name));
    }
}
