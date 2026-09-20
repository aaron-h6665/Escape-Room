# Recording format 3

## Contract

A JSON object contains metadata, `poses`, `worldCheckpoints`, and `events`. The source recording remains immutable. A takeover creates a new recording with source IDs, source hash, handoff timestamp, last included source event sequence, handoff room, and an initial checkpoint of the inherited state.

| Field | Meaning |
| --- | --- |
| `formatVersion` | 3 for the complete-state study contract. |
| `attemptId`, `recordingId` | Opaque IDs; store as text. |
| `participantCode` | Optional researcher pseudonym. Empty means absent. |
| `recordingKind` | `normal`, `observation`, or `takeover`. |
| `sourceRecordingId`, `sourceAttemptId` | Source recording and linked observation attempt for a takeover. |
| `sourceSha256` | SHA-256 of the exact source file bytes. |
| `levelVersion`, `configurationId`, `buildVersion` | Variant, puzzle configuration and application build. |
| `platform`, `inputDevice`, `settingsJson` | Initial device context and options. Changes are session events. |
| `createdUtc`, `endedUtc` | UTC ISO 8601 timestamps. |
| `status`, `terminationReason` | Completion/termination outcome; an incomplete run is not a successful completion. |
| `duration` | Active duration in seconds; pauses/focus loss excluded. |
| `takeoverAtRecordingTime`, `takeoverAtGameTime` | Source active time and inherited game time at handoff. |
| `takeoverAfterSequence` | Last source event represented in the inherited state. |
| `targetIds` | Stable authored identity manifest; checked against the loaded scene before playback. |
| `snapshotDelta` | Requested maximum sampling frequency interval; 1/30 second by default. |

## State and timing

Capture occurs after gameplay and camera LateUpdate. Periodic samples are stamped with the actual capture time; frame stalls do not generate several identical poses with fictitious earlier times. Semantic events are not throttled and force a complete checkpoint at the end of that rendered frame.

Each pose includes active/game seconds, position, body rotation, and camera pitch. Each checkpoint includes `frameTime`, `lastEventSequence`, and complete `gameData`: motor velocity and stance, camera height, selected inventory slot and item source IDs, world pickups, puzzle drafts/progress, open notes, cipher inspection, answer-entry text/focus, door/safe animation progress, Simon cue phase/index/remaining time, and keypad feedback time.

Gameplay timers advance on Unity's shared scaled clock. Pause/focus loss freezes it. Playback restores the latest checkpoint at or before the playhead, evaluates supported continuous presentation from its remaining time, and interpolates the camera pose. It does not re-run semantic events already represented by a checkpoint. Presentation accuracy is bounded by source sampling/frame rate; no new high-frequency motion is invented.

Handoff uses the already-displayed state, does not advance the source first, and consumes the initiating input until release. Open interfaces remain open and their controls become live. Continued interaction may diverge immediately; that is recorded in the takeover segment, not merged into the source.

## Events

`sequence` is a monotonically increasing integer within a recording. Equal timestamps retain sequence order. Event `utc` supplies wall-clock context for interruptions without adding paused time to active gameplay. `recordingTime` and `gameTime` are seconds. `objectId` is stable; `objectName` is the readable label; `objectCategory` distinguishes objects from session events. `succeeded` and `stateChanged` are separate: a failed attempt is still an event.

`eventKind` describes the action; `textValue` contains a complete answer/input value where applicable; `numberValue` contains the event-specific numeric value; `customPayload` retains existing puzzle-specific detail. Room IDs identify location. Authored milestone IDs are `simon_completed`, `caesar_completed`, `keypad_completed`, and `escape_completed`.

Session events include pause/resume, focus loss/restoration, settings changes, input-device changes, and takeover. Object events include reads, pickups/drops, button presses, answer drafts/submissions, failures, and puzzle completion. Movement and camera pose are behavioral/view traces, not measurements of eye gaze.

Solved Simon rewatch emits `simon_rewatch_started` and keeps the puzzle solved. Its delay/on/gap stages use the existing `timelineStage`, `cueIndex`, `stageRemaining`, and `sequence` fields; no format-version change is needed. Completion events are not emitted again. The Simon-linked cipher uses level `three-rooms-polish-v4` and configuration `simon-caesar-dynamic-v4`, so older puzzle recordings are excluded from compatible selection.

## Compatibility

Selection requires a completed normal recording with format 3, matching level/configuration, valid ordered events, and nonempty valid pose/checkpoint sequences beginning at time zero. Scene identities are authored into the scene and validated for duplicates. Changing gameplay, layouts, timing, or replay semantics requires a new configuration/level version and fresh source recordings.

Legacy preview is explicitly opt-in and cannot support validated takeover. Do not relabel old recordings as format 3. No migration can infer an unrecorded velocity, timer remainder, or UI draft reliably.
