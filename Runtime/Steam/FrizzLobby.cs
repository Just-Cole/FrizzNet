using System;
using System.Collections.Generic;
using Steamworks;
using FrizzNet.Logging;

namespace FrizzNet.Steam
{
    /// <summary>
    /// High-level API for interacting with Steam Lobbies.
    /// Exposes methods to create, join, leave, set metadata, and query members.
    /// </summary>
    public static class FrizzLobby
    {
        /// <summary>
        /// The SteamID of the lobby we are currently in. Returns CSteamID.Nil if not in a lobby.
        /// </summary>
        public static CSteamID CurrentLobbyId { get; internal set; } = CSteamID.Nil;

        /// <summary>
        /// Gets the string representation of the current Lobby ID.
        /// </summary>
        public static string CurrentLobbyIdString => CurrentLobbyId == CSteamID.Nil ? string.Empty : CurrentLobbyId.m_SteamID.ToString();

        /// <summary>
        /// Whether the local player is currently inside a Steam lobby.
        /// </summary>
        public static bool InLobby => CurrentLobbyId != CSteamID.Nil;

        // Lobby Events
        public static event Action<CSteamID> OnLobbyCreatedEvent;
        public static event Action<CSteamID> OnLobbyJoinedEvent;
        public static event Action OnLobbyLeftEvent;
        public static event Action<CSteamID, CSteamID, bool> OnLobbyMemberChangedEvent; // Lobby, Member, Joined(true)/Left(false)
        public static event Action<CSteamID, CSteamID> OnLobbyDataUpdatedEvent; // Lobby, Member
        public static event Action<CSteamID, CSteamID, CSteamID> OnLobbyOwnerChangedEvent; // Lobby, OldOwner, NewOwner

        /// <summary>
        /// Creates a new Steam lobby.
        /// </summary>
        /// <param name="maxPlayers">Maximum lobby size.</param>
        /// <param name="lobbyType">Type of lobby (Public, FriendsOnly, Private).</param>
        public static void Create(int maxPlayers = 4, ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic)
        {
            if (!SteamManager.Initialized)
            {
                FrizzLogger.LogError("Cannot create lobby: Steam is not initialized.");
                return;
            }

            FrizzLogger.LogNetwork($"Requesting Steam to create lobby of type {lobbyType} with {maxPlayers} max players...");
            SteamMatchmaking.CreateLobby(lobbyType, maxPlayers);
        }

        /// <summary>
        /// Joins a Steam lobby by its unique ulong ID.
        /// </summary>
        /// <param name="lobbyId">The lobby's Steam ID.</param>
        public static void Join(ulong lobbyId)
        {
            Join(new CSteamID(lobbyId));
        }

        /// <summary>
        /// Joins a Steam lobby by its CSteamID.
        /// </summary>
        /// <param name="lobbyId">The lobby's Steam ID.</param>
        public static void Join(CSteamID lobbyId)
        {
            if (!SteamManager.Initialized)
            {
                FrizzLogger.LogError("Cannot join lobby: Steam is not initialized.");
                return;
            }

            FrizzLogger.LogNetwork($"Requesting to join lobby {lobbyId}...");
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        /// <summary>
        /// Leaves the current Steam lobby.
        /// </summary>
        public static void Leave()
        {
            if (CurrentLobbyId == CSteamID.Nil) return;

            FrizzLogger.LogNetwork($"Leaving lobby {CurrentLobbyId}...");
            SteamMatchmaking.LeaveLobby(CurrentLobbyId);
            CSteamID leftLobby = CurrentLobbyId;
            CurrentLobbyId = CSteamID.Nil;
            OnLobbyLeftEvent?.Invoke();
        }

        /// <summary>
        /// Opens the default Steam Overlay to invite friends to the current lobby.
        /// </summary>
        public static void InviteFriends()
        {
            if (!InLobby)
            {
                FrizzLogger.LogWarning("Cannot invite friends: Not currently in a lobby.");
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyId);
        }

        /// <summary>
        /// Directly invites a specific user to the current lobby.
        /// </summary>
        public static bool InviteUser(ulong userSteamId)
        {
            if (!InLobby)
            {
                FrizzLogger.LogWarning("Cannot invite user: Not currently in a lobby.");
                return false;
            }
            return SteamMatchmaking.InviteUserToLobby(CurrentLobbyId, new CSteamID(userSteamId));
        }

        /// <summary>
        /// Gets the SteamID of the current lobby owner (host).
        /// </summary>
        public static CSteamID GetOwner()
        {
            if (!InLobby) return CSteamID.Nil;
            return SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
        }

        /// <summary>
        /// Gets list of all members currently in the lobby.
        /// </summary>
        public static List<CSteamID> GetMembers()
        {
            var list = new List<CSteamID>();
            if (!InLobby) return list;

            int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId);
            for (int i = 0; i < count; i++)
            {
                list.Add(SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, i));
            }
            return list;
        }

        /// <summary>
        /// Gets the persona name of a lobby member.
        /// </summary>
        public static string GetMemberName(CSteamID memberId)
        {
            return SteamFriends.GetFriendPersonaName(memberId);
        }

        /// <summary>
        /// Sets a custom metadata key-value pair on the lobby (Only works if we are the lobby owner).
        /// </summary>
        public static void SetMetadata(string key, string value)
        {
            if (!InLobby) return;
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, key, value);
        }

        /// <summary>
        /// Gets a metadata value from the lobby by key.
        /// </summary>
        public static string GetMetadata(string key)
        {
            if (!InLobby) return string.Empty;
            return SteamMatchmaking.GetLobbyData(CurrentLobbyId, key);
        }

        /// <summary>
        /// Sets a custom metadata key-value pair for the local player's member entry in the lobby.
        /// </summary>
        public static void SetMemberMetadata(string key, string value)
        {
            if (!InLobby) return;
            SteamMatchmaking.SetLobbyMemberData(CurrentLobbyId, key, value);
        }

        /// <summary>
        /// Gets a metadata value for a specific lobby member.
        /// </summary>
        public static string GetMemberMetadata(CSteamID memberId, string key)
        {
            if (!InLobby) return string.Empty;
            return SteamMatchmaking.GetLobbyMemberData(CurrentLobbyId, memberId, key);
        }

        /// <summary>
        /// Sets whether the local player is ready.
        /// </summary>
        public static void SetReadyState(bool ready)
        {
            SetMemberMetadata("ready", ready ? "true" : "false");
        }

        /// <summary>
        /// Gets whether a specific lobby member is ready.
        /// </summary>
        public static bool IsMemberReady(CSteamID memberId)
        {
            string readyVal = GetMemberMetadata(memberId, "ready");
            return readyVal == "true";
        }

        // Internal trigger methods used by SteamTransport callbacks to forward events
        internal static void TriggerLobbyCreated(CSteamID lobbyId)
        {
            CurrentLobbyId = lobbyId;
            OnLobbyCreatedEvent?.Invoke(lobbyId);
        }

        internal static void TriggerLobbyJoined(CSteamID lobbyId)
        {
            CurrentLobbyId = lobbyId;
            OnLobbyJoinedEvent?.Invoke(lobbyId);
        }

        internal static void TriggerLobbyMemberChanged(CSteamID lobbyId, CSteamID memberId, bool joined)
        {
            OnLobbyMemberChangedEvent?.Invoke(lobbyId, memberId, joined);
        }

        internal static void TriggerLobbyDataUpdate(CSteamID lobbyId, CSteamID memberId)
        {
            OnLobbyDataUpdatedEvent?.Invoke(lobbyId, memberId);
        }

        internal static void TriggerLobbyOwnerChanged(CSteamID lobbyId, CSteamID oldOwner, CSteamID newOwner)
        {
            OnLobbyOwnerChangedEvent?.Invoke(lobbyId, oldOwner, newOwner);
        }
    }
}
