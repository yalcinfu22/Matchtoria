public class ColorBomb : TileModel, ITriggerable, IMovable
{
    private TileType m_targetColor = TileType.None;
    private bool m_isMoving = false;
    public TileType TargetColor => m_targetColor;
    public bool IsMoving { get => m_isMoving; set => m_isMoving = value; }

    public ColorBomb() : base(TileType.ColorBomb) { }

    public void SetTargetColor(TileType color) => m_targetColor = color;

    public override Damage GetDeathEffect() => GetTriggerEffect();

    public Damage GetTriggerEffect()
    {
        return DamagePatterns.ColorBombDamage(m_targetColor);
    }

    public Damage GetDamageEffect() => DamagePatterns.DamageYourself;

}