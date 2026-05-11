public static class GameConfig
{
    public const float CELL_SIZE = 1f;
    public const float TILE_SCALE_PERCENTAGE = 0.99f; // 0.93 → 0.96
    public const float BORDER_SIZE = 0.15f;

    // Animation timing — shared by Model (command emission) and View (tween duration)
    public const float FALL_TIME = 0.1f;
    public const float MOVE_TIME = 0.2f;
    public const float SPAWN_DROP_TIME = 0.1f;

    // Pause inserted between a triggerable explosion (TNT/Rocket/ColorBomb)
    // and the subsequent fall. Lets the player register the impact before
    // tiles start cascading. Applied only when Phase-3 collected triggers.
    public const float TRIGGER_FALL_GAP = 0.3f;

    // Smallest timestamp gap used to disambiguate command ordering in BoardView's
    // sorted iteration when two commands touch the same cell (e.g. DestroySelf and
    // a Fall whose source/target is that cell, or Fall source vs Spawn target on
    // the top row). Visually imperceptible (~1 ms) but enforces explicit ordering
    // instead of relying on LINQ stable-sort insertion order.
    public const float COMMAND_TIME_BUMP = 0.001f;

    // Choreography beat between the swap-arrival "trigger" tile destroy and the
    // chain destroys (other matched tiles + adjacent damage). Trigger pops on
    // arrival; chain pops STAGGER later. Fall starts in parallel with the chain
    // (max-emitted + COMMAND_TIME_BUMP), not after it finishes.
    public const float MATCH_TRIGGER_STAGGER = 0.05f;
}