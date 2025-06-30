using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ButtonAction : MonoBehaviour
{
    private NetworkManager NetworkManagerRef;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        NetworkManagerRef = GetComponentInParent<NetworkManager>();
    }
    
    public void StartHost()
    {
        NetworkManagerRef.StartHost();
    }
    
    public void StartClient()
    {
        NetworkManagerRef.StartClient();
    }

    public void SumbitNewPosition()
    {
        var playerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject(); // Get Player Controller 0
        var player = playerObject.GetComponent<PlayerMovement>();
        player.Move();
    }
}
