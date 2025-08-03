# Unity Netcode Scene Transition Setup Guide

## 🚨 Critical NetworkManager Configuration

Your scene transition issues are likely caused by improper NetworkManager setup. Here's the **exact configuration** needed:

### **1. NetworkManager Component Settings**

In your NetworkManager GameObject:

```csharp
// NetworkManager Inspector Settings:
✅ Enable Scene Management: TRUE (CRITICAL!)
✅ Auto Spawn Player Prefab Client Side: FALSE
✅ Player Prefab: BaseCharPrefab (your main character prefab)

// Scene Management Settings:
✅ Default Main Scene: "SampleScene" (your main menu)
✅ Additional Scene Names: ["Playground"] (your game scene)
```

### **2. Scene Build Settings**

In **File → Build Settings**:
```
Scene Index 0: SampleScene (Main Menu)
Scene Index 1: Playground (Game Scene)
```

### **3. Transport Configuration**

Unity Transport Component:
```csharp
Connection Data: Use defaults
Server Listen Address: 0.0.0.0
Port: 7777
Max Connect Attempts: 60
Connect Timeout MS: 1000
Disconnect Timeout MS: 30000
```

## 🔧 **Fixed Scene Transitions**

Your SceneTransitionManager now uses the **proper Unity Netcode approach**:

### **Before (Problematic)**:
```csharp
// ❌ Wrong: Used ServerRpc for scene loading
[ServerRpc] LoadSceneServerRpc(sceneName);

// ❌ Wrong: Per-client event handling
OnSynchronizeComplete += PerClientHandler;

// ❌ Wrong: Mixed scene loading approaches
SceneManager.LoadScene() // Client-side loading
```

### **After (Fixed)**:
```csharp
// ✅ Correct: Direct server scene loading
NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

// ✅ Correct: Proper scene event handling
OnSceneEvent += HandleAllSceneEvents;

// ✅ Correct: Wait for ALL clients synchronized
SceneEventType.SynchronizeComplete
```

## 🎯 **Key Improvements Made**

### **1. Proper Scene Event Handling**
```csharp
// NEW: Handles all scene events properly
private void OnSceneEvent(SceneEvent sceneEvent)
{
    switch (sceneEvent.SceneEventType)
    {
        case SceneEventType.LoadEventCompleted:
            // Individual client finished loading
            break;
        case SceneEventType.SynchronizeComplete:
            // ALL clients are synchronized - APPLY CHARACTER DATA HERE
            break;
    }
}
```

### **2. Eliminated ServerRpc Redundancy**
```csharp
// OLD: Unnecessary ServerRpc
[ServerRpc] LoadSceneServerRpc(sceneName)

// NEW: Direct server-side loading
LoadSceneNetworked(sceneName) // Server only
```

### **3. Better Synchronization Timing**
```csharp
// NEW: Only apply character data when ALL clients are ready
case SceneEventType.SynchronizeComplete:
    if (IsServer && sceneEvent.SceneName == gameSceneName)
    {
        ApplyCharacterSelectionsToSpawnedPlayers();
    }
```

## 🧪 **Testing Your Fixes**

### **Test Sequence**:
1. **Start Host** in main menu
2. **Connect Client** - both should see lobby
3. **Select Characters** - previews should sync
4. **Start Game** - both should transition to same game scene
5. **Check Console** - should see "ALL CLIENTS synchronized"

### **Expected Log Messages**:
```
SceneTransitionManager: Starting networked scene transition to Playground
SceneTransitionManager: Scene event received - LoadEventCompleted
SceneTransitionManager: ALL CLIENTS synchronized - Playground
SceneTransitionManager: All clients synchronized in game scene, applying character selections...
```

## 📋 **Common Issues & Solutions**

### **Issue: "Players don't travel to same scene"**
**Solution**: Ensure `Enable Scene Management = TRUE` in NetworkManager

### **Issue: "Character data lost after scene transition"**
**Solution**: Character caching now happens BEFORE scene transition

### **Issue: "Scene events not firing"**
**Solution**: Proper `OnSceneEvent` subscription in `OnNetworkSpawn`

### **Issue: "Multiple character applications"**
**Solution**: Only apply on `SynchronizeComplete` (when ALL clients ready)

## 🎮 **Final Setup Checklist**

- [ ] NetworkManager: `Enable Scene Management = TRUE`
- [ ] NetworkManager: `Auto Spawn Player Prefab Client Side = FALSE`
- [ ] Build Settings: Both scenes added in correct order
- [ ] SceneTransitionManager: Updated with new code
- [ ] Test: Both players transition together
- [ ] Test: Character selections persist after transition

Your multiplayer scene transitions should now work perfectly with proper Unity Netcode synchronization!