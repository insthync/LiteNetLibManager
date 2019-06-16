## Identity

The `LiteNetLibIdentity` identifies objects across the network, between server and clients. Its primary data is a `ObjectId` which is allocated by the server and then set on clients. This is used in network communications to be able to lookup game objects on different machines. 

It can placed to scene as network scene object and also used to registering as prefab in `LiteNetLibAssets.spawnablePrefabs` to spawn as network spawn object.

For an object with sub-components in a hierarchy. `LiteNetLibIdentity` must be on the root object, and `LiteNetLibBehaviour` scripts must also be on the root object.

## Behaviour

The `LiteNetLibBehaviour` is base class which should be inherited by scripts which contain networking functionality.

This is allows you to invoke `LiteNetLibNetFunction`, receive various callbacks, and automatically sync `LiteNetLibSyncField` and `LiteNetLibSyncList` from server to client. There can be multiple `LiteNetLibBehaviour` on a single game object but only one `LiteNetLibIdentity`. 

Some of the built-in components of the networking system are derived from `LiteNetLibBehaviour`, including `LiteNetLibTransform` and `LiteNetLibVisibleChecker`.

There are following event functions that overrideable:
- `OnSetOwnerClient(bool isOwnerClient)`, This function will be called when this client has been verified as owner client (Player who can controls it)
- `OnNetworkDestroy()`, This function will be called when object destroy from server
- `OnBehaviourValidate()`, This function will be called when function OnValidate() have been called in editor
- `OnSetup()`, This function will be called when this behaviour have been setup by identity. You may do some initialize things within this function such as register net functions
- `ShouldAddSubscriber()`, Override this function to decides that other object should add new object as subscriber or not
- `OnRebuildSubscribers()`, This will be called by Identity when rebuild subscribers, will return `TRUE` if subscribers have to rebuild, you can override this function to create your own interest management
- `ShouldSyncBehaviour()`, Override this function to make condition to write custom data to client
- `OnSerialize(NetDataWriter writer)`, Override this function to write custom data to send from server to client
- `OnDeserialize(NetDataReader reader)`, Override this function to read data from server at client
- `OnServerSubscribingAdded()`, Override this function to change object visibility when this added to player as subcribing. You may enable renderer here when this object is subscribe by other objects
- `OnServerSubscribingRemoved()`, Override this function to change object visibility when this removed from player as subcribing. You may disable renderer here when this object is unsubscribe by otehr objects

There are following fields that used to check authority:
- `IsServer`, Use this to check that the script is running on server or not, it will be `TRUE` when game started as server or host
- `IsClient`, Use this to check that the script is running on client or not, it will be `TRUE` when game started as client or host
- `IsOwnerClient`, Use this to check that the script is running on owner client or not, it will be `TRUE` when game started as client or host and it is spawned as player for the client