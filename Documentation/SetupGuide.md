# FrizzNet Setup & Architecture Guide

FrizzNet is a lightweight, Steam-first, connection-oriented networking framework for Unity 6+. 
It utilizes **Steamworks.NET** and **Steam Networking Sockets** to offer a simple P2P networking experience with lobbies and state replication.

---

## 🚀 Quick Start

### 1. Requirements
*   Unity 6+
*   Steam Client running and logged in.
*   **Steamworks.NET** package installed in Unity.

### 2. Configuration Setup
*   Ensure that the `steam_appid.txt` file exists in your project root containing the AppID (default test AppID is `480` for SpaceWar).
*   Add a GameObject named `NetworkManager` to your first scene.
*   Attach the [NetworkManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/NetworkManager.cs) and [SteamTransport](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Steam/SteamTransport.cs) components to it.
*   Reference the `SteamTransport` component inside the `Transport Component` field of the `NetworkManager`.

---

## 🛠️ Key Components & API

### 1. Steam Matchmaking Lobbies (`FrizzLobby`)
The static `FrizzLobby` class provides simple APIs to handle Steam lobbies. When a player creates or joins a lobby, `SteamTransport` automatically initializes host/client connection routines.

```csharp
using FrizzNet.Steam;

// Create a public lobby for up to 4 players
FrizzLobby.Create(4, ELobbyType.k_ELobbyTypePublic);

// Join a lobby by ID
FrizzLobby.Join(lobbyId);

// Leave lobby
FrizzLobby.Leave();

// Invite friends using Steam overlay
FrizzLobby.InviteFriends();
```

Listen to lobby events:
```csharp
FrizzLobby.OnLobbyCreatedEvent += (lobbyId) => Debug.Log($"Lobby created: {lobbyId}");
FrizzLobby.OnLobbyJoinedEvent += (lobbyId) => Debug.Log($"Lobby entered: {lobbyId}");
FrizzLobby.OnLobbyLeftEvent += () => Debug.Log("Lobby left.");
```

---

### 2. Packet Messaging (`MessageWriter` & `MessageReader`)
Serialize and deserialize primitives over the network using unmanaged buffers:

**Writing a packet:**
```csharp
using FrizzNet.Messaging;

using (MessageWriter writer = new MessageWriter())
{
    writer.WriteString("Hello Player!");
    writer.WriteInt(42);
    writer.WriteBool(true);

    // Send to server (reliable)
    NetworkManager.Instance.SendToServer(MyMessageId, writer, true);
}
```

**Reading a packet:**
Register a message handler inside `Start()` or `Awake()`:
```csharp
private void Start()
{
    NetworkManager.Instance.RegisterHandler(MyMessageId, OnReceiveMyMessage);
}

private void OnReceiveMyMessage(ulong senderId, MessageReader reader)
{
    string msgText = reader.ReadString();
    int number = reader.ReadInt();
    bool flag = reader.ReadBool();

    Debug.Log($"Received {msgText}, {number}, {flag} from client {senderId}");
}
```

---

### 3. Spawning & Object Sync
To synchronize a GameObject across the network:
1. Attach a [NetworkIdentity](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/NetworkIdentity.cs) to the prefab.
2. Drag the prefab into the **Spawnable Prefabs** list on the `NetworkManager` component (or assign it to the `FrizzPlayerSpawner` which handles dynamic registration).
3. Attach [FrizzNetworkTransform](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzNetworkTransform.cs) to sync position and rotation smoothly.

**Spawning a prefab (Host only):**
```csharp
// Spawns a prefab on host and replicates to all clients
GameObject myPlayer = NetworkManager.Instance.Spawn(playerPrefab, spawnPos, Quaternion.identity, ownerSteamId);
```

**Despawning a prefab (Host only):**
```csharp
NetworkManager.Instance.Despawn(myPlayer);
```

**Automated Player Spawning (`FrizzPlayerSpawner`):**
For player characters, you can use the [FrizzPlayerSpawner](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzPlayerSpawner.cs) component to automate player instantiation and zone placement:
*   **Auto-Spawn**: Set `Auto Spawn` to true to automatically instantiate the player avatar when the Host starts the lobby or when clients connect.
*   **Spawn Point Selection**: Cycles through your configured `Spawn Points` using `Random` or `RoundRobin` selection.
*   **Collision Checking**: Enable `Avoid Occupied Spawn Points` to dynamically check for obstacles at spawn points before placing the character, falling back to other points if blocked.

---

### 4. Real-time Voice Chat (`FrizzVoiceManager`)
FrizzNet supports out-of-the-box spatialized voice chat using the Steam Client's native audio recording settings.

**Voice Chat Setup:**
1. Attach the [FrizzVoiceManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzVoiceManager.cs) component to a persistent GameObject in your startup scene (e.g., the same GameObject as `NetworkManager`).
2. Adjust settings like Push-to-Talk, PTT Key, volume levels, and spatial audio rolloff directly in the inspector or Editor Monitor tool.

**Using Spatial Audio:**
If `Spatial Audio` is checked, the voice stream for each player will automatically be positioned at their matching networked avatar's transform position in 3D space by tracking the `NetworkIdentity` owning that client ID.

---

### 5. Network Animation Sync (`FrizzNetworkAnimator`)
Synchronize character animations smoothly across the network:
1. Attach the [FrizzNetworkAnimator](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzNetworkAnimator.cs) component to any GameObject with a Unity `Animator` component.
2. In the inspector, configure the `Send Rate` to adjust update frequency.
3. For float, int, and bool parameters, the component automatically tracks changes from the authoritative client and synchronizes them to remote clients.
4. For triggers, call `SetTrigger(...)` directly on the `FrizzNetworkAnimator` component (instead of the `Animator` component) to replicate trigger states instantly.

```csharp
// Example: Setting a trigger on the FrizzNetworkAnimator component
[SerializeField] private FrizzNetworkAnimator m_NetworkAnimator;

void PerformJump()
{
    // Sets the trigger locally and broadcasts it to all other clients
    m_NetworkAnimator.SetTrigger("Jump");
}
```

---

### 6. Automated Server Spawning (`FrizzServerSpawner`)
Automate the instantiation of static server-owned objects (NPCs, chests, doors, obstacles) on lobby initialization:
1. Create a GameObject in your host scene and attach the [FrizzServerSpawner](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzServerSpawner.cs) component.
2. Add items to the `Spawn Items` list by providing the registered prefab and the target spawn location Transform.
3. Keep `Spawn On Lobby Join` enabled to spawn the items automatically when the host lobby is created, or call `SpawnAll()` manually.

---

## 🖥️ Editor Monitoring Tool
FrizzNet includes a premium monitor window inside Unity to track network status in real-time.
Access it via:
`Tools > FrizzNet`

**Features:**
*   Live API status indicators.
*   Lobby ID and local user credentials display.
*   Interactive buttons to create/leave lobbies during Play Mode.
*   Real-time listing of lobby members, network objects, and active connections.
*   Global toggle to enable/disable FrizzNet console logging.
