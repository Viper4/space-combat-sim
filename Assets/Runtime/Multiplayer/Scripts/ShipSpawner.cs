using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet;
using UnityEngine;
using SpaceStuff;
using FishNet.Managing.Scened;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public class ShipSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private NetworkObject shipPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<int, Ship> playerShips = new();

    private int nextSpawnIndex;

    private void Awake()
    {
        Debug.Log("[ShipSpawner] Initializing ShipSpawner.");

        if (InstanceFinder.IsOffline)
        {
            SpawnOfflinePlayerShip();
        }
        else if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }
    }

    private void OnDestroy()
    {
        if (InstanceFinder.ServerManager != null)
        {
            // InstanceFinder.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        playerShips.Clear();
    }

    private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
    {
        if (args.Scene.name != "MainScene")
            return;

        if (args.Added)
        {
            // StartCoroutine(WaitToSpawnShip(args.Connection, args.Scene));
            SpawnPlayerShip(args.Connection);
        }
        else
        {
            RemovePlayerShip(args.Connection);
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        switch (args.ConnectionState)
        {
            case FishNet.Transporting.RemoteConnectionState.Started:
                // SpawnPlayerShip(conn);
                break;
            case FishNet.Transporting.RemoteConnectionState.Stopped:
                RemovePlayerShip(conn);
                break;
        }
    }

    private void SpawnOfflinePlayerShip()
    {
        if (!InstanceFinder.IsOffline)
            return;

        if (playerShips.ContainsKey(0))
            return;

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        GetSpawnPoint(out spawnPosition, out spawnRotation);

        NetworkObject shipObject = Instantiate(shipPrefab, spawnPosition, spawnRotation);
        shipObject.name = "Player";

        if (!shipObject.TryGetComponent<Ship>(out var ship))
        {
            Debug.LogError("[ShipSpawner] Ship prefab is missing Ship component.");
            return;
        }

        playerShips.Add(0, ship);

        Debug.Log($"[ShipSpawner] Spawned offline ship.");
    }

    private void SpawnPlayerShip(NetworkConnection conn)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (playerShips.ContainsKey(conn.ClientId))
            return;

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        GetSpawnPoint(out spawnPosition, out spawnRotation);

        NetworkObject shipObject = Instantiate(shipPrefab, spawnPosition, spawnRotation);
        if (shipObject.TryGetComponent<ScaledTransform>(out var scaledTransform))
        {
            scaledTransform.realPosition = spawnPosition.ToVector3d();
        }
        PlayerRegistry.TryGetPlayer(conn.ClientId, out var playerInfo);
        shipObject.name = playerInfo.Username;

        InstanceFinder.ServerManager.Spawn(shipObject, conn);
        Debug.Log($"[ShipSpawner] Spawned ship for client {conn.ClientId}");

        if (!shipObject.TryGetComponent<Ship>(out var ship))
        {
            Debug.LogError("[ShipSpawner] Ship prefab is missing Ship component.");
            return;
        }

        playerShips.Add(conn.ClientId, ship);
    }

    private void RemovePlayerShip(NetworkConnection conn)
    {
        if (!playerShips.TryGetValue(conn.ClientId, out Ship ship))
            return;

        playerShips.Remove(conn.ClientId);

        if (ship != null)
        {
            NetworkObject nob = ship.GetComponent<NetworkObject>();

            if (nob != null && nob.IsSpawned)
                InstanceFinder.ServerManager.Despawn(nob);
        }
    }

    private void GetSpawnPoint(out Vector3 position, out Quaternion rotation)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        Transform spawn = spawnPoints[nextSpawnIndex];

        position = spawn.position;
        rotation = spawn.rotation;

        nextSpawnIndex++;

        if (nextSpawnIndex >= spawnPoints.Length)
            nextSpawnIndex = 0;
    }

    public Ship GetPlayerShip(NetworkConnection conn)
    {
        playerShips.TryGetValue(conn.ClientId, out Ship ship);
        return ship;
    }

    public Ship GetPlayerShip(int clientId)
    {
        playerShips.TryGetValue(clientId, out Ship ship);
        return ship;
    }

    public void RespawnPlayer(NetworkConnection conn)
    {
        RemovePlayerShip(conn);
        SpawnPlayerShip(conn);
    }
}