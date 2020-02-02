# How does it work - Part 1

In this part, I will explains about `LiteNetLibManager` it won't include `LiteNetLibGameManager` yet.

About `LiteNetLibManager` it's just a manager which manage connections, send and receive networking messages. It's not doing something special and won't help you to make multiplayer game easier yet.

So whats you can do with `LiteNetLibManager` are:

* Start server and wait an clients to connect.
* Start client and connect to server.
* Send messages from server to clients and clients to server.
* Register networking message for server and clients, Registering with ID and function which will be called when receive message with the ID.

`LiteNetLibManager` itself does not registered with any networking message, developers can register networking messages after start server or start client.

```
// client message register example
StartClient();
if (IsClientConnected)
{
    RegisterClientMessage(0, ReceiveFromServer_0);
}
void ReceiveFromServer_0(LiteNetLibMessageHandler messageHandler)
{
    Debug.Log("Receive: " + messageHandler.reader.GetInt() + " from server");
}

// Server message register example
StartServer();
if (IsServer)
{
    RegisterServerMessage(0, ReceiveFromClient_0);
}
void ReceiveFromClient_0(LiteNetLibMessageHandler messageHandler)
{
    Debug.Log("Receive: " + messageHandler.reader.GetInt() + " from " + messageHandler.connectionId);
}
```

For classes which derived from `LiteNetLibManager`, they should register messages in override functions:`RegisterClientMessages()`, `RegisterServerMessages()`.

```
protected override void RegisterClientMessages()
{
    RegisterClientMessage(0, ReceiveFromServer_0);
}

protected override void RegisterServerMessages()
{
    RegisterServerMessage(0, ReceiveFromClient_0);
}
```

* * *

## Server workflow

When server started it will waiting for connecting clients and also waiting for incoming messages from clients. When any client connected, server will generate connection ID (By Transport class). So server can know messages sent from which client and can send messages to target client.

## Client workflow

When client started and connected to server, it will waiting for incoming messages from server. That is it.