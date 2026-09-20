#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EscapeRoomLevelBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Level.unity";
    const string MaterialFolder = "Assets/_Project/Materials/Prototype";
    const string PaperTexturePath = "Assets/Jovial Games/Paper_Texture_Bundle/1080/Paper_6.png";
    const int InteractableLayer = 6;
    const int BlockerLayer = 7;

    static Material wallMaterial;
    static Material floorMaterial;
    static Material darkMaterial;
    static Material redMaterial;
    static Material blueMaterial;
    static Material greenMaterial;
    static Material yellowMaterial;
    static Material paperMaterial;
    static Material displayMaterial;
    static Material keypadPanelMaterial;
    static Material keypadButtonMaterial;

    [MenuItem("Tools/Escape Room/Rebuild Level Prototype")]
    public static void BuildFromMenu() => Build();

    [MenuItem("Tools/Escape Room/Upgrade Prototype Keypad")]
    public static void UpgradePrototypeKeypad()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureAssets();
        Transform gameplay = GameObject.Find("EscapeRoomPrototype/Gameplay")?.transform;
        if (gameplay == null) throw new InvalidOperationException("EscapeRoomPrototype/Gameplay is missing.");

        Transform oldKeypad = gameplay.Find("Room3_NumericKeypad");
        if (oldKeypad != null) UnityEngine.Object.DestroyImmediate(oldKeypad.gameObject);
        PrototypeSlidingDoor finalDoor = gameplay.Find("Room3_FinalDoors")?.GetComponent<PrototypeSlidingDoor>();
        if (finalDoor == null) throw new InvalidOperationException("Room3_FinalDoors is missing its sliding-door component.");

        CreateKeypad(gameplay, finalDoor);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Prototype keypad upgraded with a recessed viewport, fitted status text, textured controls, and delete input.");
    }

    [MenuItem("Tools/Escape Room/Validate Level Prototype")]
    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find("EscapeRoomPrototype");
        if (root == null || !root.activeInHierarchy) throw new InvalidOperationException("EscapeRoomPrototype root is missing or inactive.");
        RequireCount<SimonSaysController>(root, 1);
        RequireCount<ColorKeyChoicePuzzle>(root, 1);
        RequireCount<NumericKeypadPuzzle>(root, 1);
        RequireCount<NumericKeypadButton>(root, 13);
        RequireCount<NoteInteractable>(root, 3);
        RequireCount<PrototypeSlidingDoor>(root, 3);
        RequireCount<EscapeRoomExit>(root, 1);
        if (root.GetComponentInChildren<SceneTransitionOnInteract>(true) != null)
            throw new InvalidOperationException("The prototype must progress through rooms in one scene.");

        MonoBehaviour[] statefulObjects = root.GetComponentsInChildren<MonoBehaviour>(true)
            .Where(component => component is SimonSaysController
                || component is NoteInteractable
                || component is ColorKeyChoicePuzzle
                || component is NumericKeypadPuzzle
                || component is PrototypeSlidingDoor
                || component is EscapeRoomExit)
            .ToArray();
        foreach (MonoBehaviour statefulObject in statefulObjects)
        {
            if (!(statefulObject is IDataPersistence) || !(statefulObject is IReplayObject))
                throw new InvalidOperationException(statefulObject.name + " must implement IDataPersistence and IReplayObject.");
        }

        NumericKeypadPuzzle keypad = root.GetComponentInChildren<NumericKeypadPuzzle>(true);
        SerializedObject serializedKeypad = new SerializedObject(keypad);
        if (serializedKeypad.FindProperty("correctCode").stringValue != "4271")
            throw new InvalidOperationException("The final keypad code must combine the two clues into 4271.");
        HashSet<string> keypadValues = root.GetComponentsInChildren<NumericKeypadButton>(true)
            .Select(button => new SerializedObject(button).FindProperty("value").stringValue)
            .ToHashSet();
        string[] requiredKeys = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "clear", "delete", "enter" };
        if (requiredKeys.Any(value => !keypadValues.Contains(value)))
            throw new InvalidOperationException("The numeric keypad is missing one or more digits, CLEAR, DELETE, or ENTER.");

        if (root.GetComponentsInChildren<ColorKeyChoiceInteractable>(true).Length != 0)
            throw new InvalidOperationException("The Caesar room must not contain colored key choices.");
        RequireCount<CaesarAnswerTerminal>(root, 1);

        PlayerMotor[] activePlayers = UnityEngine.Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
        if (activePlayers.Length != 1) throw new InvalidOperationException("Exactly one active PlayerMotor is required; found " + activePlayers.Length + ".");
        if (activePlayers[0].gameObject.name != "Player (Simon Says Room)") throw new InvalidOperationException("The active player is not the preserved Simon Says room player.");
        if (UnityEngine.Object.FindFirstObjectByType<DataPersistenceManager>() == null) throw new InvalidOperationException("DataPersistenceManager is missing.");
        if (UnityEngine.Object.FindFirstObjectByType<ReplayManager>() == null) throw new InvalidOperationException("ReplayManager is missing.");
        foreach (GameObject gameObject in root.GetComponentsInChildren<Transform>(true).Select(value => value.gameObject))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) != 0)
                throw new InvalidOperationException("Missing script on " + gameObject.name + ".");
        }
        string[] clueTexts = root.GetComponentsInChildren<NoteInteractable>(true).Select(note => note.ClueText).ToArray();
        if (!clueTexts.Any(text => text.Contains(CaesarCipherMath.ShiftSymbols(CaesarPuzzleClue.Solution, root.GetComponentInChildren<SimonSaysController>().FinalGreenCount)))) throw new InvalidOperationException("The Caesar phrase clue is missing.");
        if (!clueTexts.Any(text => text.Contains("42")) || !clueTexts.Any(text => text.Contains("71"))) throw new InvalidOperationException("The split keypad clues are missing.");
        Debug.Log("Level validation passed: continuous three-room flow, 3 persisted doors, 3 replayable notes, Caesar phrase terminal, 13-button keypad, exit trigger, and victory UI are present.");
    }

    static void RequireCount<T>(GameObject root, int expected) where T : Component
    {
        int actual = root.GetComponentsInChildren<T>(true).Length;
        if (actual != expected) throw new InvalidOperationException(typeof(T).Name + " count was " + actual + "; expected " + expected + ".");
    }

    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<StudyRoom>(true) != null))
            throw new InvalidOperationException("Prototype rebuild stopped: Level contains the configured study environment. Use Tools/Escape Room/Repair Current Level In Place to update this level without replacing its rooms.");
        EnsureAssets();

        GameObject previousPrototype = GameObject.Find("EscapeRoomPrototype");
        if (previousPrototype != null) UnityEngine.Object.DestroyImmediate(previousPrototype);

        PlayerMotor playerMotor = SelectSimonRoomPlayer();
        GameObject player = playerMotor != null ? playerMotor.gameObject : null;
        if (player == null) throw new InvalidOperationException("Level requires its configured Player before the prototype can be assembled.");

        PlayerMotor[] playersBeforeCleanup = UnityEngine.Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PreservePlayerFromPrefab(player);
        TransferPlayerConfigurationAndRemoveDuplicates(player, playersBeforeCleanup);
        DisableLegacyGameplayRoots(scene, player);

        GameObject root = new GameObject("EscapeRoomPrototype");
        GameObject environment = Child(root, "Environment");
        GameObject gameplay = Child(root, "Gameplay");
        GameObject ui = Child(root, "PrototypeUI");

        BuildEnvironment(environment.transform);
        PrototypeSlidingDoor roomOneDoor = CreateSlidingDoor(gameplay.transform, "Room1_SimonExitDoor", new Vector3(0f, 1.5f, 5f), "room1_simon_exit");
        PrototypeSlidingDoor roomTwoDoor = CreateSlidingDoor(gameplay.transform, "Room2_KeyExitDoor", new Vector3(0f, 1.5f, 15f), "room2_key_exit");
        PrototypeSlidingDoor finalDoor = CreateSlidingDoor(gameplay.transform, "Room3_FinalDoors", new Vector3(0f, 1.5f, 25f), "room3_final_exit");

        SimonSaysController simon = CreateSimonSays(gameplay.transform, new Vector3(0f, 1.65f, 1.8f));
        UnityEventTools.AddPersistentListener(simon.OnPuzzleCompleted, roomOneDoor.Open);
        EditorUtility.SetDirty(simon);

        CreateNote(gameplay.transform, "Room2_CaesarNote", new Vector3(-3.8f, 1.4f, 8.2f), Quaternion.Euler(0f, 90f, 0f),
            CaesarPuzzleClue.ForSimon(UnityEngine.Object.FindFirstObjectByType<SimonSaysController>()), "room2_caesar_note");
        CreatePhraseTerminal(gameplay.transform, roomTwoDoor);

        CreateNote(gameplay.transform, "Room3_FirstDigitsNote", new Vector3(-3.8f, 1.4f, 18.1f), Quaternion.Euler(0f, 90f, 0f),
            "TORN CODE — LEFT HALF\n\nThe code begins with:\n\n42", "room3_note_first_digits");
        CreateNote(gameplay.transform, "Room3_LastDigitsNote", new Vector3(3.8f, 1.4f, 18.1f), Quaternion.Euler(0f, -90f, 0f),
            "TORN CODE — RIGHT HALF\n\nThe code ends with:\n\n71", "room3_note_last_digits");
        CreateKeypad(gameplay.transform, finalDoor);
        CreateVictory(ui.transform, gameplay.transform);
        CreateSigns(environment.transform);

        ConfigurePlayer(player);
        EnsureManagers();
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Escape-room prototype rebuilt in Level: Simon Says -> Caesar phrase -> four-digit keypad -> exit victory.");
    }

    static void EnsureAssets()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials")) AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Prototype");
        }

        wallMaterial = GetMaterial("Wall", new Color(0.16f, 0.19f, 0.24f), 0.1f);
        floorMaterial = GetMaterial("Floor", new Color(0.08f, 0.095f, 0.12f), 0.15f);
        darkMaterial = GetMaterial("DarkMetal", new Color(0.025f, 0.035f, 0.05f), 0.65f);
        redMaterial = GetMaterial("RedKey", new Color(0.85f, 0.055f, 0.04f), 0.45f);
        blueMaterial = GetMaterial("BlueKey", new Color(0.025f, 0.24f, 1f), 0.45f);
        greenMaterial = GetMaterial("SimonGreen", new Color(0.04f, 0.75f, 0.12f), 0.3f);
        yellowMaterial = GetMaterial("SimonYellow", new Color(1f, 0.65f, 0.03f), 0.3f);
        paperMaterial = GetMaterial("NotePaper", new Color(0.86f, 0.78f, 0.56f), 0f);
        paperMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PaperTexturePath);
        if (paperMaterial.HasProperty("_Smoothness")) paperMaterial.SetFloat("_Smoothness", 0.08f);
        displayMaterial = GetMaterial("Display", new Color(0.02f, 0.12f, 0.15f), 0.25f);
        keypadPanelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Keypad/Materials/Standard/keypad_panelmaterial_standard.mat");
        keypadButtonMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Keypad/Materials/Standard/keypad_buttonMaterial_standard.mat");
        if (keypadPanelMaterial == null) keypadPanelMaterial = darkMaterial;
        if (keypadButtonMaterial == null) keypadButtonMaterial = wallMaterial;

    }

    static Material GetMaterial(string name, Color color, float metallic)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", metallic > 0.3f ? 0.72f : 0.28f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void PreservePlayerFromPrefab(GameObject player)
    {
        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(player);
        if (prefabRoot != null) PrefabUtility.UnpackPrefabInstance(prefabRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        player.transform.SetParent(null, true);
        player.SetActive(true);
    }

    static PlayerMotor SelectSimonRoomPlayer()
    {
        PlayerMotor[] players = UnityEngine.Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PlayerMotor markedPlayer = players.FirstOrDefault(value => value != null && value.gameObject.name == "Player (Simon Says Room)");
        if (markedPlayer != null) return markedPlayer;

        foreach (PlayerMotor candidate in players)
        {
            for (Transform ancestor = candidate.transform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor.name.IndexOf("Simon", StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            }
        }

        return players.FirstOrDefault();
    }

    static void TransferPlayerConfigurationAndRemoveDuplicates(GameObject keptPlayer, PlayerMotor[] originalPlayers)
    {
        PlayerMotor configuredDuplicate = originalPlayers.FirstOrDefault(value => value != null && value.gameObject != keptPlayer && value.GetComponent<Inventory>() != null);
        PlayerUI keptUi = keptPlayer.GetComponent<PlayerUI>() ?? keptPlayer.AddComponent<PlayerUI>();
        Inventory keptInventory = keptPlayer.GetComponent<Inventory>() ?? keptPlayer.AddComponent<Inventory>();
        PlayerInventoryInput keptInventoryInput = keptPlayer.GetComponent<PlayerInventoryInput>() ?? keptPlayer.AddComponent<PlayerInventoryInput>();

        if (configuredDuplicate != null)
        {
            PlayerUI sourceUi = configuredDuplicate.GetComponent<PlayerUI>();
            Inventory sourceInventory = configuredDuplicate.GetComponent<Inventory>();
            PlayerInventoryInput sourceInventoryInput = configuredDuplicate.GetComponent<PlayerInventoryInput>();
            if (sourceUi != null) EditorUtility.CopySerialized(sourceUi, keptUi);
            if (sourceInventory != null) EditorUtility.CopySerialized(sourceInventory, keptInventory);
            if (sourceInventoryInput != null) EditorUtility.CopySerialized(sourceInventoryInput, keptInventoryInput);
        }

        InputManager input = keptPlayer.GetComponent<InputManager>() ?? keptPlayer.AddComponent<InputManager>();
        Set(keptPlayer.GetComponent<PlayerMotor>(), "inputManager", input);
        Set(keptInventoryInput, "inputManager", input);
        Set(keptInventoryInput, "inventory", keptInventory);
        keptPlayer.name = "Player (Simon Says Room)";
        keptPlayer.tag = "Player";

        foreach (PlayerMotor duplicate in UnityEngine.Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (duplicate != null && duplicate.gameObject != keptPlayer)
                UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
        }
    }

    static void DisableLegacyGameplayRoots(Scene scene, GameObject player)
    {
        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            if (candidate == player || candidate.name == "EscapeRoomPrototype") continue;
            bool isLegacyRoom = candidate.name == "SafeKeypadRoom"
                || candidate.name == "CaesarCipherRoom"
                || candidate.name == "SimonSaysRoom";
            if (isLegacyRoom)
            {
                candidate.SetActive(false);
                continue;
            }
            bool keep = candidate.GetComponentInChildren<Canvas>(true) != null
                || candidate.GetComponentInChildren<EventSystem>(true) != null
                || candidate.GetComponentInChildren<DataPersistenceManager>(true) != null
                || candidate.GetComponentInChildren<ReplayManager>(true) != null
                || candidate.GetComponentInChildren<PauseManager>(true) != null
                || candidate.GetComponentInChildren<TimeSpentManager>(true) != null
                || candidate.GetComponentInChildren<UnityEngine.Rendering.Volume>(true) != null
                || candidate.GetComponentInChildren<Light>(true) != null;
            if (!keep) candidate.SetActive(false);
        }

        foreach (SimonSaysController oldSimon in UnityEngine.Object.FindObjectsByType<SimonSaysController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (oldSimon.transform.root.gameObject != player) oldSimon.transform.root.gameObject.SetActive(false);
        }
    }

    static void BuildEnvironment(Transform parent)
    {
        for (int room = 0; room < 3; room++)
        {
            float center = room * 10f;
            Cube(parent, "Room" + (room + 1) + "_Floor", new Vector3(0f, -0.15f, center), new Vector3(10f, 0.3f, 10f), floorMaterial, BlockerLayer);
            Cube(parent, "Room" + (room + 1) + "_LeftWall", new Vector3(-5f, 1.5f, center), new Vector3(0.3f, 3f, 10f), wallMaterial, BlockerLayer);
            Cube(parent, "Room" + (room + 1) + "_RightWall", new Vector3(5f, 1.5f, center), new Vector3(0.3f, 3f, 10f), wallMaterial, BlockerLayer);
            Cube(parent, "Room" + (room + 1) + "_Ceiling", new Vector3(0f, 3.35f, center), new Vector3(10f, 0.3f, 10f), wallMaterial, BlockerLayer);
            CreateDoorwayWall(parent, center + 5f, "Room" + (room + 1) + "_NorthWall");
            CreateLight(parent, new Vector3(0f, 2.8f, center), room == 1 ? new Color(0.55f, 0.72f, 1f) : new Color(1f, 0.72f, 0.48f));
        }
        Cube(parent, "StartingWall", new Vector3(0f, 1.5f, -5f), new Vector3(10f, 3f, 0.3f), wallMaterial, BlockerLayer);
        Cube(parent, "ExitPlatform", new Vector3(0f, -0.1f, 28.5f), new Vector3(6f, 0.2f, 7f), floorMaterial, BlockerLayer);
        Cube(parent, "ExitLeftRail", new Vector3(-3f, 0.55f, 28.5f), new Vector3(0.18f, 1.1f, 7f), wallMaterial, BlockerLayer);
        Cube(parent, "ExitRightRail", new Vector3(3f, 0.55f, 28.5f), new Vector3(0.18f, 1.1f, 7f), wallMaterial, BlockerLayer);
    }

    static void CreateDoorwayWall(Transform parent, float z, string name)
    {
        Cube(parent, name + "_Left", new Vector3(-3.25f, 1.5f, z), new Vector3(3.5f, 3f, 0.3f), wallMaterial, BlockerLayer);
        Cube(parent, name + "_Right", new Vector3(3.25f, 1.5f, z), new Vector3(3.5f, 3f, 0.3f), wallMaterial, BlockerLayer);
        Cube(parent, name + "_Header", new Vector3(0f, 3f, z), new Vector3(3f, 0.7f, 0.3f), wallMaterial, BlockerLayer);
    }

    static PrototypeSlidingDoor CreateSlidingDoor(Transform parent, string name, Vector3 position, string id)
    {
        GameObject root = Child(parent.gameObject, name);
        root.transform.position = position;
        Transform left = Cube(root.transform, "LeftPanel", new Vector3(-0.76f, 0f, 0f), new Vector3(1.48f, 3f, 0.22f), darkMaterial, BlockerLayer).transform;
        Transform right = Cube(root.transform, "RightPanel", new Vector3(0.76f, 0f, 0f), new Vector3(1.48f, 3f, 0.22f), darkMaterial, BlockerLayer).transform;
        PrototypeSlidingDoor door = root.AddComponent<PrototypeSlidingDoor>();
        Set(door, "id", id);
        Set(door, "leftPanel", left);
        Set(door, "rightPanel", right);
        Set(door, "leftOpenOffset", new Vector3(-1.55f, 0f, 0f));
        Set(door, "rightOpenOffset", new Vector3(1.55f, 0f, 0f));
        return door;
    }

    static SimonSaysController CreateSimonSays(Transform parent, Vector3 position)
    {
        GameObject console = Cube(parent, "Room1_SimonSays", position, new Vector3(4.2f, 2.8f, 0.35f), darkMaterial, InteractableLayer);
        console.transform.rotation = Quaternion.identity;
        SimonSaysController controller = console.AddComponent<SimonSaysController>();
        Set(controller, "id", "room1_simon_says");
        Set(controller, "startInteractionCollider", console.GetComponent<Collider>());
        Set(controller, "highlightOnFocus", false);

        SimonSaysButton[] buttons = new SimonSaysButton[4];
        SimonButtonColor[] colors = { SimonButtonColor.Red, SimonButtonColor.Blue, SimonButtonColor.Green, SimonButtonColor.Yellow };
        Material[] materials = { redMaterial, blueMaterial, greenMaterial, yellowMaterial };
        Vector3[] offsets = { new Vector3(-1.05f, 0.62f, -0.24f), new Vector3(1.05f, 0.62f, -0.24f), new Vector3(1.05f, -0.62f, -0.24f), new Vector3(-1.05f, -0.62f, -0.24f) };
        for (int index = 0; index < 4; index++)
        {
            GameObject buttonObject = Cube(console.transform, colors[index] + "Button", offsets[index], new Vector3(1.45f, 0.82f, 0.28f), materials[index], InteractableLayer);
            buttons[index] = buttonObject.AddComponent<SimonSaysButton>();
            Set(buttons[index], "controller", controller);
            Set(buttons[index], "buttonColor", colors[index]);
            Set(buttons[index], "buttonIndex", index);
            Set(buttons[index], "buttonRenderer", buttonObject.GetComponent<Renderer>());
            Set(buttons[index], "pressedLocalOffset", new Vector3(0f, 0f, 0.07f));
        }
        Set(controller, "buttons", buttons);
        Set(controller, "clockwiseButtons", buttons);
        WorldText(parent, "SimonInstruction", "ROOM 1 — MEMORY\nRepeat the growing color sequence", new Vector3(0f, 2.85f, 1.62f), Quaternion.identity, 0.28f, Color.white, TextAlignmentOptions.Center);
        return controller;
    }

    static void CreatePhraseTerminal(Transform parent, PrototypeSlidingDoor exitDoor)
    {
        GameObject puzzleObject = Child(parent.gameObject, "Room2_CaesarPhrasePuzzle");
        ColorKeyChoicePuzzle puzzle = puzzleObject.AddComponent<ColorKeyChoicePuzzle>();
        Set(puzzle, "id", "room2_caesar_phrase");
        Set(puzzle, "exitDoor", exitDoor);

        GameObject terminalObject = Cube(parent, "Room2_DecodedPhraseTerminal", new Vector3(0f, 1.35f, 12.35f), new Vector3(3.4f, 2.2f, 0.3f), darkMaterial, InteractableLayer);
        CaesarAnswerTerminal terminal = terminalObject.AddComponent<CaesarAnswerTerminal>();
        Set(terminal, "puzzle", puzzle);
        Set(terminal, "promptMessage", "Press E to enter the decoded phrase");

        TMP_Text label = WorldText(terminalObject.transform, "TerminalLabel", "DECODED PHRASE\nTERMINAL", new Vector3(0f, 0.25f, -0.2f), Quaternion.identity, 0.22f, new Color(0.35f, 0.95f, 1f), TextAlignmentOptions.Center);
        label.rectTransform.sizeDelta = new Vector2(10f, 4f);
        Set(puzzle, "feedbackText", label);

        GameObject canvasObject = new GameObject("CaesarAnswerEntryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        canvasObject.transform.SetParent(terminalObject.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasObject.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.045f, 0.97f);

        TextMeshProUGUI title = OverlayText(canvasObject.transform, "TerminalTitle", new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.82f), 38f, "ENTER THE DECODED PHRASE");
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.93f, 0.73f, 0.3f);
        TextMeshProUGUI entry = OverlayText(canvasObject.transform, "TypedPhrase", new Vector2(0.2f, 0.43f), new Vector2(0.8f, 0.62f), 46f, "_");
        entry.fontStyle = FontStyles.Bold;
        TextMeshProUGUI status = OverlayText(canvasObject.transform, "TerminalStatus", new Vector2(0.18f, 0.2f), new Vector2(0.82f, 0.35f), 24f, "Type the decoded phrase, then press Enter. Escape cancels.");
        status.color = new Color(0.75f, 0.8f, 0.88f);
        Set(terminal, "entryCanvas", canvasObject);
        Set(terminal, "entryText", entry);
        Set(terminal, "statusText", status);
        canvasObject.SetActive(false);
    }

    static TextMeshProUGUI OverlayText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float fontSize, string value)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.anchorMin = anchorMin;
        text.rectTransform.anchorMax = anchorMax;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    static void CreateKeypad(Transform parent, PrototypeSlidingDoor finalDoor)
    {
        GameObject root = Child(parent.gameObject, "Room3_NumericKeypad");
        root.transform.localPosition = new Vector3(0f, 1.95f, 23.8f);
        GameObject housing = Cube(root.transform, "TexturedHousing", Vector3.zero, new Vector3(3.4f, 3.9f, 0.32f), keypadPanelMaterial, BlockerLayer);
        housing.GetComponent<Renderer>().sharedMaterial = keypadPanelMaterial;

        NumericKeypadPuzzle keypad = root.AddComponent<NumericKeypadPuzzle>();
        Set(keypad, "id", "room3_keypad");
        Set(keypad, "correctCode", "4271");
        Set(keypad, "codeLength", 4);
        Set(keypad, "finalDoor", finalDoor);

        Cube(root.transform, "DisplayBezel", new Vector3(0f, 1.28f, -0.24f), new Vector3(2.75f, 0.68f, 0.16f), keypadPanelMaterial, BlockerLayer);
        Cube(root.transform, "DisplayViewport", new Vector3(0f, 1.28f, -0.34f), new Vector3(2.38f, 0.38f, 0.08f), displayMaterial, BlockerLayer);
        TMP_Text displayText = WorldText(root.transform, "DisplayText", "----", new Vector3(0f, 1.28f, -0.395f), Quaternion.identity, 0.3f, new Color(0.35f, 0.95f, 1f), TextAlignmentOptions.Center);
        displayText.rectTransform.sizeDelta = new Vector2(2.18f, 0.32f);
        displayText.enableAutoSizing = true;
        displayText.fontSizeMin = 0.14f;
        displayText.fontSizeMax = 0.3f;
        displayText.enableWordWrapping = false;
        displayText.overflowMode = TextOverflowModes.Truncate;
        Set(keypad, "displayText", displayText);

        string[] values = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "clear", "0", "enter" };
        for (int index = 0; index < values.Length; index++)
        {
            int row = index / 3;
            int column = index % 3;
            Vector3 local = new Vector3((column - 1) * 0.82f, 0.60f - row * 0.50f, -0.31f);
            CreateKeypadButton(root.transform, keypad, values[index], local, new Vector3(0.64f, 0.38f, 0.18f));
        }

        CreateKeypadButton(root.transform, keypad, "delete", new Vector3(0f, -1.45f, -0.31f), new Vector3(2.28f, 0.38f, 0.18f));
        WorldText(parent, "KeypadInstruction", "ROOM 3 — REASSEMBLE THE CODE", new Vector3(0f, 4.15f, 23.65f), Quaternion.identity, 0.24f, Color.white, TextAlignmentOptions.Center);
    }

    static void CreateKeypadButton(Transform parent, NumericKeypadPuzzle keypad, string value, Vector3 position, Vector3 scale)
    {
        GameObject button = Cube(parent, "Keypad_" + value, position, scale, keypadButtonMaterial, InteractableLayer);
        NumericKeypadButton component = button.AddComponent<NumericKeypadButton>();
        Set(component, "keypad", keypad);
        Set(component, "value", value);
        string label = value == "clear" ? "CLR" : value == "delete" ? "DEL" : value == "enter" ? "ENT" : value;
        TMP_Text buttonText = WorldText(button.transform, "Label", label, new Vector3(0f, 0f, -0.58f), Quaternion.identity, value.Length > 1 ? 0.15f : 0.2f, Color.white, TextAlignmentOptions.Center);
        buttonText.rectTransform.sizeDelta = new Vector2(2f, 0.8f);
    }

    static NoteInteractable CreateNote(Transform parent, string name, Vector3 position, Quaternion rotation, string text, string id)
    {
        GameObject note = Cube(parent, name, position, new Vector3(1.35f, 1.75f, 0.08f), paperMaterial, InteractableLayer);
        note.transform.rotation = rotation;
        NoteInteractable interactable = note.AddComponent<NoteInteractable>();
        Set(interactable, "id", id);
        Set(interactable, "promptMessage", "Press E to read note");

        GameObject canvasObject = new GameObject(name + "_ReadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        GameObject panel = new GameObject("NotePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(860f, 484f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PaperTexturePath);
        panelImage.color = new Color(0.93f, 0.86f, 0.69f, 1f);
        panelImage.raycastTarget = false;
        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(12f, -12f);

        GameObject textObject = new GameObject("ClueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0.08f);
        textRect.anchorMax = new Vector2(0.92f, 0.92f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI clue = textObject.GetComponent<TextMeshProUGUI>();
        clue.text = text + "\n\n<color=#675947><size=55%>Press E or Escape to put the note down</size></color>";
        clue.fontSize = 40f;
        clue.alignment = TextAlignmentOptions.Center;
        clue.color = new Color(0.16f, 0.115f, 0.075f, 1f);
        clue.fontStyle = FontStyles.Italic;
        clue.characterSpacing = 2f;
        clue.lineSpacing = 8f;
        clue.enableWordWrapping = true;

        Set(interactable, "noteCanvas", canvasObject);
        Set(interactable, "notePanel", panel);
        Set(interactable, "noteText", textObject);
        return interactable;
    }

    static void CreateVictory(Transform uiParent, Transform gameplayParent)
    {
        GameObject canvasObject = new GameObject("VictoryScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        canvasObject.transform.SetParent(uiParent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
        Image background = canvasObject.GetComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.05f, 0.96f);

        GameObject textObject = new GameObject("VictoryText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.12f, 0.2f);
        rect.anchorMax = new Vector2(0.88f, 0.8f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 54f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.4f, 0.9f, 1f);

        GameObject trigger = Child(gameplayParent.gameObject, "ExitVictoryTrigger");
        trigger.transform.position = new Vector3(0f, 1.2f, 29f);
        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(5.5f, 2.4f, 1f);
        EscapeRoomExit exit = trigger.AddComponent<EscapeRoomExit>();
        Set(exit, "id", "escape_room_victory");
        Set(exit, "victoryScreen", canvasObject);
        Set(exit, "victoryText", text);
        canvasObject.SetActive(false);
    }

    static void CreateSigns(Transform parent)
    {
        WorldText(parent, "WelcomeSign", "ESCAPE PROTOCOL\nSolve each mental challenge. No dexterity required.", new Vector3(0f, 2.65f, -4.75f), Quaternion.identity, 0.25f, new Color(0.65f, 0.9f, 1f), TextAlignmentOptions.Center);
        WorldText(parent, "Room2Sign", "ROOM 2 — CAESAR CIPHER\nUse the note and wheel, then enter the decoded phrase.", new Vector3(0f, 2.75f, 6f), Quaternion.identity, 0.23f, Color.white, TextAlignmentOptions.Center);
        WorldText(parent, "ExitSign", "EXIT", new Vector3(0f, 2.8f, 25.15f), Quaternion.Euler(0f, 180f, 0f), 0.35f, new Color(0.3f, 1f, 0.55f), TextAlignmentOptions.Center);
    }

    static void ConfigurePlayer(GameObject player)
    {
        player.transform.SetPositionAndRotation(new Vector3(0f, 1.05f, -3.4f), Quaternion.identity);
        player.SetActive(true);
        PlayerInteract interact = player.GetComponent<PlayerInteract>();
        if (interact != null)
        {
            Set(interact, "distance", 4f);
            Set(interact, "mask", (LayerMask)(1 << InteractableLayer));
            Set(interact, "blockerMask", (LayerMask)(1 << BlockerLayer));
        }
    }

    static void EnsureManagers()
    {
        DataPersistenceManager persistence = UnityEngine.Object.FindFirstObjectByType<DataPersistenceManager>(FindObjectsInactive.Include);
        if (persistence == null)
        {
            persistence = Child(null, "DataPersistenceManager").AddComponent<DataPersistenceManager>();
        }
        Set(persistence, "fileName", "escape-room-prototype.game");
        if (UnityEngine.Object.FindFirstObjectByType<ReplayManager>(FindObjectsInactive.Include) == null)
        {
            Child(null, "ReplayManager").AddComponent<ReplayManager>();
        }
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject eventSystem = Child(null, "EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, int layer)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.layer = layer;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    static TMP_Text WorldText(Transform parent, string name, string content, Vector3 position, Quaternion rotation, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = position;
        textObject.transform.localRotation = rotation;
        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.rectTransform.sizeDelta = new Vector2(18f, 4f);
        return text;
    }

    static void CreateLight(Transform parent, Vector3 position, Color color)
    {
        GameObject lightObject = Child(parent.gameObject, "RoomLight");
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 12f;
        light.intensity = 4.5f;
        light.color = color;
        light.shadows = LightShadows.Soft;
    }

    static GameObject Child(GameObject parent, string name)
    {
        GameObject child = new GameObject(name);
        if (parent != null) child.transform.SetParent(parent.transform, false);
        return child;
    }

    static void Set(UnityEngine.Object target, string propertyName, object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no serialized property '" + propertyName + "'.");
        switch (property.propertyType)
        {
            case SerializedPropertyType.String: property.stringValue = (string)value; break;
            case SerializedPropertyType.Boolean: property.boolValue = (bool)value; break;
            case SerializedPropertyType.Integer: property.intValue = (int)value; break;
            case SerializedPropertyType.Enum: property.enumValueIndex = Convert.ToInt32(value); break;
            case SerializedPropertyType.Float: property.floatValue = Convert.ToSingle(value); break;
            case SerializedPropertyType.Vector3: property.vector3Value = (Vector3)value; break;
            case SerializedPropertyType.LayerMask: property.intValue = ((LayerMask)value).value; break;
            case SerializedPropertyType.ObjectReference: property.objectReferenceValue = value as UnityEngine.Object; break;
            default:
                if (property.isArray && value is Array array)
                {
                    property.arraySize = array.Length;
                    for (int index = 0; index < array.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = array.GetValue(index) as UnityEngine.Object;
                }
                else throw new InvalidOperationException("Unsupported property type for " + propertyName + ": " + property.propertyType);
                break;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
