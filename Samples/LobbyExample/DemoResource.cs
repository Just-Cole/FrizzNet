using UnityEngine;
using FrizzNet.Core;

namespace FrizzNet.Samples
{
    /// <summary>
    /// Component placed on dynamically spawned resources.
    /// Handles trigger collision logic authoritatively on the host.
    /// </summary>
    public class DemoResource : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Only run resource collection logic on the Host!
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost) return;

            DemoPlayerController player = other.GetComponent<DemoPlayerController>();
            if (player != null)
            {
                // Grow the player cube
                player.Grow(0.15f);

                // Despawn this resource globally across all clients
                NetworkManager.Instance.Despawn(gameObject);

                // Ask the spawn manager to spawn a replacement resource
                DemoSpawnManager spawnManager = FindAnyObjectByType<DemoSpawnManager>();
                if (spawnManager != null)
                {
                    spawnManager.SpawnResource();
                }
            }
        }
    }
}
