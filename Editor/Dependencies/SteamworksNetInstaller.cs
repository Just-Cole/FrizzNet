using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
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
        private const string InstallRequestedKey = "FrizzNet.SteamworksNetInstallRequested";

        private static AddRequest s_AddRequest;

        static SteamworksNetInstaller()
        {
            EditorApplication.delayCall += EnsureInstalled;
        }

        private static void EnsureInstalled()
        {
            if (IsSteamworksInstalled())
                return;

            if (SessionState.GetBool(InstallRequestedKey, false))
                return;

            SessionState.SetBool(InstallRequestedKey, true);
            Debug.Log("[FrizzNet] Steamworks.NET is required. Installing from GitHub...");
            s_AddRequest = Client.Add(PackageGitUrl);
            EditorApplication.update += PollAddRequest;
        }

        private static void PollAddRequest()
        {
            if (s_AddRequest == null || !s_AddRequest.IsCompleted)
                return;

            EditorApplication.update -= PollAddRequest;

            if (s_AddRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[FrizzNet] Installed Steamworks.NET {s_AddRequest.Result.version}.");
            }
            else
            {
                string error = s_AddRequest.Error != null ? s_AddRequest.Error.message : "Unknown Package Manager error.";
                Debug.LogError(
                    "[FrizzNet] Failed to install Steamworks.NET automatically. " +
                    "Add it from Package Manager with this Git URL:\n" +
                    PackageGitUrl + "\n" +
                    error);
                SessionState.SetBool(InstallRequestedKey, false);
            }

            s_AddRequest = null;
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
    }
}
