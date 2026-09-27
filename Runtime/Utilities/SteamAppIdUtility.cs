using System;
using System.IO;
using UnityEngine;
using FrizzNet.Logging;

namespace FrizzNet.Steam
{
    internal static class SteamAppIdUtility
    {
        public const string AppId = "480";

        public static void EnsureFile()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot != null)
                TryWrite(projectRoot.FullName);

            if (Application.isEditor)
            {
                // Never stamp steam_appid.txt or SteamAppId onto the Unity Editor process.
                // Steam then treats the editor as SpaceWar and relaunches/closes it.
                Environment.SetEnvironmentVariable("SteamAppId", null);
                return;
            }

            Environment.SetEnvironmentVariable("SteamAppId", AppId);

            string cwd = Directory.GetCurrentDirectory();
            if (!IsUnityEditorInstallDirectory(cwd))
                TryWrite(cwd);
        }

        private static bool IsUnityEditorInstallDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return false;

            string full = Path.GetFullPath(directory);
            return full.IndexOf("Unity" + Path.DirectorySeparatorChar + "Hub" + Path.DirectorySeparatorChar + "Editor", StringComparison.OrdinalIgnoreCase) >= 0
                   || full.EndsWith(Path.DirectorySeparatorChar + "Editor", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryWrite(string directory)
        {
            if (string.IsNullOrEmpty(directory) || IsUnityEditorInstallDirectory(directory))
                return;

            try
            {
                string path = Path.Combine(directory, "steam_appid.txt");
                if (!File.Exists(path) || File.ReadAllText(path).Trim() != AppId)
                    File.WriteAllText(path, AppId);
            }
            catch (Exception e)
            {
                FrizzLogger.LogWarning("Could not write steam_appid.txt to " + directory + ": " + e.Message);
            }
        }
    }
}
