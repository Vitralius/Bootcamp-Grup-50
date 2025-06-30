using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour // networkbehaviour is similar to replicated in Unreal
{
    
    public NetworkVariable<Vector3> PlayerPosition = new NetworkVariable<Vector3>();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner) // IsLocallyController()
        {
            Move(); // If Is locally controlled then move.
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            transform.position = PlayerPosition.Value; // You are already server just do what you do
        }
        else
        {
            SubmitPositionRequestServerRPC(); // Request a new position from server
        }

    }
    // [ClienRpc] // Client Call
    [ServerRpc] // Server Call  
    void SubmitPositionRequestServerRPC(ServerRpcParams rpcParams = default)
    {
        PlayerPosition.Value = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));;
    }
    
    public void Move()
    {
        if (NetworkManager.Singleton.IsServer) // Has Authority check.
        {
            Vector3 move = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
            transform.position = move;
            PlayerPosition.Value = move;
            Debug.Log($"Detected NetworkVariable Change: Previous: {PlayerPosition.Value.x} | Current: {PlayerPosition.Value.y}");
        }
        else
        {
            SubmitPositionRequestServerRPC(); // Request a new position from server
        }
    }
    
}
