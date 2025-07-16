# Character Selection System Implementation Plan

## Overview
Implementation of character selection system for Unity multiplayer game using existing ThirdPersonController.cs as the base for all characters. Characters will share the same input system and movement code, but differ in stats, abilities, and visual appearance.

## System Architecture

### Core Principle
- **Single Prefab Approach (Option B)**: Use one player prefab with ThirdPersonController.cs
- **Runtime Modification**: Apply character-specific data at spawn using CharacterLoader component
- **Network Synchronization**: Character selections synced across all clients
- **Shared Input System**: All characters use the same ThirdPersonController input system

## Implementation Steps

### **Step 1: Create CharacterData ScriptableObject System** ✅ PENDING
**Purpose**: Store character information that will be applied to ThirdPersonController

**Components to Create**:
- `CharacterData.cs` - ScriptableObject containing:
  - Character stats (MoveSpeed, SprintSpeed, JumpHeight, Gravity, etc.)
  - Visual data (character mesh, materials, animations)
  - Character metadata (name, description, icon)
  - Ability flags (EnableDoubleJump, MaxJumps, etc.)

**File Location**: `Assets/Scripts/Player/CharacterData.cs`

### **Step 2: Create CharacterLoader Component** ✅ PENDING
**Purpose**: Apply CharacterData to spawned ThirdPersonController at runtime

**Components to Create**:
- `CharacterLoader.cs` - MonoBehaviour component that:
  - Receives CharacterData reference
  - Modifies ThirdPersonController public stats
  - Swaps character mesh/visuals
  - Applies character-specific settings

**File Location**: `Assets/Scripts/Player/CharacterLoader.cs`

**Integration**: Attach to same GameObject as ThirdPersonController

### **Step 3: Character Selection Manager** ✅ PENDING
**Purpose**: Handle network synchronization of character selections

**Components to Create**:
- `CharacterSelectionManager.cs` - NetworkBehaviour that:
  - Uses `NetworkVariable<int>` for each player's selected character ID
  - Provides character selection validation (prevent duplicates)
  - Syncs selections across all clients
  - Integrates with existing lobby system

**File Location**: `Assets/Scripts/Network/CharacterSelectionManager.cs`

**Integration**: Works with existing `LobbyReadySystem.cs` and `MultiplayerLobbyUI.cs`

### **Step 4: Single Prefab with Runtime Loading** ✅ PENDING
**Purpose**: Modify existing player prefab to support character loading

**Implementation**:
- Keep existing player prefab structure
- Add CharacterLoader component to player prefab
- Ensure NetworkObject and ThirdPersonController remain unchanged
- Character data applied after spawn based on selection

**Benefits of Single Prefab Approach**:
- Simplified network prefab registration
- Consistent networking behavior
- Easier maintenance
- Runtime flexibility

### **Step 5: Update Lobby UI System** ✅ PENDING
**Purpose**: Add character selection interface to lobby

**Components to Modify**:
- `MultiplayerLobbyUI.cs` - Add character selection UI
- Create character selection screen with:
  - Character preview images
  - Character stats display
  - Real-time selection sync
  - Ready state integration

**File Location**: Modify existing `Assets/Scripts/Network/MultiplayerLobbyUI.cs`

### **Step 6: Refactor Spawn System** ✅ PENDING
**Purpose**: Apply character selection data during player spawn

**Components to Modify**:
- `SpawnManager.cs` or create new spawn logic
- Disable default player prefab auto-spawn
- Use custom `SpawnAsPlayerObject()` with character data
- Load selected character data via CharacterLoader

**Integration**: Works with existing `PlayerSpawnFix.cs` and `SpawnManager.cs`

### **Step 7: Ready State System Integration** ✅ PENDING
**Purpose**: Ensure players are ready before game starts

**Components to Modify**:
- `LobbyReadySystem.cs` - Integrate character selection with ready states
- Players must select character AND be ready
- Host can only start when all players have selected characters and are ready

### **Step 8: Scene Transition Integration** ✅ PENDING
**Purpose**: Maintain character selection data across scene transitions

**Components to Modify**:
- `SceneTransitionManager.cs` - Ensure character data persists
- Character selections maintained from lobby to game
- Proper cleanup and data persistence
- Handle disconnection scenarios

**Integration**: Works with existing `SceneTransitionManager.cs`

## Technical Considerations

### Network Architecture
- **Server Authority**: Character selections validated by server
- **Data Persistence**: Character data stored in NetworkVariables
- **Scene Persistence**: Selections maintained across scene transitions

### Character Data Flow
1. Player selects character in lobby UI
2. CharacterSelectionManager syncs choice to server
3. Server validates and broadcasts to all clients
4. On game start, SpawnManager spawns player prefab
5. CharacterLoader applies selected character data to ThirdPersonController
6. Player spawns with correct stats, visuals, and abilities

### Integration Points
- **Existing ThirdPersonController**: All public stats will be modified by CharacterLoader
- **Existing Network Scripts**: CharacterSelectionManager integrates with current lobby system
- **Existing UI**: Character selection extends current lobby UI
- **Existing Spawn System**: Enhanced to support character data application

## File Structure
```
Assets/
├── Scripts/
│   ├── Player/
│   │   ├── CharacterData.cs (NEW)
│   │   └── CharacterLoader.cs (NEW)
│   └── Network/
│       ├── CharacterSelectionManager.cs (NEW)
│       ├── MultiplayerLobbyUI.cs (MODIFY)
│       ├── LobbyReadySystem.cs (MODIFY)
│       ├── SpawnManager.cs (MODIFY)
│       └── SceneTransitionManager.cs (MODIFY)
```

## Dependencies
- Unity Netcode for GameObjects (existing)
- Existing ThirdPersonController.cs (base for all characters)
- Existing lobby and networking infrastructure

## Status Tracking
- ✅ PENDING: Not started
- 🔄 IN PROGRESS: Currently being implemented  
- ✅ COMPLETED: Implementation finished
- ❌ BLOCKED: Waiting on dependencies or approval

---

*This document will be updated as implementation progresses. Each step will be marked with current status and any implementation notes.*