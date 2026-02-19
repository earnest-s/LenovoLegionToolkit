using System;
using System.IO;

namespace LenovoLegionToolkit.Lib.Utils;

public static class Folders
{
    public static string Program => AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty;

    public static string AppData
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var newFolderPath = Path.Combine(localAppData, "LOQNova");
            var oldFolderPath = Path.Combine(localAppData, "LenovoLegionToolkit");

            // One-time migration: move existing settings from old folder to new folder.
            if (!Directory.Exists(newFolderPath) && Directory.Exists(oldFolderPath))
            {
                try { Directory.Move(oldFolderPath, newFolderPath); }
                catch { /* migration is best-effort; fall through to create */ }
            }

            Directory.CreateDirectory(newFolderPath);
            return newFolderPath;
        }
    }

    public static string Temp
    {
        get
        {
            var appData = Path.GetTempPath();
            var folderPath = Path.Combine(appData, "LOQNova");
            Directory.CreateDirectory(folderPath);
            return folderPath;
        }
    }
}
