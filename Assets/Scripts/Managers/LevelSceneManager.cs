using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class LevelSceneManager : MonoBehaviour
{
    [System.Serializable] 
    public struct PoolPrefabEntry
    {
        public PoolType type;
        public TileView prefab;
    }

    [SerializeField] private BoardManager m_BoardManagerPrefab;
    [SerializeField] private LevelUIManager m_LevelUIManagerPrefab;
    [SerializeField] private BoardView m_BoardViewPrefab;
    [SerializeField] private BoardBuilder _boardBuilderPrefab;
    [SerializeField] private SpriteRenderer m_Background;
    [SerializeField] private float m_EndPopupDelay = 1f;

    private bool m_GameEnded;

    [SerializeField] private PoolPrefabEntry[] prefabEntries;
    private Dictionary<PoolType, TileView> prefabs;

    private BoardManager m_BoardManager;
    private BoardPoolManager m_BoardPoolManager;
    private LevelUIManager m_LevelUIManager;
    private LevelManager m_LevelManager;
    private BoardView _boardView;
    private BoardBuilder _boardBuilder;
    private void Awake()
    {
        // Build prefab dictionary from inspector entries
        prefabs = new Dictionary<PoolType, TileView>();
        foreach (var entry in prefabEntries)
        {
            if (!prefabs.ContainsKey(entry.type))
            {
                prefabs.Add(entry.type, entry.prefab);
            }
            else
            {
                Debug.LogWarning($"Duplicate PoolType in prefabEntries: {entry.type}");
            }
        }

        // 1. Load data
        int currentLevel = PlayerDataManager.Instance.CurrentLevel;
        LevelData levelData = LevelLoader.Instance.LoadLevelData(currentLevel);

        // 2. Create logic
        m_LevelManager = new LevelManager();
        m_LevelManager.StartLevel(levelData);
        m_BoardPoolManager = new BoardPoolManager(prefabs, levelData);

        // 3. Instantiate systems
        m_LevelUIManager = Instantiate(m_LevelUIManagerPrefab);
        Level level = m_LevelManager.Level;
        m_LevelUIManager.Initialize(level.Requirements, level.RemainingMoves);

        level.OnMovesChanged       += m_LevelUIManager.HandleMovesChanged;
        level.OnRequirementChanged += m_LevelUIManager.HandleRequirementChanged;

        m_BoardManager = Instantiate(m_BoardManagerPrefab);
        _boardView = Instantiate(m_BoardViewPrefab);

        _boardBuilder = Instantiate(_boardBuilderPrefab);
        _boardBuilder.BuildBoard(_boardView, levelData.grid_width, levelData.grid_height);
        AdjustCamera(levelData.grid_width, levelData.grid_height);

        if (m_Background != null)
            FitBackgroundToCamera(m_Background, Camera.main);

        m_BoardManager.Initialize(levelData, _boardView, m_BoardPoolManager);

        m_BoardManager.OnSwapCompleted  += level.ConsumeMove;
        m_BoardManager.OnTilesDestroyed += HandleTilesDestroyed;
        m_BoardManager.OnBoardSettled   += level.CheckGameEnd;

        level.OnLevelWon  += HandleLevelWon;
        level.OnLevelLost += HandleLevelLost;
    }

    private void HandleTilesDestroyed(Dictionary<TargetType, int> bucket)
    {
        Level level = m_LevelManager.Level;
        if (level == null) return;
        foreach (var kvp in bucket)
            level.UpdateRequirement(kvp.Key, kvp.Value);
    }

    private void HandleLevelWon()
    {
        if (m_GameEnded) return;
        m_GameEnded = true;
        m_BoardManager.LockInput();
        PlayerDataManager.Instance.CompleteLevel();
        StartCoroutine(ShowEndAfterDelay(true));
    }

    private void HandleLevelLost()
    {
        if (m_GameEnded) return;
        m_GameEnded = true;
        m_BoardManager.LockInput();
        StartCoroutine(ShowEndAfterDelay(false));
    }

    private IEnumerator ShowEndAfterDelay(bool won)
    {
        yield return new WaitForSeconds(m_EndPopupDelay);
        m_LevelUIManager.ShowEnd(won, m_LevelManager.Level.LevelNumber);
    }

    private void OnDestroy()
    {
        if (m_LevelManager?.Level == null) return;
        Level level = m_LevelManager.Level;

        if (m_LevelUIManager != null)
        {
            level.OnMovesChanged       -= m_LevelUIManager.HandleMovesChanged;
            level.OnRequirementChanged -= m_LevelUIManager.HandleRequirementChanged;
        }

        if (m_BoardManager != null)
        {
            m_BoardManager.OnSwapCompleted  -= level.ConsumeMove;
            m_BoardManager.OnTilesDestroyed -= HandleTilesDestroyed;
            m_BoardManager.OnBoardSettled   -= level.CheckGameEnd;
        }

        level.OnLevelWon  -= HandleLevelWon;
        level.OnLevelLost -= HandleLevelLost;
    }

    private void FitBackgroundToCamera(SpriteRenderer sr, Camera cam)
    {
        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * cam.aspect;

        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        float scaleX = camWidth / spriteWidth;
        float scaleY = camHeight / spriteHeight;

        float scale = Mathf.Max(scaleX, scaleY); // ekranı tamamen doldur

        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, sr.transform.position.z);
    }

    private void AdjustCamera(int gridWidth, int gridHeight)
    {
        Camera cam = Camera.main;

        float cellSize = GameConfig.CELL_SIZE;
        float borderSize = GameConfig.BORDER_SIZE;
        float screenAspect = (float)Screen.width / Screen.height;

        float boardW = gridWidth * cellSize + borderSize * 2f;
        float boardH = gridHeight * cellSize + borderSize * 2f;

        // Fit board width to screen width (this is what Royal Match does)
        float horizontalPadding = 0f;
        float orthoSize = (boardW + horizontalPadding) / (2f * screenAspect);

        cam.orthographicSize = orthoSize;

        // Now figure out where to put the camera
        // Total visible world height:
        float visibleH = orthoSize * 2f;

        // We want the board near the top, not centered
        // Board center is at 0, so shift camera up to push board down from top
        float boardTopY = boardH / 2f;
        float cameraTopY = cam.transform.position.y + orthoSize;

        // Place board so its top edge is slightly below the screen top
        float topMargin = cellSize * 5f; // space for UI bar, tweak this
        float cameraY = boardTopY + topMargin - orthoSize;

        cam.transform.position = new Vector3(0f, cameraY, -10f);

        cam.enabled = false;
        cam.enabled = true;

        Debug.Log($"Camera - ortho: {orthoSize}, pos: {cam.transform.position}, visibleH: {visibleH}, boardH: {boardH}");
    }
}