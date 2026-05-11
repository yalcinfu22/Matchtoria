using System.Collections.Generic;

// Parses level-data tile id strings into (TileType, initialHealth).
// "box" and "box3" both resolve to TileType.Box; the suffix overrides the
// per-type default health when present. Shared by TileFactory (model build)
// and BoardPoolManager (view build) so the two paths cannot diverge.
public static class TileIdParser
{
    public struct Result
    {
        public TileType Type;
        public int Health;
        public bool Valid;
    }

    private static readonly Dictionary<string, TileType> s_baseIds = new Dictionary<string, TileType>
    {
        { "red", TileType.Red },
        { "green", TileType.Green },
        { "blue", TileType.Blue },
        { "yellow", TileType.Yellow },
        { "box", TileType.Box },
        { "stone", TileType.Stone },
        { "vase", TileType.Vase },
        { "ro_h", TileType.HorizontalRocket },
        { "ro_v", TileType.VerticalRocket },
        { "TNT", TileType.TNT },
        { "ColorBomb", TileType.ColorBomb }
    };

    private static readonly Dictionary<TileType, int> s_defaultHealth = new Dictionary<TileType, int>
    {
        { TileType.Box, 1 },
        { TileType.Vase, 2 }
    };

    public static Result Parse(string id)
    {
        if (string.IsNullOrEmpty(id) || id == "null")
            return new Result { Valid = false };

        // Strip trailing digits as health override; remainder is the base id.
        int splitAt = id.Length;
        while (splitAt > 0 && char.IsDigit(id[splitAt - 1]))
            splitAt--;

        string baseId = id.Substring(0, splitAt);
        string suffix = id.Substring(splitAt);

        if (!s_baseIds.TryGetValue(baseId, out TileType type))
            return new Result { Valid = false };

        int health = 0;
        if (s_defaultHealth.TryGetValue(type, out int defaultHp))
            health = defaultHp;

        if (suffix.Length > 0 && int.TryParse(suffix, out int parsedHp))
            health = parsedHp;

        return new Result { Type = type, Health = health, Valid = true };
    }
}
