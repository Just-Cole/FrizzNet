# ⚡ FrizzNet

FrizzNet is a lightweight, Steam-first, connection-oriented networking framework for Unity 6+. By utilizing **Steamworks.NET** and **Steam Networking Sockets (P2P)**, FrizzNet enables seamless, NAT-punching multiplayer experiences with matchmaking lobbies, raw binary packet serialization, and state replication without the need for dedicated servers.

---

## 🚀 Key Features

*   **Steam Matchmaking Lobbies**: Effortlessly create, join, leave, and query lobbies using the static [FrizzLobby](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Steam/FrizzLobby.cs) API.
*   **Steam Networking Sockets**: Highly reliable, low-latency connection-oriented P2P communication powered by Steam's virtual networking infrastructure.
*   **Low-Overhead Packet Serialization**: Custom binary reading and writing via unmanaged memory with [MessageReader](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Messaging/MessageReader.cs) and [MessageWriter](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Messaging/MessageWriter.cs).
*   **State & Transform Synchronization**: Synchronize positions and rotations unreliably over the network with smooth, jitter-free interpolation via [FrizzNetworkTransform](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzNetworkTransform.cs).
*   **Animator State Replication**: Synchronize Unity animations, states, and parameters seamlessly across remote clients using [FrizzNetworkAnimator](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzNetworkAnimator.cs).
*   **Automated Player Spawning**: Simplify instantiation and layout matching via [FrizzPlayerSpawner](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzPlayerSpawner.cs) supporting random/round-robin and check-based spawn validation.
*   **Server Entity Spawning**: Instantiate host-owned static world objects, NPCs, or interactive prefabs automatically using [FrizzServerSpawner](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzServerSpawner.cs).
*   **Server/Session Management**: Manage player slots, lobby accessibility, lobby names, passwords, and authoritative player kicking via [FrizzServerManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzServerManager.cs).
*   **Host Migration**: Automatically handles Steam lobby owner changes, promoting client socket loops to hosts and routing connections seamlessly.
*   **Integrated Voice Chat**: Built-in spatialized and push-to-talk voice communication utilizing Steam's native voice codecs with [FrizzVoiceManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzVoiceManager.cs).
*   **Unity Editor Monitor Window**: Premium, custom Unity inspector dashboard (`Tools > FrizzNet`) for tracking active connections, lobby details, and network objects in real-time.

---

## 🛠️ Requirements & Installation

1.  **Unity 6+** (supports newer Unity LTS versions).
2.  **Steam Client** active and logged in on your development machine.
3.  **Steamworks.NET** package imported/installed in your Unity project.
4.  A `steam_appid.txt` file placed in the root of your project directory containing your AppID (e.g., `480` for SpaceWar testing).

---

## ⚙️ Quick Start Setup

1.  Create a persistent `GameObject` in your startup scene and name it `NetworkManager`.
2.  Attach both the [NetworkManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/NetworkManager.cs) and [SteamTransport](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Steam/SteamTransport.cs) scripts to this GameObject.
3.  Assign the `SteamTransport` component to the **Transport Component** slot on the [NetworkManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/NetworkManager.cs).
4.  Configure your spawnable prefabs inside the **Spawnable Prefabs** list on the NetworkManager inspector.

For a detailed step-by-step walkthrough, check out the [SetupGuide](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Documentation/SetupGuide.md).

---

## 📖 Core API Usage

### 1. Steam Matchmaking & Lobbies (`FrizzLobby`)

Use [FrizzLobby](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Steam/FrizzLobby.cs) for matchmaking and friend interactions:

```csharp
using FrizzNet.Steam;

// Create a lobby
FrizzLobby.Create(maxPlayers: 4, ELobbyType.k_ELobbyTypePublic);

// Join an existing lobby
FrizzLobby.Join(lobbyId);

// Invite friends using Steam overlay
FrizzLobby.InviteFriends();

// Set Lobby metadata (Host Only)
FrizzLobby.SetMetadata("map", "Alpha Outpost");
```

### 2. Message Serialization (`MessageWriter` & `MessageReader`)

Define custom packet payloads with [MessageWriter](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Messaging/MessageWriter.cs) and [MessageReader](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Messaging/MessageReader.cs):

```csharp
using FrizzNet.Messaging;
using FrizzNet.Core;

// Sending a Packet
const short MSG_CHAT = 101;
using (MessageWriter writer = new MessageWriter())
{
    writer.WriteString("Hello everyone!");
    
    // Client sends to Host (reliable delivery)
    NetworkManager.Instance.SendToServer(MSG_CHAT, writer, reliable: true);
}
```

```csharp
// Registering a Handler
void Start()
{
    NetworkManager.Instance.RegisterHandler(MSG_CHAT, OnReceiveChat);
}

void OnReceiveChat(ulong senderId, MessageReader reader)
{
    string chatMessage = reader.ReadString();
    Debug.Log($"[Chat] Client {senderId}: {chatMessage}");
}
```

### 3. Object Spawning & Transform Replication

To spawn networked objects, ensure the prefab has a [NetworkIdentity](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/NetworkIdentity.cs) component attached and is registered in the [NetworkManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/NetworkManager.cs)'s Spawnable Prefabs list.

```csharp
// Spawn prefab (Host only - will replicate automatically to all connected clients)
GameObject playerObj = NetworkManager.Instance.Spawn(playerPrefab, spawnPosition, Quaternion.identity, ownerSteamId);

// Despawn prefab (Host only)
NetworkManager.Instance.Despawn(playerObj);
```

Attach a [FrizzNetworkTransform](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzNetworkTransform.cs) component to automatically and smoothly interpolate position and rotation updates unreliably across the network.

### 4. Voice Chat Integration (`FrizzVoiceManager`)

FrizzNet has out-of-the-box support for spatialized voice chat using the Steam Client's microphone subsystem. Simply attach the [FrizzVoiceManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzVoiceManager.cs) component to a persistent GameObject in your scene and configure it:

```csharp
using FrizzNet.Core;

// Configure Voice Chat settings via code (or directly in inspector)
FrizzVoiceManager.Instance.EnableVoice = true;
FrizzVoiceManager.Instance.UsePushToTalk = true;
FrizzVoiceManager.Instance.PushToTalkKey = KeyCode.V;
FrizzVoiceManager.Instance.SpatialAudio = true; // Enables 3D audio scaling
FrizzVoiceManager.Instance.MaxAudioDistance = 50f;
```

### 5. Network Animation Sync (`FrizzNetworkAnimator`)

To synchronize animations, attach a [FrizzNetworkAnimator](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzNetworkAnimator.cs) component to the GameObject containing your `Animator` component. All float, int, and bool parameters are automatically tracked. For triggers, use the component's `SetTrigger` method:

```csharp
using FrizzNet.Core;

// Trigger animation across the P2P network:
FrizzNetworkAnimator netAnimator = GetComponent<FrizzNetworkAnimator>();
netAnimator.SetTrigger("Jump");
```

### 6. Automated Server Spawning (`FrizzServerSpawner`)

Attach the [FrizzServerSpawner](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzServerSpawner.cs) component to a manager GameObject on the Host to automatically spawn server-owned lobby entities (such as NPCs, chests, or obstacles) at designated locations:

```csharp
using FrizzNet.Core;

// Spawner executes automatically on lobby entry for the Host, 
// or can be triggered manually:
FrizzServerSpawner spawner = GetComponent<FrizzServerSpawner>();
if (NetworkManager.Instance.IsHost && !spawner.HasSpawned)
{
    spawner.SpawnAll();
}
```

### 7. Server/Session Management (`FrizzServerManager`)

Attach the [FrizzServerManager](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Runtime/Core/FrizzServerManager.cs) component to your manager GameObject to configure matchmaking parameters and authoritatively manage the session:

```csharp
using FrizzNet.Core;

// Configure session properties
FrizzServerManager.Instance.LobbyName = "FrizzNet High-Stakes Arena";
FrizzServerManager.Instance.MaxPlayers = 8;
FrizzServerManager.Instance.LobbyType = Steamworks.ELobbyType.k_ELobbyTypePublic;
FrizzServerManager.Instance.LobbyPassword = "secret_passcode";

// Start the server and create matchmaking lobby
FrizzServerManager.Instance.StartServer();

// Authoritatively kick a client (Host only)
FrizzServerManager.Instance.KickPlayer(offendingClientSteamId);
```

---

## 🎮 Included Samples

Check out the fully functional examples located in the `/Samples` directory:
*   [ChatExample](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Samples/ChatExample/ChatExample.cs): A lobby-wide text chat dashboard demonstrating custom message handlers and server packet routing.
*   [LobbyExample](file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Samples/LobbyExample/LobbyExample.cs): An IMGUI lobby manager showing player ready states, lobby owner promotion (Host Migration), and game option synchronization.

---

## 📁 Project Structure

```text
FrizzNet/
├── Runtime/
│   ├── Core/               <- NetworkManager, NetworkIdentity, FrizzNetworkTransform, FrizzPlayerSpawner, FrizzNetworkAnimator, FrizzServerSpawner, FrizzServerManager
│   ├── Steam/              <- SteamTransport, FrizzLobby matchmaking API
│   ├── Messaging/          <- MessageReader, MessageWriter serialization
│   ├── Logging/            <- FrizzLogger utility
│   └── Transport/          <- INetworkTransport interface
├── Editor/                 <- Monitor window, Inspectors, custom drawers
├── Samples/                <- Complete Chat and Lobby examples
└── Documentation/          <- Setup & Architecture guide
```

---

> [!NOTE]
> Ensure the Steam Client is running and you are logged into an active Steam account before entering Play Mode, otherwise Steamworks initialization will fail.
