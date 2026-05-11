using UnityEngine;
public class NodeModel
{
    private TileModel[] m_Layers = new TileModel[3];

    public NodeModel(TileModel top, TileModel middle, TileModel bottom)
    {
        SetLayers(top, middle, bottom);
    }

    // === Getters / Setters ===

    public TileModel GetLayer(NodeLayer layer) => m_Layers[(int)layer];

    public void SetLayer(NodeLayer layer, TileModel tile) => m_Layers[(int)layer] = tile;

    public void SetLayers(TileModel top, TileModel middle, TileModel bottom)
    {
        m_Layers[(int)NodeLayer.Top] = top;
        m_Layers[(int)NodeLayer.Middle] = middle;
        m_Layers[(int)NodeLayer.Bottom] = bottom;
    }

    // === Damage All Layers ===

    public Damage DamageLayersWith(TileType source, int damage)
    {
        Damage combined = null;

        for (int i = 0; i < m_Layers.Length; i++)
        {
            Damage layerEffect = DamageLayerWith((NodeLayer)i, source, damage);
            combined = CombineDamages(combined, layerEffect);
        }

        return combined;
    }

    public Damage DamageLayers(int damage)
    {
        Damage combined = null;

        for (int i = 0; i < m_Layers.Length; i++)
        {
            Damage layerEffect = DamageLayer((NodeLayer)i, damage);
            combined = CombineDamages(combined, layerEffect);
        }

        return combined;
    }

    // === Damage Single Layer ===

    public Damage DamageLayerWith(NodeLayer layer, TileType source, int damage)
    {
        TileModel tile = m_Layers[(int)layer];
        if (tile == null) return null;

        if (tile is IDamageable damageable)
        {
            TileStatus result = damageable.TakeDamageFrom(source, damage);
            return ResolveEffect(tile, damageable, result, layer);

        } else if(tile is ITriggerable triggerable)
        {
            if (source != TileType.HorizontalRocket && source != TileType.VerticalRocket && source != TileType.TNT) return null;
            m_Layers[(int)layer] = null;
            return triggerable.GetTriggerEffect();
        }

            return null;
    }

    public Damage DamageLayer(NodeLayer layer, int damage)
    {
        TileModel tile = m_Layers[(int)layer];
        if (tile == null) return null;

        if (tile is IDamageable damageable)
        {
            TileStatus result = damageable.TakeDamage(damage);
            return ResolveEffect(tile, damageable, result, layer);
        }

        return null;
    }

    // For triggerables
    public Damage TriggerLayer(NodeLayer layer)
    {
        TileModel tile = m_Layers[(int)layer];
        if (tile == null) return null;

        if (tile is ITriggerable triggerable)
        {
            m_Layers[(int)layer] = null;
            return triggerable.GetTriggerEffect();
        }

        return null;
    }

    // === Helpers ===

    private Damage ResolveEffect(TileModel tile, IDamageable damageable, TileStatus status, NodeLayer layer)
    {
        Damage inner;
        switch (status)
        {
            case TileStatus.Destroyed:
                m_Layers[(int)layer] = null;
                inner = tile.GetDeathEffect();
                break;
            case TileStatus.Alive:
                inner = damageable.GetDamageEffect();
                break;
            default:
                return null;
        }

        // inner + layer + tile'ı closure'layan wrapper.
        // TileType injection: DestroyYourself/DamageYourself default None ile Command yaratıyor;
        // burada ölen tile'ın type'ı enjekte edilmezse BoardManager destroy → requirement mapping çalışmaz.
        return (Vector2Int pos, int dmg, NodeModel[,] board, float ts) =>
        {
            var cmds = inner(pos, dmg, board, ts);

            for (int i = 0; i < cmds.Count; i++)
            {
                var cmd = cmds[i];
                cmd.Layer = layer;
                if (cmd.TileType == TileType.None) cmd.TileType = tile.TileType;
                cmds[i] = cmd;
            }

            return cmds;
        };
    }

    private Damage CombineDamages(Damage a, Damage b)
    {
        if (a == null) return b;
        if (b == null) return a;
        return DamagePatterns.SumDamages(a, b);
    }

    // === Queries ===

    public bool IsEmpty()
    {
        for (int i = 0; i < m_Layers.Length; i++)
        {
            if (m_Layers[i] != null) return false;
        }
        return true;
    }

    public TileModel FallTile()
    {
        TileModel toReturn = m_Layers[(int)NodeLayer.Middle];
        m_Layers[(int)NodeLayer.Middle] = null;
        return toReturn;
    }

    public bool IsTileMovable()
    {
        TileModel tile = m_Layers[(int)NodeLayer.Middle];
        return tile is IMovable;
    }

    public bool HasTileAt(NodeLayer layer) => m_Layers[(int)layer] != null;
}
public enum NodeLayer
{
    None = -1,
    Top = 0,
    Middle = 1,
    Bottom = 2
}