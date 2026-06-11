using UnityEngine;
using Steamworks;
using FrizzNet.Core;
using FrizzNet.Steam;
using FrizzNet.Logging;

namespace FrizzNet.Samples
{
    /// <summary>
    /// Coordinates the spawning of player characters on the host when client connections are established.
    /// Also dynamically sets up scene geometry (ground floor, main camera positioning) and registers the player prefab.
    /// </summary>
    [FrizzHelp("Handles dynamic player spawning and scene geometry setup (isometric camera and dark grid ground floor) at runtime.")]
    public class DemoSpawnManager : MonoBehaviour
    {
        private GameObject m_GeneratedPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 m_SpawnAreaMin = new Vector3(-8f, 0.5f, -8f);
        [SerializeField] private Vector3 m_SpawnAreaMax = new Vector3(8f, 0.5f, 8f);

        private void Awake()
        {
            CreateSceneGeometry();
            CreatePlayerPrefabTemplate();
        }

        private void Start()
        {
            // Register player prefab on the NetworkManager registry
            if (NetworkManager.Instance != null && m_GeneratedPrefab != null)
            {
                NetworkManager.Instance.RegisterSpawnablePrefab(m_GeneratedPrefab.GetComponent<NetworkIdentity>());
            }

            // Subscribe to multiplayer connection events
            NetworkManager.OnClientConnected += HandleClientConnected;
            FrizzLobby.OnLobbyJoinedEvent += HandleLobbyJoined;
        }

        private void OnDestroy()
        {
            NetworkManager.OnClientConnected -= HandleClientConnected;
            FrizzLobby.OnLobbyJoinedEvent -= HandleLobbyJoined;
        }

        private void CreateSceneGeometry()
        {
            // 1. Create Ground Floor
            if (GameObject.Find("DemoGround") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "DemoGround";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(3f, 1f, 3f); // 30x30 units floor

                // Dark grid styling color
                Renderer renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.15f, 0.15f, 0.17f);
                }
            }

            // 2. Align Main Camera to Isometric View
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 12f, -14f);
                cam.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.11f);
                cam.clearFlags = CameraClearFlags.Color;
            }
        }

        private void CreatePlayerPrefabTemplate()
        {
            // Dynamically instantiate a Cube that will act as the player prefab
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = "FrizzNetDemoPlayer";
            
            // Add FrizzNet components to Cube
            template.AddComponent<NetworkIdentity>();
            template.AddComponent<FrizzNetworkTransform>();
            template.AddComponent<DemoPlayerController>();

            // Deactivate and hide template in background
            template.transform.position = new Vector3(0f, -100f, 0f);
            template.SetActive(false);

            m_GeneratedPrefab = template;
        }



        private void HandleLobbyJoined(CSteamID lobbyId)
        {
            // Clients do not spawn themselves. The host handles it upon connection.
            // But if we are the host (e.g. lobby owner after migration), we spawn.
            CSteamID owner = FrizzLobby.GetOwner();
            if (owner == SteamUser.GetSteamID())
            {
                Invoke(nameof(SpawnHostPlayer), 0.5f);
            }
        }

        private void SpawnHostPlayer()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                ulong mySteamId = SteamUser.GetSteamID().m_SteamID;
                SpawnPlayerForId(mySteamId);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            // Host spawns a character for the newly joined client
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                SpawnPlayerForId(clientId);
            }
        }

        private void SpawnPlayerForId(ulong steamId)
        {
            // Random position inside spawn bounds
            Vector3 spawnPos = new Vector3(
                Random.Range(m_SpawnAreaMin.x, m_SpawnAreaMax.x),
                Random.Range(m_SpawnAreaMin.y, m_SpawnAreaMax.y),
                Random.Range(m_SpawnAreaMin.z, m_SpawnAreaMax.z)
            );

            FrizzLogger.LogNetwork($"SpawnManager spawning player character for SteamID {steamId} at {spawnPos}");
            NetworkManager.Instance.Spawn(m_GeneratedPrefab, spawnPos, Quaternion.identity, steamId);
        }
    }
}
