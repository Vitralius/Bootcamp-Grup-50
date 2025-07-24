# Survivorlike Combat System Implementation Gameplan - Bootcamp Edition

## Project Overview
Transform our Unity multiplayer game into a **simplified Vampire Survivors-inspired survivorlike** experience for bootcamp development, integrating automatic combat mechanics with our existing character selection and networking systems.

## ⚠️ Bootcamp Constraints & Simplified Approach
**Key Decision**: Simple host-only multiplayer architecture for manageable bootcamp development.

### Architecture Research Findings (2024)
- **Full-scale survivorlike games** require complex distributed networking (400+ enemies)
- **Unity Netcode bottlenecks** occur with 90+ NetworkTransforms 
- **Vampire Survivors uses** distributed client ownership and 1Hz enemy sync
- **Bootcamp Solution**: Simplified host-authority with limited entity counts

### Simplified Multiplayer Architecture
- **2-4 players maximum** (not 8+)
- **Host handles everything**: Enemy spawning, damage calculation, wave management  
- **Clients send input only**: Movement, upgrade choices
- **Limited entities**: 30-50 enemies max, 20-30 projectiles max
- **Shorter sessions**: 10-15 minutes instead of 30
- **Basic progression**: 5-10 levels with simple upgrades

## Genre Analysis: Survivorlike/Bullet Heaven

### Core Mechanics (Simplified for Bootcamp)
- **Automatic Combat**: Characters auto-attack enemies without manual input
- **Movement-Focused Gameplay**: Player controls movement/positioning only
- **Time-Based Survival**: **10-15 minute sessions** with escalating difficulty
- **Wave-Based Enemy Spawning**: **2-3 enemy types** that increase in intensity
- **Basic Weapon System**: **3-4 weapon types** with simple upgrades
- **Experience-Driven Progression**: Level up by collecting XP from defeated enemies

### Key Appeal
- **Easy to Learn**: Simple movement controls, automatic shooting
- **Hard to Master**: Strategic positioning, upgrade choices, timing
- **Addictive Loop**: "Just one more run" progression system
- **Cooperative Potential**: Perfect for multiplayer survival scenarios

## Current System Strengths

### ✅ Already Implemented
1. **Character Selection System**
   - `CharacterData.cs` with mesh swapping
   - `NewBaseCharacter.prefab` with network components
   - Character-specific stats and appearance

2. **Multiplayer Networking**
   - Unity Netcode for GameObjects
   - Server-authoritative architecture
   - Client prediction and network sync

3. **Third-Person Movement**
   - `ThirdPersonController.cs` with 8-way movement
   - Camera management for multiple players
   - Input system integration

4. **Health System**
   - `HealthComponent.cs` already attached to characters
   - `DamagingObject.cs` for damage dealing

## Implementation Roadmap - Bootcamp Edition

### Phase 1: Core Combat Foundation (Week 1-2)
**Goal**: Basic automatic weapon system with simple enemy AI
**Bootcamp Focus**: Keep it simple, reuse existing systems

#### 1.1 Simplified Weapon System
- [ ] Create basic `WeaponData.cs` (3-4 weapon types only)
- [ ] Implement `AutoWeapon.cs` with simple auto-fire timer
- [ ] Reuse existing `DamagingObject.cs` for all projectiles
- [ ] **Skip complex projectile behaviors** - start with straight projectiles only

#### 1.2 Basic Enemy System (Host Authority)
- [ ] Enhance existing `Enemy.cs` with chase-player AI
- [ ] Create simple `EnemySpawner.cs` (host spawns for all clients)
- [ ] **Limit**: 2 enemy types max (basic chaser, maybe one ranged)
- [ ] **Entity limit**: 30-50 enemies total, not per player

#### 1.3 Integration with Existing Systems
- [ ] Add basic weapon stats to existing `CharacterData.cs`
- [ ] **Simple networking**: Host spawns projectiles as NetworkObjects
- [ ] Integrate with existing `HealthComponent.cs` and `DamagingObject.cs`

### Phase 2: Core Gameplay Loop (Week 2-3)
**Goal**: Experience collection, leveling, and weapon upgrades
**Bootcamp Focus**: Simple progression, minimal UI

#### 2.1 Basic Experience System
- [ ] Create simple `ExperienceComponent.cs` for XP collection
- [ ] Implement XP orb spawning from defeated enemies (host authority)
- [ ] Add basic level-up system (5-10 levels max)
- [ ] **Simple UI**: Basic level-up notification, skip complex selection UI

#### 2.2 Simple Wave Management
- [ ] Create basic `WaveManager.cs` (host only)
- [ ] **Simple escalation**: Spawn rate increases every 2 minutes
- [ ] **Skip complex events** - just basic enemy count increase
- [ ] **Entity cap**: Stop spawning at 50 enemies total

#### 2.3 Basic Weapon Progression
- [ ] **Simple weapon leveling**: 3-5 levels per weapon max
- [ ] **Skip passive items** for bootcamp - weapons only
- [ ] **Basic stat scaling**: Just damage and fire rate increases

### Phase 3: Polish & Basic Features (Week 3-4)
**Goal**: Polish core systems and add basic variety
**Bootcamp Focus**: Make what exists work well

#### 3.1 Basic Polish
- [ ] Simple visual effects for weapon impacts
- [ ] Basic audio feedback for combat events
- [ ] **Skip complex weapon evolution** - just basic upgrades
- [ ] Simple UI for health, XP, and weapon displays

#### 3.2 Character Integration
- [ ] Give each character 1 starting weapon (different per character)
- [ ] **Skip complex character abilities** - just different starting weapons
- [ ] **Skip meta progression** - focus on core gameplay

#### 3.3 Game Loop Completion
- [ ] Simple "game over" and restart mechanics
- [ ] Basic win condition (survive 10-15 minutes)
- [ ] **Optional**: One simple boss enemy at the end

### Phase 4: Final Polish (Week 4-5) - Optional
**Goal**: Bug fixes and balance only
**Bootcamp Focus**: Functional game over feature-complete game

#### 4.1 Balance and Testing
- [ ] Multiplayer balance testing with 2-4 players
- [ ] Performance testing with entity limits
- [ ] Bug fixes and stability improvements

#### 4.2 Presentation Polish (If Time Allows)
- [ ] Simple main menu integration
- [ ] Basic game statistics display
- [ ] **Skip**: Complex progression, unlocks, achievements

## Technical Architecture - Bootcamp Simplified

### Simplified Weapon System Architecture

#### Core Architecture Flow (Simplified):
```
AutoWeapon -> Simple Projectile -> DamagingObject -> HealthComponent
```

#### 1. **AutoWeapon.cs** - Simple Weapon Controller
- Basic timer-based automatic firing
- Spawns simple projectiles based on `WeaponData.cs`
- **Skip complex evolution** - just level-up stat increases

#### 2. **Simple Projectile System**
**Bootcamp Approach**: Start with one projectile type only
- **StraightProjectile** - Just moves forward and deals damage
- **Skip**: Orbiting, bouncing, area damage, homing for bootcamp

#### 3. **Reuse DamagingObject.cs** (Existing System)
Already supports everything we need:
- ✅ Layer-based targeting (Player/Enemy layers)
- ✅ Once vs continuous damage
- ✅ Network synchronization
- ✅ No additional coding needed

#### 4. **WeaponData.cs** - Simple Configuration
```csharp
[CreateAssetMenu]
public class WeaponData : ScriptableObject
{
    [Header("Basic Stats")]
    public float damage;
    public float fireRate;
    public float projectileSpeed;
    public GameObject projectilePrefab; // Uses existing DamagingObject
    
    // Skip complex behaviors for bootcamp
}
```

#### Bootcamp Weapon Types (3-4 maximum):
1. **Basic Gun** - Shoots straight projectiles
2. **Shotgun** - Shoots 3 projectiles in spread
3. **Rapid Fire** - Fast, low damage projectiles
4. **Optional: Area Weapon** - Uses existing area damage from DamagingObject

#### Benefits of This Design:
- ✅ **Reuses existing `DamagingObject.cs`** - no reinventing the wheel
- ✅ **Modular behaviors** - easy to add new projectile types
- ✅ **Layer-based targeting** - already works with Player/Enemy layers
- ✅ **Network compatible** - builds on networking foundation
- ✅ **Flexible damage patterns** - once, continuous, area, timed

### Simplified File Structure - Bootcamp Edition
```
Assets/Scripts/
├── Combat/ (New folder)
│   ├── Weapons/
│   │   ├── WeaponData.cs (simple version)
│   │   ├── AutoWeapon.cs (basic auto-fire)
│   │   └── SimpleProjectile.cs (straight movement only)
│   ├── Experience/
│   │   ├── ExperienceComponent.cs (basic XP collection)
│   │   └── XPOrb.cs (simple pickup)
│   └── Enemies/
│       ├── EnemyAI.cs (enhance existing Enemy.cs)
│       ├── EnemySpawner.cs (simple spawning)
│       └── WaveManager.cs (basic wave escalation)
├── UI/ (extend existing)
│   └── SimpleCombatUI.cs (health, XP display)
└── Player/ (extend existing)
    ├── CharacterData.cs (add weapon stats)
    └── PlayerCombat.cs (weapon management)

// Skip for bootcamp:
// - Complex projectile behaviors
// - PassiveItems system
// - WeaponEvolution system
// - Complex UI systems
// - Meta progression
```

### Simplified Network Architecture - Bootcamp
- **Host Authority**: Host handles everything (enemy spawning, damage, XP)
- **Client Input Only**: Clients send movement and upgrade choices
- **Simple Sync**: Basic NetworkObject spawning for enemies/projectiles
- **Entity Limits**: Cap at 50 enemies + 30 projectiles to prevent bottlenecks

### Bootcamp Performance Approach
- **Basic Object Pooling**: Simple pool for projectiles (not complex system)
- **Entity Limits**: Hard caps prevent performance issues
- **Reuse Existing**: Leverage DamagingObject.cs and HealthComponent.cs
- **Simple Collision**: Use existing collider setup, don't optimize yet

## Multiplayer Design Decisions

### Cooperative Survival Approach
- **Shared Arena**: All players in same survival area
- **Individual Progression**: Each player levels independently
- **Shared Enemies**: Enemy waves target all players
- **Revive System**: Players can help fallen teammates
- **Scaling Difficulty**: Enemy count scales with player count

### Character Selection Integration
- **Unique Starting Loadouts**: Each character begins with different weapons
- **Stat Variations**: Health, speed, damage multipliers per character
- **Visual Identity**: Maintain character mesh/appearance system
- **Balanced Diversity**: Ensure all characters remain viable

## Bootcamp Success Metrics
- **Functionality**: 2-4 players can play together for 10-15 minutes
- **Core Loop**: Auto-combat, enemy waves, XP collection, simple upgrades work
- **Character System**: Different characters have different starting weapons
- **Performance**: Stable gameplay with entity limits (50 enemies, 30 projectiles)
- **Bootcamp Goal**: Working survivorlike demo, not feature-complete game

## Risk Assessment & Mitigation

### Technical Risks
- **Performance**: Hundreds of networked entities
  - *Mitigation*: Object pooling, server culling, efficient collision
- **Network Sync**: Complex state synchronization
  - *Mitigation*: Server authority, client prediction, batched updates
- **Balance**: Multiplayer weapon/character balance
  - *Mitigation*: Iterative testing, data-driven tuning

### Design Risks
- **Complexity Creep**: Feature overload
  - *Mitigation*: Phased development, core loop first
- **Character Identity Loss**: Generic weapon system
  - *Mitigation*: Character-specific bonuses and starting loadouts
- **Multiplayer Chaos**: Too hectic with multiple players
  - *Mitigation*: Camera management, clear visual hierarchy

## Bootcamp Development Timeline

### Week 1-2: Foundation
- **Day 1-3**: Basic `AutoWeapon.cs` with timer-based firing
- **Day 4-5**: Simple projectile using existing `DamagingObject.cs`
- **Day 6-7**: Enhance existing `Enemy.cs` with chase AI

### Week 2-3: Core Loop
- **Day 8-10**: Basic `ExperienceComponent.cs` and XP orbs
- **Day 11-12**: Simple `WaveManager.cs` with spawn rate escalation
- **Day 13-14**: Basic weapon leveling (damage/fire rate increases)

### Week 3-4: Integration & Basic Polish
- **Day 15-17**: Different starting weapons per character
- **Day 18-19**: Simple UI for health, XP, weapon level
- **Day 20-21**: Basic game over/restart mechanics

### Week 4-5: Testing & Bug Fixes
- **Final Days**: Multiplayer testing, balance, bug fixes
- **Presentation**: Working 2-4 player survivorlike demo

## Next Steps - Simplified
1. **Approve simplified bootcamp approach** ✓
2. **Start with basic weapon system** - `WeaponData.cs` and `AutoWeapon.cs`
3. **Reuse existing systems** - `DamagingObject.cs`, `HealthComponent.cs`
4. **Keep scope small** - 3-4 weapons, 2-3 enemy types maximum

## Bootcamp Philosophy
**Goal**: Working survivorlike demo that showcases core mechanics
**Not Goal**: Feature-complete game with complex systems
**Success**: 2-4 players having fun shooting enemies together for 10-15 minutes

This simplified gameplan focuses on delivering a functional multiplayer survivorlike experience within bootcamp constraints, leveraging your existing robust networking and character systems.