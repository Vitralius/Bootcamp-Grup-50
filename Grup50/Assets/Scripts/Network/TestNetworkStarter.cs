using UnityEngine;
using Unity.Netcode;

public class TestNetworkStarter : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private float delayBeforeStart = 0.1f;
    
    private void Awake()
    {
        if (startOnAwake)
        {
            Invoke(nameof(StartTestHost), delayBeforeStart);
        }
    }
    
    [ContextMenu("Start Test Host")]
    public void StartTestHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[TestNetworkStarter] NetworkManager.Singleton is null!");
            return;
        }
        
        if (NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[TestNetworkStarter] Network is already running!");
            return;
        }
        
        Debug.Log("[TestNetworkStarter] Starting local host for testing...");
        NetworkManager.Singleton.StartHost();
    }
    
    [ContextMenu("Stop Network")]
    public void StopNetwork()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[TestNetworkStarter] Stopping network...");
            NetworkManager.Singleton.Shutdown();
        }
    }
}