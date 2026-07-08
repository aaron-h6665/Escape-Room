using System.IO;
using UnityEngine;

public static class SaveFileUtility
{
    public const string DefaultSaveFileName = "data.game";

    public static string GetPath(string fileName, string dataDirPath = null)
    {
        string resolvedFileName = string.IsNullOrWhiteSpace(fileName) ? DefaultSaveFileName : fileName;
        string resolvedDataDirPath = string.IsNullOrWhiteSpace(dataDirPath) ? Application.persistentDataPath : dataDirPath;

        return Path.Combine(resolvedDataDirPath, resolvedFileName);
    }

    public static bool Exists(string fileName, string dataDirPath = null)
    {
        return File.Exists(GetPath(fileName, dataDirPath));
    }

    public static void Delete(string fileName, string dataDirPath = null)
    {
        string fullPath = GetPath(fileName, dataDirPath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
