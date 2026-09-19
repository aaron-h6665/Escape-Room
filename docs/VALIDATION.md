# Validation record

## Platform and artifacts

Unity 6000.5.0f1 on macOS ARM64. Runtime, editor, and test assemblies compile against installed Unity references. The Mac development build `/tmp/EscapeRoomStudy-Polish.app` succeeded before the final modal-cleanup fixes and explicit build-version metadata were added. The earlier `/tmp/EscapeRoomStudy.app` was launched and its Controls panel and room rendering inspected. Neither binary is a release certification of later source edits.

Scene validation passed: three rooms, one player, stable unique replay targets, required answer terminal and exit. All three camera renders were produced; room two was visually inspected after the masonry, stand and terminal-label corrections. No vendor assets were edited. The polish scene retains its existing single-round Simon override, BLUE KEY verification/choice, separate safe key, and 4271 exit code.

## Automated results

- The initial 21-test PlayMode run found a null-source playback guard issue; corrected.
- A subsequent 23-test PlayMode run passed all 23, including recovery-write failure/retry, synthetic export calculations, one complete source run, fresh-scene replay, exact takeover state, and source-byte preservation.
- The full-level test was then expanded to six handoff points: Simon cue, movement/stance, open note, decoder inspection, partial answer and keypad draft, followed by no-takeover completion. It found scene-unload cleanup problems in decoder and answer UI. The current fixes compile, but the latest Unity rerun was declined and therefore has not verified those fixes.
- The last expanded run remains **22/23 passed**; its unedited XML is [polish-expanded-playmode.xml](results/polish-expanded-playmode.xml). Do not present the earlier passing run as validation of every later change.
- EditMode execution and packaged test-runner execution are pending.

The full-level test drives real scene components programmatically; it is not a physical input playthrough. The source records accelerated puzzle transitions for state testing, not realistic participant timings.

## Outstanding acceptance checks

- Rerun all EditMode/PlayMode suites after the latest cleanup fixes; verify expanded handoffs and no-takeover outcome.
- Inspect all room viewpoints in a current packaged build, complete both variants using actual movement/interactions, replay each, and take over in every room.
- Exercise held T/RT/click consumption, repeated input, pause/focus loss, nested UI, keypad and decoder animation transitions, collisions, recovery, incompatible files and export retry on the study machine.
- Verify controls/options, main-menu Options binding, controller focus containment, 4:3/16:9 scaling and live device switching.
- A physical controller and Windows have **not** been tested. Record controller model, OS and display settings in the pilot record. Synthetic tests cannot replace this coverage.

Keep recordings from different level/configuration versions separate. Do not start validated data collection until these release checks pass.
