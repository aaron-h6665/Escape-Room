#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CaesarCipherSetupUtility
{
    const string ModelPath = "Assets/_Project/Meshes/CaesarCipher.fbx";
    const string PrefabPath = "Assets/_Project/Prefabs/Caesar Cipher Puzzle.prefab";
    const string LevelPath = "Assets/_Project/Scenes/Level.unity";
    const string InnerMaterialPath = "Assets/_Project/Materials/Materials/InnerCaesarCipherAlbedo.mat";
    const string OuterMaterialPath = "Assets/_Project/Materials/Materials/OuterCaesarCipherAlbedo.mat";
    const string FrameMaterialPath = "Assets/_Project/Materials/Materials/CaesarCipherSkeletonAlbedo.mat";

    [MenuItem("Tools/Escape Room/Build Caesar Cipher Puzzle")]
    public static void BuildFromMenu()
    {
        BuildPrefabAndLevel();
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildPrefabAndLevel();
            Debug.Log("Caesar cipher setup completed successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void BuildPrefabAndLevel()
    {
        EnsureInspectionLayer();
        GameObject prefab = BuildPrefab();
        WireIntoLevel(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static GameObject BuildPrefab()
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Material innerMaterial = AssetDatabase.LoadAssetAtPath<Material>(InnerMaterialPath);
        Material outerMaterial = AssetDatabase.LoadAssetAtPath<Material>(OuterMaterialPath);
        Material frameMaterial = AssetDatabase.LoadAssetAtPath<Material>(FrameMaterialPath);
        if (modelAsset == null || innerMaterial == null || outerMaterial == null || frameMaterial == null)
        {
            throw new InvalidOperationException("The Caesar cipher FBX and all three cipher materials must exist before building the prefab.");
        }

        GameObject root = new GameObject("Caesar Cipher Puzzle");
        try
        {
            root.layer = LayerMask.NameToLayer("Interactable");
            GameObject model = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (model == null)
            {
                throw new InvalidOperationException("Unity could not instantiate the Caesar cipher FBX.");
            }
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Cipher Visuals";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 0.2f;

            MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length < 3)
            {
                throw new InvalidOperationException($"The Caesar cipher model needs frame, inner-ring, and outer-ring meshes; only {renderers.Length} renderer(s) were found.");
            }

            MeshRenderer innerRenderer = FindRenderer(renderers, "inner", "Cylinder.002");
            MeshRenderer outerRenderer = FindRenderer(renderers, "outer", "Cylinder.001");
            MeshRenderer frameRenderer = renderers.FirstOrDefault(renderer => renderer != innerRenderer && renderer != outerRenderer);
            if (innerRenderer == null || outerRenderer == null || frameRenderer == null)
            {
                throw new InvalidOperationException("Could not identify the three imported Caesar cipher meshes. Name them Frame, Outer, and Inner in Blender or update the setup utility's fallbacks.");
            }

            innerRenderer.sharedMaterial = innerMaterial;
            outerRenderer.sharedMaterial = outerMaterial;
            frameRenderer.sharedMaterial = frameMaterial;
            innerRenderer.gameObject.name = "Inner Ring Mesh";
            outerRenderer.gameObject.name = "Outer Ring Mesh";
            frameRenderer.gameObject.name = "Cipher Frame Mesh";

            Bounds combinedBounds = frameRenderer.bounds;
            combinedBounds.Encapsulate(innerRenderer.bounds);
            combinedBounds.Encapsulate(outerRenderer.bounds);
            Vector3 commonCenter = combinedBounds.center;

            Transform wheelCenter = CreatePivot("Wheel Center", root.transform, root.transform.InverseTransformPoint(commonCenter));
            Transform innerPivot = CreatePivot("Inner Ring Pivot", root.transform, wheelCenter.localPosition);
            Transform outerPivot = CreatePivot("Outer Ring Pivot", root.transform, wheelCenter.localPosition);
            innerRenderer.transform.SetParent(innerPivot, true);
            outerRenderer.transform.SetParent(outerPivot, true);

            MeshCollider innerCollider = AddMeshCollider(innerRenderer);
            MeshCollider outerCollider = AddMeshCollider(outerRenderer);
            innerCollider.enabled = false;
            outerCollider.enabled = false;

            BoxCollider worldCollider = root.AddComponent<BoxCollider>();
            worldCollider.center = root.transform.InverseTransformPoint(combinedBounds.center);
            Vector3 localSize = root.transform.InverseTransformVector(combinedBounds.size);
            worldCollider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Max(0.15f, Mathf.Abs(localSize.y)), Mathf.Abs(localSize.z));

            CaesarCipherInteractable cipher = root.AddComponent<CaesarCipherInteractable>();
            SerializedObject serializedCipher = new SerializedObject(cipher);
            SetObject(serializedCipher, "wheelCenter", wheelCenter);
            SetObject(serializedCipher, "innerRing", innerPivot);
            SetObject(serializedCipher, "outerRing", outerPivot);
            SetVector(serializedCipher, "parentLocalRotationAxis", Vector3.up);
            SetVector(serializedCipher, "inspectionLocalViewNormal", Vector3.up);
            SetObject(serializedCipher, "worldInteractionCollider", worldCollider);
            SetObject(serializedCipher, "innerRingCollider", innerCollider);
            SetObject(serializedCipher, "outerRingCollider", outerCollider);
            SetObjects(serializedCipher, "inspectionRenderers", renderers);
            serializedCipher.FindProperty("promptMessage").stringValue = "Press E to inspect cipher";
            serializedCipher.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void WireIntoLevel(GameObject prefab)
    {
        if (prefab == null)
        {
            throw new InvalidOperationException("The Caesar cipher prefab was not created.");
        }

        Scene level = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);
        foreach (CaesarCipherInteractable existing in UnityEngine.Object.FindObjectsByType<CaesarCipherInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, level) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("Could not instantiate the Caesar cipher prefab in Level.unity.");
        }
        instance.name = "Caesar Cipher Decoder";

        GameObject keypadButton = GameObject.Find("Button");
        if (keypadButton != null)
        {
            instance.transform.position = keypadButton.transform.position + keypadButton.transform.right * 1.35f + keypadButton.transform.up * 0.2f;
            instance.transform.rotation = Quaternion.FromToRotation(Vector3.up, keypadButton.transform.forward);
        }
        else
        {
            instance.transform.position = new Vector3(0f, 1.1f, 2f);
        }

        NoteInteractable clueNote = UnityEngine.Object.FindObjectsByType<NoteInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(note => note.gameObject.name == "Note" ? 0 : 1)
            .FirstOrDefault();

        CaesarCipherInteractable cipher = instance.GetComponent<CaesarCipherInteractable>();
        SerializedObject serializedCipher = new SerializedObject(cipher);
        SetObject(serializedCipher, "sourceNote", clueNote);
        serializedCipher.FindProperty("id").stringValue = Guid.NewGuid().ToString();
        serializedCipher.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(level);
        if (!EditorSceneManager.SaveScene(level))
        {
            throw new InvalidOperationException("Could not save Level.unity after adding the Caesar cipher.");
        }
    }

    static MeshRenderer FindRenderer(MeshRenderer[] renderers, string materialToken, string nameFallback)
    {
        MeshRenderer byMaterial = renderers.FirstOrDefault(renderer => renderer.sharedMaterial != null &&
            renderer.sharedMaterial.name.IndexOf(materialToken, StringComparison.OrdinalIgnoreCase) >= 0);
        return byMaterial != null
            ? byMaterial
            : renderers.FirstOrDefault(renderer => string.Equals(renderer.gameObject.name, nameFallback, StringComparison.OrdinalIgnoreCase));
    }

    static Transform CreatePivot(string name, Transform parent, Vector3 localPosition)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = localPosition;
        pivot.transform.localRotation = Quaternion.identity;
        pivot.transform.localScale = Vector3.one;
        return pivot.transform;
    }

    static MeshCollider AddMeshCollider(MeshRenderer renderer)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            throw new InvalidOperationException($"{renderer.name} needs a MeshFilter before a ring collider can be created.");
        }
        MeshCollider collider = renderer.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = filter.sharedMesh;
        collider.convex = false;
        return collider;
    }

    static void EnsureInspectionLayer()
    {
        UnityEngine.Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
        if (tagManagerAsset == null)
        {
            throw new InvalidOperationException("Could not load ProjectSettings/TagManager.asset.");
        }
        SerializedObject tagManager = new SerializedObject(tagManagerAsset);
        SerializedProperty layers = tagManager.FindProperty("layers");
        SerializedProperty targetLayer = layers.GetArrayElementAtIndex(8);
        if (!string.IsNullOrEmpty(targetLayer.stringValue) && targetLayer.stringValue != "CipherInspection")
        {
            throw new InvalidOperationException($"Layer 8 is already named '{targetLayer.stringValue}'. Choose another free layer before building the cipher.");
        }
        targetLayer.stringValue = "CipherInspection";
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
    }

    static void SetVector(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        serializedObject.FindProperty(propertyName).vector3Value = value;
    }

    static void SetObjects<T>(SerializedObject serializedObject, string propertyName, T[] values) where T : UnityEngine.Object
    {
        SerializedProperty array = serializedObject.FindProperty(propertyName);
        array.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }
}
#endif
