using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using FishNet;
using FishNet.Object;

/// <summary>
/// All the important stuff like instantiating new torpedo or reloading torpedo can only be synchronized by server.
/// So even if client hacks this and spawns/reloads torpedos, it wont match with what the server's got.
/// </summary>
public class TorpedoPoint : MonoBehaviour
{
    private MeshRenderer staticMesh;

    [SerializeField] private ScaledTransform parent;
    [SerializeField] private ScaledRigidbody parentRB;
    [SerializeField] private Collider[] parentColliders;
    [SerializeField] private GameObject torpedoPrefab;
    [SerializeField] private Vector3d launchVelocity;
    [SerializeField] private float activateDelay;

    public bool hasTorpedo = true;

    void Start()
    {
        staticMesh = GetComponent<MeshRenderer>();
    }

    public void ReloadTorpedo()
    {
        hasTorpedo = true;
        staticMesh.enabled = true;
    }

    public Torpedo LaunchTorpedo(Vector3d initialVelocity, Vector3 initialAngularVelocity, RadarTarget target, int index, string team)
    {
        hasTorpedo = false;
        staticMesh.enabled = false;
        GameObject torpedoGO = Instantiate(torpedoPrefab);
        torpedoGO.transform.rotation = transform.rotation;
        Torpedo torpedo = torpedoGO.GetComponent<Torpedo>();
        torpedo.GetComponent<ScaledTransform>().realPosition = parent.TransformRenderPoint(transform.position);

        ScaledRigidbody torpedoRB = torpedo.GetComponent<ScaledRigidbody>();
        double globalX = transform.right.x * launchVelocity.x + transform.up.x * launchVelocity.y + transform.forward.x * launchVelocity.z;
        double globalY = transform.right.y * launchVelocity.x + transform.up.y * launchVelocity.y + transform.forward.y * launchVelocity.z;
        double globalZ = transform.right.z * launchVelocity.x + transform.up.z * launchVelocity.y + transform.forward.z * launchVelocity.z;
        torpedoRB.velocity = initialVelocity + new Vector3d(globalX, globalY, globalZ);
        torpedoRB.angularVelocity = initialAngularVelocity;

        RadarTarget torpedoTarget = torpedo.GetComponent<RadarTarget>();
        torpedo.name = torpedoPrefab.name + " " + (index + 1);
        torpedoTarget.team = team;
        if (InstanceFinder.ServerManager != null && !InstanceFinder.IsOffline)
            InstanceFinder.ServerManager.Spawn(torpedoGO.GetComponent<NetworkObject>()); // ScaledRigidbodySync handles the ScaledRB for us

        torpedo.Activate(target, activateDelay, parentRB, parentColliders);
        return torpedo;
    }
}
