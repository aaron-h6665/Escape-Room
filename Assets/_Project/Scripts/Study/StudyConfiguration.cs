using System;
using System.IO;
using UnityEngine;

/// <summary>Researcher-only file in persistentDataPath; never presented in participant menus.</summary>
[Serializable]
public sealed class StudyConfiguration
{
    public const string BuildVersion = "2026.09.19-polish-v1";
    public const string LevelVersion = "three-rooms-polish-v1";
    public const string ConfigurationId = "fixed-puzzles-v1";
    public string participantCode = "";
    public string playbackSelection = "random";
    public string recordingFile = "";
    public bool allowLegacyPreview;

    public static StudyConfiguration Read(string storageRoot)
    {
        string path = Path.Combine(storageRoot, "study-settings.json");
        if (!File.Exists(path)) return null;
        StudyConfiguration value = JsonUtility.FromJson<StudyConfiguration>(File.ReadAllText(path));
        if (value == null || (value.playbackSelection != "random" && value.playbackSelection != "manual"))
            throw new InvalidDataException("study-settings.json: playbackSelection must be random or manual.");
        return value;
    }
}

/// <summary>Presentation-only interpolation after restoring a recorded frame. Never emits gameplay events.</summary>
public interface IReplayTimeline
{
    void AdvanceReplayPresentation(float seconds);
}

public interface IReplayHandoff
{
    void OnTakeover();
}
