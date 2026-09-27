using System;
using UnityEngine;
using Steamworks;
using FrizzNet.Logging;

namespace FrizzNet.Steam
{
    /// <summary>
    /// Initializes the Steam Game Server API for dedicated authority processes.
    /// Does not use SteamUser, SteamAPI.Init, or RestartAppIfNecessary.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class SteamGameServerManager : MonoBehaviour
    {
        private static SteamGameServerManager s_Instance;
        private static bool s_Initialized;
        private static bool s_LoggedOn;
        private static CSteamID s_SteamId = CSteamID.Nil;

        public static bool Initialized => s_Initialized;
        public static bool LoggedOn => s_LoggedOn;
        public static CSteamID SteamId => s_SteamId;
        public static event Action<bool> LoggedOnChanged;

        [Header("Game Server")]
        [Tooltip("IPv4 bind address as a host-order integer. Leave 0 to bind all interfaces.")]
        [SerializeField] private uint m_BindIp;

        [Tooltip("Gameplay port advertised to Steam.")]
        [SerializeField] private ushort m_GamePort = 27015;

        [Tooltip("Steam server-browser query port.")]
        [SerializeField] private ushort m_QueryPort = 27016;

        [Tooltip("Public name shown to Steam queries.")]
        [SerializeField] private string m_ServerName = "FrizzNet Dedicated";

        [Tooltip("Maximum player slots advertised to Steam.")]
        [SerializeField] [Range(1, 64)] private int m_MaxPlayers = 4;

        [Tooltip("Steam game tags used for later ISteamMatchmakingServers queries.")]
        [SerializeField] private string m_GameTags = "game=2v2-shooter";

        [Tooltip("Steam product name. SpaceWar requires Spacewar.")]
        [SerializeField] private string m_Product = "Spacewar";

        [SerializeField] private string m_GameDescription = "2v2 Shooter";
        [SerializeField] private string m_ModDir = "spacewar";
        [SerializeField] private string m_MapName = "GameScene";
        [SerializeField] private string m_VersionString = "1.0.0.0";

        [Tooltip("If true, Initialize() runs automatically in Awake.")]
        [SerializeField] private bool m_AutoInitialize;

        private Callback<SteamServersConnected_t> m_ServersConnected;
        private Callback<SteamServerConnectFailure_t> m_ConnectFailure;
        private Callback<SteamServersDisconnected_t> m_ServersDisconnected;

        public uint BindIp { get => m_BindIp; set => m_BindIp = value; }
        public ushort GamePort { get => m_GamePort; set => m_GamePort = value; }
        public ushort QueryPort { get => m_QueryPort; set => m_QueryPort = value; }
        public string ServerName { get => m_ServerName; set => m_ServerName = value; }
        public int MaxPlayers { get => m_MaxPlayers; set => m_MaxPlayers = value; }
        public string GameTags { get => m_GameTags; set => m_GameTags = value; }
        public string Product { get => m_Product; set => m_Product = value; }
        public string GameDescription { get => m_GameDescription; set => m_GameDescription = value; }
        public string ModDir { get => m_ModDir; set => m_ModDir = value; }
        public string MapName { get => m_MapName; set => m_MapName = value; }
        public string VersionString { get => m_VersionString; set => m_VersionString = value; }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            if (m_AutoInitialize)
                Initialize();
        }

        private void Update()
        {
            if (!s_Initialized)
                return;

            GameServer.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (s_Instance != this)
                return;

            ShutdownGameServer();
            s_Instance = null;
        }

        public static void SetAdvertisedPlayers(int currentPlayers, int maxPlayers)
        {
            if (!s_Initialized || s_Instance == null)
                return;

            int clampedMax = Mathf.Clamp(maxPlayers, 1, 64);
            int clampedCurrent = Mathf.Clamp(currentPlayers, 0, clampedMax);
            SteamGameServer.SetMaxPlayerCount(clampedMax);

            string tags = s_Instance.m_GameTags;
            if (string.IsNullOrEmpty(tags))
                tags = "game=2v2-shooter";

            SteamGameServer.SetGameTags(tags + ",players=" + clampedCurrent);
        }

        public void ApplySettings(string serverName, int maxPlayers, string gameTags, ushort gamePort, ushort queryPort)
        {
            if (!string.IsNullOrEmpty(serverName))
                m_ServerName = serverName;

            m_MaxPlayers = Mathf.Clamp(maxPlayers, 1, 64);
            if (!string.IsNullOrEmpty(gameTags))
                m_GameTags = gameTags;

            if (gamePort > 0)
                m_GamePort = gamePort;

            if (queryPort > 0)
                m_QueryPort = queryPort;
        }

        public bool Initialize()
        {
            if (s_Initialized)
                return true;

            if (!Packsize.Test())
            {
                FrizzLogger.LogError("Packsize test failed. The Steamworks SDK version does not match the wrapper compiled version.");
                return false;
            }

            if (!DllCheck.Test())
            {
                FrizzLogger.LogError("DllCheck test failed. steam_api.dll/libsteam_api.so is missing or the wrong version.");
                return false;
            }

            try
            {
                s_Initialized = GameServer.Init(
                    m_BindIp,
                    m_GamePort,
                    m_QueryPort,
                    EServerMode.eServerModeAuthentication,
                    m_VersionString);
            }
            catch (DllNotFoundException e)
            {
                FrizzLogger.LogError("[Steamworks.NET] Could not load steam_api for Game Server. Error: " + e.Message);
                s_Initialized = false;
                return false;
            }

            if (!s_Initialized)
            {
                FrizzLogger.LogError("GameServer.Init failed. Confirm steam_appid.txt is 480 and Game Server DLLs are present.");
                return false;
            }

            m_ServersConnected = Callback<SteamServersConnected_t>.CreateGameServer(OnServersConnected);
            m_ConnectFailure = Callback<SteamServerConnectFailure_t>.CreateGameServer(OnConnectFailure);
            m_ServersDisconnected = Callback<SteamServersDisconnected_t>.CreateGameServer(OnServersDisconnected);

            SteamGameServer.SetDedicatedServer(true);
            SteamGameServer.SetProduct(m_Product);
            SteamGameServer.SetGameDescription(m_GameDescription);
            SteamGameServer.SetModDir(m_ModDir);
            SteamGameServer.SetServerName(m_ServerName);
            SteamGameServer.SetMaxPlayerCount(m_MaxPlayers);
            SteamGameServer.SetBotPlayerCount(0);
            SteamGameServer.SetMapName(m_MapName);
            SteamGameServer.SetGameTags(m_GameTags);
            SteamGameServer.LogOnAnonymous();

            FrizzLogger.LogInfo("Steam Game Server API initialized. Logging on anonymously (App ID 480).");
            return true;
        }

        public static void EnsureInstance()
        {
            if (s_Instance != null)
                return;

            GameObject managerGo = new GameObject("FrizzSteamGameServerManager");
            managerGo.AddComponent<SteamGameServerManager>();
            FrizzLogger.LogInfo("Created FrizzSteamGameServerManager instance.");
        }

        private void OnServersConnected(SteamServersConnected_t callback)
        {
            s_SteamId = SteamGameServer.GetSteamID();
            s_LoggedOn = true;
            SteamGameServer.SetAdvertiseServerActive(true);
            SetAdvertisedPlayers(0, s_Instance != null ? s_Instance.m_MaxPlayers : 4);
            SteamGameServerNetworkingSockets.InitAuthentication();

            FrizzLogger.LogInfo("Dedicated Steam Game Server logged on. Steam ID: " + s_SteamId.m_SteamID +
                                ". Clients can Join Dedicated with this ID.");
            LoggedOnChanged?.Invoke(true);
        }

        private void OnConnectFailure(SteamServerConnectFailure_t callback)
        {
            FrizzLogger.LogError("Steam Game Server logon failed: " + callback.m_eResult);
            if (!callback.m_bStillRetrying)
            {
                s_LoggedOn = false;
                LoggedOnChanged?.Invoke(false);
            }
        }

        private void OnServersDisconnected(SteamServersDisconnected_t callback)
        {
            FrizzLogger.LogWarning("Steam Game Server disconnected: " + callback.m_eResult);
            s_LoggedOn = false;
            LoggedOnChanged?.Invoke(false);
        }

        private static void ShutdownGameServer()
        {
            if (!s_Initialized)
                return;

            FrizzLogger.LogInfo("Shutting down Steam Game Server API...");
            if (s_LoggedOn)
                SteamGameServer.SetAdvertiseServerActive(false);

            SteamGameServer.LogOff();
            GameServer.Shutdown();
            s_Initialized = false;
            s_LoggedOn = false;
            s_SteamId = CSteamID.Nil;
        }
    }
}
