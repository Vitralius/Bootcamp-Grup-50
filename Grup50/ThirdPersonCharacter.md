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
- Advanced movement mechanics (crouch, slide)
- Physics and collision handling
- Network synchronization for position/rotation
- Input handling for movement
- Ground detection
- Camera integration

### Advanced Movement Mechanics

#### Sliding System
**Implementation**: Integrated with crouching system using speed-based decision making

**Key Features**:
- **Speed-based activation**: Must be above `SlideSpeedThreshold` (default: 3.0 m/s)
- **Entry speed boost**: Initial acceleration for satisfying slide feel
- **Steering control**: Left/right movement while maintaining momentum
- **Air-to-slide transition**: Can slide when landing while holding crouch above threshold
- **Jump momentum preservation**: Jumping from slide preserves horizontal momentum
- **Smooth transitions**: Seamless slide-to-crouch or slide-to-stand transitions

**Configuration Parameters**:
- `SlideSpeedThreshold` (float) - Minimum speed to start sliding (default: 3.0 m/s)
- `MaxSlideSpeed` (float) - Maximum slide speed cap (default: 8.0 m/s)
- `SlideDeceleration` (float) - Deceleration rate while sliding (default: 2.0 m/s²)
- `MinSlideDuration` (float) - Minimum slide time before cancellation (default: 0.3s)
- `SlideJumpMomentumMultiplier` (float) - Momentum boost when jumping from slide (default: 1.2x)
- `SlideSteeringControl` (float) - Steering responsiveness while sliding (default: 0.3, range: 0-1)
- `SlideEntryBoost` (float) - Speed multiplier when entering slide (default: 1.3x, range: 1-2x)

**Decision Logic** (HandleCrouchingAndSliding):
1. **Fast + Crouch** → Start sliding with speed boost
2. **Slow + Crouch** → Start crouching normally
3. **Slide ends + Crouch held** → Seamless transition to crouching
4. **Slide ends + Crouch released** → Stand up

**Steering System**:
- **Input processing**: Movement input relative to camera direction
- **Direction blending**: Smooth interpolation between slide momentum and input direction
- **Visual feedback**: Character rotates to face slide direction
- **Momentum preservation**: Maintains slide speed while changing direction

**Transition Mechanics**:
- **Entry boost**: `initialSpeed * SlideEntryBoost` (capped by `MaxSlideSpeed`)
- **Deceleration**: Smooth speed reduction via `SlideDeceleration`
- **Stop condition**: When speed reaches `MoveSpeed * CrouchSpeedMultiplier`
- **State transition**: Auto-crouch if button held, otherwise stand

**Trigger Conditions**:
1. **Ground slide**: `speed >= SlideSpeedThreshold` + crouch key + grounded
2. **Air-to-slide**: Landing while holding crouch + above speed threshold

**Integration Notes**:
- **Unified input handling**: Single function manages both crouch and slide logic
- **Speed-based priority**: Fast movement triggers slide, slow triggers crouch
- **Collision compatibility**: Uses same collision box adjustment as crouching
- **Animation support**: `Sliding` animation parameter with smooth transitions
- **Debug integration**: Shows sliding states and slide-momentum in debug display
- **Network compatible**: Works with existing multiplayer NetworkBehaviour setup

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

## Debug System

### Character State Debugging
The ThirdPersonController includes a comprehensive debug system to visualize character states in real-time. This is essential for developing and testing new abilities like attacking, sliding, etc.

#### **Debug Features**
- **Real-time state display** above each character
- **Speed percentage** and actual velocity
- **Character states**: Grounded/Air, Crouched, Sliding, Sprinting, Jump count, Slide-Momentum
- **Visual positioning** follows character movement
- **Multiplayer compatible** - shows for all players

#### **Configuration (Prefab Inspector)**
- `ShowDebugInfo` (bool) - Toggle debug display on/off
- `DebugTextHeight` (float) - Height offset above character (default: 2.5f)

#### **Example Debug Output**
```
GROUNDED | SPRINTING
Speed: 85% (4.5m/s)
```

```
AIR | JUMP #2
Speed: 0% (0.0m/s)
```

```
GROUNDED | CROUCHED
Speed: 45% (1.1m/s)
```

```
GROUNDED | SLIDING
Speed: 75% (6.0m/s)
```

```
AIR | JUMP #1 | SLIDE-MOMENTUM
Speed: 0% (0.0m/s)
```

#### **Adding New States for Future Abilities**
When implementing new abilities (attacking, combat states), add them to the debug system:

1. **Update `GetCharacterStateString()` method** in ThirdPersonController.cs
2. **Add state checks** for new abilities (e.g., `if (_isAttacking) states.Add("ATTACKING");`)
3. **Include relevant variables** in speed calculations if they affect movement

**Example - Sliding Integration**:
- Added `SLIDING` state when `_isSliding` is true
- Added `SLIDE-MOMENTUM` state when slide jump momentum is active
- Updated speed calculations to show slide speed vs max slide speed

#### **Development Workflow**
- **Always enable debug** during ability development
- **Test state transitions** visually in real-time
- **Verify multiplayer sync** - all players should show correct states
- **Use for balancing** - monitor speed percentages for different abilities

#### **Performance Notes**
- Debug system uses conditional compilation (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`)
- No performance impact in release builds
- Efficient GUI rendering with proper camera/bounds checking

## File Organization

### Core Files
- `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs` - Base movement with debug system
- `Assets/Scripts/AbilityController.cs` - Combat abilities (to be created)

---

*This document will be updated as abilities are defined and implemented. Always use the debug system when adding new character states or abilities.*