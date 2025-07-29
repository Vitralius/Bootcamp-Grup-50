# Unity Third-Person Multiplayer Game - CLAUDE.md

## Project Overview
This is a Unity 3D multiplayer game project built using:
- Unity 2023.3+ with Universal Render Pipeline (URP)
- Unity Netcode for GameObjects (v2.4.3) for multiplayer networking
- Unity Starter Assets Third Person Controller as the base character system
- Cinemachine for camera management
- Input System for controls

## Key Project Structure

### Player Prefab Information
**IMPORTANT**: The main player prefabs used in this project are:
- **Game Character**: `Assets/BaseCharPrefab.prefab` - Contains all necessary components: CharacterController, PlayerInput, NetworkObject, ThirdPersonController, StarterAssetsInputs, UltraSimpleMeshSwapper
- **Lobby Preview**: `Assets/BaseCharPreview.prefab` - Contains preview-only components with UltraSimpleMeshSwapper in preview mode
- Always reference BaseCharPrefab for gameplay and BaseCharPreview for character selection preview
- Other prefabs like PlayerPrefabEmpty or PlayerCapsule are NOT the primary prefabs used in gameplay

### Character Visual System
**OPTIMIZED**: The CharacterLoader system uses **Same-Skeleton Mesh Replacement** for maximum efficiency and animation compatibility:
- **Single Prefab Spawning**: SpawnManager always spawns the same `BaseCharPrefab` prefab
- **Skeletal Mesh Replacement**: CharacterData contains only skeletal mesh references (NO prefabs)
- **Same Skeleton Structure**: All characters share identical bone structure = perfect animation compatibility
- **Problem Solved**: No more nested character spawning or duplicate components

#### Same-Skeleton System Architecture:
1. **SpawnManager**: Spawns `BaseCharPrefab` prefab (always the same)
2. **CharacterData**: Contains only `skeletalMesh`, `materials`, `animatorController`
3. **UltraSimpleMeshSwapper**: Replaces only the `SkinnedMeshRenderer.sharedMesh`
4. **Animation Compatibility**: Same skeleton = all animations work perfectly

#### Creating Character Meshes:
1. **Import your character model** (FBX with mesh that uses the SAME skeleton structure)
2. **Extract the mesh** from the imported model
3. **Assign to CharacterData.skeletalMesh**
4. **CRITICAL**: Mesh MUST have identical bone count and structure as base character
5. **Validation**: System automatically validates bone count compatibility

#### Benefits of Same-Skeleton System:
- ✅ **No Nested Spawning**: Only one character prefab ever spawned
- ✅ **Perfect Animation Compatibility**: Same bones = all animations work
- ✅ **High Performance**: No prefab instantiation, only mesh swapping
- ✅ **Simple Data Structure**: Just mesh references, no complex prefabs
- ✅ **Network Efficient**: Minimal data changes for character switching

#### Troubleshooting Same-Skeleton System:
- **Error: "Bone count mismatch"** → Ensure your mesh uses the exact same skeleton structure
- **Animations not working**: Verify the mesh was rigged to the same skeleton
- **Character not visible**: Check the skeletal mesh is properly assigned to CharacterData
- **Bounds issues**: System automatically fixes bounds center and size

### Animation System (8-Way Movement)
**CRITICAL**: DirectionX and DirectionY are calculated relative to **character mesh facing direction**, not camera direction:
- **DirectionX**: -1 = moving left relative to character mesh, +1 = moving right relative to character mesh
- **DirectionY**: -1 = moving backward relative to character mesh, +1 = moving forward relative to character mesh

#### 8-Way Movement Values:
- **Forward**: DirectionX = 0, DirectionY = 1
- **Backward**: DirectionX = 0, DirectionY = -1  
- **Right**: DirectionX = 1, DirectionY = 0
- **Left**: DirectionX = -1, DirectionY = 0
- **Forward-Right**: DirectionX = 0.7, DirectionY = 0.7
- **Forward-Left**: DirectionX = -0.7, DirectionY = 0.7
- **Backward-Right**: DirectionX = 0.7, DirectionY = -0.7
- **Backward-Left**: DirectionX = -0.7, DirectionY = -0.7

#### Benefits for Aiming Mechanics:
- ✅ **Character can face different direction than movement** (essential for aiming)
- ✅ **Perfect for blend trees** with forward/backward/left/right/diagonal animations
- ✅ **Movement input remains camera-relative** (good player control)
- ✅ **Animation values are mesh-relative** (good for 8-way blending)

### Core Scripts
- `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs` - Modified from Starter Assets to inherit from `NetworkBehaviour` with ownership checks
- `Assets/Scripts/Player/PlayerMovement.cs` - Custom network movement implementation with NetworkVariable<Vector3> for position synchronization
- `Assets/Scripts/Network/ButtonAction.cs` - UI controller for host/client connection and movement testing
- `Assets/Scripts/Network/ClientNetworkAnimator.cs` - Network animation synchronization
- `Assets/Scripts/Network/ClientNetworkTransform.cs` - Network transform synchronization

### Key Features Implemented
- **Network Ownership**: Only the owner processes input and movement (`if (!IsOwner) return;`)
- **Camera Management**: Non-owners have camera disabled to prevent conflicts
- **Position Synchronization**: Using NetworkVariable for position replication
- **Server Authority**: Movement validation and authority checks
- **RPC System**: ServerRpc calls for client-to-server communication
- **Clean Scene Transitions**: SimpleSceneTransition system for lobby-to-game transitions
- **Spawn Point Management**: GameSpawnManager with ID-based spawn points (0-1-2-3-4-5)
- **Character Persistence**: Character selections persist across scene transitions

### Networking Architecture
- **ThirdPersonController**: Modified to work with Netcode, includes ownership checks for input processing
- **SimpleSceneTransition**: Clean scene management with player data caching
- **GameSpawnManager**: Proper player spawning with ownership and character loading
- **Network Variables**: Using NetworkVariable<Vector3> for position synchronization
- **Authority Model**: Server-authoritative with client prediction

## Latest Updates (Current Session)

### Scene Transition System Overhaul ✅ COMPLETED
**Date**: Current Session
**Status**: ✅ Fully Implemented and Debugged

#### What Was Done:
1. **Completely Replaced Old System**:
   - ❌ Removed complex `SceneTransitionManager` with event system
   - ❌ Removed buggy `SpawnManager` with Unity Netcode issues
   - ✅ Created clean `SimpleSceneTransition` system
   - ✅ Created robust `GameSpawnManager` with spawn points

2. **Fixed Critical Bugs**:
   - 🐛 **Player Data Caching Bug**: Only 1 player cached instead of 2
     - **Root Cause**: Unsafe access to null session data
     - **Fix**: Safe session handling with proper defaults
   - 🐛 **"Object is not spawned" Error**: NetworkObject spawning failures
     - **Root Cause**: Missing state validation before spawn operations
     - **Fix**: Added comprehensive NetworkObject state checking
   - 🐛 **Compilation Error**: Missing LINQ directive
     - **Fix**: Added `using System.Linq;` for FirstOrDefault method

3. **Enhanced Character Loading**:
   - ✅ Added retry mechanism for character application
   - ✅ Enhanced debugging and validation
   - ✅ Proper NetworkObject timing checks
   - 🔄 **In Progress**: Investigating character data flow issue

#### Key Files Modified:
- `Assets/Scripts/Network/SceneTransitionManager.cs` → `SimpleSceneTransition`
- `Assets/Scripts/Network/GameSpawnManager.cs` → Complete rewrite
- `Assets/Scripts/UI/MainMenuUI.cs` → Updated to use SimpleSceneTransition
- `Assets/Scripts/Player/UltraSimpleMeshSwapper.cs` → Updated references

#### Current Status:
- ✅ **Players spawn correctly** at designated spawn points
- ✅ **Ownership is properly assigned** to each client
- ✅ **No more "Object is not spawned" errors**
- ✅ **All connected clients are cached** (fixed 1 vs 2 player issue)
- 🔄 **Character loading under investigation** (enhanced debugging added)

### Debug Features Added:
- **Comprehensive Logging**: Track data flow from lobby to game
- **Session Validation**: Verify character selections are cached
- **Character Data Verification**: Check CharacterRegistry integration
- **Retry Mechanisms**: Handle timing issues with NetworkObject spawning
- **Context Menu Debugging**: Manual testing tools for developers

## Development Commands

### Build Commands
```bash
# No specific build commands found - check Unity's build settings or package.json for custom scripts
```

### Testing
```bash
# Unity Play Mode testing for multiplayer scenarios
# Use Unity Multiplayer Play Mode package for local testing
```

## Development Guidelines

### Character System Guidelines
- **Always refer to ThirdPersonCharacter.md** for any character-related modifications
- **Always refer to CharacterSelectionSystem.md** for character selection and loading system implementations
- **Always check web research** before implementing character system changes
- **Network-first design** - consider multiplayer implications for all character features

### Update vs FixedUpdate Guidelines
- **Use Update() for**: Input handling, CharacterController.Move(), animations, UI updates
- **Use FixedUpdate() for**: Rigidbody physics operations (AddForce, velocity changes)
- **CharacterController Exception**: Always use Update() for CharacterController - FixedUpdate() causes visual stuttering
- **Input Best Practice**: Handle input in Update() to avoid missing button presses
- **Physics Timing**: Use Time.deltaTime for frame-rate independent calculations in Update()

### Networking Best Practices
1. Always check `IsOwner` before processing input
2. Use ServerRpc for client-to-server communication
3. Use ClientRpc for server-to-client communication
4. NetworkVariables automatically sync to all clients
5. Only server should have authority over game state

### Code Conventions
- Network scripts inherit from `NetworkBehaviour`
- Use `[ServerRpc]` and `[ClientRpc]` attributes for remote procedure calls
- Prefix network methods with descriptive names (e.g., `SubmitPositionRequestServerRPC`)
- Check `NetworkManager.Singleton.IsServer` for server authority

### File Organization
- `/Assets/Scripts/Player/` - Player-specific scripts
- `/Assets/Scripts/Network/` - Networking-specific scripts
- `/Assets/StarterAssets/` - Unity Starter Assets (modified for networking)
- `/Assets/Prefabs/` - Network prefabs and game objects

## Multiplayer Setup
1. The project uses Unity Netcode for GameObjects
2. NetworkManager handles connection management
3. Player prefabs need to be registered in DefaultNetworkPrefabs
4. Current implementation supports host/client architecture

## Key Dependencies
- Unity Netcode for GameObjects (2.4.3)
- Unity Input System (1.14.0)
- Cinemachine (3.1.4)
- Unity Starter Assets Third Person Controller

## Current State
The project has basic multiplayer functionality with:
- Third-person character controller adapted for networking
- Basic position synchronization
- Host/client connection system
- Camera management for multiple players

## Collaboration Guidelines

### Working Together Approach
- **You are the decision maker** - I will suggest and explain, you approve
- **"Why?" First** - I will always explain the reasoning behind suggestions before implementing
- **Confirmation Required** - I will not implement major changes without your explicit approval
- **Documentation-Driven** - All code suggestions will reference Unity documentation when available
- **Iterative Development** - We work together step-by-step, not in large chunks
- **I'll always doubt your takes, because you are never right unless I make a proper search on the topic and agree with it.** You are never right unless You Say "I am sure of it".
- 
### Communication Protocol
1. **Suggestion Phase**: I propose a solution with detailed "why" explanation
2. **Reference Phase**: I cite relevant Unity documentation from `/unitydocs/`
3. **Approval Phase**: You review and approve/modify the approach
4. **Implementation Phase**: I implement only after your confirmation
5. **Validation Phase**: We test and refine together

### Documentation Usage
- I have access to Unity documentation in `/unitydocs/`
- I will reference specific documentation files when making suggestions
- If needed documentation is missing, I will inform you
- If documentation seems outdated/irrelevant, I will flag it

### What I Will NOT Do
- Implement major features without approval
- Make assumptions about your preferences
- Write large amounts of code without step-by-step confirmation
- Ignore your feedback or rush ahead

## Unity Documentation Reference (/unitydocs/)

### Core Components & Systems
- **CharacterControllers.html** - Character movement and collision
- **Components.html** - Component system basics
- **class-GameObject.html** - GameObject fundamentals
- **class-Transform.html** - Transform component reference
- **class-MonoBehaviour.html** - MonoBehaviour scripting
- **class-Rigidbody.html** - Physics body component

### Physics & Collision
- **collision-section.html** - Collision detection overview
- **collider-interactions.html** - Collider interaction patterns
- **collider-types-introduction.html** - Collider types and usage
- **physics-articulations.html** - Advanced physics systems

### Animation
- **AnimationOverview.html** - Animation system overview
- **class-Animator.html** - Animator component reference
- **class-AnimatorController.html** - Animator controller setup
- **class-AnimationClip.html** - Animation clip properties

### Audio
- **Audio.html** - Audio system overview
- **class-AudioSource.html** - AudioSource component
- **AudioSource-overview.html** - Audio source configuration
- **class-AudioListener.html** - Audio listener setup

### Cameras & Rendering
- **Cameras.html** - Camera system overview
- **CamerasOverview.html** - Camera setup and configuration
- **class-Camera.html** - Camera component reference
- **com.unity.cinemachine.html** - Cinemachine camera system

### Input System
- **InputLegacy.html** - Legacy input system (reference only)
- **class-InputManager.html** - Input manager settings

### UI Systems
- **ui-systems/** - Complete UI Toolkit documentation directory
- **UIElements.html** - UI Elements system overview
- **UIToolkits.html** - UI Toolkit fundamentals

### Performance & Memory
- **performance-memory.html** - Memory management
- **performance-garbage-collector.html** - GC optimization
- **performance-native-memory.html** - Native memory handling

### Multiplayer
- **multiplayer.html** - Basic multiplayer concepts
- **multiplayer-overview.html** - Multiplayer architecture overview
- **multiplayer-center.html** - Multiplayer development center

### Debugging & Development
- **managed-code-debugging.html** - Code debugging techniques
- **null-reference-exception.html** - Common exception handling
- **execution-order.html** - Script execution order

## Next Development Steps
Consider implementing:
- Proper player spawning system
- Animation synchronization improvements  
- Game state management
- Player interaction systems
- Level/scene management for multiplayer