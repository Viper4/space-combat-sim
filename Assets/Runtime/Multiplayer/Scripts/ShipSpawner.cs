using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet;
using UnityEngine;

public class ShipSpawner : NetworkBehaviour
{
    public static ShipSpawner Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] private NetworkObject shipPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<int, Ship> playerShips = new();

    private int nextSpawnIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

        // The scene this object is in (MainScene) isn't loaded until after the clients have connected, so must manually create their ships on startup
        SpawnConnectedPlayers();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;

        playerShips.Clear();
    }

    private void SpawnConnectedPlayers()
    {
        foreach (NetworkConnection conn in InstanceFinder.ServerManager.Clients.Values)
        {
            if (!playerShips.ContainsKey(conn.ClientId))
            {
                SpawnPlayerShip(conn);
            }
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        switch (args.ConnectionState)
        {
            case FishNet.Transporting.RemoteConnectionState.Started:
                SpawnPlayerShip(conn);
                break;
            case FishNet.Transporting.RemoteConnectionState.Stopped:
                RemovePlayer(conn);
                break;
        }
    }

    private void SpawnPlayerShip(NetworkConnection conn)
    {
        if (!IsServerInitialized)
            return;

        if (playerShips.ContainsKey(conn.ClientId))
            return;

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        GetSpawnPoint(out spawnPosition, out spawnRotation);

        NetworkObject shipObject = Instantiate(shipPrefab, spawnPosition, spawnRotation);

        InstanceFinder.ServerManager.Spawn(shipObject, conn);

        if (!shipObject.TryGetComponent<Ship>(out var ship))
        {
            Debug.LogError("Ship prefab is missing Ship component.");
            return;
        }

        playerShips.Add(conn.ClientId, ship);

        Debug.Log($"Spawned ship for client {conn.ClientId}");
    }

    private void RemovePlayer(NetworkConnection conn)
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
        RemovePlayer(conn);
        SpawnPlayerShip(conn);
    }
}