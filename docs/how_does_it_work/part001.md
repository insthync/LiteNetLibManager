# How it works, part 1: transport and messages

`LiteNetLibManager` starts and stops the server, client, or host, polls the selected `ITransport`, and routes messages by numeric type. It does not spawn game objects or synchronize state by itself. `LiteNetLibGameManager` adds those features in [part 2](part002.md).

Register handlers in `RegisterMessages()`, which runs while the manager initializes, before normal connection traffic. A handler receives `MessageHandlerData`; `Reader` reads the payload and `ConnectionId` identifies the sending connection on the server.

```csharp
using LiteNetLib;
using LiteNetLibManager;
using UnityEngine;

public sealed class ExampleManager : global::LiteNetLibManager.LiteNetLibManager
{
    private const ushort ExampleMessage = 11;

    protected override void RegisterMessages()
    {
        base.RegisterMessages();
        RegisterServerMessage(ExampleMessage, ReceiveFromClient);
        RegisterClientMessage(ExampleMessage, ReceiveFromServer);
    }

    private void ReceiveFromClient(MessageHandlerData message)
    {
        int value = message.Reader.GetInt();
        Debug.Log("From " + message.ConnectionId + ": " + value);
    }

    private void ReceiveFromServer(MessageHandlerData message)
    {
        int value = message.Reader.GetInt();
        Debug.Log("From server: " + value);
    }

    public void SendValueToServer(int value)
    {
        if (IsClientConnected)
            ClientSendPacket(0, DeliveryMethod.ReliableOrdered,
                ExampleMessage, writer => writer.Put(value));
    }

    public void SendValueToClient(long connectionId, int value)
    {
        if (IsServer)
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered,
                ExampleMessage, writer => writer.Put(value));
    }
}
```

The example uses type 11. When extending `LiteNetLibGameManager`, call `base.RegisterMessages()` and use an ID above `GameMsgTypes.Highest` (currently 10) to avoid its built-in messages. Use the same type and payload order on both peers. `RegisterServerMessage` handles client-to-server packets; `RegisterClientMessage` handles server-to-client packets.

The transport assigns a connection ID when a client connects. The server uses it to address a particular client. Clients wait for the connection before sending packets. For game sessions, enter-game and ready requests, see [part 2](part002.md).
