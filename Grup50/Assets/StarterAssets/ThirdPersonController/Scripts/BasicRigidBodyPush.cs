using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BasicRigidBodyPush : NetworkBehaviour
{
	[Header("Layer Settings")]
	[Tooltip("Layers that can be pushed (ground/walls should NOT be included)")]
	public LayerMask pushLayers;
	public bool canPush;
	[Range(0.5f, 5f)] public float strength = 1.1f;
	
	[Header("Rate Limiting")]
	[Tooltip("Minimum time between push attempts on the same object")]
	public float pushCooldown = 0.2f;
	[Tooltip("Maximum force that can be applied")]
	public float maxPushForce = 10f;
	[Tooltip("Reduce client push strength")]
	[Range(0.1f, 1f)] public float clientForceMultiplier = 0.7f;
	[Tooltip("Radius for finding nearby objects (smaller = better performance)")]
	public float searchRadius = 0.3f;
	
	[Header("Performance")]
	[Tooltip("Maximum colliders to check in ServerRpc (0 = no limit)")]
	public int maxCollidersToCheck = 5;
	[Tooltip("Clean old push times every N seconds")]
	public float cleanupInterval = 10f;
	
	[Header("Debug Visualization")]
	public bool showDebugArrows = true;
	public float debugArrowDuration = 3f;
	
	// Rate limiting tracking
	private Dictionary<int, float> lastPushTimes = new Dictionary<int, float>();
	
	// Per-frame collision tracking (prevents multiple hits per frame)
	private HashSet<int> pushedThisFrame = new HashSet<int>();
	private int lastFrameCount = -1;
	
	// Performance optimization - cached array for Physics.OverlapSphere
	private Collider[] nearbyCollidersCache = new Collider[10];
	private float lastCleanupTime = 0f;
	
	// Static list to store debug info for OnGUI
	private static List<DebugForceInfo> debugForces = new List<DebugForceInfo>();
	
	private struct DebugForceInfo
	{
		public Vector3 position;
		public Vector3 force;
		public float timestamp;
		public string forceText;
		public Color color;
		public bool isServerPush;
	}

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		// Only owner can initiate pushes
		if (!IsOwner || !canPush) 
			return;
		
		// Early layer filtering - only process collisions with pushable layers
		// This prevents ground/wall collision processing while maintaining physics
		var hitLayerMask = 1 << hit.collider.gameObject.layer;
		if ((hitLayerMask & pushLayers.value) == 0) 
		{
			return; // Not a pushable layer, skip entirely
		}
		
		int objectId = hit.collider.GetInstanceID();
		
		// Clear frame tracking if we're in a new frame
		if (Time.frameCount != lastFrameCount)
		{
			pushedThisFrame.Clear();
			lastFrameCount = Time.frameCount;
		}
		
		// Prevent multiple pushes per frame on same object (fixes CharacterController sub-movements)
		if (pushedThisFrame.Contains(objectId))
		{
			return; // Already pushed this object this frame
		}
		
		// Rate limiting: Check if we recently pushed this object
		if (lastPushTimes.ContainsKey(objectId) && Time.time - lastPushTimes[objectId] < pushCooldown)
		{
			return; // Too soon, skip this push
		}
		
		// Mark as pushed this frame and update last push time
		pushedThisFrame.Add(objectId);
		lastPushTimes[objectId] = Time.time;
		
		// Periodic cleanup of old push times to prevent memory growth
		if (Time.time - lastCleanupTime > cleanupInterval)
		{
			CleanupOldPushTimes();
			lastCleanupTime = Time.time;
		}
		
		if (IsServer)
		{
			// Host: Apply physics directly
			Debug.Log($"Host processing push on {hit.collider.name}, Layer: {hit.collider.gameObject.layer}");
			PushRigidBodies(hit);
		}
		else
		{
			// Client: Request server to apply physics
			Debug.Log($"Client requesting push on {hit.collider.name}, Layer: {hit.collider.gameObject.layer}");
			RequestPushServerRpc(hit.collider.transform.position, hit.moveDirection, hit.normal);
		}
	}

	private void PushRigidBodies(ControllerColliderHit hit)
	{
		// https://docs.unity3d.com/ScriptReference/CharacterController.OnControllerColliderHit.html

		// make sure we hit a non kinematic rigidbody
		Rigidbody body = hit.collider.attachedRigidbody;
		if (body == null || body.isKinematic) return;

		// make sure we only push desired layer(s)
		var bodyLayerMask = 1 << body.gameObject.layer;
		//Debug.Log($"Push check - Object layer: {body.gameObject.layer}, LayerMask: {pushLayers.value}, Match: {(bodyLayerMask & pushLayers.value) != 0}");
		if ((bodyLayerMask & pushLayers.value) == 0) return;

		// We dont want to push objects below us
		if (hit.moveDirection.y < -0.3f) return;

		// Calculate push direction from move direction, horizontal motion only
		Vector3 pushDir = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);

		// Apply the push and take strength into account (server-authoritative)
		Vector3 forceVector = pushDir * strength;
		
		// Clamp force to maximum for consistency
		if (forceVector.magnitude > maxPushForce)
		{
			forceVector = forceVector.normalized * maxPushForce;
		}
		
		body.AddForce(forceVector, ForceMode.Impulse);
		
		// Add debug visualization (red for host pushes)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
		if (showDebugArrows)
		{
			AddDebugForce(hit.collider.transform.position, forceVector, Color.red, false);
		}
#endif
	}
	
	[ServerRpc]
	private void RequestPushServerRpc(Vector3 hitPosition, Vector3 moveDirection, Vector3 hitNormal)
	{
		// Server validates and applies push from client request
		Debug.Log($"Server processing client push request at {hitPosition}");
		
		// Find the closest rigidbody near the hit position (optimized search with cached array)
		int colliderCount = Physics.OverlapSphereNonAlloc(hitPosition, searchRadius, nearbyCollidersCache, pushLayers);
		
		// Limit the number of colliders we check for performance
		int maxCheck = maxCollidersToCheck > 0 ? Mathf.Min(colliderCount, maxCollidersToCheck) : colliderCount;
		
		for (int i = 0; i < maxCheck; i++)
		{
			var collider = nearbyCollidersCache[i];
			Rigidbody body = collider.attachedRigidbody;
			if (body == null || body.isKinematic) continue;
			
			// Validate layer
			var bodyLayerMask = 1 << body.gameObject.layer;
			if ((bodyLayerMask & pushLayers.value) == 0) continue;
			
			// Don't push objects below us
			if (moveDirection.y < -0.3f) continue;
			
			// Calculate and apply push with client force reduction
			Vector3 pushDir = new Vector3(moveDirection.x, 0.0f, moveDirection.z);
			Vector3 forceVector = pushDir * strength * clientForceMultiplier; // Reduce client force
			
			// Clamp force to maximum
			if (forceVector.magnitude > maxPushForce)
			{
				forceVector = forceVector.normalized * maxPushForce;
			}
			
			body.AddForce(forceVector, ForceMode.Impulse);
			
			// Add debug visualization (green for client-initiated pushes)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (showDebugArrows)
			{
				AddDebugForce(collider.transform.position, forceVector, Color.green, true);
			}
#endif
			
			Debug.Log($"Server applied client-requested push: {forceVector} to {collider.name}");
			break; // Only push the first valid object found
		}
	}
	
	private void CleanupOldPushTimes()
	{
		// Remove push times older than twice the cooldown period to free memory
		float cleanupThreshold = Time.time - (pushCooldown * 2f);
		var keysToRemove = new List<int>();
		
		foreach (var kvp in lastPushTimes)
		{
			if (kvp.Value < cleanupThreshold)
			{
				keysToRemove.Add(kvp.Key);
			}
		}
		
		foreach (int key in keysToRemove)
		{
			lastPushTimes.Remove(key);
		}
		
		if (keysToRemove.Count > 0)
		{
			Debug.Log($"Cleaned up {keysToRemove.Count} old push time entries");
		}
	}
	
#if UNITY_EDITOR || DEVELOPMENT_BUILD
	private void AddDebugForce(Vector3 position, Vector3 force, Color color, bool isClientInitiated)
	{
		DebugForceInfo debugInfo = new DebugForceInfo
		{
			position = position,
			force = force,
			timestamp = Time.time,
			forceText = $"Server: {force.magnitude:F1}N",
			color = color,
			isServerPush = true
		};
		
		debugForces.Add(debugInfo);
		
		// Clean up old debug forces
		debugForces.RemoveAll(info => Time.time - info.timestamp > debugArrowDuration);
	}
#endif
	
#if UNITY_EDITOR || DEVELOPMENT_BUILD
	private void Update()
	{
		if (showDebugArrows)
		{
			// Clean up expired debug forces
			for (int i = debugForces.Count - 1; i >= 0; i--)
			{
				if (Time.time - debugForces[i].timestamp > debugArrowDuration)
				{
					debugForces.RemoveAt(i);
				}
			}
			
			// Draw debug arrows using Debug.DrawLine (visible in Scene view and Game view with Gizmos enabled)
			foreach (var debugInfo in debugForces)
			{
				if (Time.time - debugInfo.timestamp <= debugArrowDuration)
				{
					DrawDebugArrow(debugInfo.position, debugInfo.force.normalized, debugInfo.force.magnitude * 0.5f, debugInfo.color);
				}
			}
		}
	}
#endif
	
#if UNITY_EDITOR || DEVELOPMENT_BUILD
	private void DrawDebugArrow(Vector3 position, Vector3 direction, float length, Color color)
	{
		Vector3 endPos = position + direction * length;
		
		// Main arrow line
		Debug.DrawLine(position, endPos, color, Time.deltaTime);
		
		// Calculate arrow head
		Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * 0.3f;
		Vector3 left = -right;
		Vector3 back = -direction * 0.3f;
		
		// Arrow head lines
		Debug.DrawLine(endPos, endPos + back + right, color, Time.deltaTime);
		Debug.DrawLine(endPos, endPos + back + left, color, Time.deltaTime);
		
		// Optional: Draw a small cross at the force origin
		Vector3 crossSize = Vector3.one * 0.1f;
		Debug.DrawLine(position - crossSize, position + crossSize, color, Time.deltaTime);
		Debug.DrawLine(position - new Vector3(crossSize.x, -crossSize.y, crossSize.z), 
		              position + new Vector3(crossSize.x, -crossSize.y, crossSize.z), color, Time.deltaTime);
	}
#endif
	
#if UNITY_EDITOR || DEVELOPMENT_BUILD
	private void OnGUI()
	{
		if (!showDebugArrows || debugForces.Count == 0) return;
		
		Camera cam = Camera.main;
		if (cam == null) cam = Camera.current;
		if (cam == null) return;
		
		// Early exit if not in scene view or game view
		if (Event.current.type != EventType.Repaint) return;
		
		// Display force text for each debug force
		foreach (var debugInfo in debugForces)
		{
			if (Time.time - debugInfo.timestamp > debugArrowDuration) continue;
			
			Vector3 screenPos = cam.WorldToScreenPoint(debugInfo.position + Vector3.up * 2f);
			
			// Only draw if in front of camera and within screen bounds
			if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
			{
				// Convert to GUI coordinates (flip Y)
				screenPos.y = Screen.height - screenPos.y;
				
				// Create GUI style with color matching the arrow
				GUIStyle style = GUI.skin.label;
				style.normal.textColor = debugInfo.color;
				style.fontSize = 12;
				style.fontStyle = FontStyle.Bold;
				
				// Draw text with shadow for better visibility
				Rect textRect = new Rect(screenPos.x - 30, screenPos.y - 10, 100, 20);
				
				// Shadow
				GUI.color = new Color(0, 0, 0, 0.8f);
				GUI.Label(new Rect(textRect.x + 1, textRect.y + 1, textRect.width, textRect.height), debugInfo.forceText, style);
				
				// Main text
				GUI.color = debugInfo.color;
				GUI.Label(textRect, debugInfo.forceText, style);
			}
		}
		
		// Reset GUI color
		GUI.color = Color.white;
	}
#endif
}