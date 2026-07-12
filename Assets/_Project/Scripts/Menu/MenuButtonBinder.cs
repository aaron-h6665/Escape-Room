using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class MenuButtonBinder
{
    public static Button BindByName(Component root, Button button, string buttonName, UnityAction action)
    {
        Button resolvedButton = button != null ? button : FindButton(root, buttonName);
        Bind(resolvedButton, action);
        return resolvedButton;
    }

    public static void Bind(Button button, UnityAction action)
    {
        if (button == null)
        {
            Debug.LogWarning("Menu button binding skipped because the button reference is missing.");
            return;
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }

    public static Button FindButton(Component root, string buttonName)
    {
        if (root != null)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == buttonName)
                {
                    return button;
                }
            }
        }

#if UNITY_2023_1_OR_NEWER
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
#endif
        foreach (Button button in buttons)
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        Debug.LogWarning("Could not find menu button named " + buttonName + ".");
        return null;
    }
}
