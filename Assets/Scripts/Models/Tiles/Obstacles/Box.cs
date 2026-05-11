using System.Collections.Generic;
using UnityEngine;

public class Box : TileModel, IDamageable
{
    private int m_health;
    public int Health => m_health;

    public Box(int health = 1) : base(TileType.Box)
    {
        m_health = health;
    }

    public TileStatus TakeDamageFrom(TileType source, int amount) => TakeDamage(amount);

    public TileStatus TakeDamage(int amount)
    {
        m_health -= amount;
        return m_health > 0 ? TileStatus.Alive : TileStatus.Destroyed;
    }

    public override Damage GetDeathEffect() => DamagePatterns.DestroyYourself;

    // Instance closure carries post-damage health into the cmd so BoxView can pick
    // the matching sprite. Static DamagePatterns.DamageYourself can't see m_health.
    public Damage GetDamageEffect()
    {
        int hp = m_health;
        return (Vector2Int pos, int dmg, NodeModel[,] board, float ts) => new List<Command>
        {
            new Command(pos, pos, Commands.TakeDamage, ts, NodeLayer.None, TileType.None, hp)
        };
    }
}
