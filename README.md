# LiteNetLibManager
LiteNetLib Network Manager for Unity, this is required LiteNetLib (https://github.com/RevenantX/LiteNetLib)

### Variables & Fields

- **connectKey** The key which used to make connection between client and sever, if key mismatch connection will be failed
- **networkAddress**	The network address currently in use.
- **networkPort**	The network port currently in use.
- **maxConnections**	The maximum number of concurrent network connections to support.

### Public Functions

- **StartClient**	This starts a network client. It uses the networkAddress and networkPort properties as the address to connect to.
- **StartHost**	This starts a network "host" - a server and client in the same application.
- **StartServer**	This starts a new server.
- **StopClient**	Stops the client that the manager is using.
- **StopHost**	This stops both the client and the server that the manager is using.
- **StopServer**	Stops the server that the manager is using.

### Public Overridable Functions

- **OnServerNetworkError**	Called on the server when a network error occurs for a client connection.
- **OnServerConnected**	Called on the server when a new client connects.
- **OnServerDisconnected**	Called on the server when a client disconnects.
- **OnClientNetworkError**	Called on clients when a network error occurs.
- **OnClientConnected**	Called on the client when connected to a server.
- **OnClientDisconnected**	Called on clients when disconnected from a server.
- **OnStartClient**	This is a hook that is invoked when the client is started.
- **OnStartHost**	This hook is invoked when a host is started.
- **OnStartServer**	This hook is invoked when a server is started - including when a host is started.
- **OnStopClient**	This hook is called when a client is stopped.
- **OnStopHost**	This hook is called when a host is stopped.
- **OnStopServer**	This hook is called when a server is stopped - including when a host is stopped.
