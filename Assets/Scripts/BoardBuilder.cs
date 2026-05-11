using UnityEngine;

public class BoardBuilder : MonoBehaviour
{
    [Header("Corners (TL, TR, BL, BR)")]
    public GameObject topLeft;
    public GameObject topRight;
    public GameObject bottomLeft;
    public GameObject bottomRight;

    [Header("Edges (Top, Right, Left, Bottom)")]
    public GameObject top;
    public GameObject right;
    public GameObject left;
    public GameObject bottom;

    [Header("Middle")]
    public GameObject middle;

    public void BuildBoard(BoardView boardView, int width, int height)
    {
        float cellSize = GameConfig.CELL_SIZE;
        float border = GameConfig.BORDER_SIZE;
        float vOffset = border / 2f;   // dikey offset aynı
        float hOffset = border / 4f;   // yatay offset yarıya indi

        float gridW = width * cellSize;
        float gridH = height * cellSize;
        float halfW = gridW / 2f;
        float halfH = gridH / 2f;

        // Corners
        PlaceBorderPiece(Instantiate(bottomLeft, boardView.transform),
            new Vector2(-halfW - hOffset, -halfH - vOffset), new Vector2(border, border));
        PlaceBorderPiece(Instantiate(bottomRight, boardView.transform),
            new Vector2(halfW + hOffset, -halfH - vOffset), new Vector2(border, border));
        PlaceBorderPiece(Instantiate(topLeft, boardView.transform),
            new Vector2(-halfW - hOffset, halfH + vOffset), new Vector2(border, border));
        PlaceBorderPiece(Instantiate(topRight, boardView.transform),
            new Vector2(halfW + hOffset, halfH + vOffset), new Vector2(border, border));

        // Edges
        PlaceBorderPiece(Instantiate(bottom, boardView.transform),
            new Vector2(0f, -halfH - vOffset), new Vector2(gridW, border));
        PlaceBorderPiece(Instantiate(top, boardView.transform),
            new Vector2(0f, halfH + vOffset), new Vector2(gridW, border));
        PlaceBorderPiece(Instantiate(left, boardView.transform),
            new Vector2(-halfW - hOffset, 0f), new Vector2(border, gridH));
        PlaceBorderPiece(Instantiate(right, boardView.transform),
            new Vector2(halfW + hOffset, 0f), new Vector2(border, gridH));

        // Middle
        PlaceBorderPiece(Instantiate(middle, boardView.transform),
            new Vector2(0f, 0f), new Vector2(gridW, gridH));
    }
    private void PlaceBorderPiece(GameObject piece, Vector2 localPos, Vector2 targetWorldSize)
    {
        piece.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);

        SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // Scale the sprite to match the target world size
        Vector2 spriteSize = sr.sprite.bounds.size;
        piece.transform.localScale = new Vector3(
            targetWorldSize.x / spriteSize.x,
            targetWorldSize.y / spriteSize.y,
            1f
        );

        // Border renders behind tiles
        sr.sortingOrder = -10;
    }
}