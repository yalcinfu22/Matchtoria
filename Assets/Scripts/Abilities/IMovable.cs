public interface IMovable
{
    // True only during the FallIteration that physically moved this tile (or just
    // spawned it). Reset to false at the start of the next FallIteration's priming
    // pass — tiles that stay put for a full iter are considered settled and become
    // match-eligible. This is a fail-safe shield: even if a future timing change
    // re-introduces a window where the model "settles" before the view tween ends,
    // MatchManager skips IsMoving=true tiles so they cannot be destroyed mid-tween.
    bool IsMoving { get; set; }
}
