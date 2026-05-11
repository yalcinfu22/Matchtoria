public class Stone : TileModel, IDamageable
{
    private int m_health = 3;
    public int Health => m_health;

    public Stone() : base(TileType.Rock) { }

    public TileStatus TakeDamageFrom(TileType source, int amount)
    {
        if (source != TileType.TNT && source != TileType.HorizontalRocket && source != TileType.VerticalRocket)
            return TileStatus.Unaffected;

        return TakeDamage(amount);
    }

    public TileStatus TakeDamage(int amount)
    {
        m_health -= amount;
        return m_health > 0 ? TileStatus.Alive : TileStatus.Destroyed;
    }

    public override Damage GetDeathEffect() => DamagePatterns.DestroyYourself;
    public Damage GetDamageEffect() => DamagePatterns.DamageYourself;
}