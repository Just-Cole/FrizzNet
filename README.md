# FrizzNet

Steam-first multiplayer networking for Unity 6+.

FrizzNet gives you Steam Networking Sockets P2P, dedicated Steam Game Server hosting, matchmaking lobbies, binary messaging, replicated objects, sync components, and Steam voice.

Host owns spawning and session flow. Clients send requests; your game code validates them before changing authoritative state. Dedicated processes are host-authoritative and never own a player pawn.

## Features

- **Steam P2P** — NAT traversal via Steam Networking Sockets
- **Dedicated Game Server** — `SteamGameServerManager` + `SteamGameServerTransport` for headless host authority
- **Lobbies** — create, join, invite, browse, and sync lobby metadata
- **Messaging** — binary packets with `MessageWriter` / `MessageReader`
- **Spawning** — host-controlled spawn/despawn with prefab registry
- **Behaviours** — `NetworkBehaviour` lifecycle, RPCs, and network variables
- **Sync** — transform, animator, and rigidbody replication
- **Scenes** — host-initiated networked scene loads
- **Voice** — Steam push-to-talk / spatial voice chat with noise suppression (high-pass + adaptive gate)
- **Tools** — runtime monitor (`Tools > FrizzNet`), demo scene setup, samples

## Requirements

- Unity 6+
- Steam client running and logged in
- A Steam App ID (`480` SpaceWar works for development)

Installing FrizzNet also installs [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) into the project automatically. You do not need to add it by hand.

> `SteamManager` currently hardcodes App ID `480` in `RestartAppIfNecessary`. Change that before shipping. A project-root `steam_appid.txt` helps local launches but does not replace the code change.

## Quick start

1. Create a GameObject named `NetworkManager`.
2. Add `NetworkManager` and `SteamTransport`.
3. Assign `SteamTransport` to **Transport Component**.
4. Add your networked prefabs (with `NetworkIdentity`) to **Spawnable Prefabs**.
5. Optionally add `FrizzServerManager`, `FrizzVoiceManager`, and `FrizzNetworkSceneManager`.

```csharp
using FrizzNet.Core;
using Steamworks;

// Host a public lobby
FrizzServerManager.Instance.MaxPlayers = 8;
FrizzServerManager.Instance.LobbyName = "Co-op Session";
FrizzServerManager.Instance.LobbyType = ELobbyType.k_ELobbyTypePublic;
FrizzServerManager.Instance.StartServer();
```

```csharp
using FrizzNet.Core;
using FrizzNet.Messaging;

private const short ChatMessageId = 101;

private void OnEnable()
{
    NetworkManager.Instance.RegisterHandler(ChatMessageId, OnChatReceived);
}

private void OnDisable()
{
    if (NetworkManager.Instance != null)
    {
        NetworkManager.Instance.UnregisterHandler(ChatMessageId);
    }
}

private void SendChat(string text)
{
    using (MessageWriter writer = new MessageWriter())
    {
        writer.WriteString(text);
        NetworkManager.Instance.SendToServer(ChatMessageId, writer, true);
    }
}

private void OnChatReceived(ulong senderId, MessageReader reader)
{
    string text = reader.ReadString();
    // Validate senderId and text before the host relays or applies it.
}
```

Use **positive** custom message IDs. Negative IDs are reserved by FrizzNet.

## Documentation

Open the local reference:

- [Documentation/index.html](Documentation/index.html)

Or browse sections directly:

| Topic | Link |
| --- | --- |
| Setup | [Setup](Documentation/index.html#SetupGuide) |
| Authority | [Authority](Documentation/index.html#Authority) |
| Messaging | [Messaging](Documentation/index.html#Messaging) |
| NetworkBehaviour / RPCs | [NetworkBehaviour](Documentation/index.html#NetworkBehaviour) |
| Code samples | [Code samples](Documentation/index.html#CodeSamples) |
| Steam transport | [SteamTransport](Documentation/index.html#SteamTransport) |

## Samples

| Sample | What it shows |
| --- | --- |
| `Samples/LobbyExample` | Steam lobby UI, scene transition, demo gameplay |
| `Samples/ChatExample` | Custom message registration and host relay |

Generate demo scenes from `Tools > FrizzNet > Setup Demo Scenes`.  
Open the live monitor from `Tools > FrizzNet`.

## Dedicated Steam Game Server

Player-hosted P2P lobbies stay available for local testing. A dedicated process uses the Steam Game Server API instead of `SteamUser`.

1. Add `SteamGameServerManager` and `SteamGameServerTransport`.
2. Set `NetworkManager.IsDedicatedServer = true`. Dedicated hosts still report `IsHost == true`.
3. Call `SteamGameServerManager.Initialize()` then `Transport.StartHost` after `LoggedOn`.
4. `LocalConnectionId` on the server is the **game server Steam ID**, not a player.
5. Clients keep `SteamManager` + `SteamTransport` and `StartClient` to that Steam ID64.

SpaceWar (App ID `480`) uses anonymous logon until you have a Steam App ID and GSLT. Players still need the Steam client. The dedicated box needs Steamworks Game Server DLLs, a root `steam_appid.txt` containing `480`, and outbound Steam traffic.

Dedicated servers do not use player lobbies for authority and do not migrate host to a connected player.

## Project layout

```text
FrizzNet/
├── Runtime/
│   ├── Core/          NetworkManager, identity, sync, spawners, voice, scenes
│   ├── Steam/         SteamTransport, SteamGameServerTransport, FrizzLobby, FrizzLobbyBrowser
│   ├── Messaging/     MessageWriter, MessageReader, system message IDs
│   ├── Transport/     INetworkTransport
│   ├── Logging/       FrizzLogger
│   └── Utilities/     SteamManager, SteamGameServerManager
├── Editor/            Monitor window, demo scene setup
├── Samples/           Lobby + chat examples
└── Documentation/     HTML reference site
```

## Important limitations

- Lobby passwords are public metadata — not real authentication.
- The host must validate client messages, RPCs, variable writes, and synced state.
- Late join only restores prefab, ID, position, rotation, and owner. SyncVars, animator, rigidbody, scale, scene, and custom state need your own snapshot.
- Interest management currently filters relayed client transform updates only.
- Host migration is experimental (prefab/transform/owner restore only).
- RPC dispatch targets the first `NetworkBehaviour` on an object — keep RPC handlers on one behaviour per networked object.
