using System;
using UnityEngine;

public static class TileFactory
{
    // Test seam: lets tests inject a deterministic RNG. UnityEngine.Random ECalls into
    // native code and is unavailable in the dotnet-test runner; tests set this to a
    // System.Random-backed lambda. Null in production → falls back to UnityEngine.Random.
    public static Func<int, int> RandomRangeOverride;

    public static TileModel CreateTile(string id)
    {
        if (string.IsNullOrEmpty(id) || id == "null")
            return null;

        if (id == "random")
            return CreateRandomColorTile();

        TileIdParser.Result parsed = TileIdParser.Parse(id);
        if (!parsed.Valid)
        {
            Debug.LogError($"TileFactory: Invalid tile id '{id}'");
            return null;
        }

        return CreateTileFromType(parsed.Type, parsed.Health);
    }

    public static TileModel CreateTileFromType(TileType type, int health = 0)
    {
        switch (type)
        {
            case TileType.Red:
            case TileType.Green:
            case TileType.Blue:
            case TileType.Yellow:
                return new Matchable(type);

            case TileType.Box:
                return health > 0 ? new Box(health) : new Box();
            case TileType.Stone:
                return new Stone();
            case TileType.Vase:
                return health > 0 ? new Vase(health) : new Vase();

            case TileType.HorizontalRocket:
                return new Rocket(true);
            case TileType.VerticalRocket:
                return new Rocket(false);

            case TileType.TNT:
                return new TNT();
            case TileType.ColorBomb:
                return new ColorBomb();

            default:
                Debug.LogError($"TileFactory: No creation logic for {type}");
                return null;
        }
    }

    private static TileModel CreateRandomColorTile()
    {
        TileType[] colors = { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow };
        int index = RandomRangeOverride != null
            ? RandomRangeOverride(colors.Length)
            : UnityEngine.Random.Range(0, colors.Length);
        return new Matchable(colors[index]);
    }
}
