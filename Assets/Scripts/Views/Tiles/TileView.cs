// TileView.cs
using UnityEngine;
using UnityEngine.U2D.Animation;

public abstract class TileView : MonoBehaviour
{
    [SerializeField] protected SpriteRenderer m_SpriteRenderer;
    [SerializeField] protected SpriteLibrary spriteLibrary;

    private TileType m_TileType;
    private BoardPoolManager m_Pool;
    private float m_CellSize; // Yeni field

    public TileType TileType => m_TileType;

    public void Setup(TileType type, BoardPoolManager pool, float cellSize, int initialHealth = 0)
    {
        m_TileType = type;
        m_Pool = pool;
        m_CellSize = cellSize;

        m_SpriteRenderer.sortingOrder = 10;
        m_SpriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        // Hook for subclasses (e.g. BoxView) to capture initialHealth before sprite
        // selection runs — GetLabelByType may depend on it.
        OnSetup(initialHealth);

        SetSpriteFromLibrary();
    }

    protected virtual void OnSetup(int initialHealth) { }

    public void SetSortingOrder(int order)
    {
        m_SpriteRenderer.sortingOrder = order;
    }

    private void AdjustScale()
    {
        if (m_SpriteRenderer.sprite == null) return;

        Vector2 spriteSize = m_SpriteRenderer.sprite.bounds.size;
        float refDim = GetReferenceDimension(spriteSize);
        float targetSize = GameConfig.CELL_SIZE * GameConfig.TILE_SCALE_PERCENTAGE;
        float scale = targetSize / refDim;
        transform.localScale = Vector3.one * scale;
    }

    protected virtual float GetReferenceDimension(Vector2 spriteSize) => spriteSize.x;
    protected void ReturnToPool()
    {
        m_Pool.Return(this);
    }

    protected virtual void SetSpriteFromLibrary()
    {
        string category = GetCategoryByType();
        string label = GetLabelByType();
        Sprite sprite = spriteLibrary.GetSprite(category, label);

        if (sprite != null)
        {
            m_SpriteRenderer.sprite = sprite;
            AdjustScale();
        }
        else
        {
            Debug.LogWarning($"Sprite not found: {category}/{label}");
        }
    }

    protected virtual string GetCategoryByType() => "Default";
    protected virtual string GetLabelByType() => m_TileType.ToString();

    public void ApplyCommand(Command cmd)
    {
        switch (cmd.CommandType)
        {
            case Commands.DestroySelf:
                if (this is IAnimateDestroy destroyable)
                    destroyable.PlayDestroy();
                break;
            case Commands.TakeDamage:
                if (this is IAnimateDamage damageable)
                    damageable.PlayDamage(cmd.Health);
                break;
            case Commands.Trigger:
                if (this is IAnimateTrigger triggerable)
                    triggerable.PlayTrigger();
                break;
        }
    }
}
