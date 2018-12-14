# Overview

LiteNetLibManager is high level networking library based on LiteNetLib (https://github.com/RevenantX/LiteNetLib) for Unity3D (https://unity3d.com) which handles many of the common tasks that are required for multiplayer games. It is a server authoritative system, although it allows one of the participants to be a client and the server at the same time (host), so no dedicated server process is required, It provides:

- LiteNetLibGameManager component for network message management, spawn management, scene management
- LiteNetLibIdentity component for networked game objects
- LiteNetLibBehaviour component for networked scripts
- LiteNetLibSyncField/LiteNetLibSyncList for automatic synchronization of script variables
- LiteNetLibNetFunction for make remote procedure calls (RPCs)
- Support for placing networked objects in Unity scenes