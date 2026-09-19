# Escape Room

A Unity 6000.5.0f1 desktop escape room with three connected puzzles, normal session recording, playback, and voluntary control handoff.

## Open and run

Open this directory through Unity Hub with Unity **6000.5.0f1** and an active license. Open `Assets/_Project/Scenes/MenuScene.unity` and enter Play mode. Keep the existing menu labels: participants see controls and in-world puzzle instructions only. Controls and Options are optional panels available from the main menu and pause screen.

The normal menu action starts a fresh recorded game. The other existing action loads a compatible normal recording. Control can transfer with the existing top-center button, T, or RT. Researcher setup is a local file, not a participant-facing menu.

## Researcher documentation

- [Runbook](docs/RESEARCHER_RUNBOOK.md): setup, recording selection, running sessions, saving, troubleshooting.
- [Recording format](docs/RECORDING_FORMAT.md): state, timing, handoff, and compatibility.
- [Data exports](docs/DATA_EXPORTS.md): columns and descriptive measures.
- [Validation](docs/VALIDATION.md): checks performed and remaining release gates.
- [Commit batches](docs/COMMIT_BATCHES.md): implementation commits and exact staging commands.

## Development

Use **Tools → Escape Room → Finish Study Level** to apply the idempotent, in-place material/lighting/identity setup. This preserves existing room geometry and puzzle solutions. Do not use the older Rebuild Level Prototype action on the finalized scene.

Use **Tools → Escape Room → Validate Study Level** to check room markers, unique replay targets, player count, terminal, and exit. Run Unity Test Runner EditMode and PlayMode suites. `StudyLevelUtility.BuildMac` creates a development application at `/tmp/EscapeRoomStudy-Polish.app` for validation.

The validated recording contract is format 3. Existing format 2 files do not contain sufficient movement, cue, and animation state for reliable handoff. Do not mix recordings from the polish and redesign variants or from changed puzzle configurations.

No network service is required. Generated recordings and exports stay outside the repository under Unity's `Application.persistentDataPath`.
