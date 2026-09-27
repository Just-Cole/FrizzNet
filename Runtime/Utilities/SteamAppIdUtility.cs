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
            Environment.SetEnvironmentVariable("SteamAppId", AppId);
            TryWrite(Directory.GetCurrentDirectory());

            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot != null)
                TryWrite(projectRoot.FullName);
        }

        private static void TryWrite(string directory)
        {
            if (string.IsNullOrEmpty(directory))
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
