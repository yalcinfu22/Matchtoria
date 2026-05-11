public interface IDamageable
{
    int Health { get; }
    TileStatus TakeDamageFrom(TileType source, int amount);
    TileStatus TakeDamage(int amount);
    Damage GetDamageEffect();
}