using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class TakeoverComparison
{
    class MatchedEvent
    {
        public ReplayEventData normal;
        public ReplayEventData takeover;
        public int occurrence;
    }

    public static int WriteCsv(ReplayRecordingData normal, ReplayRecordingData takeover, string summaryId, string path)
    {
        List<MatchedEvent> matches = MatchEvents(normal, takeover);
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("summary_id,normal_recording_id,takeover_recording_id,takeover_at_seconds,event_number,what_happened,object_name,object_id,normal_time_after_takeover_seconds,takeover_time_after_takeover_seconds,difference_seconds,plain_english_result");

        int normalCount = normal?.events?.Count(e => IsComparable(e) && e.recordingTime >= takeover.takeoverAtRecordingTime) ?? 0;
        int takeoverCount = takeover?.events?.Count(IsComparable) ?? 0;
        List<ReplayEventData> normalContinuation = normal?.events?.Where(e => IsComparable(e) && e.recordingTime >= takeover.takeoverAtRecordingTime).ToList() ?? new List<ReplayEventData>();
        List<ReplayEventData> takeoverContinuation = takeover?.events?.Where(IsComparable).ToList() ?? new List<ReplayEventData>();
        string overall = $"Overall: takeover started at {takeover.takeoverAtRecordingTime:0.0} seconds. "
            + $"Normal player: {normalCount} changes, {CountKind(normalContinuation, "picked_up")} pickups, {CountKind(normalContinuation, "dropped")} drops, {CountObjectives(normalContinuation)} objectives. "
            + $"Takeover player: {takeoverCount} changes, {CountKind(takeoverContinuation, "picked_up")} pickups, {CountKind(takeoverContinuation, "dropped")} drops, {CountObjectives(takeoverContinuation)} objectives.";
        AppendRow(csv, summaryId, normal?.recordingId, takeover?.recordingId, takeover?.takeoverAtRecordingTime ?? 0f, 0, "Overall summary", "Whole run", string.Empty, null, null, null, overall);

        int eventNumber = 1;
        foreach (MatchedEvent match in matches)
        {
            ReplayEventData display = match.takeover ?? match.normal;
            float? normalTime = match.normal != null ? match.normal.recordingTime - takeover.takeoverAtRecordingTime : (float?)null;
            float? takeoverTime = match.takeover != null ? match.takeover.recordingTime : (float?)null;
            float? difference = normalTime.HasValue && takeoverTime.HasValue ? takeoverTime.Value - normalTime.Value : (float?)null;
            string result;
            if (!normalTime.HasValue)
            {
                result = "Only the takeover player did this.";
            }
            else if (!takeoverTime.HasValue)
            {
                result = "Only the normal player did this.";
            }
            else if (Mathf.Abs(difference.Value) < 0.05f)
            {
                result = "Both players did this at about the same time.";
            }
            else if (difference.Value < 0f)
            {
                result = $"Takeover did this {Mathf.Abs(difference.Value):0.0} seconds faster.";
            }
            else
            {
                result = $"Takeover did this {difference.Value:0.0} seconds slower.";
            }

            string whatHappened = Humanize(display.eventKind, display.state, match.occurrence);
            AppendRow(csv, summaryId, normal?.recordingId, takeover?.recordingId, takeover?.takeoverAtRecordingTime ?? 0f, eventNumber++, whatHappened, display.objectName, display.objectId, normalTime, takeoverTime, difference, result);
        }

        AtomicWrite(path, csv.ToString());
        return matches.Count + 1;
    }

    static List<MatchedEvent> MatchEvents(ReplayRecordingData normal, ReplayRecordingData takeover)
    {
        List<ReplayEventData> normalEvents = normal?.events?.Where(e => IsComparable(e) && e.recordingTime >= takeover.takeoverAtRecordingTime).ToList() ?? new List<ReplayEventData>();
        List<ReplayEventData> takeoverEvents = takeover?.events?.Where(IsComparable).ToList() ?? new List<ReplayEventData>();
        Dictionary<string, Queue<ReplayEventData>> normalByKey = BuildQueues(normalEvents);
        Dictionary<string, int> occurrences = new Dictionary<string, int>();
        List<MatchedEvent> matches = new List<MatchedEvent>();

        foreach (ReplayEventData takeoverEvent in takeoverEvents)
        {
            string key = takeoverEvent.ComparisonKey;
            occurrences.TryGetValue(key, out int occurrence);
            occurrence++;
            occurrences[key] = occurrence;
            ReplayEventData normalEvent = null;
            if (normalByKey.TryGetValue(key, out Queue<ReplayEventData> queue) && queue.Count > 0)
            {
                normalEvent = queue.Dequeue();
            }

            matches.Add(new MatchedEvent { normal = normalEvent, takeover = takeoverEvent, occurrence = occurrence });
        }

        foreach (KeyValuePair<string, Queue<ReplayEventData>> pair in normalByKey)
        {
            occurrences.TryGetValue(pair.Key, out int occurrence);
            while (pair.Value.Count > 0)
            {
                occurrence++;
                matches.Add(new MatchedEvent { normal = pair.Value.Dequeue(), occurrence = occurrence });
            }
        }

        return matches.OrderBy(m => m.takeover?.recordingTime ?? float.MaxValue).ThenBy(m => m.normal?.recordingTime ?? float.MaxValue).ToList();
    }

    static Dictionary<string, Queue<ReplayEventData>> BuildQueues(IEnumerable<ReplayEventData> events)
    {
        Dictionary<string, Queue<ReplayEventData>> result = new Dictionary<string, Queue<ReplayEventData>>();
        foreach (ReplayEventData replayEvent in events)
        {
            if (!result.TryGetValue(replayEvent.ComparisonKey, out Queue<ReplayEventData> queue))
            {
                queue = new Queue<ReplayEventData>();
                result.Add(replayEvent.ComparisonKey, queue);
            }
            queue.Enqueue(replayEvent);
        }
        return result;
    }

    static bool IsComparable(ReplayEventData replayEvent)
    {
        return replayEvent != null && replayEvent.stateChanged;
    }

    static int CountKind(IEnumerable<ReplayEventData> events, string fragment)
    {
        return events.Count(replayEvent => (replayEvent.eventKind ?? string.Empty).IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static int CountObjectives(IEnumerable<ReplayEventData> events)
    {
        return events.Count(replayEvent => replayEvent.state == ReplayObjectState.Open
            && (string.Equals(replayEvent.objectCategory, "Door", StringComparison.OrdinalIgnoreCase)
                || string.Equals(replayEvent.objectCategory, "Safe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(replayEvent.objectCategory, "Room", StringComparison.OrdinalIgnoreCase)));
    }

    static string Humanize(string eventKind, ReplayObjectState state, int occurrence)
    {
        string words = string.IsNullOrWhiteSpace(eventKind) ? "Event" : eventKind.Replace('_', ' ');
        string suffix = occurrence > 1 ? $" (time {occurrence})" : string.Empty;
        return $"{words}: {state}{suffix}";
    }

    static void AppendRow(StringBuilder csv, string summaryId, string normalId, string takeoverId, float takeoverAt, int eventNumber, string action, string objectName, string objectId, float? normalTime, float? takeoverTime, float? difference, string result)
    {
        string[] fields =
        {
            summaryId, normalId, takeoverId, Format(takeoverAt), eventNumber.ToString(CultureInfo.InvariantCulture), action, objectName, objectId,
            normalTime.HasValue ? Format(normalTime.Value) : string.Empty,
            takeoverTime.HasValue ? Format(takeoverTime.Value) : string.Empty,
            difference.HasValue ? Format(difference.Value) : string.Empty,
            result
        };
        csv.AppendLine(string.Join(",", fields.Select(Escape)));
    }

    static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    static string Escape(string value)
    {
        value = value ?? string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    static void AtomicWrite(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, contents, Encoding.UTF8);
        if (File.Exists(path))
        {
            File.Replace(temporaryPath, path, null);
        }
        else
        {
            File.Move(temporaryPath, path);
        }
    }
}
