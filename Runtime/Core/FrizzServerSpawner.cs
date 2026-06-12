using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FrizzNet.Logging;
using FrizzNet.Steam;
using Steamworks;

namespace FrizzNet.Core
{
    /// <summary>
    /// Component that automatically spawns server-owned / host-owned networked GameObjects 
    /// (e.g. world obstacles, interactable chests, NPCs) at designated locations when the host session starts.
    /// </summary>
    [DisallowMultipleComponent]
    [FrizzHelp("Spawns a list of prefabs at designated locations automatically on the Host when the session starts.", "index.html#FrizzServerSpawner")]
    public class FrizzServerSpawner : MonoBehaviour
    {
        [System.Serializable]
        public struct ServerSpawnItem
        {
            [Tooltip("The prefab to spawn. Must have a NetworkIdentity component.")]
            public GameObject Prefab;

            [Tooltip("The location where the prefab should be spawned.")]
            public Transform SpawnLocation;
        }

        [Header("Spawn Configuration")]
        [Tooltip("List of prefabs and corresponding locations to instantiate when the host is created.")]
        [SerializeField] private List<ServerSpawnItem> m_SpawnItems = new List<ServerSpawnItem>();

        [Tooltip("If true, automatically spawns items when joining the lobby as host.")]
        [SerializeField] private bool m_SpawnOnLobbyJoin = true;

        private bool m_HasSpawned = false;

        // Public properties
        public List<ServerSpawnItem> SpawnItems => m_SpawnItems;
        public bool SpawnOnLobbyJoin { get => m_SpawnOnLobbyJoin; set => m_SpawnOnLobbyJoin = value; }
        public bool HasSpawned => m_HasSpawned;

        private void Start()
        {
            if (m_SpawnOnLobbyJoin)
            {
                FrizzLobby.OnLobbyJoinedEvent += HandleLobbyJoined;
            }
        }

        private void OnDestroy()
        {
            if (m_SpawnOnLobbyJoin)
            {
                FrizzLobby.OnLobbyJoinedEvent -= HandleLobbyJoined;
            }
        }

        private void HandleLobbyJoined(CSteamID lobbyId)
        {
            // Only the owner of the lobby (Host) triggers the server spawning routine
            CSteamID owner = FrizzLobby.GetOwner();
            if (owner == SteamUser.GetSteamID())
            {
                StartCoroutine(SpawnItemsCoroutine());
            }
        }

        private IEnumerator SpawnItemsCoroutine()
        {
            // Give a small delay to ensure network and lobby handshakes are complete
            yield return new WaitForSeconds(0.6f);

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                SpawnAll();
            }
        }

        /// <summary>
        /// Spawns all registered spawn items on the Host.
        /// </summary>
        public void SpawnAll()
        {
            if (m_HasSpawned)
            {
                FrizzLogger.LogWarning("[FrizzServerSpawner] Spawner has already executed.");
                return;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost)
            {
                FrizzLogger.LogError("[FrizzServerSpawner] Spawning server items is only valid on the Host.");
                return;
            }

            FrizzLogger.LogNetwork($"[FrizzServerSpawner] Initializing server spawn routine for {m_SpawnItems.Count} items.");
            
            foreach (var item in m_SpawnItems)
            {
                if (item.Prefab == null)
                {
                    FrizzLogger.LogWarning("[FrizzServerSpawner] Spawn item contains an unassigned Prefab.");
                    continue;
                }

                NetworkIdentity identity = item.Prefab.GetComponent<NetworkIdentity>();
                if (identity == null)
                {
                    FrizzLogger.LogError($"[FrizzServerSpawner] Spawn Prefab '{item.Prefab.name}' is missing a NetworkIdentity component.");
                    continue;
                }

                // Register the prefab dynamically in case it wasn't registered in NetworkManager inspector
                NetworkManager.Instance.RegisterSpawnablePrefab(identity);

                Vector3 position = item.SpawnLocation != null ? item.SpawnLocation.position : Vector3.zero;
                Quaternion rotation = item.SpawnLocation != null ? item.SpawnLocation.rotation : Quaternion.identity;

                FrizzLogger.LogNetwork($"[FrizzServerSpawner] Spawning '{item.Prefab.name}' at {position} (owned by Server).");
                NetworkManager.Instance.Spawn(item.Prefab, position, rotation, ownerId: 0);
            }

            m_HasSpawned = true;
        }
    }
}
