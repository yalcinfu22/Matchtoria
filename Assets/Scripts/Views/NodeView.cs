using UnityEngine;

public class NodeView : MonoBehaviour
{
    private TileView[] _layers = new TileView[3];
    private BoardPoolManager _poolManager;
    private int _row;
    private int _totalRows;

    public void Init(BoardPoolManager poolManager, string topRaw, string middleRaw, string bottomRaw, int row, int totalRows)
    {
        _poolManager = poolManager;
        _row = row;
        _totalRows = totalRows;
        SetupLayer(NodeLayer.Bottom, bottomRaw);
        SetupLayer(NodeLayer.Middle, middleRaw);
        SetupLayer(NodeLayer.Top, topRaw);
    }

    private int GetSortingOrder(NodeLayer layer)
    {
        int rowOrder = _row * 100;
        int layerOrder = layer switch
        {
            NodeLayer.Bottom => 0,
            NodeLayer.Middle => 10,
            NodeLayer.Top => 20,
            _ => 0
        };
        return rowOrder + layerOrder;
    }

    private void SetupLayer(NodeLayer layer, string rawId)
    {
        // Pool parses rawId and runs Setup with the parsed health — single source of truth.
        TileView tile = _poolManager.Get(rawId);
        if (tile == null) return;

        _layers[(int)layer] = tile;
        tile.transform.SetParent(transform, false);
        tile.transform.localPosition = Vector3.zero;
        tile.SetSortingOrder(GetSortingOrder(layer));
    }

    public TileView GetTile(NodeLayer layer)
    {
        return _layers[(int)layer];
    }

    public void SetTile(NodeLayer layer, TileView tile, bool worldPositionStays = false)
    {
        // Safety net: setting a non-null tile into an already-occupied slot orphans
        // the previous TileView (still parented under this NodeView, invisible to model
        // → renders as a stuck overlap). The model layer should clear (null) first.
        // Surfaces upstream bugs like missing DestroySelf or Trigger commands.
        if (tile != null && _layers[(int)layer] != null && _layers[(int)layer] != tile)
        {
            Debug.LogWarning($"[NodeView] SetTile orphaning previous {layer} tile " +
                $"({_layers[(int)layer].TileType}) by {tile.TileType} — upstream forgot to null this slot");
        }

        _layers[(int)layer] = tile;
        if (tile == null) return;

        tile.transform.SetParent(transform, worldPositionStays);
        if (!worldPositionStays) tile.transform.localPosition = Vector3.zero;
        tile.SetSortingOrder(GetSortingOrder(layer));
    }
}
