using UnityEngine;

public struct Command
{
    public Vector2 StartPosition;
    public Vector2 TargetPosition;
    public Commands CommandType;
    public float startTimeStamp;
    public NodeLayer Layer;
    public TileType TileType;
    public int Health;

    public Command(Vector2 startPosition, Vector2 targetPosition, Commands commandType, float timeStamp, NodeLayer layer, TileType tileType = TileType.None, int health = 0)
    {
        StartPosition = startPosition;
        TargetPosition = targetPosition;
        CommandType = commandType;
        startTimeStamp = timeStamp;
        Layer = layer;
        TileType = tileType;
        Health = health;
    }

    public static int CompareCommandByTimeStamp(Command a, Command b)
    {
        return a.startTimeStamp.CompareTo(b.startTimeStamp);
    }
}

public enum Commands
{
    None,
    Move,
    Swap,
    Trigger,
    Fall,
    FallLeft,
    FallRight,
    TakeDamage,
    DestroySelf,
    Spawn,
    Merge,
    ExplosionStart,
    ExplosionEnd
}
