using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TorpedoPoint : MonoBehaviour
{
    MeshRenderer staticMesh;
    [SerializeField] GameObject torpedoPrefab;
    [SerializeField] Vector3 launchVelocity;
    public bool hasTorpedo = true;
    [SerializeField] float activateDelay;

    void Start()
    {
        staticMesh = GetComponent<MeshRenderer>();
    }

    public void ReloadTorpedo()
    {
        hasTorpedo = true;
        staticMesh.enabled = true;
    }

    public Torpedo LaunchTorpedo(Vector3 initialVelocity, RadarTarget target, int index, string team)
    {
        hasTorpedo = false;
        staticMesh.enabled = false;
        Torpedo torpedo = Instantiate(torpedoPrefab, transform.position, transform.rotation).GetComponent<Torpedo>();
        torpedo.name = torpedoPrefab.name + " " + (index + 1);
        torpedo.GetComponent<RadarTarget>().team = team;
        Rigidbody torpedoRB = torpedo.GetComponent<Rigidbody>();
        torpedoRB.linearVelocity = initialVelocity;
        torpedoRB.AddRelativeForce(launchVelocity, ForceMode.VelocityChange);
        torpedo.Activate(target, activateDelay);
        return torpedo;
    }
}
