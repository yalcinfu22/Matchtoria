using UnityEngine;
using UnityEngine.UI;

public class RequirementGridLayoutGroup : GridLayoutGroup
{
    public override void SetLayoutHorizontal()
    {
        int columns = constraintCount;
        int rows = Mathf.Max(1, Mathf.CeilToInt(rectChildren.Count / (float)columns));
        float availableWidth = rectTransform.rect.width - padding.horizontal;
        float availableHeight = rectTransform.rect.height - padding.vertical;
        float cellWidth = (availableWidth - spacing.x * (columns - 1)) / columns;
        float cellHeight = (availableHeight - spacing.y * (rows - 1)) / rows;
        float size = Mathf.Max(0f, Mathf.Min(cellWidth, cellHeight));

        cellSize = new Vector2(size, size);
        base.SetLayoutHorizontal();
    }

    public override void SetLayoutVertical()
    {
        base.SetLayoutVertical();

        int remaining = rectChildren.Count % constraintCount;
        if (remaining == 0)
            return;

        float rowWidth = remaining * cellSize.x + (remaining - 1) * spacing.x;
        float startOffset = GetStartOffset(0, rowWidth);
        int firstChild = rectChildren.Count - remaining;

        for (int index = 0; index < remaining; index++)
        {
            float position = startOffset + index * (cellSize.x + spacing.x);
            SetChildAlongAxis(rectChildren[firstChild + index], 0, position, cellSize.x);
        }
    }
}
