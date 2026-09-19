# Researcher runbook

## Participant presentation

Keep the existing game-mode menu labels. Do not explain the experiment, describe a previous player, identify a condition, or suggest when to take control. Provide only the same optional Controls/Options interface and in-world puzzle instructions. Researcher documentation, configuration, traces, and comparisons are not part of the participant interface.

The controls page documents action bindings, including the existing takeover action, without explaining its purpose. It does not add a mandatory practice round. Use the same availability of controls information across sessions.

## Setup

1. Use the intended variant/build and record its version. Unity 6000.5.0f1 is the development baseline. Validate the actual study machine and physical controller before recruitment.
2. Find `Application.persistentDataPath`. With the existing product settings on macOS this is normally `~/Library/Application Support/DefaultCompany/My project`; on Windows it is normally `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project`. Do not rename Company/Product during a study without migrating the data location.
3. Place `study-settings.json` directly in that directory, using the example below. The file is researcher-only and is read when a new run or playback starts. No file means random compatible selection with an empty participant code.
4. Obtain fresh, complete format-3 normal runs from this same variant/configuration. Place source files under `recordings/normal/`. Keep an immutable backup of source recordings.
5. Test a complete normal run, no-takeover observation, and takeover continuation. Confirm all expected outputs before collecting data.

```json
{
  "participantCode": "P001",
  "playbackSelection": "random",
  "recordingFile": "",
  "allowLegacyPreview": false
}
```

For manual selection set `playbackSelection` to `manual` and `recordingFile` to the source recording ID, filename, or absolute local JSON path. A missing, incomplete, malformed, or incompatible file stops launch; it never falls back to another recording. Random selection is uniform over eligible files; assignment is not counterbalanced automatically. Selected source IDs and SHA-256 hashes are saved with observation and takeover sessions.

Participant codes are optional researcher-supplied pseudonyms. Do not enter names or unnecessary identifiers. No server assigns or verifies codes.

## Session procedure

- Set the participant code before launch. Keep the build, puzzle configuration, and selection procedure fixed for a study batch.
- Run from the existing menu. Normal mode begins from a clean scene, without takeover controls. Playback exposes the existing takeover controls and otherwise blocks gameplay input.
- Record settings at launch and any subsequent changes. Pauses and application-focus loss stop active time and are logged. Wall-clock UTC timestamps remain distinct from active durations.
- At completion, check that there is no save error. If saving fails, use Retry save. Menu and Exit must not discard pending finalized output.
- For a stopped session, retain its incomplete outcome. An observer who never takes over is a valid observation outcome, not missing data.
- Back up the whole attempt directory plus source JSON. Use IDs as text and join by column names, not positions.

## Files and recovery

- `recordings/normal/<recording-id>.json`: original live runs.
- `recordings/observation/<recording-id>.json`: observation metadata and observer interruption events, including no-takeover outcomes.
- `recordings/takeover/<recording-id>.json`: new continuation; the source is never overwritten.
- `exports/<role>/<attempt-id>/`: session, event, puzzle and applicable comparison CSVs.
- `recovery/<recording-id>.json`: periodically replaced recovery snapshot, written approximately every five active seconds, plus session start/handoff.

Recovery files may lag the most recent interaction by five active seconds. Final outputs are atomic per file, not a multi-file filesystem transaction; retrying regenerates exports using the same attempt ID. Do not count recovery and final files as separate attempts. Recovery files remain outside the random playback pool. Preserve a recovery file as incomplete evidence if a process crashes; do not silently treat it as a completed source or automatically resume a participant's interrupted experiment.

## Legacy preview

`allowLegacyPreview: true` with manual selection permits researcher inspection of older recordings. It does not make their missing state recoverable, and takeover is disabled for those files. Do not use this mode for study sessions. Keep legacy files and their analysis separate from format 3.

## Troubleshooting

- **Unable to load:** inspect the editor/player log and selection file. Check completion status, format, variant, configuration, and source path.
- **Could not save:** restore writable disk space/access, then Retry save. Preserve recovery files if the process must stop.
- **Editor license unavailable:** sign in to Unity Hub and activate an existing license. Batch execution may need the active Hub/Editor licensing IPC connection.
- **Unexpected scene change:** use the continuous Level scene, not vendor demos or the older prototype rebuild action.

Follow the study's existing access, retention, and backup policy. Files contain detailed behavior, typed drafts, timestamps, settings, and participant codes; do not commit real participant data to Git.
