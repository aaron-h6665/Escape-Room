using UnityEngine;
using UnityEngine.UI;
using System;

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

    public void Initialize(string inventoryId, Item item, Action<string> selectItemAction)
    {
        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (image != null)
        {
            image.sprite = item.icon;
            image.enabled = item.icon != null;
        }

        transform.localScale = Vector3.one;
        if (button != null)
        {
            button.onClick.AddListener(() => selectItemAction.Invoke(inventoryId));
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
        {
            background.color = selected ? selectedColor : normalColor;
        }
    }

    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }
}
