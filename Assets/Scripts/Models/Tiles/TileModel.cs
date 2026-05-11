public abstract class TileModel
{
    protected TileType m_tileType;
    public TileType TileType => m_tileType;

    protected TileModel(TileType type)
    {
        m_tileType = type;
    }

    public abstract Damage GetDeathEffect();
}
public enum TileType
{
    None,
    Red,
    Green,
    Blue,
    Yellow,
    Rock,
    Purple,
    VerticalRocket,
    HorizontalRocket,
    TNT,
    ColorBomb,
    Vase,
    Stone,
    Box
}

public enum TileStatus
{
    None,
    Unaffected,
    Alive,  
    Destroyed
}