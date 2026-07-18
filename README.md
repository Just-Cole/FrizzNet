# FrizzNet

FrizzNet is a lightweight multiplayer framework for Unity 6+. It provides Steam Networking Sockets for internet play, a local TCP transport for development, binary messages, replicated objects, synchronization components, Steam lobbies, and Steam voice chat.

FrizzNet uses a host-and-clients topology. The host owns object spawning and session flow. Client-authored gameplay requests must still be validated by your game code before changing authoritative state.

## Features

- Steam P2P transport, lobbies, invites, metadata, and lobby browsing
- Local TCP sessions for testing without a Steam lobby
- Binary custom messaging with reliable and unreliable delivery
- Host-controlled network object spawning and despawning
- `NetworkBehaviour` lifecycle, RPC helpers, and network variables
- Position, rotation, optional scale, Animator parameter, trigger, and Rigidbody synchronization
- Player and host-owned world-object spawners
- Host-initiated scene loading and optional distance-based transform filtering
- Steam voice capture, relay, playback, and spatial speakers
- Experimental graceful host-migration snapshots
- Unity inspectors, a runtime monitor, scene setup tools, and samples

## Requirements

- Unity 6+
- Steamworks.NET (the runtime assembly references it for both transports)
- Steam client and a valid App ID when using Steam features

The included `SteamManager` currently calls `RestartAppIfNecessary` with App ID `480`. Change that implementation before shipping under your own Steam application. A `steam_appid.txt` file is still useful for local Steam testing, but does not replace that code change.

## Choose a setup

- [Steam setup](Documentation/index.html#SteamTransport) — lobbies, invites, NAT traversal, and internet play
- [Local setup](Documentation/index.html#LocalTransport) — same-machine or LAN TCP testing
- [Complete setup and architecture guide](Documentation/index.html#SetupGuide)

Both setups require a `NetworkManager` and exactly one enabled component implementing `INetworkTransport`. Assign that component to **Transport Component**, then register every dynamically spawned prefab in **Spawnable Prefabs** on every peer.

## Documentation

- [Authority model](Documentation/index.html#Authority)
- [Custom messaging](Documentation/index.html#Messaging)
- [NetworkBehaviour, RPCs, and network variables](Documentation/index.html#NetworkBehaviour)
- [Synchronization systems](Documentation/index.html#FrizzNetworkTransform)
- [Code samples](Documentation/index.html#CodeSamples)
- [Local HTML reference](Documentation/index.html)

## Minimal custom message

Use positive message IDs. Negative IDs are reserved by FrizzNet.

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

private void SendChatToHost(string text)
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

## Important limitations

- Steam lobby passwords are public metadata and are not secure access control.
- The host must validate client messages, RPCs, variable writes, and synchronized state.
- Late join recreates spawned prefabs with position, rotation, and owner only. Current SyncVar, Animator, Rigidbody, scale, scene, and custom gameplay state require an application-level snapshot.
- Interest management currently filters relayed client transform updates only.
- Host migration restores prefab identity, transform, scale, owner, and ID seed only, and should be treated as experimental.
- RPC dispatch currently targets the first `NetworkBehaviour` on an object. Keep RPC handlers on one behaviour per networked object.
- Steam voice chat is not available through `LocalTransport`.

See the HTML reference for full behavior, examples, and safe usage rules.

## Included samples

- `Samples/LocalTest` — local host/join flow
- `Samples/LobbyExample` — Steam lobby and demo session flow
- `Samples/ChatExample` — custom message registration and relay

Use `Tools > FrizzNet` for the runtime monitor. Additional setup commands are available under `Tools > FrizzNet`.
