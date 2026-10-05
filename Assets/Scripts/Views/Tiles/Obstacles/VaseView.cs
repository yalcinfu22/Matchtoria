using UnityEngine;

public class VaseTileView : DamageableTileView
{
    protected override float GetReferenceDimension(Vector2 size) => Mathf.Max(size.x, size.y);
}
