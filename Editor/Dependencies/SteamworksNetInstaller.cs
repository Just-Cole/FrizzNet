using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace FrizzNet.Editor.Dependencies
{
    [InitializeOnLoad]
    internal static class SteamworksNetInstaller
    {
        private const string PackageName = "com.rlabrecque.steamworks.net";
        private const string PackageGitUrl =
            "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1";
        private const string ManifestPath = "Packages/manifest.json";

        static SteamworksNetInstaller()
        {
            EnsureInstalled();
        }

        private static void EnsureInstalled()
        {
            if (IsSteamworksInstalled() || ManifestHasSteamworks())
                return;

            if (!TryAddSteamworksToManifest())
            {
                Debug.LogError(
                    "[FrizzNet] Could not add Steamworks.NET to the project manifest. " +
                    "Add it from Package Manager with this Git URL:\n" +
                    PackageGitUrl);
            }
        }

        private static bool IsSteamworksInstalled()
        {
            PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages();
            for (int i = 0; i < packages.Length; i++)
            {
                if (packages[i].name == PackageName)
                    return true;
            }

            return false;
        }

        private static bool ManifestHasSteamworks()
        {
            if (!File.Exists(ManifestPath))
                return false;

            string json = File.ReadAllText(ManifestPath);
            return json.IndexOf("\"" + PackageName + "\"", StringComparison.Ordinal) >= 0;
        }

        private static bool TryAddSteamworksToManifest()
        {
            if (!File.Exists(ManifestPath))
                return false;

            string json = File.ReadAllText(ManifestPath);
            const string dependenciesKey = "\"dependencies\"";
            int dependenciesIndex = json.IndexOf(dependenciesKey, StringComparison.Ordinal);
            if (dependenciesIndex < 0)
                return false;

            int braceIndex = json.IndexOf('{', dependenciesIndex);
            if (braceIndex < 0)
                return false;

            string entry = Environment.NewLine + "    \"" + PackageName + "\": \"" + PackageGitUrl + "\",";
            json = json.Insert(braceIndex + 1, entry);
            File.WriteAllText(ManifestPath, json);
            Debug.Log("[FrizzNet] Added Steamworks.NET to Packages/manifest.json.");
            return true;
        }
    }
}
