# Third Person Character System Documentation

## Overview
This document outlines the character system architecture for our Unity multiplayer game. The system is built on top of Unity's Starter Assets Third Person Controller with a clean separation between movement and abilities.

## Architecture

### Core Design
- **ThirdPersonController.cs** - Movement, walking, jumping, physics, network synchronization
- **AbilityController.cs** - Fantasy abilities (combat, magic, special moves), network synchronization
- **Character Types** - Different combinations of abilities, not separate classes

### Design Principles
- Clean separation between movement and abilities
- Network-first design for multiplayer compatibility
- Composition over inheritance
- Component-based architecture following Unity patterns

## Movement System

### ThirdPersonController.cs
**Location**: `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs`

**Responsibilities**:
- Basic movement (walk, run, jump)
- Physics and collision handling
- Network synchronization for position/rotation
- Input handling for movement
- Ground detection
- Camera integration

## Combat System

### AbilityController.cs
**Responsibilities**:
- Fantasy combat abilities
- Ability cooldowns and resource management
- Network synchronization for ability execution
- Ability input handling

## Network Integration

### Multiplayer Considerations
- **Server Authority**: All ability validation on server
- **Client Prediction**: Local ability execution with server reconciliation
- **Network Variables**: Synchronized character states
- **RPC System**: Ability execution and effect communication

### Network Architecture
- ThirdPersonController handles movement networking
- AbilityController handles ability networking
- Both systems work together seamlessly

## Character Types

Character types are defined by their combination of abilities rather than separate classes:
- Different ability loadouts create different character types
- Same base ThirdPersonController + AbilityController for all characters
- Flexibility to mix and match abilities

## File Organization

### Core Files
- `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs` - Base movement
- `Assets/Scripts/AbilityController.cs` - Combat abilities (to be created)

---

*This document will be updated as abilities are defined and implemented.*