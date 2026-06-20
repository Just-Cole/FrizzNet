using System;
using System.Collections.Generic;
using Steamworks;
using FrizzNet.Logging;
using UnityEngine;

namespace FrizzNet.Steam
{
    /// <summary>
    /// Represents a lobby entry returned from a Steam lobby list query.
    /// </summary>
    public struct FrizzLobbyInfo
    {
        public CSteamID LobbyId;
        public string Name;
        public int MemberCount;
        public int MaxMembers;
        public bool HasPassword;
    }

    /// <summary>
    /// Queries and returns public Steam lobbies for matchmaking browser UI.
    /// </summary>
    public static class FrizzLobbyBrowser
    {
        private static CallResult<LobbyMatchList_t> s_LobbyMatchListCallResult;
        private static Action<List<FrizzLobbyInfo>> s_OnComplete;
        private static Action<string> s_OnFailed;

        /// <summary>
        /// Requests a list of public lobbies matching optional filters.
        /// </summary>
        public static void RequestLobbyList(Action<List<FrizzLobbyInfo>> onComplete, Action<string> onFailed = null, int maxResults = 50)
        {
            if (!SteamManager.Initialized)
            {
                onFailed?.Invoke("Steam is not initialized.");
                return;
            }

            s_OnComplete = onComplete;
            s_OnFailed = onFailed;

            SteamMatchmaking.AddRequestLobbyListResultCountFilter(Mathf.Clamp(maxResults, 1, 50));
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterDefault);

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            s_LobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
            s_LobbyMatchListCallResult.Set(handle);
        }

        private static void OnLobbyMatchList(LobbyMatchList_t callback, bool ioFailure)
        {
            if (ioFailure)
            {
                s_OnFailed?.Invoke("Steam lobby list request failed.");
                s_OnComplete = null;
                return;
            }

            var results = new List<FrizzLobbyInfo>();
            int count = (int)callback.m_nLobbiesMatching;

            for (int i = 0; i < count; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                string name = SteamMatchmaking.GetLobbyData(lobbyId, "name");
                if (string.IsNullOrEmpty(name))
                {
                    name = $"Lobby {lobbyId.m_SteamID}";
                }

                string password = SteamMatchmaking.GetLobbyData(lobbyId, "password");
                results.Add(new FrizzLobbyInfo
                {
                    LobbyId = lobbyId,
                    Name = name,
                    MemberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
                    MaxMembers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId),
                    HasPassword = !string.IsNullOrEmpty(password)
                });
            }

            FrizzLogger.LogNetwork($"[LobbyBrowser] Found {results.Count} public lobbies.");
            s_OnComplete?.Invoke(results);
            s_OnComplete = null;
        }
    }
}
