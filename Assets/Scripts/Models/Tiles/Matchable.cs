public class Matchable : TileModel, IDamageable, IMatchable, IMovable
{
    private int m_health = 1;
    private bool m_isMatched = false;
    private bool m_isMoving = false;

    public int Health => m_health;
    public bool IsMatched => m_isMatched;
    public bool IsMoving { get => m_isMoving; set => m_isMoving = value; }

    public Matchable(TileType color) : base(color) { }

    public void MarkAsMatched() => m_isMatched = true;

    public TileStatus TakeDamageFrom(TileType source, int amount)
    {
        bool isMatchSource = source == TileType.Red || source == TileType.Green
                          || source == TileType.Blue || source == TileType.Yellow;

        if (isMatchSource)
        {
            // Match adjacency damage: sadece bu match'in parçası olan tile ölür.
            // Yan match'in komşusu farklı renk matchable korunur.
            if (!m_isMatched) return TileStatus.Unaffected;
            return TakeDamage(amount);
        }

        // Special source (rocket/TNT): m_isMatched immunity Phase 6 trigger'ı bloke eder.
        if (m_isMatched && source != TileType.ColorBomb)
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