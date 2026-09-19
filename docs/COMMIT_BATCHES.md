# Implementation commit batches

Run from the repository root. These lists exclude the pre-existing `.DS_Store` changes. Each batch includes its Unity metadata. Do not stage unrelated files.

## 1. Record complete takeover state; add neutral menus and study exports

```sh
git add -- Assets/_Project/Scripts/DataPersistence/Data/GameData.cs Assets/_Project/Scripts/Interactables/CaesarAnswerTerminal.cs Assets/_Project/Scripts/Interactables/CaesarCipherInteractable.cs Assets/_Project/Scripts/Interactables/ColorKeyChoicePuzzle.cs Assets/_Project/Scripts/Interactables/EscapeRoomExit.cs Assets/_Project/Scripts/Interactables/HingeDoor.cs Assets/_Project/Scripts/Interactables/LockedSafeInteractable.cs Assets/_Project/Scripts/Interactables/NoteInteractable.cs Assets/_Project/Scripts/Interactables/NumericKeypadPuzzle.cs Assets/_Project/Scripts/Interactables/PrototypeSlidingDoor.cs Assets/_Project/Scripts/Interactables/SimonSaysController.cs Assets/_Project/Scripts/Inventory/Inventory.cs Assets/_Project/Scripts/Inventory/InventoryUI.cs Assets/_Project/Scripts/Inventory/ItemUI.cs Assets/_Project/Scripts/Menu/GameExitUtility.cs Assets/_Project/Scripts/Menu/MenuController.cs Assets/_Project/Scripts/Menu/PauseMenuController.cs Assets/_Project/Scripts/Playback/ReplayEventData.cs Assets/_Project/Scripts/Playback/ReplayIdentity.cs Assets/_Project/Scripts/Playback/ReplayManager.cs Assets/_Project/Scripts/Playback/SnapshotData.cs Assets/_Project/Scripts/Playback/TakeoverOverlay.cs Assets/_Project/Scripts/Player/InputManager.cs Assets/_Project/Scripts/Player/PauseManager.cs Assets/_Project/Scripts/Player/PlayerInteract.cs Assets/_Project/Scripts/Player/PlayerLook.cs Assets/_Project/Scripts/Player/PlayerMotor.cs Assets/_Project/Scripts/Study.meta Assets/_Project/Scripts/Study/StableReplayId.cs Assets/_Project/Scripts/Study/StableReplayId.cs.meta Assets/_Project/Scripts/Study/StudyConfiguration.cs Assets/_Project/Scripts/Study/StudyConfiguration.cs.meta Assets/_Project/Scripts/Study/StudyExports.cs Assets/_Project/Scripts/Study/StudyExports.cs.meta Assets/_Project/Scripts/Study/StudyFrameRecorder.cs Assets/_Project/Scripts/Study/StudyFrameRecorder.cs.meta Assets/_Project/Scripts/Study/StudyMenuPanel.cs Assets/_Project/Scripts/Study/StudyMenuPanel.cs.meta Assets/_Project/Scripts/Study/StudyOptions.cs Assets/_Project/Scripts/Study/StudyOptions.cs.meta Assets/_Project/Scripts/Study/StudyRoom.cs Assets/_Project/Scripts/Study/StudyRoom.cs.meta Assets/_Project/Scripts/TimeSpentManager.cs Assets/_Project/Tests/PlayMode/ReplayFlowRuntimeTests.cs Assets/_Project/Tests/PlayMode/StudyReliabilityTests.cs Assets/_Project/Tests/PlayMode/StudyReliabilityTests.cs.meta Assets/_Project/Tests/PlayMode/ZZStudyLevelFlowTests.cs Assets/_Project/Tests/PlayMode/ZZStudyLevelFlowTests.cs.meta
git commit -m 'Record complete takeover state; add neutral menus and study exports'
```

## 2. Unify room materials; add stable scene identities and build utilities

```sh
git add -- Assets/_Project/Editor/StudyLevelUtility.cs Assets/_Project/Editor/StudyLevelUtility.cs.meta Assets/_Project/Materials/Study.meta Assets/_Project/Materials/Study/Floor.mat Assets/_Project/Materials/Study/Floor.mat.meta Assets/_Project/Materials/Study/Masonry.mat Assets/_Project/Materials/Study/Masonry.mat.meta Assets/_Project/Materials/Study/Plinth.mat Assets/_Project/Materials/Study/Plinth.mat.meta Assets/_Project/Scenes/Level.unity Assets/_Project/Shaders.meta Assets/_Project/Shaders/StudyStone.shader Assets/_Project/Shaders/StudyStone.shader.meta
git commit -m 'Unify room materials; add stable scene identities and build utilities'
```

## 3. Document study operation, exports, and validation limits

```sh
git add -- README.md docs/COMMIT_BATCHES.md docs/DATA_EXPORTS.md docs/RECORDING_FORMAT.md docs/RESEARCHER_RUNBOOK.md docs/VALIDATION.md docs/examples/README.md docs/examples/exports/normal/synthetic-normal-attempt/events.csv docs/examples/exports/normal/synthetic-normal-attempt/puzzles.csv docs/examples/exports/normal/synthetic-normal-attempt/session.csv docs/examples/exports/takeover/synthetic-takeover-attempt/comparison.csv docs/examples/exports/takeover/synthetic-takeover-attempt/events.csv docs/examples/exports/takeover/synthetic-takeover-attempt/puzzles.csv docs/examples/exports/takeover/synthetic-takeover-attempt/session.csv docs/examples/synthetic-normal.json docs/examples/synthetic-takeover.json docs/results/polish-expanded-playmode.xml
git commit -m 'Document study operation, exports, and validation limits'
```

