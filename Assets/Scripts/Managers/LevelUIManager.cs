using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LevelUIManager : MonoBehaviour
{
    [Serializable]
    public struct TileSpriteEntry
    {
        public TargetType type;
        public Sprite sprite;
    }

    [SerializeField] private TMP_Text m_MovesText;
    [SerializeField] private Transform m_RequirementsContainer;
    [SerializeField] private RequirementSlotView m_RequirementSlotPrefab;
    [SerializeField] private TileSpriteEntry[] m_TileSprites;
    [SerializeField] private LevelEndPopup m_LevelEndPopup;

    private Dictionary<TargetType, RequirementSlotView> m_SlotsByType;
    private Dictionary<TargetType, Sprite> m_SpritesByType;

    public void Initialize(IReadOnlyDictionary<TargetType, int> requirements, int moves)
    {
        BuildSpriteLookup();
        SpawnRequirementSlots(requirements);

        m_MovesText.text = moves.ToString();
    }

    private void BuildSpriteLookup()
    {
        m_SpritesByType = new Dictionary<TargetType, Sprite>();
        foreach (var entry in m_TileSprites)
        {
            if (!m_SpritesByType.ContainsKey(entry.type))
                m_SpritesByType.Add(entry.type, entry.sprite);
            else
                Debug.LogWarning($"Duplicate TileSprite mapping for: {entry.type}");
        }
    }

    private void SpawnRequirementSlots(IReadOnlyDictionary<TargetType, int> requirements)
    {
        m_SlotsByType = new Dictionary<TargetType, RequirementSlotView>();
        foreach (var kvp in requirements)
        {
            var slot = Instantiate(m_RequirementSlotPrefab, m_RequirementsContainer);
            Sprite icon = m_SpritesByType.TryGetValue(kvp.Key, out var s) ? s : null;
            Debug.Log($"[UI] Slot for {kvp.Key}: lookup found = {icon != null}, sprite name = {(icon != null ? icon.name : "NULL")}");
            slot.Setup(icon, kvp.Value);
            m_SlotsByType.Add(kvp.Key, slot);
        }
    }

    public void HandleMovesChanged(int remaining)
    {
        m_MovesText.text = remaining.ToString();
    }

    public void HandleRequirementChanged(TargetType type, int remaining)
    {
        if (m_SlotsByType.TryGetValue(type, out var slot))
            slot.UpdateCount(remaining);
    }

    public void ShowEnd(bool won, int levelNumber) => m_LevelEndPopup.Show(won, levelNumber);
}
