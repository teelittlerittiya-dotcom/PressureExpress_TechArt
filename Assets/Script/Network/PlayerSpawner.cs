using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;

    [SerializeField] private Transform spawnPoint;

    public override void OnNetworkSpawn()
    {
        // Only the Server manages spawning
        if (IsServer)
        {
            // 1. Trigger when the Scene finishes loading (For the Host + Early Joiners)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            
            // 2. Trigger when a NEW player connects late (For Late Joiners)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    // Event 1: Scene Load Finished
    private void OnSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        // Spawn for everyone who is already here
        foreach (var clientId in clientsCompleted)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    // Event 2: New Client Connected
    private void OnClientConnected(ulong clientId)
    {
        // Spawn for the new person who just arrived
        SpawnPlayerForClient(clientId);
    }

    // Small depth separation per client so multiple players on the same spawn point don't Z-fight / overlap.
    private const float PlayerDepthStep = 0.005f;

    private void SpawnPlayerForClient(ulong clientId)
    {
        // Safety Check: Don't spawn if they already have a player object
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null) return; 
        }

        Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;
        // Normal base Z position for player spawn is -3.3f, with a slight distinct Z offset per client to avoid sprite overlapping / Z-fighting
        float playerZ = -3.3f - (clientId % 8) * PlayerDepthStep;
        Vector3 spawnPos = new Vector3(basePos.x, basePos.y, playerZ);

        // Create the object at the calculated spawn position directly
        GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        
        // Spawn it on the network and assign ownership to the client
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, destroyWithScene: true);

        // Force the correct spawn position across all clients via RPC
        CharacterController2D controller = playerInstance.GetComponent<CharacterController2D>();
        if (controller != null)
        {
            controller.SetSpawnPosition(spawnPos);
        }
        else
        {
            playerInstance.transform.position = spawnPos;
        }
    }
}