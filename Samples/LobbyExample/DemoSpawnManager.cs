using UnityEngine;
using Steamworks;
using FrizzNet.Core;
using FrizzNet.Steam;
using FrizzNet.Logging;
using FrizzNet.Messaging;

namespace FrizzNet.Samples
{
    /// <summary>
    /// Coordinates the spawning of player characters on the host when client connections are established.
    /// Also dynamically sets up scene geometry (ground floor, main camera positioning) and registers the player prefab.
    /// Manages the spawn lifecycle of resource cubes for the grow-and-eat game loop on the Host.
    /// </summary>
    public class DemoSpawnManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Assign custom Player Prefab. If empty, a default cube template is generated procedurally.")]
        [SerializeField] private GameObject m_PlayerPrefab;

        [Tooltip("Assign custom Resource Prefab. If empty, a default sphere template is generated procedurally.")]
        [SerializeField] private GameObject m_ResourcePrefab;

        private GameObject m_ActivePlayerPrefab;
        private GameObject m_ActiveResourcePrefab;

        [Header("Scene Configuration")]
        [Tooltip("Name of the lobby scene to return to on disconnect.")]
        [SerializeField] private string m_LobbySceneName = "DemoLobbyScene";

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 m_SpawnAreaMin = new Vector3(-8f, 0.5f, -8f);
        [SerializeField] private Vector3 m_SpawnAreaMax = new Vector3(8f, 0.5f, 8f);

        private const short MSG_SCENE_READY = 5000;

        private void Awake()
        {
            CreateSceneGeometry();

            // Set up Player Prefab
            if (m_PlayerPrefab != null)
            {
                m_ActivePlayerPrefab = m_PlayerPrefab;
            }
            else
            {
                CreatePlayerPrefabTemplate();
            }

            // Set up Resource Prefab
            if (m_ResourcePrefab != null)
            {
                m_ActiveResourcePrefab = m_ResourcePrefab;
            }
            else
            {
                CreateResourcePrefabTemplate();
            }
        }

        private void Start()
        {
            // Register player and resource prefabs on the NetworkManager registry
            if (NetworkManager.Instance != null)
            {
                if (m_ActivePlayerPrefab != null)
                {
                    NetworkManager.Instance.RegisterSpawnablePrefab(m_ActivePlayerPrefab.GetComponent<NetworkIdentity>());
                }
                if (m_ActiveResourcePrefab != null)
                {
                    NetworkManager.Instance.RegisterSpawnablePrefab(m_ActiveResourcePrefab.GetComponent<NetworkIdentity>());
                }
            }

            // Subscribe to network disconnection and lobby left events
            NetworkManager.OnDisconnected += HandleDisconnected;
            FrizzLobby.OnLobbyLeftEvent += HandleLobbyLeft;

            TryBeginGameplaySession();
        }

        private void OnDestroy()
        {
            NetworkManager.OnDisconnected -= HandleDisconnected;
            FrizzLobby.OnLobbyLeftEvent -= HandleLobbyLeft;

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                NetworkManager.Instance.UnregisterHandler(MSG_SCENE_READY);
            }
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
                cam.transform.position = new Vector3(0f, 15f, -18f);
                cam.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.11f);
                cam.clearFlags = CameraClearFlags.Color;
            }
        }

        private void CreatePlayerPrefabTemplate()
        {
            // Dynamically instantiate a Cube that will act as the player prefab
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = "FrizzNetDemoPlayer";
            
            // Make the BoxCollider a trigger to allow player-player overlap and trigger events
            BoxCollider bc = template.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.isTrigger = true;
            }
            
            // Add kinematic Rigidbody to allow trigger/physics collision messages to fire
            Rigidbody rb = template.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            
            // Add FrizzNet components to Cube
            template.AddComponent<NetworkIdentity>();
            template.AddComponent<FrizzNetworkTransform>();
            template.AddComponent<DemoPlayerController>();

            // Deactivate and hide template in background
            template.transform.position = new Vector3(0f, -100f, 0f);
            template.SetActive(false);

            m_ActivePlayerPrefab = template;
        }

        private void CreateResourcePrefabTemplate()
        {
            // Dynamically instantiate a Sphere that acts as the resource prefab
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            template.name = "FrizzNetDemoResource";
            
            // Configure trigger collider
            SphereCollider sc = template.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
            }

            // Assign cyan neon color
            Renderer renderer = template.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0f, 0.9f, 1f); // Neon Cyan
            }

            // Add FrizzNet components (static, does not need transform sync)
            template.AddComponent<NetworkIdentity>();
            template.AddComponent<DemoResource>();

            // Deactivate and hide template in background
            template.transform.position = new Vector3(0f, -100f, 0f);
            template.SetActive(false);

            m_ActiveResourcePrefab = template;
        }

        public Vector3 GetRandomSpawnPosition()
        {
            return new Vector3(
                Random.Range(m_SpawnAreaMin.x, m_SpawnAreaMax.x),
                0.5f,
                Random.Range(m_SpawnAreaMin.z, m_SpawnAreaMax.z)
            );
        }

        public void StartGameLoop()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                FrizzLogger.LogNetwork("[Game] Host starting resource game loop. Spawning resources...");
                for (int i = 0; i < 15; i++)
                {
                    SpawnResource();
                }
            }
        }

        public void SpawnResource()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost) return;
            if (m_ActiveResourcePrefab == null) return;

            Vector3 spawnPos = Vector3.zero;
            bool foundClearSpot = false;

            // Find all active players in the scene to avoid overlapping them
            DemoPlayerController[] players = FindObjectsByType<DemoPlayerController>();

            // Try up to 30 times to find a position that does not overlap any player
            for (int attempt = 0; attempt < 30; attempt++)
            {
                spawnPos = GetRandomSpawnPosition();
                bool overlapsPlayer = false;

                foreach (var player in players)
                {
                    if (player == null) continue;

                    // Calculate overlap radius based on player's current localScale
                    float playerRadius = player.transform.localScale.x * 0.5f;
                    float minSafeDistance = playerRadius + 1.0f; // 1.0f covers the resource radius (0.5f) plus a small safety margin

                    if (Vector3.Distance(player.transform.position, spawnPos) < minSafeDistance)
                    {
                        overlapsPlayer = true;
                        break;
                    }
                }

                if (!overlapsPlayer)
                {
                    foundClearSpot = true;
                    break;
                }
            }

            if (!foundClearSpot)
            {
                // Fallback: spawn it near the edge if the center area is fully covered to prevent infinite loop recursion
                spawnPos = new Vector3(
                    Random.value > 0.5f ? 9f : -9f,
                    0.5f,
                    Random.value > 0.5f ? 9f : -9f
                );
            }

            NetworkManager.Instance.Spawn(m_ActiveResourcePrefab, spawnPos, Quaternion.identity);
        }

        private void HandleDisconnected()
        {
            FrizzLogger.LogNetwork("[Game] Client disconnected from server. Returning to Lobby scene...");
            LoadLobbyScene();
        }

        private void HandleLobbyLeft()
        {
            FrizzLogger.LogNetwork("[Game] Left lobby. Returning to Lobby scene...");
            LoadLobbyScene();
        }

        private void TryBeginGameplaySession()
        {
            if (NetworkManager.Instance == null) return;
            if (!FrizzLobby.InLobby) return;

            bool steamHost = false;
            if (SteamManager.Initialized)
            {
                steamHost = FrizzLobby.GetOwner().m_SteamID == SteamUser.GetSteamID().m_SteamID;
            }

            if (NetworkManager.Instance.IsHost && steamHost)
            {
                NetworkManager.Instance.RegisterHandler(MSG_SCENE_READY, HandleClientSceneReady);
                SpawnHostPlayer();
            }
            else if (NetworkManager.Instance.IsClient)
            {
                SendSceneReadyMessage();
            }
        }

        private void LoadLobbyScene()
        {
            if (FrizzNetworkSceneManager.Instance != null)
            {
                FrizzNetworkSceneManager.Instance.LoadSceneLocal(m_LobbySceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(m_LobbySceneName);
            }
        }

        private void SendSceneReadyMessage()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsClient)
            {
                using (MessageWriter writer = new MessageWriter())
                {
                    writer.WriteLong((long)NetworkManager.LocalConnectionId);
                    NetworkManager.Instance.SendToServer(MSG_SCENE_READY, writer, true);
                }
                FrizzLogger.LogNetwork("[Game] Sent MSG_SCENE_READY to Host.");
            }
        }

        private void HandleClientSceneReady(ulong connectionId, MessageReader reader)
        {
            ulong clientSteamId = (ulong)reader.ReadLong();
            FrizzLogger.LogNetwork($"[Game] Host received MSG_SCENE_READY from Client {clientSteamId}. Spawning player...");
            SpawnPlayerForId(clientSteamId);
        }

        private void SpawnHostPlayer()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                SpawnPlayerForId(NetworkManager.LocalConnectionId);
                StartGameLoop();
            }
        }

        private void SpawnPlayerForId(ulong steamId)
        {
            // Random position inside spawn bounds
            Vector3 spawnPos = GetRandomSpawnPosition();

            FrizzLogger.LogNetwork($"SpawnManager spawning player character for SteamID {steamId} at {spawnPos}");
            NetworkManager.Instance.Spawn(m_ActivePlayerPrefab, spawnPos, Quaternion.identity, steamId);
        }
    }
}
