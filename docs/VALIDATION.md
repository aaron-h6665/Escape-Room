# Validation record

## Platform and artifacts

Unity 6000.5.0f1 on macOS ARM64. Runtime, editor, and test assemblies compile against installed Unity references. The Mac development build `/tmp/EscapeRoomStudy-Polish.app` succeeded before the final modal-cleanup fixes and explicit build-version metadata were added. The earlier `/tmp/EscapeRoomStudy.app` was launched and its Controls panel and room rendering inspected. Neither binary is a release certification of later source edits.

Scene validation passed before the Caesar redesign: three rooms, one player, stable unique replay targets, required answer terminal and exit. All three camera renders were produced; room two was visually inspected after the masonry, stand and terminal-label corrections. No vendor assets were edited. The current scene uses a typed Caesar phrase to unlock the passage directly, retains the separate safe key, and keeps the 4271 exit code. Current post-redesign verification is recorded below.

## Simon-linked cipher and backtracking — 2026-09-20

Current build `2026.09.20-polish-v4`, level `three-rooms-polish-v4`, configuration `simon-caesar-dynamic-v4`:

- **30/30 PlayMode tests passed**: [results](results/simon-cipher-playmode.xml). Includes the full escape and eight replay/takeover handoffs, solved Simon rewatch, mid-cue restoration, repeated bidirectional CharacterController traversal, and synthetic keyboard/gamepad input plus mouse hit-testing and dragging.
- **17/17 EditMode tests passed**: [results](results/simon-cipher-editmode.xml), including every-letter outer-to-inner decoding and 27-symbol wraparound.
- The live scene uses one green press for testing. The clue generator follows the final-pattern green count, including changes during PlayMode: one green produces `TJMFOU PSCJU`, two produce `UKNGPV QTDKV`, and three produce `VLOHQW RUELW`. Each decodes to `SILENT ORBIT` with its corresponding clockwise shift.
- Rendered the actual wheel at A/A and one clockwise notch; verified every letter of `TJMFOU PSCJU` outer-to-inner. Fresh runtime rendering did not reproduce an incorrect mapping. Inspection now always uses the authored front face, and R / controller north resets A/A; the HUD reports the notch count and aligned letter.
- Physical paper, readable overlay, and pinned clue share the generated text. Their text equality and the updated pinned panel overflow check pass. Paper rendering was inspected in the preceding revision; the longer current instructions have not received a fresh paper-layout visual pass.
- Fitted the boundary to the actual closed door leaf. Rendered both sides of the closed door and confirmed the side gaps are covered. Straight/oblique ray checks pass, and the actual CharacterController crosses the open passage four times in alternating directions.
- These checks ran in the Unity editor. They do not certify a packaged build, a manual end-to-end input playthrough, or physical controller hardware. Fresh recordings are required for this configuration.

The results below describe earlier revisions and are retained as history.

## Automated results

- After the Caesar phrase redesign, the runtime, editor, and PlayMode test assemblies compiled successfully with Unity's generated Roslyn response files. Only pre-existing Unity API deprecation warnings remain.
- The open Unity editor prevented a second batch-mode instance from saving the scene migration. Runtime startup removes the obsolete key choices before rendering and replaces the old clue text; the in-editor repair command now permanently removes those scene objects when next run. PlayMode execution and a current room-two visual pass remain pending.
- The initial 21-test PlayMode run found a null-source playback guard issue; corrected.
- A subsequent 23-test PlayMode run passed all 23, including recovery-write failure/retry, synthetic export calculations, one complete source run, fresh-scene replay, exact takeover state, and source-byte preservation.
- The full-level test was then expanded to six handoff points: Simon cue, movement/stance, open note, decoder inspection, partial answer and keypad draft, followed by no-takeover completion. It found scene-unload cleanup problems in decoder and answer UI. The current fixes compile, but the latest Unity rerun was declined and therefore has not verified those fixes.
- The last expanded run remains **22/23 passed**; its unedited XML is [polish-expanded-playmode.xml](results/polish-expanded-playmode.xml). Do not present the earlier passing run as validation of every later change.
- EditMode execution and packaged test-runner execution are pending.

The full-level test drives real scene components programmatically; it is not a physical input playthrough. The source records accelerated puzzle transitions for state testing, not realistic participant timings.

## Outstanding acceptance checks

- Rerun the suites after subsequent source changes; the current editor results above include expanded handoffs and the no-takeover outcome.
- Inspect all room viewpoints in a current packaged build, complete both variants using actual movement/interactions, replay each, and take over in every room.
- Exercise held T/RT/click consumption, repeated input, pause/focus loss, nested UI, keypad and decoder animation transitions, collisions, recovery, incompatible files and export retry on the study machine.
- Verify controls/options, main-menu Options binding, controller focus containment, 4:3/16:9 scaling and live device switching.
- A physical controller and Windows have **not** been tested. Record controller model, OS and display settings in the pilot record. Synthetic tests cannot replace this coverage.

Keep recordings from different level/configuration versions separate. Do not start validated data collection until these release checks pass.
