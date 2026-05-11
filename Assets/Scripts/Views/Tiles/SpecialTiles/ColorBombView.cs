public class ColorBombView : TileView, IAnimateTrigger
{
    protected override string GetCategoryByType() => TileType.ToString();
    public void PlayTrigger() => ReturnToPool();
}
