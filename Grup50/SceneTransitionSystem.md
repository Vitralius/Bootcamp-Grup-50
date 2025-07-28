# Clean Scene Transition System

## Overview

This is a simple, clean scene transition system for Unity Netcode multiplayer games. It handles transitioning from lobby to game scene with player data persistence and proper spawn point management.

## Components

### 📋 SimpleSceneTransition
**File**: `Assets/Scripts/Network/SceneTransitionManager.cs`
**Purpose**: Manages scene transitions between lobby and game scenes

**Key Features**:
- ✅ Uses Unity Netcode `NetworkSceneManager.OnLoadEventCompleted` for proper timing
- ✅ Caches player data (clientId, characterId, playerName) before transitions
- ✅ Simple API: `StartGameTransition()` and `ReturnToLobby()`
- ✅ Server-only control with proper error handling
- ✅ Integrates with PlayerSessionData for character selections

**Usage**:
```csharp
// Start transition to game (host only)
SimpleSceneTransition.Instance.StartGameTransition();

// Return to lobby (host only)  
SimpleSceneTransition.Instance.ReturnToLobby();

// Check current scene
bool inLobby = SimpleSceneTransition.Instance.IsInLobby();
bool inGame = SimpleSceneTransition.Instance.IsInGame();
```

### 🎮 GameSpawnManager
**File**: `Assets/Scripts/Network/GameSpawnManager.cs`
**Purpose**: Spawns players at specific spawn points with IDs 0-1-2-3-4-5 etc.

**Key Features**:
- ✅ Spawn point assignment: `clientId % spawnPoints.Length`
- ✅ Player spawning: `SpawnPlayerWithData(clientId, characterId, playerName)`
- ✅ Character data application after network spawn
- ✅ Proper camera enablement for owners
- ✅ NetworkObject spawning with correct ownership

**Setup**:
1. **Create Empty GameObjects** for spawn points in your game scene
2. **Position them** where you want players to spawn (e.g., spawn point 0, 1, 2, 3, 4, 5)
3. **Assign to GameSpawnManager** inspector array
4. **Set Player Prefab** to your networked player prefab

**Player Assignment Logic**:
- Client 0 → Spawn Point 0
- Client 1 → Spawn Point 1  
- Client 2 → Spawn Point 2
- Client 6 → Spawn Point 0 (cycles back)

## Flow Diagram

```
[Lobby Scene]
     ↓ Host clicks "Start Game"
[SimpleSceneTransition.StartGameTransition()]
     ↓ Cache player data
     ↓ Load game scene via NetworkSceneManager
[Game Scene Loaded - OnLoadEventCompleted]
     ↓ Find GameSpawnManager
     ↓ For each cached player data:
[GameSpawnManager.SpawnPlayerWithData()]
     ↓ Spawn at spawn point (clientId % spawnPoints.Length)
     ↓ Apply character selection
     ↓ Enable player components
[Players ready in game!]
```

## Setup Instructions

### 1. Scene Setup
**Lobby Scene** (`SampleScene`):
- Place `SimpleSceneTransition` component on a GameObject
- Set up lobby UI with character selection

**Game Scene** (`Playground`):
- Create empty GameObjects for spawn points: `SpawnPoint_0`, `SpawnPoint_1`, etc.
- Position them where players should spawn
- Place `GameSpawnManager` component on a GameObject
- Assign spawn points array and player prefab

### 2. NetworkManager Configuration
```csharp
✅ Enable Scene Management: TRUE
✅ Auto Spawn Player Prefab Client Side: FALSE
✅ Player Prefab: Your networked player prefab
```

### 3. Required Components
**Player Prefab must have**:
- `NetworkObject` component
- `UltraSimpleMeshSwapper` for character loading
- `Camera` (will be enabled only for owner)

## Integration Points

### MainMenuUI Integration
```csharp
// In start game button handler
if (SimpleSceneTransition.Instance != null)
{
    SimpleSceneTransition.Instance.StartGameTransition();
}
```

### Character Selection Integration
The system automatically:
1. **Caches character selections** from `PlayerSessionData` before transition
2. **Passes character data** to `GameSpawnManager`
3. **Applies character meshes** via `UltraSimpleMeshSwapper` after spawn

## Debug Features

### SimpleSceneTransition Debug
- Console logs show transition progress
- Player data caching information
- Scene load completion status

### GameSpawnManager Debug
```csharp
[ContextMenu("Debug - Spawn Test Players")]    // Manually spawn players
[ContextMenu("Debug - Show Spawn Points")]     // List all spawn points
```

## Best Practices

### ✅ Do
- Always check `IsServer` before calling transition methods
- Assign spawn points in your game scene inspector
- Test with multiple clients to verify spawn point cycling
- Use the debug context menus for testing

### ❌ Don't
- Call transition methods from client (server only)
- Modify scene transitions during active transition
- Forget to assign player prefab to GameSpawnManager
- Skip spawn point setup (players will use default positions)

## Troubleshooting

### "GameSpawnManager not found in game scene!"
- Ensure GameSpawnManager is placed in your game scene
- Check that the component is active and enabled

### "Player prefab is not assigned!"
- Assign your networked player prefab in GameSpawnManager inspector
- Ensure prefab has NetworkObject component

### Players spawn at wrong positions
- Check spawn points array is properly assigned
- Verify spawn point GameObjects are positioned correctly
- Use "Debug - Show Spawn Points" context menu to verify setup

### Character selections not applied
- Ensure PlayerSessionData is working in lobby
- Check UltraSimpleMeshSwapper is on player prefab
- Verify CharacterRegistry has character data

## Migration from Old System

**Removed Components**:
- ❌ Old `SceneTransitionManager` (complex event system)
- ❌ Old `SpawnManager` (timing issues and Unity bugs)

**Replaced With**:
- ✅ `SimpleSceneTransition` (clean and simple)
- ✅ `GameSpawnManager` (proper spawn points)

**Benefits**:
- 🎯 **Simpler**: Less code, easier to understand
- 🚀 **Reliable**: Uses Unity Netcode best practices
- 🔧 **Maintainable**: Clear separation of concerns
- 🎮 **Flexible**: Easy to add more spawn points or features