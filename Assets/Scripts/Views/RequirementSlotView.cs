using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RequirementSlotView : MonoBehaviour
{
    [SerializeField] private Image m_Icon;
    [SerializeField] private TMP_Text m_Count;
    [SerializeField] private GameObject m_Tick;

    public void Setup(Sprite icon, int initialCount)
    {
        Debug.Log($"[Slot] Setup called: m_Icon assigned = {m_Icon != null}, incoming icon = {(icon != null ? icon.name : "NULL")}");
        if (m_Icon != null) m_Icon.sprite = icon;
        UpdateCount(initialCount);
    }

    public void UpdateCount(int count)
    {
        if (count > 0)
        {
            m_Count.gameObject.SetActive(true);
            m_Count.text = count.ToString();
            if (m_Tick != null) m_Tick.SetActive(false);
        }
        else
        {
            m_Count.gameObject.SetActive(false);
            if (m_Tick != null) m_Tick.SetActive(true);
        }
    }
}
