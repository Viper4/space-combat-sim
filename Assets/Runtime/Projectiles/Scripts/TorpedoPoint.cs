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
    [SerializeField] private GameObject torpedoPrefab;
    [SerializeField] private Vector3d launchVelocity;
    public bool hasTorpedo = true;
    [SerializeField] private float activateDelay;

    void Start()
    {
        staticMesh = GetComponent<MeshRenderer>();
    }

    public void ReloadTorpedo()
    {
        hasTorpedo = true;
        staticMesh.enabled = true;
    }

    public Torpedo LaunchTorpedo(Vector3d launchPosition, Vector3d initialVelocity, RadarTarget target, int index, string team)
    {
        hasTorpedo = false;
        staticMesh.enabled = false;
        GameObject torpedoGO = Instantiate(torpedoPrefab);
        torpedoGO.transform.rotation = transform.rotation;
        Torpedo torpedo = torpedoGO.GetComponent<Torpedo>();
        torpedo.GetComponent<Collider>().enabled = false;
        RadarTarget torpedoTarget = torpedo.GetComponent<RadarTarget>();
        torpedo.name = torpedoPrefab.name + " " + (index + 1);
        torpedoTarget.team = team;
        if (InstanceFinder.ServerManager != null && !InstanceFinder.IsOffline)
            InstanceFinder.ServerManager.Spawn(torpedoGO.GetComponent<NetworkObject>()); // ScaledRigidbodySync handles the ScaledRB for us

        StartCoroutine(LaunchRoutine(torpedoTarget, launchPosition, initialVelocity));
        torpedo.Activate(target, activateDelay);
        return torpedo;
    }

    private IEnumerator LaunchRoutine(RadarTarget torpedoTarget, Vector3d launchPosition, Vector3d initialVelocity)
    {
        while(torpedoTarget.scaledRigidbody == null)
        {
            yield return null;
        }
        torpedoTarget.scaledRigidbody.EnableScaledColliders(false);
        double globalX = transform.right.x * launchVelocity.x + transform.up.x * launchVelocity.y + transform.forward.x * launchVelocity.z;
        double globalY = transform.right.y * launchVelocity.x + transform.up.y * launchVelocity.y + transform.forward.y * launchVelocity.z;
        double globalZ = transform.right.z * launchVelocity.x + transform.up.z * launchVelocity.y + transform.forward.z * launchVelocity.z;
        torpedoTarget.scaledRigidbody.velocity = initialVelocity + new Vector3d(globalX, globalY, globalZ);
        // torpedoTarget.scaledRigidbody.AddForce(launchVelocity, ForceMode.VelocityChange);
        torpedoTarget.scaledRigidbody.scaledTransform.realPosition = launchPosition;
    }
}
