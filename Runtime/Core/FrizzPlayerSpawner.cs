using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FrizzNet.Logging;
using FrizzNet.Steam;
using Steamworks;

namespace FrizzNet.Core
{
    public enum SpawnSelectionMode
    {
        Random,
        RoundRobin
    }

    /// <summary>
    /// Component that manages the spawning of player characters on the Host
    /// and synchronizes authority across the network.
    /// </summary>
    [DisallowMultipleComponent]
    [FrizzHelp("Manages player character spawning across the network, supporting multiple selection modes and overlap checks.", "index.html#FrizzPlayerSpawner")]
    public class FrizzPlayerSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("The player prefab to spawn. Must have a NetworkIdentity component.")]
        [SerializeField] private GameObject m_PlayerPrefab;

        [Tooltip("List of valid spawn locations.")]
        [SerializeField] private List<Transform> m_SpawnPoints = new List<Transform>();

        [Tooltip("How a spawn point is selected from the list.")]
        [SerializeField] private SpawnSelectionMode m_SpawnMode = SpawnSelectionMode.Random;

        [Tooltip("If true, automatically spawns players on network connection events.")]
        [SerializeField] private bool m_AutoSpawn = true;

        [Header("Collision Avoidance")]
        [Tooltip("If true, checks if a spawn point is blocked before placing a player there.")]
        [SerializeField] private bool m_AvoidOccupiedSpawnPoints = true;

        [Tooltip("Radius of the sphere checked for blocking objects.")]
        [SerializeField] private float m_OccupationCheckRadius = 1.5f;

        [Tooltip("Layers to check when determining if a spawn point is occupied.")]
        [SerializeField] private LayerMask m_OccupiedLayerMask = ~0;

        private int m_NextRoundRobinIndex = 0;

        // Public properties for developer inspection / customization
        public GameObject PlayerPrefab { get => m_PlayerPrefab; set => m_PlayerPrefab = value; }
        public List<Transform> SpawnPoints => m_SpawnPoints;
        public SpawnSelectionMode SpawnMode { get => m_SpawnMode; set => m_SpawnMode = value; }
        public bool AutoSpawn { get => m_AutoSpawn; set => m_AutoSpawn = value; }
        public bool AvoidOccupiedSpawnPoints { get => m_AvoidOccupiedSpawnPoints; set => m_AvoidOccupiedSpawnPoints = value; }
        public float OccupationCheckRadius { get => m_OccupationCheckRadius; set => m_OccupationCheckRadius = value; }
        public LayerMask OccupiedLayerMask { get => m_OccupiedLayerMask; set => m_OccupiedLayerMask = value; }

        private void Start()
        {
            if (m_PlayerPrefab == null)
            {
                FrizzLogger.LogError("[FrizzPlayerSpawner] Player Prefab is not assigned.");
                return;
            }

            var identity = m_PlayerPrefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                FrizzLogger.LogError($"[FrizzPlayerSpawner] Assigned Player Prefab '{m_PlayerPrefab.name}' does not have a NetworkIdentity component.");
                return;
            }

            // Dynamically register the prefab so NetworkManager knows how to spawn it
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RegisterSpawnablePrefab(identity);
            }

            if (m_AutoSpawn)
            {
                NetworkManager.OnClientConnected += HandleClientConnected;
                FrizzLobby.OnLobbyJoinedEvent += HandleLobbyJoined;
            }
        }

        private void OnDestroy()
        {
            if (m_AutoSpawn)
            {
                NetworkManager.OnClientConnected -= HandleClientConnected;
                FrizzLobby.OnLobbyJoinedEvent -= HandleLobbyJoined;
            }
        }

        private void HandleLobbyJoined(CSteamID lobbyId)
        {
            // If we are the host/owner of the lobby, we spawn the host's player character
            CSteamID owner = FrizzLobby.GetOwner();
            if (owner == SteamUser.GetSteamID())
            {
                // Give a small delay to ensure lobby and network states are fully synchronized
                StartCoroutine(SpawnHostPlayerCoroutine(owner.m_SteamID));
            }
        }

        private IEnumerator SpawnHostPlayerCoroutine(ulong steamId)
        {
            yield return new WaitForSeconds(0.5f);
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                SpawnPlayer(steamId);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                SpawnPlayer(clientId);
            }
        }

        /// <summary>
        /// Spawns a player character for the specified connection/Steam ID at a selected spawn point.
        /// Only valid on the Host.
        /// </summary>
        public GameObject SpawnPlayer(ulong ownerId)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost)
            {
                FrizzLogger.LogError("[FrizzPlayerSpawner] Only the Host can spawn players.");
                return null;
            }

            if (m_PlayerPrefab == null)
            {
                FrizzLogger.LogError("[FrizzPlayerSpawner] Cannot spawn: Player Prefab is not assigned.");
                return null;
            }

            Transform spawnPoint = GetNextSpawnPoint();
            Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            return SpawnPlayer(ownerId, position, rotation);
        }

        /// <summary>
        /// Spawns a player character for the specified connection/Steam ID at a custom coordinate.
        /// Only valid on the Host.
        /// </summary>
        public GameObject SpawnPlayer(ulong ownerId, Vector3 position, Quaternion rotation)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost)
            {
                FrizzLogger.LogError("[FrizzPlayerSpawner] Only the Host can spawn players.");
                return null;
            }

            if (m_PlayerPrefab == null)
            {
                FrizzLogger.LogError("[FrizzPlayerSpawner] Cannot spawn: Player Prefab is not assigned.");
                return null;
            }

            FrizzLogger.LogNetwork($"[FrizzPlayerSpawner] Spawning player prefab '{m_PlayerPrefab.name}' for Owner {ownerId} at position {position}");
            return NetworkManager.Instance.Spawn(m_PlayerPrefab, position, rotation, ownerId);
        }

        /// <summary>
        /// Selects the next appropriate spawn point based on selection mode and occupation checks.
        /// </summary>
        private Transform GetNextSpawnPoint()
        {
            if (m_SpawnPoints == null || m_SpawnPoints.Count == 0)
            {
                FrizzLogger.LogWarning("[FrizzPlayerSpawner] No spawn points assigned. Spawning at origin.");
                return null;
            }

            // Filter out any null entries in the spawn points list
            List<Transform> validPoints = m_SpawnPoints.FindAll(p => p != null);
            if (validPoints.Count == 0)
            {
                FrizzLogger.LogWarning("[FrizzPlayerSpawner] All assigned spawn points are null. Spawning at origin.");
                return null;
            }

            if (m_AvoidOccupiedSpawnPoints)
            {
                // Try to find an unoccupied spawn point first
                List<Transform> unoccupiedPoints = new List<Transform>();
                foreach (var point in validPoints)
                {
                    if (!IsSpawnPointOccupied(point))
                    {
                        unoccupiedPoints.Add(point);
                    }
                }

                if (unoccupiedPoints.Count > 0)
                {
                    return SelectFromList(unoccupiedPoints);
                }

                FrizzLogger.LogWarning("[FrizzPlayerSpawner] All spawn points are occupied. Falling back to default selection strategy.");
            }

            return SelectFromList(validPoints);
        }

        private Transform SelectFromList(List<Transform> points)
        {
            if (points == null || points.Count == 0) return null;

            if (m_SpawnMode == SpawnSelectionMode.Random)
            {
                int index = UnityEngine.Random.Range(0, points.Count);
                return points[index];
            }
            else // RoundRobin
            {
                // Find index matching next round robin index, wrap if needed
                int index = m_NextRoundRobinIndex % points.Count;
                m_NextRoundRobinIndex = (index + 1) % points.Count;
                return points[index];
            }
        }

        /// <summary>
        /// Checks if there are any colliders within the check sphere at the spawn point.
        /// </summary>
        private bool IsSpawnPointOccupied(Transform spawnPoint)
        {
            Collider[] colliders = Physics.OverlapSphere(
                spawnPoint.position, 
                m_OccupationCheckRadius, 
                m_OccupiedLayerMask, 
                QueryTriggerInteraction.Ignore
            );

            // If we found any colliders, it's occupied
            return colliders.Length > 0;
        }
    }
}
