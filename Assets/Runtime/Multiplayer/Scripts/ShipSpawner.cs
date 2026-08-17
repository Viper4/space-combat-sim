using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet;
using UnityEngine;
using SpaceStuff;
using FishNet.Managing.Scened;

public class ShipSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private NetworkObject shipPrefab;
    [SerializeField] private Vector3d[] spawnCenters;
    [SerializeField] private double[] spawnRadii;
    [SerializeField] private double bufferRadius = 100.0;
    [SerializeField] private LayerMask collisionLayers;

    private readonly Dictionary<int, Ship> playerShips = new();

    private int nextSpawnIndex;

    private void Awake()
    {
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

        NetworkObject shipObject = Instantiate(shipPrefab);
        shipObject.name = "Player";

        if (!shipObject.TryGetComponent<Ship>(out var ship))
        {
            Debug.LogError(GameLog.ComponentNotFound(this, "SpawnOfflinePlayerShip", shipObject, "Ship"));
            return;
        }

        GetSpawnPoint(bufferRadius, out Vector3d spawnPosition, out Quaternion spawnRotation);
        if (!shipObject.TryGetComponent<ScaledTransform>(out var scaledTransform))
        {
            Debug.LogWarning(GameLog.ComponentNotFound(this, $"SpawnOfflinePlayerShip at {spawnPosition}", shipObject, "ScaledTransform"));
        }
        else
        {
            scaledTransform.realPosition = spawnPosition;
        }
        shipObject.transform.rotation = spawnRotation;

        playerShips.Add(-1, ship);

        Debug.Log(GameLog.ObjectLog(this, "Spawned offline player ship."));
    }

    private void SpawnPlayerShip(NetworkConnection conn)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (playerShips.ContainsKey(conn.ClientId))
            return;

        NetworkObject shipObject = Instantiate(shipPrefab);
        PlayerRegistry.TryGetPlayer(conn.ClientId, out var playerInfo);
        shipObject.name = playerInfo.Username;
        if (!shipObject.TryGetComponent<Ship>(out var ship))
        {
            Debug.LogError(GameLog.ComponentNotFound(this, "SpawnPlayerShip", shipObject, "Ship"));
            return;
        }

        GetSpawnPoint(bufferRadius, out Vector3d spawnPosition, out Quaternion spawnRotation);
        if (!shipObject.TryGetComponent<ScaledTransform>(out var scaledTransform))
        {
            Debug.LogWarning(GameLog.ComponentNotFound(this, $"SpawnPlayerShip at {spawnPosition}", shipObject, "ScaledTransform"));
        }
        else
        {
            scaledTransform.realPosition = spawnPosition;
        }
        shipObject.transform.rotation = spawnRotation;

        InstanceFinder.ServerManager.Spawn(shipObject, conn);

        playerShips.Add(conn.ClientId, ship);

        Debug.Log(GameLog.ObjectLog(this, $"Spawned player ship for client {conn.ClientId}."));
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

    private void GetSpawnPoint(double bufferRadius, out Vector3d position, out Quaternion rotation)
    {
        if (spawnCenters == null || spawnCenters.Length == 0)
        {
            position = Vector3d.zero;
            rotation = Quaternion.identity;
            return;
        }

        Vector3d center = spawnCenters[nextSpawnIndex];
        position = center + Random.insideUnitSphere.ToVector3d() * spawnRadii[nextSpawnIndex];

        for (int i = 0; i < 100; i++)
        {
            if (!ScaledSpacePhysics.Instance.CheckSphere(position, bufferRadius, collisionLayers, true))
            {
                break;
            }
            position = center + Random.insideUnitSphere.ToVector3d() * spawnRadii[nextSpawnIndex];
        }
        rotation = Random.rotation;

        nextSpawnIndex++;

        if (nextSpawnIndex >= spawnCenters.Length)
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