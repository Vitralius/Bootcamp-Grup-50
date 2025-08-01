# Clean Scene Transition System

## Overview

This is a simple, clean scene transition system for Unity Netcode multiplayer games. It handles transitioning from lobby to game scene with player data persistence and proper spawn point management.

## Current Status (Latest Update)

### ✅ **COMPLETED TODAY**
- **Fixed Player Data Caching Bug**: System now properly caches data for ALL connected clients (was only caching 1 player instead of 2)
- **Fixed "Object is not spawned" Error**: Added proper NetworkObject state validation before spawning
- **Enhanced Character Loading**: Added retry mechanism for character application when NetworkObject isn't immediately ready
- **Improved Error Handling**: Added comprehensive debugging and validation throughout the system
- **LINQ Support**: Fixed compilation error by adding missing `using System.Linq;` directive

### 🔄 **IN PROGRESS**
- **Character Loading Issue**: Players spawn but selected characters don't load (investigating data flow from lobby to game)
- **Debug Logging**: Enhanced logging added to track character data flow and identify where selections are lost

### ⏳ **PENDING**
- Complete character loading fix based on debug findings
- Setup spawn points in game scene (0-1-2-3-4-5)
- Configure GameSpawnManager with player prefab and spawn points

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
- ✅ **NEW**: Enhanced debugging with detailed session and client logging
- ✅ **NEW**: Safe session data handling with proper null checks

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
- ✅ **NEW**: NetworkObject state validation before spawning
- ✅ **NEW**: Character application retry mechanism
- ✅ **NEW**: Enhanced ownership verification and fixing
- ✅ **NEW**: Comprehensive character data validation

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

## Recent Bug Fixes

### 🐛 **Fixed: Player Data Caching Bug**
**Problem**: Only 1 player data was cached instead of 2
**Root Cause**: Unsafe access to `matchingSession` properties when session was null/default
**Solution**: 
```csharp
// Safe session data handling with defaults first
var playerData = new PlayerData
{
    clientId = clientId,
    characterId = 0, // Default
    playerName = $"Player_{clientId}" // Default
};

// Only use session data if valid session found
if (!matchingSession.Equals(default) && matchingSession.selectedCharacterId > 0)
{
    playerData.characterId = matchingSession.selectedCharacterId;
    // ... apply session data
}
```

### 🐛 **Fixed: "Object is not spawned" Error**
**Problem**: `SpawnAsPlayerObject()` called on already spawned or invalid NetworkObjects
**Root Cause**: Missing state validation before network operations
**Solution**:
```csharp
// Check NetworkObject state before spawning
if (networkObject.IsSpawned)
{
    Debug.LogError("NetworkObject already spawned! Cannot spawn again.");
    Destroy(playerObject);
    return;
}

// Spawn and verify success
networkObject.SpawnAsPlayerObject(clientId, false);
if (!networkObject.IsSpawned)
{
    Debug.LogError("Failed to spawn NetworkObject!");
    Destroy(playerObject);
    return;
}
```

### 🐛 **Enhanced: Character Loading System**
**Problem**: Characters weren't being applied to spawned players
**Investigation**: Added comprehensive debugging and retry mechanism
**Solution**:
```csharp
// Retry mechanism for character application
private System.Collections.IEnumerator RetryCharacterApplication(GameObject playerObject, ulong clientId, int characterId)
{
    int maxRetries = 10; // 1 second max wait
    int retries = 0;
    
    while (retries < maxRetries)
    {
        yield return new WaitForSeconds(0.1f);
        retries++;
        
        var networkObject = playerObject.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            var characterLoader = playerObject.GetComponent<UltraSimpleMeshSwapper>();
            if (characterLoader != null)
            {
                characterLoader.SetNetworkCharacterId(characterId);
                Debug.Log($"✅ Successfully applied character {characterId} after {retries} retries");
                yield break;
            }
        }
    }
    
    Debug.LogError($"❌ Failed to apply character after {maxRetries} retries");
}
```

## Flow Diagram

```
[Lobby Scene]
     ↓ Host clicks "Start Game"
[SimpleSceneTransition.StartGameTransition()]
     ↓ Cache player data from PlayerSessionData
     ↓ Enhanced validation and debugging
     ↓ Load game scene via NetworkSceneManager
[Game Scene Loaded - OnLoadEventCompleted]
     ↓ Find GameSpawnManager
     ↓ For each cached player data:
[GameSpawnManager.SpawnPlayerWithData()]
     ↓ Validate NetworkObject state
     ↓ Spawn at spawn point (clientId % spawnPoints.Length)
     ↓ Verify ownership and fix if needed
     ↓ Apply character selection with retry mechanism
     ↓ Enable player components
[Players ready in game!]
```

## Debug Features

### SimpleSceneTransition Debug
- **Session Tracking**: Logs all available player sessions with character selections
- **Client Tracking**: Shows all connected clients during caching
- **Data Validation**: Verifies character data exists in CharacterRegistry
- **Caching Details**: Shows exactly what data is cached for each client

### GameSpawnManager Debug
- **Character Validation**: Checks if character data exists before spawning
- **NetworkObject State**: Validates spawning state and ownership
- **Retry Logging**: Tracks character application attempts and success/failure
- **Component Verification**: Ensures all required components are present

```csharp
[ContextMenu("Debug - Spawn Test Players")]    // Manually spawn players
[ContextMenu("Debug - Show Spawn Points")]     // List all spawn points
[ContextMenu("Debug - Fix All Player Ownership")] // Fix ownership issues
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
4. **Retries application** if NetworkObject isn't ready initially

## Best Practices

### ✅ Do
- Always check `IsServer` before calling transition methods
- Assign spawn points in your game scene inspector
- Test with multiple clients to verify spawn point cycling
- Use the debug context menus for testing
- Check console logs for detailed debugging information

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
- **NEW**: Check console for character validation logs
- **NEW**: Look for "Character data NOT found" or "invalid character ID" errors
- **NEW**: Verify PlayerSessionData is working in lobby
- **NEW**: Check if retry mechanism is triggering
- Ensure UltraSimpleMeshSwapper is on player prefab
- Verify CharacterRegistry has character data

### "Object is not spawned" errors
- **FIXED**: Enhanced NetworkObject state validation
- **FIXED**: Added proper error handling and cleanup

### Only 1 player cached instead of 2
- **FIXED**: Safe session data handling
- **FIXED**: Proper null checking for session matching

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
- 🐛 **Robust**: Enhanced error handling and debugging
- 📊 **Observable**: Comprehensive logging for troubleshooting

## Known Issues (Under Investigation)

### Character Loading Issue
**Status**: 🔄 In Progress
**Problem**: Players spawn successfully but selected characters don't load (default character appears)
**Investigation**: Enhanced debugging added to track data flow from lobby to game
**Next Steps**: Analyze debug logs to identify where character selection data is lost

**Debug Questions to Check**:
1. Are character sessions being found during caching?
2. Are character IDs > 0 in cached data?
3. Does CharacterRegistry contain the character data?
4. Is `SetNetworkCharacterId` being called successfully?
5. Is the NetworkVariable system working properly?

The enhanced debugging system should reveal the exact point where character data is lost or not properly applied.