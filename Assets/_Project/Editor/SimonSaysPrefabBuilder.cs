using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimonSaysPrefabBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/New Scene.unity";
    const string PrefabPath = "Assets/_Project/Prefabs/SimonSays.prefab";

    public static void ReportSimonHierarchy()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = FindSimonRoot(scene);
        if (root == null)
        {
            throw new InvalidOperationException("Could not find the Simon model in New Scene.");
        }

        Debug.Log("SIMON_REPORT root=" + GetPath(root.transform));
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            Renderer renderer = transform.GetComponent<Renderer>();
            MeshFilter filter = transform.GetComponent<MeshFilter>();
            MeshCollider collider = transform.GetComponent<MeshCollider>();
            string materials = renderer == null
                ? string.Empty
                : string.Join(",", renderer.sharedMaterials.Select(material => material != null ? material.name : "null"));
            string mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : string.Empty;
            string colliderMesh = collider != null && collider.sharedMesh != null ? collider.sharedMesh.name : string.Empty;
            Vector3 center = renderer != null ? root.transform.InverseTransformPoint(renderer.bounds.center) : transform.localPosition;
            Debug.Log(
                "SIMON_REPORT path=" + GetPath(transform)
                + " renderer=" + (renderer != null)
                + " materials=" + materials
                + " mesh=" + mesh
                + " collider=" + (collider != null)
                + " colliderMesh=" + colliderMesh
                + " center=" + center.ToString("F4"));
        }
    }

    public static void BuildSimonSaysPrefab()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = FindSimonRoot(scene);
        if (root == null)
        {
            throw new InvalidOperationException("Could not find the Simon model in New Scene.");
        }

        ConfigureRootAndButtons(root);
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            root,
            PrefabPath,
            InteractionMode.AutomatedAction);
        if (prefab == null)
        {
            throw new InvalidOperationException("Unity could not save the Simon Says prefab.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("SIMON_BUILD_COMPLETE prefab=" + PrefabPath);
    }

    static void ConfigureRootAndButtons(GameObject root)
    {
        SetLayerRecursively(root, LayerMask.NameToLayer("Interactable"));

        SimonSaysController controller = root.GetComponent<SimonSaysController>();
        if (controller == null)
        {
            controller = root.AddComponent<SimonSaysController>();
        }

        AudioSource controllerAudio = root.GetComponent<AudioSource>();
        if (controllerAudio == null)
        {
            controllerAudio = root.AddComponent<AudioSource>();
        }
        ConfigureAudioSource(controllerAudio);

        Dictionary<SimonButtonColor, Transform> buttonTransforms = ResolveButtonTransforms(root);
        List<SimonSaysButton> buttons = new List<SimonSaysButton>();
        foreach (SimonButtonColor color in Enum.GetValues(typeof(SimonButtonColor)))
        {
            if (!buttonTransforms.TryGetValue(color, out Transform transform))
            {
                throw new InvalidOperationException("Could not map a Simon mesh to " + color + ".");
            }

            SimonSaysButton button = transform.GetComponent<SimonSaysButton>();
            if (button == null)
            {
                button = transform.gameObject.AddComponent<SimonSaysButton>();
            }

            AudioSource audioSource = transform.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = transform.gameObject.AddComponent<AudioSource>();
            }
            ConfigureAudioSource(audioSource);

            button.Configure(
                controller,
                color,
                transform.GetComponent<Renderer>(),
                audioSource,
                GetFrequency(color));
            EditorUtility.SetDirty(button);
            buttons.Add(button);
        }

        SimonSaysButton[] clockwise = SortClockwise(root.transform, buttons);
        BoxCollider startCollider = root.GetComponent<BoxCollider>();
        if (startCollider == null)
        {
            startCollider = root.AddComponent<BoxCollider>();
        }
        ConfigureStartCollider(root.transform, buttons, startCollider);

        string stableId = "9db0fb79-7b49-4be0-94ca-2e997e44e60d";
        controller.Configure(
            stableId,
            buttons.ToArray(),
            clockwise,
            controllerAudio,
            startCollider);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(root);
    }

    static Dictionary<SimonButtonColor, Transform> ResolveButtonTransforms(GameObject root)
    {
        Dictionary<SimonButtonColor, Transform> result = new Dictionary<SimonButtonColor, Transform>();
        List<Transform> candidates = root.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform != root.transform
                && transform.GetComponent<Renderer>() != null
                && transform.GetComponent<Collider>() != null)
            .ToList();

        foreach (Transform candidate in candidates)
        {
            string searchable = BuildSearchableName(candidate);
            foreach (SimonButtonColor color in Enum.GetValues(typeof(SimonButtonColor)))
            {
                if (!result.ContainsKey(color)
                    && searchable.IndexOf(color.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(color, candidate);
                }
            }
        }

        if (result.Count == 4)
        {
            return result;
        }

        throw new InvalidOperationException(
            "Automatic color mapping found " + result.Count
            + " of 4 buttons. Run ReportSimonHierarchy and update the mapping rules.");
    }

    static string BuildSearchableName(Transform transform)
    {
        Renderer renderer = transform.GetComponent<Renderer>();
        MeshFilter filter = transform.GetComponent<MeshFilter>();
        MeshCollider collider = transform.GetComponent<MeshCollider>();
        List<string> names = new List<string> { transform.name };
        if (renderer != null)
        {
            names.AddRange(renderer.sharedMaterials
                .Where(material => material != null)
                .Select(material => material.name));
        }
        if (filter != null && filter.sharedMesh != null)
        {
            names.Add(filter.sharedMesh.name);
        }
        if (collider != null && collider.sharedMesh != null)
        {
            names.Add(collider.sharedMesh.name);
        }
        return string.Join(" ", names);
    }

    static SimonSaysButton[] SortClockwise(Transform root, List<SimonSaysButton> buttons)
    {
        List<Vector3> centers = buttons
            .Select(button => root.InverseTransformPoint(button.GetComponent<Renderer>().bounds.center))
            .ToList();
        Vector3 variance = CalculateVariance(centers);
        int smallestAxis = variance.x <= variance.y && variance.x <= variance.z
            ? 0
            : variance.y <= variance.z ? 1 : 2;

        return buttons
            .OrderByDescending(button =>
            {
                Vector3 point = root.InverseTransformPoint(button.GetComponent<Renderer>().bounds.center);
                if (smallestAxis == 0) return Mathf.Atan2(point.z, point.y);
                if (smallestAxis == 1) return Mathf.Atan2(point.z, point.x);
                return Mathf.Atan2(point.y, point.x);
            })
            .ToArray();
    }

    static Vector3 CalculateVariance(List<Vector3> points)
    {
        Vector3 mean = points.Aggregate(Vector3.zero, (sum, point) => sum + point) / Mathf.Max(1, points.Count);
        Vector3 variance = Vector3.zero;
        foreach (Vector3 point in points)
        {
            Vector3 delta = point - mean;
            variance += new Vector3(delta.x * delta.x, delta.y * delta.y, delta.z * delta.z);
        }
        return variance / Mathf.Max(1, points.Count);
    }

    static void ConfigureStartCollider(
        Transform root,
        IEnumerable<SimonSaysButton> buttons,
        BoxCollider startCollider)
    {
        bool hasBounds = false;
        Bounds localBounds = new Bounds();
        foreach (SimonSaysButton button in buttons)
        {
            Renderer renderer = button != null
                ? button.GetComponent<Renderer>()
                : null;
            if (renderer == null)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = worldBounds.center
                            + Vector3.Scale(
                                worldBounds.extents,
                                new Vector3(x, y, z));
                        Vector3 localCorner = root.InverseTransformPoint(
                            worldCorner);
                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        if (!hasBounds)
        {
            throw new InvalidOperationException(
                "Could not calculate a Simon start interaction surface.");
        }

        const float thickness = 0.025f;
        startCollider.isTrigger = true;
        startCollider.center = new Vector3(
            localBounds.center.x,
            localBounds.max.y + thickness * 0.5f,
            localBounds.center.z);
        startCollider.size = new Vector3(
            Mathf.Max(0.05f, localBounds.size.x),
            thickness,
            Mathf.Max(0.05f, localBounds.size.z));
        startCollider.enabled = true;
    }

    static float GetFrequency(SimonButtonColor color)
    {
        switch (color)
        {
            case SimonButtonColor.Red: return 261.63f;
            case SimonButtonColor.Blue: return 329.63f;
            case SimonButtonColor.Green: return 392f;
            case SimonButtonColor.Yellow: return 523.25f;
            default: return 261.63f;
        }
    }

    static void ConfigureAudioSource(AudioSource audioSource)
    {
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 0.5f;
        audioSource.maxDistance = 12f;
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        if (layer < 0)
        {
            throw new InvalidOperationException("The Interactable layer does not exist.");
        }

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.layer = layer;
        }
    }

    static GameObject FindSimonRoot(Scene scene)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(gameObject => gameObject.name == "Simon");
    }

    static string GetPath(Transform transform)
    {
        List<string> names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }
}
