using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.U2D.Animation;

public class LevelUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text m_MovesText;
    [SerializeField] private Transform m_RequirementsContainer;
    [SerializeField] private RequirementSlotView m_RequirementSlotPrefab;
    [SerializeField] private SpriteLibraryAsset m_SpriteLibrary;
    [SerializeField] private LevelEndPopup m_LevelEndPopup;

    private Dictionary<TargetType, RequirementSlotView> m_SlotsByType;

    public void Initialize(IReadOnlyDictionary<TargetType, int> requirements, int moves)
    {
        SpawnRequirementSlots(requirements);

        m_MovesText.text = moves.ToString();
    }

    private Sprite GetRequirementSprite(TargetType type)
    {
        if (m_SpriteLibrary == null)
        {
            Debug.LogError("Requirement sprite library is not assigned.", this);
            return null;
        }

        switch (type)
        {
            case TargetType.Red:
            case TargetType.Green:
            case TargetType.Blue:
            case TargetType.Yellow:
                return m_SpriteLibrary.GetSprite("Matchable", type.ToString());
            case TargetType.Box:
                return m_SpriteLibrary.GetSprite("Box", "Box1");
            case TargetType.Vase:
                return m_SpriteLibrary.GetSprite("Vase", "Vase2");
            case TargetType.Rock:
                return m_SpriteLibrary.GetSprite("Stone", "Stone");
            default:
                return null;
        }
    }

    private void SpawnRequirementSlots(IReadOnlyDictionary<TargetType, int> requirements)
    {
        m_SlotsByType = new Dictionary<TargetType, RequirementSlotView>();
        foreach (var kvp in requirements)
        {
            var slot = Instantiate(m_RequirementSlotPrefab, m_RequirementsContainer);
            Sprite icon = GetRequirementSprite(kvp.Key);
            if (icon == null)
                Debug.LogWarning($"Requirement sprite not found for: {kvp.Key}", this);
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
