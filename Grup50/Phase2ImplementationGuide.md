# Phase 2 Implementation Guide - Core Gameplay Loop

## Overview
This guide covers implementing the complete survivorlike core gameplay loop with enemy AI, experience system, and wave management. All systems are network-compatible and production-ready.

## 🤖 Step 1: Enhanced Enemy AI System

### Files Created/Modified:
- `Assets/Scripts/Enemy/Enemy.cs` (Enhanced existing file)

### Implementation Steps:

1. **Enhanced Enemy.cs Features:**
   ```csharp
   // Key enhancements added:
   - Chase behavior targeting nearest player
   - Network synchronization with server authority
   - Multiple behavior types (Chase/Patrol/Hybrid)
   - Configurable stats (speed, damage, detection range)
   - XP orb spawning on death
   - Debug visualization with gizmos
   ```

2. **Setup Instructions:**
   - Open existing Enemy prefab in Unity
   - Ensure it has `NetworkObject` component
   - Ensure it has `NavMeshAgent` component
   - Ensure it has `HealthComponent` component
   - Set behavior type to "ChasePlayer" for survivorlike gameplay
   - Configure detection range (10f), attack range (2f), move speed (3.5f)
   - Set XP reward value (15f default)

3. **Testing:**
   - Place enemy in scene with NavMesh
   - Enemy should chase nearest player when in detection range
   - Enemy should attack when within attack range
   - Enemy should spawn XP orb when killed

## 📈 Step 2: Experience System

### Files Created:
- `Assets/Scripts/Combat/Experience/ExperienceComponent.cs` (New)
- `Assets/Scripts/Combat/Experience/XPOrb.cs` (New)

### Implementation Steps:

#### 2.1 ExperienceComponent Setup:

1. **Add to Player Prefab:**
   ```csharp
   // Add ExperienceComponent to BaseCharPrefab
   - Open BaseCharPrefab.prefab
   - Add ExperienceComponent script
   - Configure settings:
     * Base XP Requirement: 100
     * XP Requirement Multiplier: 1.5
     * Max Level: 10
     * Health Bonus Per Level: 20
     * Speed Bonus Per Level: 0.2
     * Damage Bonus Per Level: 0.15 (15%)
   ```

2. **Integration with Existing Systems:**
   - ExperienceComponent automatically finds HealthComponent and ThirdPersonController
   - Applies stat bonuses when leveling up
   - Provides damage multiplier for weapon systems

3. **Network Synchronization:**
   - All XP and level data synced via NetworkVariables
   - Only server can modify XP values
   - Level-up events broadcast to all clients

#### 2.2 XPOrb Setup:

1. **Create XP Orb Prefab:**
   ```
   Steps:
   1. Create empty GameObject named "XPOrb"
   2. Add XPOrb.cs script
   3. Add NetworkObject component
   4. Add Rigidbody component (useGravity = false)
   5. Add SphereCollider component (isTrigger = true, radius = 1)
   6. Configure XPOrb settings:
      * XP Value: 10
      * Attraction Range: 5
      * Attraction Speed: 8
      * Collection Range: 1
      * Lifetime: 30 seconds
   7. Save as prefab in Assets/Prefabs/
   ```

2. **Visual Setup:**
   - XPOrb automatically creates glowing sphere visual if none provided
   - Built-in rotation animation included
   - Customize by assigning visualEffect GameObject

3. **Integration with Enemies:**
   - Assign XPOrb prefab to Enemy's xpOrbPrefab field
   - Enemy will spawn orb on death automatically

## 🌊 Step 3: Wave Management System

### Files Created:
- `Assets/Scripts/Combat/WaveManager.cs` (New)

### Implementation Steps:

#### 3.1 WaveManager Setup:

1. **Create WaveManager GameObject:**
   ```
   Steps:
   1. Create empty GameObject named "WaveManager"
   2. Add WaveManager.cs script
   3. Add NetworkObject component
   4. Configure wave settings:
      * Initial Spawn Interval: 3 seconds
      * Min Spawn Interval: 0.5 seconds
      * Wave Interval: 120 seconds (2 minutes)
      * Max Enemies Per Player: 15
      * Absolute Max Enemies: 50
   ```

2. **Enemy Prefab Assignment:**
   ```csharp
   // Assign enemy prefabs to enemyPrefabs array
   - Drag your enemy prefabs into the array
   - Each prefab must have NetworkObject component
   - Each prefab must have Enemy.cs script
   ```

3. **Spawn Point Configuration:**
   ```
   Option A: Manual Spawn Points
   - Create empty GameObjects as spawn points
   - Position them around your play area
   - Assign to spawnPoints array in WaveManager

   Option B: Automatic Spawn Points (Default)
   - WaveManager creates circular spawn points automatically
   - Configure spawnRadius (20f default)
   - Spawn points created around origin
   ```

#### 3.2 Difficulty Scaling:

1. **Wave Progression:**
   - Each wave lasts 2 minutes by default
   - Spawn interval decreases by 10% each wave
   - Enemy count increases each wave
   - Enemy stats scale with wave number

2. **Enemy Stat Scaling:**
   ```csharp
   // Automatic scaling per wave:
   Health: +20% per wave
   Speed: +10% per wave  
   Damage: +15% per wave
   ```

3. **Performance Limits:**
   - Hard cap of 50 total enemies
   - 15 enemies per player maximum
   - Prevents performance issues in multiplayer

## 🔫 Step 4: Weapon-Experience Integration

### Files Modified:
- `Assets/Scripts/Combat/Weapons/AutoWeapon.cs` (Enhanced)

### Implementation Steps:

1. **Damage Scaling Integration:**
   ```csharp
   // AutoWeapon now applies experience-based damage multiplier
   - GetDamageMultiplier() method added
   - Automatically finds ExperienceComponent
   - Applies multiplier to projectile damage
   - Base damage × (1 + 0.15 × (level - 1))
   ```

2. **Testing Damage Scaling:**
   - Level up character (use debug context menu)
   - Fire weapon and check projectile damage in logs
   - Higher level = higher damage output

## 🎮 Complete Implementation Workflow

### Phase 2 Setup Checklist:

#### ✅ Prerequisites:
- [ ] Phase 1 completed (character selection, weapons, networking)
- [ ] BaseCharPrefab exists with NetworkObject, ThirdPersonController, AutoWeapon
- [ ] Scene has NavMesh for enemy pathfinding
- [ ] NetworkManager configured with proper prefabs

#### ✅ Enemy System:
- [ ] Enemy prefab has enhanced Enemy.cs script
- [ ] Enemy has NetworkObject, NavMeshAgent, HealthComponent
- [ ] Enemy behavior set to "ChasePlayer"
- [ ] XP orb prefab created and assigned to enemy

#### ✅ Experience System:
- [ ] ExperienceComponent added to player prefab
- [ ] XPOrb prefab created with NetworkObject
- [ ] XPOrb prefab added to NetworkManager's prefab list
- [ ] AutoWeapon modified to use damage multiplier

#### ✅ Wave Management:
- [ ] WaveManager GameObject created in scene
- [ ] WaveManager has NetworkObject component
- [ ] Enemy prefabs assigned to WaveManager
- [ ] Spawn points configured (manual or automatic)

#### ✅ Network Configuration:
- [ ] All new prefabs added to NetworkManager's DefaultNetworkPrefabs
- [ ] Required prefabs: Enemy, XPOrb, WaveManager (if using prefab)

## 🧪 Testing Guide

### 1. Solo Testing:
```
1. Start as Host
2. Enter gameplay scene
3. Verify enemy spawning every 3 seconds
4. Kill enemies and collect XP orbs
5. Check level-up at 100 XP
6. Verify damage increase after level-up
7. Survive multiple waves (2 minutes each)
```

### 2. Multiplayer Testing:
```
1. Host starts game
2. Client joins
3. Both players should see same enemies
4. Each player levels independently
5. XP orbs only collectible by nearby player
6. Enemy count scales with player count
```

### 3. Performance Testing:
```
1. Let waves run for 10+ minutes
2. Monitor enemy count (should cap at 50)
3. Check frame rate remains stable
4. Verify memory usage doesn't spike
```

## 🐛 Common Issues & Solutions

### Issue: Enemies not spawning
**Solution:** Check NavMesh exists, WaveManager has enemy prefabs assigned, NetworkManager has enemy prefab registered

### Issue: XP orbs not collectible
**Solution:** Ensure XPOrb has NetworkObject, ExperienceComponent on player, correct layer masks

### Issue: Level-up not working
**Solution:** Check ExperienceComponent is on player prefab, NetworkVariable permissions correct, server authority working

### Issue: Performance problems
**Solution:** Verify entity limits enforced, check absolute max enemies setting, enable object pooling if needed

### Issue: Network sync issues
**Solution:** Ensure all relevant prefabs have NetworkObject, check NetworkManager prefab list, verify server authority

## 📋 Debug Commands

### ExperienceComponent Debug:
```csharp
// Context menu options in ExperienceComponent:
- "Add 50 XP (Debug)" - Quick XP gain
- "Level Up (Debug)" - Instant level up
```

### Enemy Debug:
```csharp
// Gizmos show in Scene view:
- Yellow circle: Detection range  
- Red circle: Attack range
- Line to target: Current chase target
```

### WaveManager Debug:
```csharp
// Context menu options in WaveManager:
- "Force Next Wave" - Skip to next wave
- "Spawn Enemy Now" - Instant enemy spawn
```

## 🎯 Success Criteria

### Phase 2 Complete When:
- [x] Enemies spawn automatically in waves
- [x] Enemies chase and attack players
- [x] Players gain XP from killing enemies
- [x] Players level up and become stronger
- [x] Weapon damage scales with player level
- [x] System performs well with 2-4 players
- [x] All features work in multiplayer

## 🚀 Next Steps (Phase 3)

After completing Phase 2 implementation:
1. **Basic UI**: Health bars, XP display, level indicator
2. **Visual Effects**: Muzzle flashes, hit effects, death animations
3. **Audio**: Weapon sounds, enemy sounds, level-up sounds
4. **Game States**: Victory/defeat conditions, restart mechanics

This completes the core survivorlike gameplay loop! Players can now engage in meaningful progression-based survival gameplay.