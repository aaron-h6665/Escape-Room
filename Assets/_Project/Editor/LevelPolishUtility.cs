#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LevelPolishUtility
{
    const string ScenePath = "Assets/_Project/Scenes/Level.unity";
    const string DiagnosticPath = "/tmp/level-diagnostics.txt";
    const string MaterialFolder = "Assets/_Project/Materials/Prototype";
    const int InteractableLayer = 6;
    const int BlockerLayer = 7;

    [MenuItem("Tools/Escape Room/Repair Current Level In Place")]
    public static void RepairCurrentLevelInPlace()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = RequireRoot(scene, "Player");
        GameObject simonRoom = RequireRoot(scene, "SimonSaysRoom");
        GameObject safeRoom = RequireRoot(scene, "SafeKeypadRoom");

        RepairInteractionPrompt(scene, player);
        Inventory inventory = RepairPlayerInventory(scene, player);
        RepairSafeKey(safeRoom, inventory);
        RepairNotes(scene);
        RepairSimonBoundary(simonRoom);
        RepairCaesarKeyChoice(scene, inventory);
        RepairCrosshair(scene);
        RepairKeypadAndExit(scene, safeRoom);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Current Level repaired in place: centered prompt, readable notes, working inventory/key pickup, Simon boundary blockers, interactive keypad, animated exit, and good-game screen.");
    }

    [MenuItem("Tools/Escape Room/Validate Current Level Repairs")]
    public static void ValidateCurrentLevelRepairs()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        PlayerUI playerUI = UnityEngine.Object.FindFirstObjectByType<PlayerUI>();
        if (playerUI == null) throw new InvalidOperationException("Active PlayerUI is missing.");
        SerializedProperty promptProperty = new SerializedObject(playerUI).FindProperty("promptText");
        TMP_Text prompt = promptProperty.objectReferenceValue as TMP_Text;
        if (prompt == null || prompt.text == "New Text" || prompt.fontSize > 28f)
            throw new InvalidOperationException("The centered interaction prompt is not configured correctly.");
        if (playerUI.GetComponent<Inventory>() == null)
            throw new InvalidOperationException("The active player is missing Inventory.");

        GameObject boundary = GameObject.Find("SimonSaysRoom/SimonCaesarBoundaryColliders");
        if (boundary == null || boundary.GetComponentsInChildren<BoxCollider>(true).Length != 3)
            throw new InvalidOperationException("The Simon/Caesar boundary needs three doorway-safe blockers.");
        if (UnityEngine.Object.FindObjectsByType<SceneTransitionOnInteract>(FindObjectsInactive.Include).Length != 0)
            throw new InvalidOperationException("Level still contains a scene transition instead of in-scene progression.");

        NoteInteractable[] notes = UnityEngine.Object.FindObjectsByType<NoteInteractable>(FindObjectsInactive.Include);
        if (notes.Length < 3) throw new InvalidOperationException("Expected the Caesar note and two keypad notes.");
        foreach (NoteInteractable note in notes)
        {
            TMP_Text text = note.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(value => value.name == "ReadableNoteText");
            if (text == null || text.rectTransform.anchorMin.x < 0.05f || text.rectTransform.anchorMax.x > 0.95f)
                throw new InvalidOperationException(note.name + " does not have a readable bounded note UI.");
        }

        ItemPickupInteractable safeKey = GameObject.Find("SafeKeypadRoom/SafeKey")?.GetComponent<ItemPickupInteractable>();
        if (safeKey == null || safeKey.GetComponent<BoxCollider>() == null || safeKey.GetComponent<BoxCollider>().bounds.size.magnitude < 0.5f)
            throw new InvalidOperationException("Safe key pickup collider is missing or too small.");

        NumericKeypadPuzzle keypad = UnityEngine.Object.FindFirstObjectByType<NumericKeypadPuzzle>();
        NumericKeypadButton[] buttons = UnityEngine.Object.FindObjectsByType<NumericKeypadButton>();
        if (keypad == null || buttons.Length != 10)
            throw new InvalidOperationException("The final keypad must have one puzzle and ten interactive digit buttons.");
        HashSet<string> values = buttons.Select(button => new SerializedObject(button).FindProperty("value").stringValue).ToHashSet();
        if (Enumerable.Range(0, 10).Any(number => !values.Contains(number.ToString())))
            throw new InvalidOperationException("The keypad is missing one or more digits from 0 through 9.");
        if (new SerializedObject(keypad).FindProperty("displayText").objectReferenceValue == null)
            throw new InvalidOperationException("The keypad entry display is missing.");
        if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomExit>(FindObjectsInactive.Include) == null)
            throw new InvalidOperationException("The good-game exit trigger is missing.");

        ColorKeyChoicePuzzle keyChoice = UnityEngine.Object.FindFirstObjectByType<ColorKeyChoicePuzzle>();
        if (keyChoice == null || UnityEngine.Object.FindObjectsByType<ColorKeyChoiceInteractable>().Length != 2)
            throw new InvalidOperationException("The Caesar room needs red/blue answer verification choices.");
        if (GameObject.Find("CaesarCipherRoom/CaesarSafeBoundaryColliders")?.GetComponentsInChildren<BoxCollider>(true).Length != 3)
            throw new InvalidOperationException("The Caesar/safe-room boundary is not sealed around its door.");
        CaesarCipherInteractable cipher = UnityEngine.Object.FindFirstObjectByType<CaesarCipherInteractable>();
        if (cipher == null || cipher.gameObject.layer != InteractableLayer || cipher.GetComponent<Collider>() == null)
            throw new InvalidOperationException("The Caesar decoder is not reachable as an interactable.");
        SerializedObject serializedCipher = new SerializedObject(cipher);
        if (serializedCipher.FindProperty("innerIndex").intValue != 13 ||
            serializedCipher.FindProperty("outerIndex").intValue != 14 ||
            serializedCipher.FindProperty("selectedRing").enumValueIndex != 0)
            throw new InvalidOperationException("The Caesar decoder must begin A/A with only the inner ring selected.");
        if (UnityEngine.Object.FindFirstObjectByType<CaesarAnswerTerminal>() == null)
            throw new InvalidOperationException("The Caesar decoded-phrase terminal is missing.");
        if (GameObject.Find("Canvas/Crosshair") != null)
            throw new InvalidOperationException("The legacy duplicate crosshair still exists.");

        Debug.Log("Current Level repair validation passed: prompt, notes, inventory pickup, boundary collision, 10-button keypad, animated doors, exit platform, and good-game UI are configured.");
    }

    static void RepairInteractionPrompt(Scene scene, GameObject player)
    {
        TMP_Text prompt = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
            .FirstOrDefault(text => text.name == "PromptText");
        if (prompt == null) throw new InvalidOperationException("Could not find Canvas/PromptText.");

        prompt.text = string.Empty;
        prompt.fontSize = 26f;
        prompt.enableWordWrapping = false;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.raycastTarget = false;
        RectTransform rect = prompt.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -90f);
        rect.sizeDelta = new Vector2(520f, 42f);

        PlayerUI playerUI = player.GetComponent<PlayerUI>() ?? player.AddComponent<PlayerUI>();
        Set(playerUI, "promptText", prompt);
    }

    static Inventory RepairPlayerInventory(Scene scene, GameObject player)
    {
        Inventory inventory = player.GetComponent<Inventory>() ?? player.AddComponent<Inventory>();
        PlayerInventoryInput inventoryInput = player.GetComponent<PlayerInventoryInput>() ?? player.AddComponent<PlayerInventoryInput>();
        InputManager inputManager = player.GetComponent<InputManager>();
        Set(inventoryInput, "inputManager", inputManager);
        Set(inventoryInput, "inventory", inventory);

        InventoryUI inventoryUI = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
        if (inventoryUI != null) Set(inventory, "ui", inventoryUI);
        GameObject droppedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dropped Item Prefab 1.prefab");
        if (droppedPrefab != null) Set(inventory, "droppedItemPrefab", droppedPrefab);

        Item[] catalog = new[]
        {
            AssetDatabase.LoadAssetAtPath<Item>("Assets/_Project/Scripts/Inventory/Key.asset"),
            AssetDatabase.LoadAssetAtPath<Item>("Assets/_Project/Scripts/Inventory/SafeKey.asset"),
            AssetDatabase.LoadAssetAtPath<Item>("Assets/_Project/Scripts/Inventory/BlueKey.asset")
        }.Where(item => item != null).ToArray();
        Set(inventory, "itemCatalog", catalog);

        PlayerInteract interact = player.GetComponent<PlayerInteract>();
        if (interact != null)
        {
            Set(interact, "distance", 4.5f);
            Set(interact, "mask", (LayerMask)(1 << InteractableLayer));
            Set(interact, "blockerMask", (LayerMask)(1 << BlockerLayer));
        }
        return inventory;
    }

    static void RepairSafeKey(GameObject safeRoom, Inventory inventory)
    {
        Transform keyTransform = safeRoom.transform.Find("SafeKey");
        if (keyTransform == null) throw new InvalidOperationException("SafeKeypadRoom/SafeKey is missing.");
        GameObject key = keyTransform.gameObject;
        key.layer = InteractableLayer;
        ItemPickupInteractable pickup = key.GetComponent<ItemPickupInteractable>();
        if (pickup == null) throw new InvalidOperationException("SafeKey is missing ItemPickupInteractable.");
        Set(pickup, "inventory", inventory);
        Set(pickup, "promptMessage", "Press E to pick up the safe key");

        BoxCollider collider = key.GetComponent<BoxCollider>() ?? key.AddComponent<BoxCollider>();
        Vector3 scale = key.transform.lossyScale;
        collider.size = new Vector3(
            0.72f / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
            0.42f / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
            0.72f / Mathf.Max(Mathf.Abs(scale.z), 0.001f));
        Renderer renderer = key.GetComponentInChildren<Renderer>();
        if (renderer != null) collider.center = key.transform.InverseTransformPoint(renderer.bounds.center);
    }

    static void RepairNotes(Scene scene)
    {
        GameObject caesar = RequireRoot(scene, "CaesarCipherRoom");
        GameObject safe = RequireRoot(scene, "SafeKeypadRoom");
        ConfigureReadableNote(caesar.transform.Find("Note")?.GetComponent<NoteInteractable>(),
            "CAESAR'S NOTE\n\nEOXH NHB\n\nRotate the inner ring CCW 3.", "caesar_blue_key_note");
        ConfigureReadableNote(safe.transform.Find("SafeNote")?.GetComponent<NoteInteractable>(),
            "TORN CODE — LEFT HALF\n\nThe code begins with:\n\n42", "keypad_first_digits_note");
        ConfigureReadableNote(safe.transform.Find("SafeNote (1)")?.GetComponent<NoteInteractable>(),
            "TORN CODE — RIGHT HALF\n\nThe code ends with:\n\n71", "keypad_last_digits_note");
    }

    static void ConfigureReadableNote(NoteInteractable note, string clue, string id)
    {
        if (note == null) throw new InvalidOperationException("A required note is missing.");
        SerializedObject serialized = new SerializedObject(note);
        GameObject configuredObject = serialized.FindProperty("noteCanvas").objectReferenceValue as GameObject;
        Canvas canvas = configuredObject != null ? configuredObject.GetComponent<Canvas>() : null;
        if (canvas == null && configuredObject != null) canvas = configuredObject.GetComponentInParent<Canvas>(true);
        if (canvas == null) canvas = note.GetComponentInChildren<Canvas>(true);
        if (canvas == null) throw new InvalidOperationException(note.name + " is missing its note canvas.");
        GameObject canvasObject = canvas.gameObject;

        foreach (Transform child in canvasObject.transform.Cast<Transform>().ToArray())
        {
            if (child.name != "ReadableNotePanel") child.gameObject.SetActive(false);
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>() ?? canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform existingPanel = canvasObject.transform.Find("ReadableNotePanel");
        GameObject panel = existingPanel != null ? existingPanel.gameObject : new GameObject("ReadableNotePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        panel.SetActive(true);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.23f, 0.18f);
        panelRect.anchorMax = new Vector2(0.77f, 0.82f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.10f, 0.085f, 0.06f, 0.98f);

        Transform existingText = panel.transform.Find("ReadableNoteText");
        TextMeshProUGUI text = existingText != null
            ? existingText.GetComponent<TextMeshProUGUI>()
            : new GameObject("ReadableNoteText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(panel.transform, false);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0.08f, 0.08f);
        textRect.anchorMax = new Vector2(0.92f, 0.92f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        text.text = clue + "\n\n<size=60%><color=#AEB6C5>Press E or Escape to close</color></size>";
        text.fontSize = 32f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = 34f;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.96f, 0.91f, 0.76f);
        text.margin = Vector4.zero;
        text.raycastTarget = false;

        Set(note, "noteCanvas", canvasObject);
        Set(note, "notePanel", panel);
        Set(note, "noteText", text.gameObject);
        Set(note, "id", id);
        Set(note, "promptMessage", "Press E to read note");
        canvasObject.SetActive(false);
    }

    static void RepairSimonBoundary(GameObject simonRoom)
    {
        foreach (SceneTransitionOnInteract transition in UnityEngine.Object.FindObjectsByType<SceneTransitionOnInteract>(FindObjectsInactive.Include))
        {
            UnityEngine.Object.DestroyImmediate(transition);
        }

        Transform existing = simonRoom.transform.Find("SimonCaesarBoundaryColliders");
        GameObject boundary = existing != null ? existing.gameObject : new GameObject("SimonCaesarBoundaryColliders");
        boundary.transform.SetParent(simonRoom.transform, false);
        boundary.transform.localPosition = Vector3.zero;
        foreach (Transform child in boundary.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);

        CreateWorldBlocker(boundary.transform, "LeftWallBlocker", new Vector3(-5.06f, 7.2f, 10f), new Vector3(4.32f, 10f, 0.22f));
        CreateWorldBlocker(boundary.transform, "RightWallBlocker", new Vector3(0.55f, 7.2f, 10f), new Vector3(4.46f, 10f, 0.22f));
        CreateWorldBlocker(boundary.transform, "DoorHeaderBlocker", new Vector3(-2.29f, 8.43f, 10f), new Vector3(1.22f, 7.55f, 0.22f));
    }

    static void RepairCaesarKeyChoice(Scene scene, Inventory inventory)
    {
        GameObject caesarRoom = RequireRoot(scene, "CaesarCipherRoom");
        Transform doorTransform = caesarRoom.transform.Find("HingeDoor");
        if (doorTransform == null) throw new InvalidOperationException("CaesarCipherRoom/HingeDoor is missing.");
        HingeDoor exitDoor = doorTransform.GetComponent<HingeDoor>() ?? doorTransform.gameObject.AddComponent<HingeDoor>();
        Set(exitDoor, "id", "caesar_room_verified_exit");
        Set(exitDoor, "promptMessage", "Decode the note and submit the matching key");
        Animator doorAnimator = doorTransform.GetComponentInChildren<Animator>(true);
        if (doorAnimator != null) Set(exitDoor, "myDoor", doorAnimator);
        doorTransform.gameObject.layer = InteractableLayer;

        Transform puzzleTransform = caesarRoom.transform.Find("BlueKeyVerification");
        GameObject puzzleObject = puzzleTransform != null ? puzzleTransform.gameObject : new GameObject("BlueKeyVerification");
        puzzleObject.transform.SetParent(caesarRoom.transform, false);
        ColorKeyChoicePuzzle puzzle = puzzleObject.GetComponent<ColorKeyChoicePuzzle>() ?? puzzleObject.AddComponent<ColorKeyChoicePuzzle>();
        Set(puzzle, "id", "caesar_blue_key_verification");
        Set(puzzle, "inventory", inventory);
        Set(puzzle, "blueKeyItem", AssetDatabase.LoadAssetAtPath<Item>("Assets/_Project/Scripts/Inventory/BlueKey.asset"));
        Set(puzzle, "hingeExitDoor", exitDoor);

        ColorKeyChoiceInteractable blueChoice = ConfigureColorKeyChoice(caesarRoom.transform.Find("HingeDoorKey"), puzzle, true);
        ColorKeyChoiceInteractable redChoice = ConfigureColorKeyChoice(caesarRoom.transform.Find("HingeDoorKey (1)"), puzzle, false);
        Set(puzzle, "blueKey", blueChoice);
        Set(puzzle, "redKey", redChoice);

        Transform oldFeedback = caesarRoom.transform.Find("KeyVerificationFeedback");
        if (oldFeedback != null) UnityEngine.Object.DestroyImmediate(oldFeedback.gameObject);
        RepairCaesarDecoder(caesarRoom, puzzle);

        CreateDoorwayBoundary(caesarRoom.transform, "CaesarSafeBoundaryColliders", 0.1f, 0.28f);
    }

    static void RepairCaesarDecoder(GameObject caesarRoom, ColorKeyChoicePuzzle puzzle)
    {
        CaesarCipherInteractable cipher = caesarRoom.GetComponentInChildren<CaesarCipherInteractable>(true);
        if (cipher == null) throw new InvalidOperationException("Caesar Cipher Decoder prefab is missing from the Caesar room.");
        cipher.gameObject.layer = InteractableLayer;
        cipher.transform.position = new Vector3(-5.25f, 3.75f, 0.38f);
        cipher.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Collider cipherCollider = cipher.GetComponent<Collider>();
        if (cipherCollider != null) cipherCollider.enabled = true;
        NoteInteractable sourceNote = caesarRoom.transform.Find("Note")?.GetComponent<NoteInteractable>();
        Set(cipher, "sourceNote", sourceNote);
        Set(cipher, "promptMessage", "Press E to inspect the Caesar decoder");
        Set(cipher, "innerIndex", 13);
        Set(cipher, "outerIndex", 14);
        Set(cipher, "selectedRing", 0);

        Transform existingTerminal = caesarRoom.transform.Find("DecodedPhraseTerminal");
        GameObject terminal = existingTerminal != null ? existingTerminal.gameObject : new GameObject("DecodedPhraseTerminal");
        terminal.transform.SetParent(caesarRoom.transform, true);
        terminal.transform.position = new Vector3(-3.55f, 3.65f, 0.39f);
        terminal.transform.rotation = Quaternion.identity;
        terminal.layer = InteractableLayer;
        foreach (Transform child in terminal.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        CaesarAnswerTerminal terminalComponent = terminal.GetComponent<CaesarAnswerTerminal>() ?? terminal.AddComponent<CaesarAnswerTerminal>();
        Set(terminalComponent, "puzzle", puzzle);
        Set(terminalComponent, "promptMessage", "Press E to enter the decoded phrase");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "TerminalBody";
        body.layer = InteractableLayer;
        body.transform.SetParent(terminal.transform, false);
        body.transform.localScale = new Vector3(1.55f, 1.0f, 0.16f);
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/DarkMetal.mat");
        if (dark != null) body.GetComponent<Renderer>().sharedMaterial = dark;

        TextMeshPro label = new GameObject("TerminalLabel", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(terminal.transform, false);
        label.transform.localPosition = new Vector3(0f, 0f, 0.095f);
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        label.transform.localScale = Vector3.one * 0.085f;
        label.rectTransform.sizeDelta = new Vector2(17f, 8f);
        label.text = "DECODED PHRASE\nTERMINAL\n\nPress E";
        label.fontSize = 3.2f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.35f, 0.95f, 1f);
        label.sortingOrder = 10;
        Set(puzzle, "feedbackText", label);

        GameObject canvasObject = new GameObject("CaesarAnswerEntryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        canvasObject.transform.SetParent(terminal.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
        canvasObject.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.045f, 0.97f);

        TextMeshProUGUI title = CreateOverlayText(canvasObject.transform, "TerminalTitle", new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.82f), 38f);
        title.text = "ENTER THE DECODED PHRASE";
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.93f, 0.73f, 0.3f);
        TextMeshProUGUI entry = CreateOverlayText(canvasObject.transform, "TypedPhrase", new Vector2(0.2f, 0.43f), new Vector2(0.8f, 0.62f), 46f);
        entry.text = "_";
        entry.fontStyle = FontStyles.Bold;
        TextMeshProUGUI status = CreateOverlayText(canvasObject.transform, "TerminalStatus", new Vector2(0.18f, 0.2f), new Vector2(0.82f, 0.35f), 24f);
        status.text = "Type the decoded phrase, then press Enter. Escape cancels.";
        status.color = new Color(0.75f, 0.8f, 0.88f);
        Set(terminalComponent, "entryCanvas", canvasObject);
        Set(terminalComponent, "entryText", entry);
        Set(terminalComponent, "statusText", status);
        canvasObject.SetActive(false);
    }

    static TextMeshProUGUI CreateOverlayText(Transform parent, string name, Vector2 min, Vector2 max, float fontSize)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.anchorMin = min;
        text.rectTransform.anchorMax = max;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static void RepairCrosshair(Scene scene)
    {
        GameObject legacy = GameObject.Find("Canvas/Crosshair");
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
    }

    static ColorKeyChoiceInteractable ConfigureColorKeyChoice(Transform keyTransform, ColorKeyChoicePuzzle puzzle, bool blue)
    {
        if (keyTransform == null) throw new InvalidOperationException("A Caesar-room key is missing.");
        ItemPickupInteractable pickup = keyTransform.GetComponent<ItemPickupInteractable>();
        if (pickup != null) UnityEngine.Object.DestroyImmediate(pickup);
        keyTransform.gameObject.layer = InteractableLayer;
        ColorKeyChoiceInteractable choice = keyTransform.GetComponent<ColorKeyChoiceInteractable>() ?? keyTransform.gameObject.AddComponent<ColorKeyChoiceInteractable>();
        Set(choice, "puzzle", puzzle);
        Set(choice, "isBlueKey", blue);
        Set(choice, "promptMessage", blue ? "Press E to verify BLUE KEY" : "Press E to verify RED KEY");
        BoxCollider collider = keyTransform.GetComponent<BoxCollider>() ?? keyTransform.gameObject.AddComponent<BoxCollider>();
        Vector3 scale = keyTransform.lossyScale;
        collider.size = new Vector3(0.55f / Mathf.Max(Mathf.Abs(scale.x), 0.001f), 0.4f / Mathf.Max(Mathf.Abs(scale.y), 0.001f), 0.55f / Mathf.Max(Mathf.Abs(scale.z), 0.001f));
        Material colorMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + (blue ? "/BlueKey.mat" : "/RedKey.mat"));
        Renderer renderer = keyTransform.GetComponentInChildren<Renderer>();
        if (renderer != null && colorMaterial != null)
        {
            renderer.sharedMaterial = colorMaterial;
            collider.center = keyTransform.InverseTransformPoint(renderer.bounds.center);
        }
        return choice;
    }

    static void CreateDoorwayBoundary(Transform room, string name, float z, float doorCenterX)
    {
        Transform existing = room.Find(name);
        GameObject boundary = existing != null ? existing.gameObject : new GameObject(name);
        boundary.transform.SetParent(room, false);
        foreach (Transform child in boundary.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        CreateWorldBlocker(boundary.transform, "LeftWallBlocker", new Vector3(-3.73f, 7.2f, z), new Vector3(6.94f, 10f, 0.22f));
        CreateWorldBlocker(boundary.transform, "RightWallBlocker", new Vector3(1.79f, 7.2f, z), new Vector3(1.98f, 10f, 0.22f));
        CreateWorldBlocker(boundary.transform, "DoorHeaderBlocker", new Vector3(doorCenterX, 8.43f, z), new Vector3(1.1f, 7.55f, 0.22f));
    }

    static void CreateWorldBlocker(Transform parent, string name, Vector3 worldPosition, Vector3 worldSize)
    {
        GameObject blocker = new GameObject(name, typeof(BoxCollider));
        blocker.layer = BlockerLayer;
        blocker.transform.SetParent(parent, true);
        blocker.transform.position = worldPosition;
        blocker.GetComponent<BoxCollider>().size = worldSize;
    }

    static void RepairKeypadAndExit(Scene scene, GameObject safeRoom)
    {
        Transform keypadRoot = safeRoom.transform.Find("KeyPad");
        if (keypadRoot == null) throw new InvalidOperationException("SafeKeypadRoom/KeyPad is missing.");
        NumericKeypadPuzzle keypad = keypadRoot.GetComponent<NumericKeypadPuzzle>() ?? keypadRoot.gameObject.AddComponent<NumericKeypadPuzzle>();
        Set(keypad, "id", "safe_room_numeric_keypad");
        Set(keypad, "correctCode", "4271");
        Set(keypad, "codeLength", 4);
        Set(keypad, "autoSubmitOnCodeLength", true);

        Dictionary<int, Transform> digitMeshes = new Dictionary<int, Transform>();
        foreach (Transform child in keypadRoot.Cast<Transform>())
        {
            if (!TryParseKeypadNumber(child.name, out int digit)) continue;
            digitMeshes[digit] = child;
        }
        if (digitMeshes.Count != 10) throw new InvalidOperationException("Could not map all ten decorative keypad buttons.");

        foreach (KeyValuePair<int, Transform> entry in digitMeshes)
        {
            Transform buttonTransform = entry.Value;
            buttonTransform.gameObject.layer = InteractableLayer;
            BoxCollider collider = buttonTransform.GetComponent<BoxCollider>() ?? buttonTransform.gameObject.AddComponent<BoxCollider>();
            Vector3 scale = buttonTransform.lossyScale;
            collider.size = new Vector3(
                0.115f / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
                0.105f / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
                0.12f / Mathf.Max(Mathf.Abs(scale.z), 0.001f));
            NumericKeypadButton button = buttonTransform.GetComponent<NumericKeypadButton>() ?? buttonTransform.gameObject.AddComponent<NumericKeypadButton>();
            Set(button, "keypad", keypad);
            Set(button, "value", entry.Key.ToString());
            CreateDigitLabel(buttonTransform, entry.Key.ToString());
        }

        TMP_Text display = CreateKeypadDisplay(keypadRoot);
        Set(keypad, "displayText", display);

        Transform slidingDoorsTransform = safeRoom.transform.Find("SlidingDoors");
        if (slidingDoorsTransform == null || slidingDoorsTransform.childCount < 2)
            throw new InvalidOperationException("SafeKeypadRoom/SlidingDoors is incomplete.");
        Transform leftPanel = slidingDoorsTransform.Cast<Transform>().OrderBy(child => child.position.x).First();
        Transform rightPanel = slidingDoorsTransform.Cast<Transform>().OrderByDescending(child => child.position.x).First();
        PrototypeSlidingDoor doors = slidingDoorsTransform.GetComponent<PrototypeSlidingDoor>() ?? slidingDoorsTransform.gameObject.AddComponent<PrototypeSlidingDoor>();
        Set(doors, "id", "safe_room_final_doors");
        Set(doors, "leftPanel", leftPanel);
        Set(doors, "rightPanel", rightPanel);
        Set(doors, "leftOpenOffset", new Vector3(-2.15f, 0f, 0f));
        Set(doors, "rightOpenOffset", new Vector3(2.15f, 0f, 0f));
        Set(doors, "animationDuration", 0.9f);
        Set(keypad, "finalDoor", doors);

        CreateExitPlatformAndVictory(scene, safeRoom.transform);
    }

    static bool TryParseKeypadNumber(string name, out int digit)
    {
        digit = -1;
        if (!name.StartsWith("KeyPad (", StringComparison.Ordinal) || !name.EndsWith(")", StringComparison.Ordinal)) return false;
        string value = name.Substring(8, name.Length - 9);
        if (!int.TryParse(value, out int parsed) || parsed < 1 || parsed > 10) return false;
        digit = parsed == 10 ? 0 : parsed;
        return true;
    }

    static void CreateDigitLabel(Transform button, string value)
    {
        Transform existing = button.Find("DigitLabel");
        TextMeshPro label = existing != null ? existing.GetComponent<TextMeshPro>() : new GameObject("DigitLabel", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(button, false);
        label.transform.localPosition = new Vector3(0f, 0f, 0.62f);
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        label.transform.localScale = Vector3.one * 0.3f;
        label.rectTransform.sizeDelta = new Vector2(4f, 4f);
        label.text = value;
        label.fontSize = 4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.sortingOrder = 20;
        label.raycastTarget = false;
    }

    static TMP_Text CreateKeypadDisplay(Transform keypadRoot)
    {
        Transform existing = keypadRoot.Find("TypedNumberDisplay");
        TextMeshPro display = existing != null ? existing.GetComponent<TextMeshPro>() : new GameObject("TypedNumberDisplay", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        display.transform.SetParent(keypadRoot, false);
        display.transform.position = new Vector3(-5.05f, 4.05f, -9.84f);
        display.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        display.transform.localScale = Vector3.one * 0.075f;
        display.rectTransform.sizeDelta = new Vector2(14f, 3f);
        display.text = "----";
        display.fontSize = 4f;
        display.fontStyle = FontStyles.Bold;
        display.alignment = TextAlignmentOptions.Center;
        display.color = new Color(0.35f, 0.95f, 1f);
        display.sortingOrder = 20;
        display.raycastTarget = false;
        return display;
    }

    static void CreateExitPlatformAndVictory(Scene scene, Transform safeRoom)
    {
        Transform existingPlatform = safeRoom.Find("ExitPlatform");
        GameObject platform = existingPlatform != null ? existingPlatform.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "ExitPlatform";
        platform.layer = BlockerLayer;
        platform.transform.SetParent(safeRoom, true);
        platform.transform.position = new Vector3(-2.22f, 2.10f, -12.5f);
        platform.transform.localScale = new Vector3(5f, 0.2f, 5f);
        Material floor = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Floor.mat");
        if (floor != null) platform.GetComponent<Renderer>().sharedMaterial = floor;

        GameObject canvasObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "GoodGameScreen");
        if (canvasObject == null)
            canvasObject = new GameObject("GoodGameScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
        canvasObject.GetComponent<Image>().color = new Color(0.012f, 0.022f, 0.04f, 1f);

        Transform oldProtocol = canvasObject.transform.Find("CompletionProtocol");
        TextMeshProUGUI protocol = oldProtocol != null ? oldProtocol.GetComponent<TextMeshProUGUI>() : new GameObject("CompletionProtocol", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        protocol.transform.SetParent(canvasObject.transform, false);
        protocol.rectTransform.anchorMin = new Vector2(0.08f, 0.84f);
        protocol.rectTransform.anchorMax = new Vector2(0.65f, 0.91f);
        protocol.rectTransform.offsetMin = protocol.rectTransform.offsetMax = Vector2.zero;
        protocol.text = "ESCAPE PROTOCOL // COMPLETE";
        protocol.fontSize = 18f;
        protocol.fontStyle = FontStyles.Bold;
        protocol.alignment = TextAlignmentOptions.Left;
        protocol.color = new Color(0.93f, 0.73f, 0.3f);

        Transform oldRule = canvasObject.transform.Find("CompletionRule");
        Image rule = oldRule != null ? oldRule.GetComponent<Image>() : new GameObject("CompletionRule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        rule.transform.SetParent(canvasObject.transform, false);
        rule.rectTransform.anchorMin = new Vector2(0.08f, 0.825f);
        rule.rectTransform.anchorMax = new Vector2(0.34f, 0.831f);
        rule.rectTransform.offsetMin = rule.rectTransform.offsetMax = Vector2.zero;
        rule.color = new Color(0.93f, 0.73f, 0.3f);
        rule.raycastTarget = false;

        Transform existingText = canvasObject.transform.Find("GoodGameText");
        TextMeshProUGUI text = existingText != null ? existingText.GetComponent<TextMeshProUGUI>() : new GameObject("GoodGameText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(canvasObject.transform, false);
        text.rectTransform.anchorMin = new Vector2(0.1f, 0.24f);
        text.rectTransform.anchorMax = new Vector2(0.9f, 0.72f);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.text = "ESCAPE ROOM COMPLETE\n\n<size=55%><color=#C2CAD8>You have completed the escape room.\nThank you for participating.</color></size>";
        text.fontSize = 58f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 34f;
        text.fontSizeMax = 58f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;

        Transform existingTrigger = safeRoom.Find("ExitVictoryTrigger");
        GameObject trigger = existingTrigger != null ? existingTrigger.gameObject : new GameObject("ExitVictoryTrigger", typeof(BoxCollider), typeof(EscapeRoomExit));
        trigger.transform.SetParent(safeRoom, true);
        trigger.transform.position = new Vector3(-2.22f, 3.25f, -12.1f);
        BoxCollider collider = trigger.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(4.6f, 2.4f, 1f);
        EscapeRoomExit exit = trigger.GetComponent<EscapeRoomExit>();
        Set(exit, "id", "escape_room_good_game");
        Set(exit, "victoryScreen", canvasObject);
        Set(exit, "victoryText", text);
        canvasObject.SetActive(false);
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static GameObject RequireRoot(Scene scene, string name)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(value => value.name == name);
        if (root == null) throw new InvalidOperationException("Level is missing root '" + name + "'.");
        return root;
    }

    static void Set(UnityEngine.Object target, string propertyName, object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no serialized property '" + propertyName + "'.");
        if (property.propertyType == SerializedPropertyType.String) property.stringValue = (string)value;
        else if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = (bool)value;
        else if (property.propertyType == SerializedPropertyType.Integer) property.intValue = (int)value;
        else if (property.propertyType == SerializedPropertyType.Enum) property.enumValueIndex = (int)value;
        else if (property.propertyType == SerializedPropertyType.Float) property.floatValue = Convert.ToSingle(value);
        else if (property.propertyType == SerializedPropertyType.Vector3) property.vector3Value = (Vector3)value;
        else if (property.propertyType == SerializedPropertyType.LayerMask) property.intValue = ((LayerMask)value).value;
        else if (property.propertyType == SerializedPropertyType.ObjectReference) property.objectReferenceValue = value as UnityEngine.Object;
        else if (property.isArray && value is Array array)
        {
            property.arraySize = array.Length;
            for (int index = 0; index < array.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = array.GetValue(index) as UnityEngine.Object;
        }
        else throw new InvalidOperationException("Unsupported serialized property " + propertyName + ".");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Escape Room/Dump Current Level Diagnostics")]
    public static void DumpCurrentLevelDiagnostics()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StringBuilder report = new StringBuilder();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            report.AppendLine($"ROOT {root.name} activeSelf={root.activeSelf} active={root.activeInHierarchy} position={root.transform.position}");
        }

        report.AppendLine("\nRELEVANT OBJECTS");
        foreach (Transform transform in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
        {
            GameObject gameObject = transform.gameObject;
            Component[] components = gameObject.GetComponents<Component>();
            bool relevant = components.Any(component => component is PlayerUI
                || component is PlayerInteract
                || component is Canvas
                || component is TMP_Text
                || component is NoteInteractable
                || component is ItemPickupInteractable
                || component is NumericKeypadPuzzle
                || component is NumericKeypadButton
                || component is Keypad
                || component is HingeDoor
                || component is CaesarCipherInteractable
                || component is ColorKeyChoicePuzzle
                || component is ColorKeyChoiceInteractable
                || component is PlayerCrosshair
                || component is PrototypeSlidingDoor)
                || gameObject.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0
                || gameObject.name.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) >= 0
                || gameObject.name.IndexOf("Key", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!relevant) continue;

            string componentNames = string.Join(",", components.Where(component => component != null).Select(component => component.GetType().Name));
            report.AppendLine($"{GetPath(transform)} | self={gameObject.activeSelf} active={gameObject.activeInHierarchy} layer={gameObject.layer} world={transform.position} local={transform.localPosition} components=[{componentNames}]");
            if (gameObject.TryGetComponent(out TMP_Text text))
            {
                report.AppendLine($"  TEXT font={text.fontSize} rect={text.rectTransform.rect.size} anchors={text.rectTransform.anchorMin}->{text.rectTransform.anchorMax} value={text.text.Replace('\n', '|')}");
            }
            if (gameObject.TryGetComponent(out Collider collider))
            {
                report.AppendLine($"  COLLIDER enabled={collider.enabled} trigger={collider.isTrigger} bounds={collider.bounds}");
            }
        }

        File.WriteAllText(DiagnosticPath, report.ToString());
        Debug.Log("Current Level diagnostics written to " + DiagnosticPath);
    }

    static string GetPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
#endif
