# Synthetic export fixtures

These are invented behavioral examples, not participant data and not selectable gameplay recordings. They intentionally omit full pose/checkpoint streams. Generate them with `StudyLevelUtility.WriteExamples`; it uses the production exporter.

Expected values: source duration 30 seconds; takeover at 15 seconds; completed continuation duration 8 seconds; source remaining completion time 15 seconds; first continuation gameplay input at 1 second. The remaining keypad milestone occurs at source 20 seconds (5 seconds after handoff), continuation 3 seconds, difference -2 seconds. Simon was completed before handoff and must not appear in the comparison rows. Caesar has no observed completion in this synthetic example.

Empty cells mean unavailable/not applicable. These fixtures demonstrate export calculations, not replay compatibility or an experimental effect.
