public class Rocket : TileModel, ITriggerable, IMovable
{
    private bool m_isHorizontal;
    private bool m_isMoving = false;
    public bool IsHorizontal => m_isHorizontal;
    public bool IsMoving { get => m_isMoving; set => m_isMoving = value; }

    public Rocket(bool isHorizontal) : base(isHorizontal ? TileType.HorizontalRocket : TileType.VerticalRocket)
    {
        m_isHorizontal = isHorizontal;
    }
    // Both death and trigger do the same thing
    public override Damage GetDeathEffect() => GetTriggerEffect();

    public Damage GetTriggerEffect()
    {
        return m_isHorizontal
            ? DamagePatterns.HorizontalRocketDamage
            : DamagePatterns.VerticalRocketDamage;
    }

    public Damage GetDamageEffect() => DamagePatterns.DamageYourself;
}