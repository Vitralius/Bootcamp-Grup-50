# Unity Multiplayer Lobby System Documentation

## Overview

This document outlines the complete lobby system implementation for the Unity multiplayer game. The system provides a seamless flow from main menu to lobby to game using Unity Lobby Service, Relay, and Netcode for GameObjects.

## Architecture Design

### Scene Structure
- **MainMenu Scene** - Handles both main menu AND lobby states (no separate lobby scene needed)
- **Game Scene** - Existing gameplay scene (configurable via Inspector)
- **Simplified Flow**: MainMenu (lobby state) → Game → back to MainMenu

### Key Benefits
- ✅ **Simplified scene management** - Only 2 scenes needed
- ✅ **Configurable scene names** - Set via Inspector
- ✅ **Combined UI** - No separate lobby scene needed
- ✅ **Maintains existing multiplayer** - Enhanced with proper flow
- ✅ **Unity Services integration** - Lobby + Relay + Authentication

## Core Components

### 1. SceneTransitionManager.cs
**Location**: `Assets/Scripts/Network/SceneTransitionManager.cs`

**Purpose**: Handles NetworkSceneManager-based scene transitions with configurable scene names.

**Key Features**:
- Server-authoritative scene switching
- Configurable scene names via Inspector
- Proper NetworkSceneManager integration
- Event system for transition notifications

**Inspector Configuration**:
```csharp
[Header("Scene Names - Configure in Inspector")]
[SerializeField] private string mainMenuSceneName = "SampleScene";
[SerializeField] private string gameSceneName = "Playground";
```

**Key Methods**:
- `TransitionToMainMenu()` - Return to main menu (non-networked for clients leaving)
- `TransitionToGame()` - Server-only transition to game scene
- `SetSceneNames(string mainMenu, string game)` - Runtime scene name configuration

### 2. MainMenuUI.cs
**Location**: `Assets/Scripts/UI/MainMenuUI.cs`

**Purpose**: Combined main menu and lobby UI controller with automatic state switching.

**Key Features**:
- **Main Menu State**: Create/Join lobby buttons, quit game
- **Lobby State**: Player list, ready system, start game, leave lobby
- **Automatic UI switching** when entering/leaving lobbies
- **Join lobby panel** with code input validation
- **Ready system integration** for multiplayer coordination

**UI Structure Required**:
```
Canvas
├── MainMenuPanel (GameObject)
│   ├── GameTitle (TextMeshPro)
│   ├── CreateLobbyButton (Button)
│   ├── JoinLobbyButton (Button)
│   └── QuitGameButton (Button)
│
├── JoinLobbyPanel (GameObject) - Initially inactive
│   ├── LobbyCodeInput (TMP_InputField)
│   ├── ConfirmJoinButton (Button)
│   └── CancelJoinButton (Button)
│
├── LobbyStatePanel (GameObject) - Initially inactive
│   ├── LobbyCodeDisplay (TextMeshPro)
│   ├── CopyCodeButton (Button)
│   ├── PlayersList (TextMeshPro)
│   ├── ReadyButton (Button)
│   ├── StartGameButton (Button) - Only visible to host
│   └── LeaveLobbyButton (Button)
│
├── StatusText (TextMeshPro)
└── LoadingPanel (GameObject) - Initially inactive
```

**State Management**:
- `UpdateUIState()` - Switches between main menu and lobby UI based on lobby status
- `SetMainMenuButtonsVisible(bool visible)` - Shows/hides main menu buttons
- `UpdateLobbyInfo()` - Updates lobby code display
- `UpdatePlayersList()` - Updates player list with count
- `UpdateReadyButton()` - Updates ready button state and color
- `UpdateStartGameButton()` - Host-only button for game start

### 3. LobbyReadySystem.cs
**Location**: `Assets/Scripts/Network/LobbyReadySystem.cs`

**Purpose**: Network-synchronized ready system for lobby coordination.

**Key Features**:
- **Network synchronization** of player ready states
- **Server authority** for ready state validation
- **Event system** for ready state changes
- **All players ready detection** for game start condition

**Network Variables**:
```csharp
private NetworkVariable<int> readyPlayerCount = new NetworkVariable<int>(0);
private Dictionary<string, bool> playerReadyStates = new Dictionary<string, bool>();
```

**Key Methods**:
- `SetPlayerReady(bool ready)` - Set local player ready state
- `IsPlayerReady(string playerId)` - Check if specific player is ready
- `AreAllPlayersReady()` - Check if all players are ready (for game start)
- `GetReadyPlayerCount()` - Get total number of ready players
- `ResetAllPlayers()` - Server-only reset all ready states

**Events**:
```csharp
public event System.Action<string, bool> OnPlayerReadyChanged;
public event System.Action<bool> OnAllPlayersReadyChanged;
```

### 4. Enhanced MultiplayerManager.cs
**Location**: `Assets/Scripts/Network/MultiplayerManager.cs`

**Modifications Made**:
- **Removed automatic scene transitions** after lobby creation/joining
- **Players stay in MainMenu** until host starts the game
- **Maintains existing functionality** for lobby creation, joining, and management

**Key Changes**:
```csharp
// REMOVED: Automatic transition to lobby scene
// if (SceneTransitionManager.Instance != null)
// {
//     SceneTransitionManager.Instance.TransitionToLobby();
// }
```

**Existing Features Preserved**:
- Unity Services integration (Authentication, Lobby, Relay)
- Lobby creation with relay codes
- Lobby joining by code
- Player management and updates
- Heartbeat system for lobby persistence
- Error handling and event system

## Implementation Flow

### Game Start Flow
1. **Player opens game** → MainMenu scene loads
2. **Authentication** → Unity Authentication Service signs in anonymously
3. **Create Lobby**:
   - Host creates lobby via Unity Lobby Service
   - Relay allocation created for secure P2P connection
   - UI switches to lobby state (stays in MainMenu scene)
4. **Join Lobby**:
   - Players enter lobby code
   - Join lobby via Unity Lobby Service
   - Connect to host via Unity Relay
   - UI switches to lobby state
5. **Lobby Coordination**:
   - Players see lobby info and player list
   - Players ready up using ready system
   - Host sees "Start Game" button when all ready
6. **Game Start**:
   - Host clicks "Start Game"
   - SceneTransitionManager transitions all players to Game scene
   - Multiplayer game session begins
7. **Game End**:
   - Game ends → players return to MainMenu scene
   - Lobby is disbanded, ready states reset

### Network Authority Model
- **Server Authority**: All scene transitions, ready state validation, game start
- **Client Prediction**: Local UI updates for responsiveness
- **Network Synchronization**: Player states, lobby info, ready status

## Setup Instructions

### 1. Scene Configuration
**In Unity Inspector** (SceneTransitionManager component):
- Set "Main Menu Scene Name" to your main menu scene name
- Set "Game Scene Name" to your gameplay scene name

**Build Settings**:
- Add MainMenu scene (index 0)
- Add Game scene (index 1)

### 2. GameObject Setup
**Required GameObjects in MainMenu scene**:
```
SceneTransitionManager (GameObject)
├── NetworkObject (Component)
└── SceneTransitionManager (Component)

LobbyManager (GameObject)
├── MultiplayerManager (Component)
└── LobbyReadySystem (Component) - Created automatically

UI (Canvas)
├── MainMenuUI (Component)
└── [UI Elements as described above]
```

### 3. Component Configuration
**MultiplayerManager**:
- Ensure DontDestroyOnLoad is set
- Configure lobby settings (max players, lobby name)

**NetworkManager**:
- Enable Scene Management
- Configure Unity Transport with Relay
- Set up Network Prefabs

**MainMenuUI**:
- Assign all UI element references in Inspector
- Configure button text and colors

### 4. Dependencies
**Required Unity Packages**:
- Unity Netcode for GameObjects
- Unity Services Core
- Unity Authentication
- Unity Lobby
- Unity Relay
- Unity Transport (UTP)

## Event System

### SceneTransitionManager Events
```csharp
public event Action<string> OnSceneTransitionStarted;
public event Action<string> OnSceneTransitionCompleted;
public event Action<string> OnSceneTransitionFailed;
```

### MultiplayerManager Events (Existing)
```csharp
public event Action<string> OnLobbyCodeGenerated;
public event Action<string> OnLobbyJoined;
public event Action OnLobbyLeft;
public event Action<string> OnLobbyError;
public event Action<List<string>> OnLobbyPlayersUpdated;
```

### LobbyReadySystem Events
```csharp
public event System.Action<string, bool> OnPlayerReadyChanged;
public event System.Action<bool> OnAllPlayersReadyChanged;
```

## Error Handling

### Common Issues and Solutions

**Scene Transition Failures**:
- Ensure scene names match exactly in Build Settings
- Check NetworkManager Scene Management is enabled
- Verify server authority for scene transitions

**Lobby Connection Issues**:
- Check Unity Services authentication status
- Verify Relay allocation and join codes
- Monitor network connectivity

**Ready System Desync**:
- Ensure LobbyReadySystem is NetworkBehaviour
- Check server authority for ready state changes
- Verify NetworkVariable synchronization

## Testing Workflow

### Single Player Testing
1. Start in MainMenu scene
2. Create lobby → UI switches to lobby state
3. Test ready button functionality
4. Test "Start Game" button (host only)
5. Verify scene transition to Game scene

### Multiplayer Testing
1. **Host**: Create lobby in MainMenu
2. **Client**: Join lobby with code
3. Both players ready up
4. Host starts game → all players transition
5. Test return to MainMenu after game

### Build Testing
1. Test scene transitions in built application
2. Verify UI scaling across resolutions
3. Test network connectivity with actual Relay service
4. Validate lobby persistence and heartbeat system

## Performance Considerations

### Network Optimization
- Ready system uses NetworkVariable for efficient synchronization
- Lobby updates use polling with reasonable intervals (2s)
- Scene transitions are batched for all clients simultaneously

### Memory Management
- Proper event unsubscription in OnDestroy methods
- DontDestroyOnLoad objects cleaned up appropriately
- UI elements properly disposed when switching states

### Scalability
- System supports up to 4 players per lobby (configurable)
- Relay service handles P2P connections efficiently
- Lobby Service manages multiple concurrent lobbies

## Future Enhancements

### Potential Improvements
- **Spectator Mode**: Allow players to join as observers
- **Lobby Browser**: Public lobby listing and search
- **Player Profiles**: Persistent player data and statistics
- **Chat System**: Text chat in lobby and game
- **Region Selection**: Geographic server selection for optimal latency
- **Reconnection**: Automatic reconnection after network interruption

### Advanced Features
- **Tournament Mode**: Bracket-style multiplayer tournaments
- **Custom Game Modes**: Different rule sets selectable in lobby
- **Replay System**: Game recording and playback
- **Analytics Integration**: Player behavior and performance tracking

---

*This system provides a robust foundation for Unity multiplayer games with modern networking best practices and Unity's recommended services stack.*