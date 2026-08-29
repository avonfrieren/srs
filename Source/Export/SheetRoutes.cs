using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeedrunSheet;

/// A route through the game inside a category: which chapters it visits and,
/// in each, which checkpoints. The sheet expresses this by hiding the rows a
/// route does not use on its category tab, and row visibility is not something
/// a CSV export carries -- so the composition is transcribed here instead,
/// from the tab's visible rows and from the derivations on FileTimes.
///
/// Read only by the export screen. The mod's own selection still runs on
/// SegmentCategory; nothing here is written to the settings.
///
/// Names are the sheet's own ("5b6b", "2a 💙 DTS"), and the checkpoints are
/// srs's, keyed by scope -- the side of a folded chapter, which is the unit the
/// sheet's own chapter blocks use ("5a 📼" and "5b" are two blocks).
///
/// Game-free, so the table is unit-tested against the imported segments.
internal sealed record SheetRoute(string Category, string Name,
    Dictionary<string, string[]> ByScope) {
    /// "All" carries no composition and filters nothing.
    public bool FiltersRows => ByScope != null;

    public bool Covers(string scope) =>
        ByScope == null || (scope != null && ByScope.ContainsKey(scope));

    /// the segments this route plays in that scope, in sheet order. Empty when
    /// the route does not go there at all.
    public IReadOnlyList<string> Checkpoints(string scope) =>
        ByScope != null && scope != null && ByScope.TryGetValue(scope, out string[] names)
            ? names
            : [];
}

internal static class SheetRoutes {
    /// the entry that steps outside the routes and lists everything
    public const string AllRoutes = "All";

    // Blocks shared by every route. A chapter taken plainly contributes all of
    // its checkpoints; one taken for a collectible stops at it.
    private static readonly string[] Prologue = ["Granny"];
    private static readonly string[] Ch1a = ["Start", "Crossing", "Chasm"];
    private static readonly string[] Ch2a = ["Start", "Intervention", "Awake"];
    // the heart visit is its own block on the sheet, and it comes first: the
    // chapter is entered twice, which is why its derivation counts two entry
    // cutscenes
    private static readonly string[] Ch2aHeart = ["Start Heart", "Start", "Intervention", "Awake"];
    private static readonly string[] Ch3a = ["Start", "Huge Mess", "Elevator Shaft", "Presidential Suite"];
    private static readonly string[] Ch3aHeart = ["Start", "Huge Mess Heart", "Elevator Shaft", "Presidential Suite"];
    private static readonly string[] Ch4a = ["Start", "Shrine", "Old Trail", "Cliff Face"];
    private static readonly string[] Ch4aHeart = ["Start", "Shrine Heart", "Old Trail", "Cliff Face"];
    private static readonly string[] Ch5a = ["5a Start", "Depths", "Unravelling", "Search", "Rescue"];
    private static readonly string[] Ch5aTape = ["5a Start", "Depths Tape"];
    private static readonly string[] Ch5b = ["5b Start", "Central Chamber", "Through the Mirror", "Mix Master"];
    private static readonly string[] Ch6a = ["6a Start", "Lake", "Hollows", "Reflection", "6a Rock Bottom", "Resolution"];
    private static readonly string[] Ch6aTape = ["6a Start", "Lake", "Hollows Tape"];
    private static readonly string[] Ch6b = ["6b Start", "Falling", "6b Rock Bottom", "Reprieve"];
    private static readonly string[] Ch7a = ["0m", "500m", "1000m", "1500m", "2000m", "2500m", "3000m"];
    private static readonly string[] Ch8a = ["Start", "Into the Core", "Hot and Cold", "HotM Vertical", "HotM Horizontal"];
    // the skip runs from Farewell's start to Determination; the three segments
    // after it are the same either way
    private static readonly string[] Farewell = [
        "Start", "Singular", "Power Source", "Remembered", "Event Horizon", "Determination",
        "Stubbornness", "Reconciliation", "Farewell"];
    private static readonly string[] FarewellDts = [
        "Start DTS", "Singular DTS", "Power Source DTS", "Remembered DTS", "Event Horizon DTS",
        "Determination DTS", "Stubbornness", "Reconciliation", "Farewell"];

    // Only Any% and True Ending are stable on the sheet; ARB, All Cassettes,
    // Bny%, All Hearts and 100% are not transcribed yet. Bny% and All Hearts
    // have no route dimension at all, which the sheet says in its own words.
    private static readonly SheetRoute[] Defined = [
        new("Any%", "5a6a", new() {
            ["Prologue"] = Prologue, ["1a"] = Ch1a, ["2a"] = Ch2a, ["3a"] = Ch3a, ["4a"] = Ch4a,
            ["5a"] = Ch5a, ["6a"] = Ch6a, ["7a"] = Ch7a,
        }),
        new("Any%", "5b6a", new() {
            ["Prologue"] = Prologue, ["1a"] = Ch1a, ["2a"] = Ch2a, ["3a"] = Ch3a, ["4a"] = Ch4a,
            ["5a"] = Ch5aTape, ["5b"] = Ch5b, ["6a"] = Ch6a, ["7a"] = Ch7a,
        }),
        new("Any%", "5b6b", new() {
            ["Prologue"] = Prologue, ["1a"] = Ch1a, ["2a"] = Ch2a, ["3a"] = Ch3a, ["4a"] = Ch4a,
            ["5a"] = Ch5aTape, ["5b"] = Ch5b, ["6a"] = Ch6aTape, ["6b"] = Ch6b, ["7a"] = Ch7a,
        }),
        new("True Ending", "2a 💙 No DTS", new() {
            ["Prologue"] = Prologue, ["1a"] = Ch1a, ["2a"] = Ch2aHeart, ["3a"] = Ch3aHeart, ["4a"] = Ch4aHeart,
            ["5a"] = Ch5aTape, ["5b"] = Ch5b, ["6a"] = Ch6a, ["7a"] = Ch7a,
            ["8a"] = Ch8a, ["Farewell"] = Farewell,
        }),
        new("True Ending", "2a 💙 DTS", new() {
            ["Prologue"] = Prologue, ["1a"] = Ch1a, ["2a"] = Ch2aHeart, ["3a"] = Ch3aHeart, ["4a"] = Ch4aHeart,
            ["5a"] = Ch5aTape, ["5b"] = Ch5b, ["6a"] = Ch6a, ["7a"] = Ch7a,
            ["8a"] = Ch8a, ["Farewell"] = FarewellDts,
        }),
        new("True Ending", "6b No DTS", new() {
            ["Prologue"] = Prologue, ["1a"] = Ch1a, ["2a"] = Ch2a, ["3a"] = Ch3aHeart, ["4a"] = Ch4aHeart,
            ["5a"] = Ch5aTape, ["5b"] = Ch5b, ["6a"] = Ch6aTape, ["6b"] = Ch6b, ["7a"] = Ch7a,
            ["8a"] = Ch8a, ["Farewell"] = Farewell,
        }),
        new("True Ending", "6b DTS", new() {
            ["Prologue"] = Prologue, ["1a"] = Ch1a, ["2a"] = Ch2a, ["3a"] = Ch3aHeart, ["4a"] = Ch4aHeart,
            ["5a"] = Ch5aTape, ["5b"] = Ch5b, ["6a"] = Ch6aTape, ["6b"] = Ch6b, ["7a"] = Ch7a,
            ["8a"] = Ch8a, ["Farewell"] = FarewellDts,
        }),
    ];

    public static readonly SheetRoute[] All = WithAllPerCategory(Defined);

    /// Appends each category's "All": every checkpoint its own routes play, and
    /// no more. Not "every row of the chapter" -- a checkpoint no route of the
    /// category visits belongs to another category, and showing it under this
    /// one says something false. Stepping outside the categories entirely is
    /// what the category-level "All" is for.
    private static SheetRoute[] WithAllPerCategory(SheetRoute[] routes) {
        List<SheetRoute> built = [];
        foreach (string category in routes.Select(route => route.Category).Distinct()) {
            SheetRoute[] of = routes.Where(route => route.Category == category).ToArray();
            built.AddRange(of);

            Dictionary<string, string[]> union = [];
            foreach (string scope in of.SelectMany(route => route.ByScope.Keys).Distinct()) {
                union[scope] = [.. of
                    .Where(route => route.ByScope.ContainsKey(scope))
                    .SelectMany(route => route.ByScope[scope])
                    .Distinct()];
            }

            built.Add(new SheetRoute(category, AllRoutes, union));
        }

        return [.. built];
    }

    /// in table order, which is the sheet's
    public static string[] Categories =>
        All.Select(route => route.Category).Distinct().ToArray();

    public static SheetRoute[] Of(string category) =>
        All.Where(route => route.Category == category).ToArray();
}
