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
2. Drag the prefab into the **Spawnable Prefabs** list on the `NetworkManager` component.
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
