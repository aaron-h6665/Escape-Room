#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StudyLevelUtility
{
    const string ScenePath = "Assets/_Project/Scenes/Level.unity";
    const string Materials = "Assets/_Project/Materials/Study";
    [MenuItem("Tools/Escape Room/Finish Study Level")]
    public static void FinishLevel()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory(Materials);
        Material wall = CopyMaterial("Assets/LB3D/RandomBlocks/Materials/Floors/Dungeon-2/Dungeon-2-L0-V1.mat", "Masonry", new Color(0.95f, 0.9f, 0.82f));
        Material floor = CopyMaterial("Assets/LB3D/RandomBlocks/Materials/Floors/RandomMasonry/RandomMasonry-L0-v1.mat", "Floor", new Color(0.65f, 0.65f, 0.6f));
        string[] rooms = { "SimonSaysRoom", "CaesarCipherRoom", "SafeKeypadRoom" };
        Color[] accents = { new Color(1f, 0.82f, 0.61f), new Color(0.72f, 0.84f, 1f), new Color(1f, 0.9f, 0.72f) };
        for (int index = 0; index < rooms.Length; index++)
        {
            GameObject room = GameObject.Find(rooms[index]);
            if (room == null) throw new InvalidOperationException("Missing " + rooms[index]);
            Renderer[] surfaces = room.GetComponentsInChildren<Renderer>(true).Where(r =>
            {
                Vector3 size = r.bounds.size;
                bool horizontal = size.x > 3f && size.z > 3f && size.y < 0.7f;
                bool vertical = size.y > 2.5f && ((size.x > 3f && size.z < 0.7f) || (size.z > 3f && size.x < 0.7f));
                return (horizontal || vertical) && r.GetComponentInParent<Interactable>() == null;
            }).ToArray();
            foreach (var renderer in surfaces)
                renderer.sharedMaterials = Enumerable.Repeat(renderer.bounds.size.y < 0.7f ? floor : wall, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
            Bounds bounds = surfaces.Length > 0 ? surfaces[0].bounds : new Bounds(room.transform.position, new Vector3(8, 6, 8));
            foreach (var renderer in surfaces) bounds.Encapsulate(renderer.bounds);
            var marker = room.GetComponent<StudyRoom>() ?? room.AddComponent<StudyRoom>();
            marker.roomId = "room_" + (index + 1);
            // Adjacent bounds meet at the existing door planes; room labels never overlap.
            if (index == 0) bounds.SetMinMax(new Vector3(-7.3f, 1.3f, 9.95f), new Vector3(2.9f, 13f, 21f));
            else if (index == 1) bounds.SetMinMax(new Vector3(-7.3f, 1.3f, 0f), new Vector3(2.9f, 13f, 9.95f));
            else bounds.SetMinMax(new Vector3(-7.3f, 1.3f, -15f), new Vector3(2.9f, 13f, 0f));
            marker.worldBounds = bounds;
            Transform existing = room.transform.Find("StudyFillLight");
            var light = existing != null ? existing.GetComponent<Light>() : new GameObject("StudyFillLight", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(room.transform, true);
            light.transform.position = new Vector3(bounds.center.x, 5.3f, bounds.center.z);
            light.type = LightType.Point; light.color = accents[index]; light.intensity = 5f; light.range = Mathf.Max(bounds.size.x, bounds.size.z) * 1.2f;
            light.shadows = LightShadows.None;
            Debug.Log(room.name + " bounds " + bounds);
        }
        Material plinth = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Plinth.mat");
        if (plinth == null) { plinth = new Material(Shader.Find("Universal Render Pipeline/Lit")); plinth.name = "Plinth"; AssetDatabase.CreateAsset(plinth, Materials + "/Plinth.mat"); }
        plinth.SetColor("_BaseColor", new Color(0.22f, 0.27f, 0.3f));
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            if (renderer.name.Contains("Stand")) renderer.sharedMaterials = Enumerable.Repeat(plinth, renderer.sharedMaterials.Length).ToArray();
        FinishDoorFrames(plinth);
        EnsurePersistentBlueKey();
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(component is IReplayEventTarget)) continue;
            var identity = component.GetComponent<StableReplayId>() ?? component.gameObject.AddComponent<StableReplayId>();
            if (string.IsNullOrWhiteSpace(identity.value)) identity.value = Guid.NewGuid().ToString("N");
        }
        // Preserve the existing terminal while making space for its shared controller keyboard.
        foreach (var terminal in UnityEngine.Object.FindObjectsByType<CaesarAnswerTerminal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(terminal);
            var canvas = so.FindProperty("entryCanvas").objectReferenceValue as GameObject;
            if (canvas == null) continue;
            var scale = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scale != null) { scale.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution = new Vector2(1280, 720); }
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(1000, 70);
                string name = text.name.ToLowerInvariant();
                if (name.Contains("title")) text.rectTransform.anchoredPosition = new Vector2(0, 230);
                else if (text == so.FindProperty("entryText").objectReferenceValue) text.rectTransform.anchoredPosition = new Vector2(0, 140);
                else if (text == so.FindProperty("statusText").objectReferenceValue) text.rectTransform.anchoredPosition = new Vector2(0, 70);
            }
        }
        foreach (var text in UnityEngine.Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include))
        {
            if (text.name == "TerminalLabel")
            {
                text.transform.localScale = Vector3.one;
                text.transform.rotation = Quaternion.Euler(0, 180, 0);
                text.rectTransform.sizeDelta = new Vector2(1.45f, 0.88f);
                text.fontSize = 1.1f;
                text.text = "DECODED PHRASE\nTERMINAL";
            }
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.3f, 0.32f, 0.36f);
        RenderSettings.ambientEquatorColor = new Color(0.19f, 0.2f, 0.23f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.115f, 0.1f);
        RenderSettings.fog = false;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Validate();
        File.WriteAllLines("/tmp/study-scene.txt", UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.name + " | " + t.position + " | " + t.lossyScale + " | " + string.Join(",", t.GetComponents<Component>().Select(c => c != null ? c.GetType().Name : "Missing"))));
    }
    static void FinishDoorFrames(Material trim)
    {
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            if (renderer.name == "Frame_1m") renderer.sharedMaterials = Enumerable.Repeat(trim, renderer.sharedMaterials.Length).ToArray();
        var room = GameObject.Find("SafeKeypadRoom");
        string[] names = { "ExitLeftFrame", "ExitRightFrame", "ExitHeaderFrame" };
        Vector3[] centers = { new Vector3(-4.31f, 3.7f, -9.84f), new Vector3(-0.14f, 3.7f, -9.84f), new Vector3(-2.22f, 5.27f, -9.84f) };
        Vector3[] sizes = { new Vector3(0.16f, 3.16f, 0.12f), new Vector3(0.16f, 3.16f, 0.12f), new Vector3(4.33f, 0.16f, 0.12f) };
        for (int i = 0; i < names.Length; i++)
        {
            var existing = room.transform.Find(names[i]);
            var part = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = names[i]; part.transform.SetParent(room.transform, true);
            part.transform.position = centers[i]; part.transform.localScale = sizes[i];
            part.GetComponent<Renderer>().sharedMaterial = trim;
            var collider = part.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }
        var parent = GameObject.Find("SimonSaysRoom").transform;
        if (parent.Find("StorageCabinet") == null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meshes/cabinet 2.fbx");
            if (model != null)
            {
                var cabinet = UnityEngine.Object.Instantiate(model, parent); cabinet.name = "StorageCabinet";
                var renderers = cabinet.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    cabinet.transform.localScale *= 1.6f / Mathf.Max(0.01f, bounds.size.y);
                    bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    cabinet.transform.position += new Vector3(1.65f - bounds.center.x, 2.2f - bounds.min.y, 17.3f - bounds.center.z);
                    bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    var blocker = new GameObject("CabinetCollision", typeof(BoxCollider)); blocker.transform.SetParent(parent, true);
                    blocker.transform.position = bounds.center; blocker.GetComponent<BoxCollider>().size = bounds.size; blocker.layer = 7;
                }
            }
        }
    }

    static void EnsurePersistentBlueKey()
    {
        var choice = UnityEngine.Object.FindAnyObjectByType<ColorKeyChoicePuzzle>();
        if (choice == null) return;
        var serialized = new SerializedObject(choice);
        var original = serialized.FindProperty("blueKey").objectReferenceValue as ColorKeyChoiceInteractable;
        if (original == null) throw new InvalidOperationException("Blue key choice missing.");
        Transform existing = original.transform.parent.Find("AwardedBlueKey");
        GameObject reward = existing != null ? existing.gameObject : UnityEngine.Object.Instantiate(original.gameObject, original.transform.parent);
        reward.name = "AwardedBlueKey";
        var interaction = reward.GetComponent<ColorKeyChoiceInteractable>();
        if (interaction != null) UnityEngine.Object.DestroyImmediate(interaction);
        var identity = reward.GetComponent<StableReplayId>();
        if (identity != null) UnityEngine.Object.DestroyImmediate(identity);
        var pickup = reward.GetComponent<ItemPickupInteractable>() ?? reward.AddComponent<ItemPickupInteractable>();
        var so = new SerializedObject(pickup);
        so.FindProperty("id").stringValue = choice.ReplayTargetId + ":blue";
        so.FindProperty("item").objectReferenceValue = serialized.FindProperty("blueKeyItem").objectReferenceValue;
        so.FindProperty("destroyOnPickup").boolValue = false;
        so.FindProperty("isPickedUp").boolValue = true;
        so.FindProperty("promptMessage").stringValue = "Press E to pick up the blue key";
        so.ApplyModifiedPropertiesWithoutUndo(); reward.SetActive(false);
    }

    public static void CaptureViews()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory("/tmp/escape-views");
        Camera camera = UnityEngine.Object.FindAnyObjectByType<PlayerLook>().cam;
        Vector3[] positions = { new Vector3(-1.8f, 3.9f, 17f), new Vector3(-2.2f, 3.9f, 8f), new Vector3(-2.2f, 3.9f, -1f) };
        for (int i = 0; i < positions.Length; i++)
        {
            camera.transform.position = positions[i]; camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target; camera.Render();
            RenderTexture.active = target; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes("/tmp/escape-views/room-" + (i + 1) + ".png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    public static void FinishAndCapture() { FinishLevel(); CaptureViews(); WriteExamples(); }
    public static void FinishCaptureAndBuild() { FinishAndCapture(); BuildMac(); }

    public static void WriteExamples()
    {
        string root = "docs/examples";
        var normal = new ReplayRecordingData("normal", 1f / 30f) { recordingId = "synthetic-normal", attemptId = "synthetic-normal-attempt", participantCode = "EXAMPLE", duration = 30f, status = "completed", terminationReason = "game_completed", createdUtc = "2026-09-19T00:00:00Z", endedUtc = "2026-09-19T00:00:30Z" };
        normal.events.Add(new ReplayEventData { sequence = 1, recordingTime = 10f, eventKind = "simon_completed", objectId = "simon", objectCategory = "Puzzle", milestoneId = "simon_completed", succeeded = true });
        normal.events.Add(new ReplayEventData { sequence = 2, recordingTime = 20f, eventKind = "keypad_solved", objectId = "keypad", objectCategory = "Keypad", milestoneId = "keypad_completed", succeeded = true });
        var takeover = new ReplayRecordingData("takeover", 1f / 30f) { recordingId = "synthetic-takeover", attemptId = "synthetic-takeover-attempt", participantCode = "EXAMPLE", sourceRecordingId = normal.recordingId, duration = 8f, takeoverAtRecordingTime = 15f, takeoverAfterSequence = 1, takeoverRoomId = "room_3", status = "completed", terminationReason = "game_completed", createdUtc = "2026-09-19T00:01:00Z", endedUtc = "2026-09-19T00:01:08Z" };
        takeover.events.Add(new ReplayEventData { sequence = 1, recordingTime = 1f, eventKind = "keypad_input", objectId = "keypad", objectCategory = "Keypad", textValue = "4", succeeded = true });
        takeover.events.Add(new ReplayEventData { sequence = 2, recordingTime = 3f, eventKind = "keypad_solved", objectId = "keypad", objectCategory = "Keypad", milestoneId = "keypad_completed", succeeded = true });
        StudyExports.AtomicWrite(root + "/synthetic-normal.json", JsonUtility.ToJson(normal, true));
        StudyExports.AtomicWrite(root + "/synthetic-takeover.json", JsonUtility.ToJson(takeover, true));
        StudyExports.Write(root, normal, null); StudyExports.Write(root, takeover, normal);
    }

    static Material CopyMaterial(string source, string name, Color tint)
    {
        string path = Materials + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var original = AssetDatabase.LoadAssetAtPath<Material>(source);
        if (original == null) throw new InvalidOperationException(source);
        bool created = material == null;
        if (created) material = new Material(Shader.Find("EscapeRoom/World Stone")) { name = name };
        material.shader = Shader.Find("EscapeRoom/World Stone");
        material.shaderKeywords = Array.Empty<string>();
        Texture albedo = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture;
        material.SetTexture("_BaseMap", albedo); material.SetColor("_BaseColor", tint); material.SetFloat("_Tiling", 0.3f); material.SetFloat("_Mortar", name == "Masonry" ? 1f : 0f);
        if (created) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
        return material;
    }
    [MenuItem("Tools/Escape Room/Validate Study Level")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var targets = components.OfType<IReplayEventTarget>().ToArray();
        if (targets.GroupBy(t => t.ReplayTargetId).Any(g => g.Count() > 1)) throw new InvalidOperationException("Duplicate replay identity.");
        if (components.OfType<StudyRoom>().Count() != 3) throw new InvalidOperationException("Expected three room markers.");
        if (components.OfType<PlayerMotor>().Count(p => p.gameObject.activeInHierarchy) != 1) throw new InvalidOperationException("Expected one player.");
        if (components.OfType<SceneTransitionOnInteract>().Any()) throw new InvalidOperationException("Study rooms must remain in one scene.");
        if (!components.OfType<CaesarAnswerTerminal>().Any() || !components.OfType<EscapeRoomExit>().Any()) throw new InvalidOperationException("Missing required gameplay component.");
        Debug.Log("Study validation passed: 3 rooms, one player, stable unique replay targets, terminal and exit.");
    }
    public static void BuildMac()
    {
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/_Project/Scenes/MenuScene.unity", "Assets/_Project/Scenes/LoadingScene.unity", ScenePath, "Assets/_Project/Scenes/PauseScene.unity" },
            locationPathName = "/tmp/EscapeRoomStudy-Polish.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.Development });
        if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed: " + result.summary.result);
    }
}
#endif
