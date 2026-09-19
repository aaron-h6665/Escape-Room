using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class StudyExports
{
    public static string Number(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    public static string Cell(string value)
    {
        value = value ?? "";
        string trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+@-".IndexOf(trimmed[0]) >= 0
            && !double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    static void Row(StringBuilder output, params string[] values) => output.AppendLine(string.Join(",", values.Select(Cell)));
    public static void AtomicWrite(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        using (FileStream file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            file.Write(bytes, 0, bytes.Length);
            file.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
    public static void Write(string root, ReplayRecordingData data, ReplayRecordingData source)
    {
        string folder = Path.Combine(root, "exports", data.recordingKind, data.attemptId);
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("AttemptId,ParticipantCode,RecordingId,SourceRecordingId,Role,LevelVersion,ConfigurationId,BuildVersion,Platform,InputDevice,CreatedUtc,EndedUtc,Status,TerminationReason,ActiveSeconds,TakeoverOccurred,TakeoverAtSeconds,TakeoverRoomId,FirstInteractionAfterTakeoverSeconds,RemainingCompletionSeconds,SourceRemainingCompletionSeconds,SourceSha256,SettingsJson");
        ReplayEventData first = data.events.FirstOrDefault(IsGameplayInteraction);
        bool takeover = data.recordingKind == "takeover";
        Row(csv, data.attemptId, data.participantCode, data.recordingId, data.sourceRecordingId, data.recordingKind,
            data.levelVersion, data.configurationId, data.buildVersion, data.platform, data.inputDevice,
            data.createdUtc, data.endedUtc, data.status, data.terminationReason, Number(data.duration), takeover ? "TRUE" : "FALSE",
            takeover ? Number(data.takeoverAtRecordingTime) : "", data.takeoverRoomId,
            takeover && first != null ? Number(first.recordingTime) : "",
            takeover && data.status == "completed" ? Number(data.duration) : "",
            takeover && source?.status == "completed" ? Number(source.duration - data.takeoverAtRecordingTime) : "", data.sourceSha256, data.settingsJson);
        AtomicWrite(Path.Combine(folder, "session.csv"), csv.ToString());
        csv.Clear();
        csv.AppendLine("AttemptId,RecordingId,Sequence,Utc,ActiveSeconds,GameSeconds,RoomId,ObjectId,ObjectName,Category,Event,State,Succeeded,StateChanged,MilestoneId,Value,NumberValue");
        foreach (ReplayEventData e in data.events)
            Row(csv, data.attemptId, data.recordingId, e.sequence.ToString(CultureInfo.InvariantCulture), e.utc, Number(e.recordingTime), Number(e.gameTime), e.roomId,
                e.objectId, e.objectName, e.objectCategory, e.eventKind, e.state.ToString(), e.succeeded ? "TRUE" : "FALSE", e.stateChanged ? "TRUE" : "FALSE", e.milestoneId, e.textValue, Number(e.numberValue));
        AtomicWrite(Path.Combine(folder, "events.csv"), csv.ToString());
        csv.Clear();
        csv.AppendLine("AttemptId,RoomId,PuzzleId,PuzzleName,FirstInteractionSeconds,CompletionSeconds,FailedAttempts,Outcome");
        foreach (string puzzle in new[] { "simon", "caesar", "keypad" })
        {
            var events = data.events.Where(e => PuzzleId(e) == puzzle).ToList();
            ReplayEventData firstAction = events.FirstOrDefault(IsGameplayInteraction);
            ReplayEventData done = events.FirstOrDefault(e => e.milestoneId == puzzle + "_completed");
            bool inherited = data.recordingKind == "takeover" && source != null && source.events.Any(e => e.sequence <= data.takeoverAfterSequence && e.milestoneId == puzzle + "_completed");
            string room = puzzle == "simon" ? "room_1" : puzzle == "caesar" ? "room_2" : "room_3";
            Row(csv, data.attemptId, room, puzzle, puzzle == "simon" ? "Simon Says" : puzzle == "caesar" ? "Caesar and key choice" : "Exit keypad",
                firstAction != null ? Number(firstAction.recordingTime) : "", done != null ? Number(done.recordingTime) : "",
                events.Count(e => !e.succeeded && (e.eventKind == "simon_failed" || e.eventKind == "caesar_answer_submitted" || e.eventKind == "key_choice_submitted" || e.eventKind == "keypad_denied")).ToString(CultureInfo.InvariantCulture),
                inherited ? "inherited_completed" : done != null ? "completed" : events.Count == 0 ? "not_observed" : "not_completed_in_this_segment");
        }
        AtomicWrite(Path.Combine(folder, "puzzles.csv"), csv.ToString());
        if (takeover && source != null) WriteComparison(folder, data, source);
    }
    static string PuzzleId(ReplayEventData e)
    {
        if (e.eventKind.StartsWith("simon_", StringComparison.Ordinal)) return "simon";
        if (e.eventKind.StartsWith("caesar_", StringComparison.Ordinal) || e.eventKind == "key_choice_submitted") return "caesar";
        if (e.eventKind.StartsWith("keypad_", StringComparison.Ordinal)) return "keypad";
        return "";
    }
    static bool IsGameplayInteraction(ReplayEventData e)
    {
        if (e.objectCategory == "Session" || e.objectCategory == "Room") return false;
        return e.eventKind.EndsWith("_interacted", StringComparison.Ordinal) || e.eventKind.EndsWith("_submitted", StringComparison.Ordinal)
            || e.eventKind.EndsWith("_attempted", StringComparison.Ordinal) || e.eventKind.EndsWith("_pressed", StringComparison.Ordinal)
            || e.eventKind == "keypad_input" || e.eventKind == "note_opened" || e.eventKind == "item_dropped"
            || e.eventKind == "inventory_slot_selected" || e.eventKind == "caesar_answer_entry_changed" || e.eventKind == "simon_started" || e.eventKind == "caesar_ring_rotated";
    }

    static void WriteComparison(string folder, ReplayRecordingData data, ReplayRecordingData source)
    {
        StringBuilder csv = new StringBuilder("AttemptId,SourceRecordingId,TakeoverRecordingId,MilestoneId,OriginalSecondsAfterHandoff,TakeoverSecondsAfterHandoff,DifferenceSeconds,Outcome\n");
        var normal = source.events.Where(e => e.sequence > data.takeoverAfterSequence && !string.IsNullOrEmpty(e.milestoneId)).GroupBy(e => e.milestoneId).ToDictionary(g => g.Key, g => g.First());
        var taken = data.events.Where(e => !string.IsNullOrEmpty(e.milestoneId)).GroupBy(e => e.milestoneId).ToDictionary(g => g.Key, g => g.First());
        foreach (string id in normal.Keys.Union(taken.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            normal.TryGetValue(id, out ReplayEventData n); taken.TryGetValue(id, out ReplayEventData t);
            float a = n != null ? n.recordingTime - data.takeoverAtRecordingTime : 0;
            Row(csv, data.attemptId, source.recordingId, data.recordingId, id, n != null ? Number(a) : "", t != null ? Number(t.recordingTime) : "",
                n != null && t != null ? Number(t.recordingTime - a) : "", n != null && t != null ? "matched" : n == null ? "takeover_only" : "original_only");
        }
        AtomicWrite(Path.Combine(folder, "comparison.csv"), csv.ToString());
    }
}
