using UnityEngine;
using Steamworks;
using FrizzNet.Logging;

namespace FrizzNet.Steam
{
    /// <summary>
    /// Lightweight manager responsible for initializing the Steamworks API, 
    /// running callbacks every frame, and shutting down on application quit.
    /// </summary>
    [DisallowMultipleComponent]
    public class SteamManager : MonoBehaviour
    {
        private static SteamManager s_Instance;
        private static bool s_Initialized;

        /// <summary>
        /// Gets whether the Steamworks API is initialized successfully.
        /// </summary>
        public static bool Initialized => s_Initialized;

        [Header("Steam Configuration")]
        [Tooltip("Force Steamworks to shutdown if the client is not running. Usually recommended.")]
        [SerializeField] private bool m_RequireSteamClient = true;

        private void Awake()
        {
            // Singleton management
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            if (s_Initialized) return;

            // Check if Steam is running
            if (Packsize.Test())
            {
                FrizzLogger.LogInfo("Packsize test passed.");
            }
            else
            {
                FrizzLogger.LogError("Packsize test failed. The Steamworks SDK version does not match the wrapper compiled version.");
                return;
            }

            if (DllCheck.Test())
            {
                FrizzLogger.LogInfo("DllCheck test passed.");
            }
            else
            {
                FrizzLogger.LogError("DllCheck test failed. steam_api.dll/libsteam_api.dylib/libsteam_api.so is missing or wrong version.");
                return;
            }

            try
            {
                // If Steam client is not running, and we require it
                if (m_RequireSteamClient && SteamAPI.RestartAppIfNecessary((AppId_t)480))
                {
                    FrizzLogger.LogWarning("Restarting app through Steam client...");
                    Application.Quit();
                    return;
                }
            }
            catch (System.DllNotFoundException e)
            {
                FrizzLogger.LogError($"[Steamworks.NET] Could not load steam_api.dll. Error: {e.Message}");
                return;
            }

            // Init Steam
            s_Initialized = SteamAPI.Init();
            if (!s_Initialized)
            {
                FrizzLogger.LogError("SteamAPI.Init() failed! Is the Steam client running and logged in?");
            }
            else
            {
                FrizzLogger.LogInfo("Steamworks API successfully initialized.");
            }
        }

        private void Update()
        {
            if (!s_Initialized) return;

            // Pump Steamworks callbacks
            SteamAPI.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (s_Instance != this) return;

            s_Instance = null;
            if (s_Initialized)
            {
                FrizzLogger.LogInfo("Shutting down Steamworks API...");
                SteamAPI.Shutdown();
                s_Initialized = false;
            }
        }

        /// <summary>
        /// Ensures a SteamManager instance exists in the scene.
        /// </summary>
        public static void EnsureInstance()
        {
            if (s_Instance != null) return;

            GameObject managerGo = new GameObject("FrizzSteamManager");
            managerGo.AddComponent<SteamManager>();
            FrizzLogger.LogInfo("Created FrizzSteamManager instance.");
        }
    }
}
