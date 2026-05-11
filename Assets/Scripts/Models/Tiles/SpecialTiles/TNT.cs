using UnityEngine;
public class TNT : TileModel, ITriggerable, IMovable
{

    private int m_health = 1;
    private bool m_isMoving = false;

    public int Health => m_health;
    public bool IsMoving { get => m_isMoving; set => m_isMoving = value; }

    public TNT() : base(TileType.TNT) { }

    public override Damage GetDeathEffect() => GetTriggerEffect();

    public Damage GetTriggerEffect()
    {
        return DamagePatterns.TNTDamage;
    }

    public Damage GetDamageEffect() => GetTriggerEffect();
}