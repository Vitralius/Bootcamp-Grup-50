# Unity Third-Person Multiplayer Game - CLAUDE.md

## Project Overview
This is a Unity 3D multiplayer game project built using:
- Unity 2023.3+ with Universal Render Pipeline (URP)
- Unity Netcode for GameObjects (v2.4.3) for multiplayer networking
- Unity Starter Assets Third Person Controller as the base character system
- Cinemachine for camera management
- Input System for controls

## Key Project Structure

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

### Networking Architecture
- **ThirdPersonController**: Modified to work with Netcode, includes ownership checks for input processing
- **PlayerMovement**: Simple test implementation with random position updates via ServerRpc
- **Network Variables**: Using NetworkVariable<Vector3> for position synchronization
- **Authority Model**: Server-authoritative with client prediction

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