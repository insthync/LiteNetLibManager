# LiteNetLibManager

LiteNetLibManager is a Unity networking library. `LiteNetLibManager` provides transport and message handling. `LiteNetLibGameManager` adds game sessions, scenes, network objects, RPCs, and state synchronization.

## Start here

1. [Configure the game manager and assets](basic/network_manager.md).
2. [Create network identities and behaviours](basic/network_object.md).
3. [Synchronize fields and lists](basic/sync_variables.md) or [call RPCs](basic/net_function.md).
4. Read the [game state sync workflow](testing/state_sync_layout_results_2026-09-28.md) for packet behavior, layout requirements, and owner-only fields.

The [connection walkthrough](how_does_it_work/part002.md), [object lifecycle walkthrough](how_does_it_work/part003.md), and [transport guide](advanced/custom_transport_layer.md) cover the underlying flow. [Editor test results](testing/editor_test_results_2026-09-28_schema.md) record the verified cases.
