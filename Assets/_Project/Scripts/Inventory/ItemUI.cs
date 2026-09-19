using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ItemUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    Image image;
    [SerializeField]
    Image background;
    [SerializeField]
    Button button;

    [Header("Selection")]
    [SerializeField]
    Color normalColor = Color.white;
    [SerializeField]
    Color selectedColor = Color.yellow;

    int slotIndex;
    Action<int> selectSlotAction;

    public void Initialize(int slotIndex, Action<int> selectSlotAction)
    {
        this.slotIndex = slotIndex;
        this.selectSlotAction = selectSlotAction;

        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        normalColor = new Color(0.14f, 0.17f, 0.21f, 0.94f);
        selectedColor = new Color(0.72f, 0.55f, 0.26f, 1f);
        transform.localScale = Vector3.one;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }

        SetSlot(null, false);
    }

    public void SetSlot(Item item, bool selected)
    {
        if (image != null)
        {
            image.sprite = item != null ? item.icon : null;
            image.enabled = item != null && item.icon != null;
        }

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
        {
            background.color = selected ? selectedColor : normalColor;
        }
    }

    void OnClicked()
    {
        if (ReplayManager.IsPlaybackActive() || Time.timeScale == 0f) return;
        selectSlotAction?.Invoke(slotIndex);
    }

    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }
}
