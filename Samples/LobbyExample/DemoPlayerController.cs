using UnityEngine;
using Steamworks;
using FrizzNet.Core;
using FrizzNet.Steam;
using FrizzNet.Logging;
using UnityEngine.InputSystem;

namespace FrizzNet.Samples
{
    /// <summary>
    /// Sample player controller demonstrating authority checks, movement input, 
    /// and screen-space name tag rendering using Steamworks profile names.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [FrizzHelp("Reads keyboard inputs to move and rotate player characters possessing authority. Renders hovering profile name tags on screen.")]
    public class DemoPlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float m_Speed = 5f;

        private Color m_PlayerColor;
        private string m_SteamName = "Connecting...";


        private void Start()
        {
            // Give each player a unique random color based on their Owner ID
            Random.InitState((int)NetworkIdentity.OwnerConnectionId);
            m_PlayerColor = new Color(Random.value, Random.value, Random.value);

            // Apply color to the material of the Cube
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = m_PlayerColor;
            }

            // Fetch Steam Username associated with this player character
            if (SteamManager.Initialized && NetworkIdentity.OwnerConnectionId != 0)
            {
                m_SteamName = SteamFriends.GetFriendPersonaName(new CSteamID(NetworkIdentity.OwnerConnectionId));
            }
            else
            {
                m_SteamName = "Local Bot";
            }
        }

        private void Update()
        {
            // Only read input and move if this local client has authority over this object!
            if (!HasAuthority) return;

            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal = -1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal = 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical = 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical = -1f;
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (Mathf.Abs(stick.x) > 0.1f) horizontal = stick.x;
                if (Mathf.Abs(stick.y) > 0.1f) vertical = stick.y;
            }

            Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

            if (moveDirection.magnitude > 0.1f)
            {
                transform.Translate(moveDirection * m_Speed * Time.deltaTime, Space.World);
                
                // Rotate player towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }

        private void OnGUI()
        {
            if (Camera.main == null) return;

            // Project 3D player position to 2D Screen Space for a name tag
            Vector3 worldPos = transform.position + Vector3.up * 1.2f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Only draw if the player is in front of the camera
            if (screenPos.z > 0)
            {
                string tag = IsLocalPlayer ? $"<b><color=#39FF14>{m_SteamName} (You)</color></b>" : m_SteamName;
                
                GUIStyle nameTagStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    richText = true
                };

                // Draw background shadow
                nameTagStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(screenPos.x - 100 + 1, Screen.height - screenPos.y - 12 + 1, 200, 24), tag, nameTagStyle);

                // Draw foreground colored text
                nameTagStyle.normal.textColor = IsLocalPlayer ? Color.green : Color.white;
                GUI.Label(new Rect(screenPos.x - 100, Screen.height - screenPos.y - 12, 200, 24), tag, nameTagStyle);
            }
        }

        public void Grow(float amount)
        {
            Vector3 targetScale = transform.localScale + new Vector3(amount, amount, amount);
            // Cap maximum scale to 8.0f to prevent players from blocking the entire play arena
            if (targetScale.x > 8.0f)
            {
                targetScale = new Vector3(8.0f, 8.0f, 8.0f);
            }
            transform.localScale = targetScale;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Only resolve player-player collisions authoritatively on the Host
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost) return;

            DemoPlayerController otherPlayer = other.GetComponent<DemoPlayerController>();
            if (otherPlayer != null)
            {
                float mySize = transform.localScale.x;
                float otherSize = otherPlayer.transform.localScale.x;

                // Check if I am bigger than the other player by a reasonable margin
                if (mySize > otherSize + 0.05f)
                {
                    // Consumed! Other player dies and respawns
                    otherPlayer.DieAndRespawn();

                    // Host grows a bit for eating another player
                    Grow(0.3f);
                    FrizzLogger.LogNetwork($"[Game] Player with SteamID {NetworkIdentity.OwnerConnectionId} consumed player with SteamID {otherPlayer.NetworkIdentity.OwnerConnectionId}!");
                }
            }
        }

        public void DieAndRespawn()
        {
            // Reset size to default
            transform.localScale = Vector3.one;

            // Find a random spawn point
            DemoSpawnManager spawnManager = FindAnyObjectByType<DemoSpawnManager>();
            if (spawnManager != null)
            {
                transform.position = spawnManager.GetRandomSpawnPosition();
            }
            else
            {
                transform.position = new Vector3(Random.Range(-8f, 8f), 0.5f, Random.Range(-8f, 8f));
            }
        }
    }
}
