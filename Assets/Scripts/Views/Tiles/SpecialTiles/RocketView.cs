using UnityEngine;

public class RocketView : TileView, IAnimateTrigger
{
    protected override string GetCategoryByType() => TileType.ToString();
    protected override float GetReferenceDimension(Vector2 size) => Mathf.Max(size.x, size.y);
    public void PlayTrigger() => ReturnToPool();
}
