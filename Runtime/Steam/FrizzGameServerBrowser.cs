using System;
using System.Collections.Generic;
using Steamworks;
using FrizzNet.Logging;

namespace FrizzNet.Steam
{
    /// <summary>
    /// A dedicated game server returned by ISteamMatchmakingServers.
    /// </summary>
    public struct FrizzGameServerInfo
    {
        public CSteamID SteamId;
        public string Name;
        public string Map;
        public string Tags;
        public int PlayerCount;
        public int MaxPlayers;
        public int PingMs;
        public bool PasswordProtected;
        public bool Secure;
        public string Address;
    }

    /// <summary>
    /// Queries advertised Steam Game Servers for a dedicated-server browser.
    /// Internet and LAN lists are merged and filtered by game tags.
    /// </summary>
    public static class FrizzGameServerBrowser
    {
        private static ISteamMatchmakingServerListResponse s_InternetResponse;
        private static ISteamMatchmakingServerListResponse s_FriendsResponse;
        private static ISteamMatchmakingServerListResponse s_LanResponse;
        private static HServerListRequest s_InternetRequest = HServerListRequest.Invalid;
        private static HServerListRequest s_FriendsRequest = HServerListRequest.Invalid;
        private static HServerListRequest s_LanRequest = HServerListRequest.Invalid;
        private static Action<List<FrizzGameServerInfo>> s_OnComplete;
        private static Action<string> s_OnFailed;
        private static string s_RequiredTag;
        private static int s_PendingQueries;
        private static bool s_Completing;
        private static readonly Dictionary<ulong, FrizzGameServerInfo> s_Results = new Dictionary<ulong, FrizzGameServerInfo>();

        public static void RequestServerList(
            Action<List<FrizzGameServerInfo>> onComplete,
            Action<string> onFailed = null,
            string gameTag = "game=frizznet",
            string gameDir = "spacewar")
        {
            if (!SteamManager.Initialized)
            {
                onFailed?.Invoke("Steam is not initialized.");
                return;
            }

            Cancel();
            s_OnComplete = onComplete;
            s_OnFailed = onFailed;
            s_RequiredTag = gameTag;
            s_Results.Clear();
            s_PendingQueries = 0;
            s_Completing = false;

            s_InternetResponse = new ISteamMatchmakingServerListResponse(
                OnInternetServerResponded,
                OnInternetServerFailed,
                OnInternetRefreshComplete);
            s_FriendsResponse = new ISteamMatchmakingServerListResponse(
                OnFriendsServerResponded,
                OnFriendsServerFailed,
                OnFriendsRefreshComplete);
            s_LanResponse = new ISteamMatchmakingServerListResponse(
                OnLanServerResponded,
                OnLanServerFailed,
                OnLanRefreshComplete);

            AppId_t appId = SteamUtils.GetAppID();
            if (appId == AppId_t.Invalid)
                appId = (AppId_t)480;

            // Do not apply Steam-side gamedir/dedicated filters. Those drop SpaceWar
            // dedicated hosts from the Internet list while LAN still shows them.
            MatchMakingKeyValuePair_t[] noFilters = Array.Empty<MatchMakingKeyValuePair_t>();
            s_InternetRequest = SteamMatchmakingServers.RequestInternetServerList(
                appId,
                noFilters,
                0,
                s_InternetResponse);
            if (s_InternetRequest != HServerListRequest.Invalid)
                s_PendingQueries++;

            s_FriendsRequest = SteamMatchmakingServers.RequestFriendsServerList(
                appId,
                noFilters,
                0,
                s_FriendsResponse);
            if (s_FriendsRequest != HServerListRequest.Invalid)
                s_PendingQueries++;

            s_LanRequest = SteamMatchmakingServers.RequestLANServerList(appId, s_LanResponse);
            if (s_LanRequest != HServerListRequest.Invalid)
                s_PendingQueries++;

            if (s_PendingQueries == 0)
            {
                s_OnFailed?.Invoke("Steam failed to start a dedicated server query.");
                ClearCallbacks();
            }
        }

        public static void Cancel()
        {
            AbortRequest(ref s_InternetRequest);
            AbortRequest(ref s_FriendsRequest);
            AbortRequest(ref s_LanRequest);
            ClearCallbacks();
            s_PendingQueries = 0;
            s_Completing = false;
            s_Results.Clear();
        }

        private static void OnInternetServerResponded(HServerListRequest request, int index)
        {
            AddServer(request, index);
        }

        private static void OnFriendsServerResponded(HServerListRequest request, int index)
        {
            AddServer(request, index);
        }

        private static void OnLanServerResponded(HServerListRequest request, int index)
        {
            AddServer(request, index);
        }

        private static void OnInternetServerFailed(HServerListRequest request, int index)
        {
        }

        private static void OnLanServerFailed(HServerListRequest request, int index)
        {
        }

        private static void OnFriendsServerFailed(HServerListRequest request, int index)
        {
        }

        private static void OnInternetRefreshComplete(HServerListRequest request, EMatchMakingServerResponse response)
        {
            CompleteQuery(ref s_InternetRequest, "Internet", response);
        }

        private static void OnFriendsRefreshComplete(HServerListRequest request, EMatchMakingServerResponse response)
        {
            CompleteQuery(ref s_FriendsRequest, "Friends", response);
        }

        private static void OnLanRefreshComplete(HServerListRequest request, EMatchMakingServerResponse response)
        {
            CompleteQuery(ref s_LanRequest, "LAN", response);
        }

        private static void AddServer(HServerListRequest request, int index)
        {
            if (request == HServerListRequest.Invalid)
                return;

            gameserveritem_t item = SteamMatchmakingServers.GetServerDetails(request, index);
            if (item == null || !item.m_steamID.IsValid())
                return;

            string tags = item.GetGameTags();
            if (!string.IsNullOrEmpty(s_RequiredTag) && !ContainsTag(tags, s_RequiredTag))
                return;

            int playerCount = item.m_nPlayers - item.m_nBotPlayers;
            if (playerCount < 0)
                playerCount = item.m_nPlayers;

            int taggedPlayers = ReadTaggedPlayerCount(tags);
            if (playerCount <= 0 && taggedPlayers >= 0)
                playerCount = taggedPlayers;

            string name = item.GetServerName();
            if (string.IsNullOrEmpty(name))
                name = "Dedicated " + item.m_steamID.m_SteamID;

            s_Results[item.m_steamID.m_SteamID] = new FrizzGameServerInfo
            {
                SteamId = item.m_steamID,
                Name = name,
                Map = item.GetMap(),
                Tags = tags,
                PlayerCount = playerCount,
                MaxPlayers = item.m_nMaxPlayers,
                PingMs = item.m_nPing,
                PasswordProtected = item.m_bPassword,
                Secure = item.m_bSecure,
                Address = item.m_NetAdr.GetConnectionAddressString()
            };
        }

        private static void CompleteQuery(ref HServerListRequest request, string source, EMatchMakingServerResponse response)
        {
            if (request == HServerListRequest.Invalid || s_Completing)
                return;

            FrizzLogger.LogNetwork("[GameServerBrowser] " + source + " query finished: " + response);

            // RefreshComplete already ended the query. CancelQuery here re-enters this
            // callback on the same stack and overflows the Unity Editor.
            ReleaseCompletedRequest(ref request);
            s_PendingQueries--;
            if (s_PendingQueries > 0)
                return;

            s_Completing = true;
            List<FrizzGameServerInfo> results = new List<FrizzGameServerInfo>(s_Results.Count);
            foreach (KeyValuePair<ulong, FrizzGameServerInfo> pair in s_Results)
                results.Add(pair.Value);

            FrizzLogger.LogNetwork("[GameServerBrowser] Found " + results.Count + " dedicated servers.");
            Action<List<FrizzGameServerInfo>> onComplete = s_OnComplete;
            ClearCallbacks();
            s_Completing = false;
            onComplete?.Invoke(results);
        }

        private static void ReleaseCompletedRequest(ref HServerListRequest request)
        {
            if (request == HServerListRequest.Invalid)
                return;

            HServerListRequest handle = request;
            request = HServerListRequest.Invalid;
            SteamMatchmakingServers.ReleaseRequest(handle);
        }

        private static void AbortRequest(ref HServerListRequest request)
        {
            if (request == HServerListRequest.Invalid)
                return;

            HServerListRequest handle = request;
            request = HServerListRequest.Invalid;
            SteamMatchmakingServers.CancelQuery(handle);
            SteamMatchmakingServers.ReleaseRequest(handle);
        }

        private static void ClearCallbacks()
        {
            s_OnComplete = null;
            s_OnFailed = null;
            s_InternetResponse = null;
            s_FriendsResponse = null;
            s_LanResponse = null;
        }

        private static bool ContainsTag(string tags, string requiredTag)
        {
            if (string.IsNullOrEmpty(requiredTag))
                return true;
            if (string.IsNullOrEmpty(tags))
                return false;

            return tags.IndexOf(requiredTag, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ReadTaggedPlayerCount(string tags)
        {
            if (string.IsNullOrEmpty(tags))
                return -1;

            const string prefix = "players=";
            int start = tags.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return -1;

            start += prefix.Length;
            int end = tags.IndexOf(',', start);
            if (end < 0)
                end = tags.Length;

            if (int.TryParse(tags.Substring(start, end - start), out int count))
                return count;

            return -1;
        }
    }
}
