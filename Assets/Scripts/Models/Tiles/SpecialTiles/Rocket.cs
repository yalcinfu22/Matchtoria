public class Rocket : TileModel, ITriggerable, IMovable
{
    public bool IsMoving { get; set; }

    public Rocket(TileType type) : base(type)
    {
    }
    // Both death and trigger do the same thing
    public override Damage GetDeathEffect() => GetTriggerEffect();

    public Damage GetTriggerEffect()
    {
        return TileType == TileType.HorizontalRocket
            ? DamagePatterns.HorizontalRocketDamage
            : DamagePatterns.VerticalRocketDamage;
    }

    public Damage GetDamageEffect() => DamagePatterns.DamageYourself;
}
