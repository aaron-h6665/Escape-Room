using System.Collections.Generic;
using UnityEngine;

public static class ReplayIdentity
{
    public static string Resolve(Component component, string serializedId)
    {
        if (!string.IsNullOrWhiteSpace(serializedId))
        {
            return serializedId;
        }

        StableReplayId identity = component != null ? component.GetComponent<StableReplayId>() : null;
        if (identity != null && !string.IsNullOrWhiteSpace(identity.value)) return identity.value + ":" + component.GetType().Name;
        return BuildScenePath(component);
    }

    public static bool IsZero(Quaternion rotation)
    {
        return rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f;
    }

    static string BuildScenePath(Component component)
    {
        if (component == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        Transform current = component.transform;
        while (current != null)
        {
            parts.Add(current.GetSiblingIndex() + ":" + current.name);
            current = current.parent;
        }

        parts.Reverse();
        string sceneName = component.gameObject.scene.IsValid() ? component.gameObject.scene.name : "Scene";
        return component.GetType().Name + ":" + sceneName + ":" + string.Join("/", parts);
    }
}
