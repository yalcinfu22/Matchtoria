using System.Collections.Generic;
using UnityEngine;

public class Vase : TileModel, IDamageable, IMovable
{
    private int m_health;
    private bool m_isMoving = false;

    public int Health => m_health;
    public bool IsMoving { get => m_isMoving; set => m_isMoving = value; }

    public Vase(int health = 2) : base(TileType.Vase)
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

    // Instance closure carries post-damage health into the cmd so VaseView can swap
    // sprites per damage state. Same pattern as Box.GetDamageEffect.
    public Damage GetDamageEffect()
    {
        int hp = m_health;
        return (Vector2Int pos, int dmg, NodeModel[,] board, float ts) => new List<Command>
        {
            new Command(pos, pos, Commands.TakeDamage, ts, NodeLayer.None, TileType.None, hp)
        };
    }
}
