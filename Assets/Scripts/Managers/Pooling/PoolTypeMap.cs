// Pure TileType -> PoolType mapping, extracted so EditMode tests can pin the mapping
// without linking BoardPoolManager.cs (which depends on TileView + UnityEngine.Pool and
// cannot compile outside the Unity runtime).

public static class PoolTypeMap
{
    public static PoolType FromTileType(TileType tileType)
    {
        return tileType switch
        {
            TileType.Red or TileType.Green or
            TileType.Blue or TileType.Yellow or
            TileType.Purple => PoolType.Matchable,
            TileType.Rock => PoolType.Rock,
            TileType.Box => PoolType.Box,
            TileType.Vase => PoolType.Vase,
            TileType.Stone => PoolType.Stone,
            TileType.VerticalRocket => PoolType.VerticalRocket,
            TileType.HorizontalRocket => PoolType.HorizontalRocket,
            TileType.TNT => PoolType.TNT,
            TileType.ColorBomb => PoolType.ColorBomb,
            _ => PoolType.None
        };
    }
}
