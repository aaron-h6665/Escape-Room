# Data dictionary and exports

Exports use UTF-8 CSV with a header. Text is quoted/escaped; text beginning with spreadsheet formula characters is prefixed. Numeric values use invariant decimal formatting and explicit seconds units. Empty timing cells mean unobserved/not applicable, never zero.

## session.csv — one row per attempt

Identity/context columns: `AttemptId`, `ParticipantCode`, `RecordingId`, `SourceRecordingId`, `Role`, `LevelVersion`, `ConfigurationId`, `BuildVersion`, `Platform`, `InputDevice`, `CreatedUtc`, `EndedUtc`, `SourceSha256`, `SettingsJson`.

| Measure | Definition |
| --- | --- |
| `Status`, `TerminationReason` | Completed normal/continuation, observation completed without takeover, taken over, or incomplete; plus the termination trigger. |
| `ActiveSeconds` | Duration excluding pause and lost-focus time. |
| `TakeoverOccurred` | TRUE only for a takeover continuation. The linked observation outcome also records `taken_over`. |
| `TakeoverAtSeconds`, `TakeoverRoomId` | Source playhead/location when control transferred; blank when absent. |
| `FirstInteractionAfterTakeoverSeconds` | First qualifying gameplay action in the continuation (interaction, input, draft, submission, pickup attempt, drop or inventory selection); automatic cues and rewards are excluded. This is not a cognitive reaction-time estimate. |
| `RemainingCompletionSeconds` | Continuation duration if completed; blank if incomplete. |
| `SourceRemainingCompletionSeconds` | Completed source duration minus handoff time. |

## events.csv — one row per semantic event

`AttemptId`, `RecordingId`, `Sequence`, `Utc`, `ActiveSeconds`, `GameSeconds`, `RoomId`, `ObjectId`, `ObjectName`, `Category`, `Event`, `State`, `Succeeded`, `StateChanged`, `MilestoneId`, `Value`, `NumberValue`.

Keep sequence order when timestamps tie. Events before takeover belong to the source, not to the continuation. Failed actions remain in the export even if they did not change the world. Input values/drafts may reveal typed participant content.

## puzzles.csv — one row per authored puzzle

`AttemptId`, `RoomId`, `PuzzleId`, `PuzzleName`, `FirstInteractionSeconds`, `CompletionSeconds`, `FailedAttempts`, `Outcome`.

Rows aggregate Simon, Caesar/key choice, and keypad events under three stable puzzle IDs. `FirstInteractionSeconds` is the first qualifying participant action, not room onset or an automatically played cue. `FailedAttempts` counts only simon_failed, caesar_answer_submitted, key_choice_submitted, and keypad_denied events with succeeded=false; duplicate terminal presentation events do not double-count an answer. Outcomes distinguish inherited_completed, completed, not_observed, and not_completed_in_this_segment. The last outcome does not establish inability or failure.

## comparison.csv — one row per remaining authored milestone

`AttemptId`, `SourceRecordingId`, `TakeoverRecordingId`, `MilestoneId`, `OriginalSecondsAfterHandoff`, `TakeoverSecondsAfterHandoff`, `DifferenceSeconds`, `Outcome`.

Source events must have sequence greater than `takeoverAfterSequence`. Each milestone is matched by stable authored ID, taking its first occurrence in each continuation. Original time is source milestone time minus handoff time. Difference is takeover minus original; a negative value means earlier in the takeover continuation. Unmatched milestones have blank missing times/differences and `original_only` or `takeover_only` outcome.

These are descriptive within-recording comparisons. They do not alone establish a causal takeover effect. Account for assignment, voluntary selection time, device, settings, prior knowledge, incomplete outcomes, and build/configuration in the analysis.

## Quiz-app alignment

Both projects separate behavioral JSON from readable attempt exports and link original/observer results by IDs. This project intentionally uses seconds in every column labeled Seconds. It does not reproduce quiz-app's documented legacy milliseconds-in-Seconds-column convention. No existing quiz-app files are modified.
