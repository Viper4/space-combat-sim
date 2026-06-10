using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;

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
        Torpedo torpedo = Instantiate(torpedoPrefab).GetComponent<Torpedo>();
        torpedo.transform.rotation = transform.rotation;
        torpedo.GetComponent<Collider>().enabled = false;
        RadarTarget torpedoTarget = torpedo.GetComponent<RadarTarget>();
        torpedo.name = torpedoPrefab.name + " " + (index + 1);
        torpedoTarget.team = team;
        StartCoroutine(LaunchRoutine(torpedoTarget, launchPosition, initialVelocity));
        torpedo.Activate(target, activateDelay);
        return torpedo;
    }

    private IEnumerator LaunchRoutine(RadarTarget torpedoTarget, Vector3d launchPosition, Vector3d initialVelocity)
    {
        yield return new WaitWhile(() => torpedoTarget.doubleRigidbody == null); // Wait until torpedo's Start() ran
        torpedoTarget.GetComponent<Collider>().enabled = true;
        torpedoTarget.doubleRigidbody.velocity = initialVelocity;
        torpedoTarget.doubleRigidbody.AddRelativeForce(launchVelocity, ForceMode.VelocityChange);
        torpedoTarget.doubleRigidbody.scaledTransform.realPosition = launchPosition;
        Vector3d shipPos = FloatingWorldOrigin.Instance.GetComponent<ScaledTransform>().realPosition;
        Debug.Log($"Ship pos: {shipPos}");
        Debug.Log($"Torp launch: {launchPosition}, actual: {torpedoTarget.doubleRigidbody.scaledTransform.realPosition}");
        Debug.Log($"Ship distance: {(shipPos - launchPosition).magnitude}, launch error distance: {(launchPosition - torpedoTarget.doubleRigidbody.scaledTransform.realPosition).magnitude}");
    }
}
