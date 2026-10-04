namespace Celeste.Mod.SpeedrunSheet;

/// Where the player last appeared and whether they have moved since: what a
/// savestate carries for the load rule, which opens a Current Room segment only
/// for a player who has not moved since appearing. Positions are exact, the
/// sub-pixel remainder included: a tap shorter than a pixel is a move.
internal readonly record struct Stillness(float X, float Y, bool Moved) {
    /// Before any appearance: nothing a load could open from.
    public static readonly Stillness Unsettled = new(0f, 0f, true);

    /// One fed frame, after the update. A disturbance (a level from the loader,
    /// a teleport, a death, switching srs off) comes before an appearance on the
    /// same frame, and the move check comes last. With no player (at is null)
    /// there is no appearance and no move.
    public Stillness After(bool disturbed, bool appeared, (float X, float Y)? at) {
        Stillness next = disturbed ? this with { Moved = true } : this;
        if (at is not { } position) {
            return next;
        }

        if (appeared) {
            return new Stillness(position.X, position.Y, false);
        }

        return next.Moved || (position.X == next.X && position.Y == next.Y) ? next : next with { Moved = true };
    }
}
