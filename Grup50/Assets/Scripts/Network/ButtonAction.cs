using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ButtonAction : MonoBehaviour
{
    private NetworkManager NetworkManagerRef;
    public TextMeshProUGUI ButtonText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        NetworkManagerRef = GetComponentInParent<NetworkManager>();
    }
    
    public void StartHost()
    {
        NetworkManagerRef.StartHost();
        InitMovementText();
    }
    
    public void StartClient()
    {
        NetworkManagerRef.StartClient();
        InitMovementText();
    }

    public void SumbitNewPosition()
    {
        var playerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject(); // Get Player Controller 0
        var player = playerObject.GetComponent<PlayerMovement>();
        player.Move();
    }

    private void InitMovementText()
    {
        if (NetworkManager.Singleton.IsServer) // HasAuthority()
        {
            ButtonText.text = "MOVE";
        }
        else if (NetworkManager.Singleton.IsClient) // !HasAuthority()
        {
            ButtonText.text = "Request Move"; 
        }
    }
    
}
