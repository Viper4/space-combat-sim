using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(ScaledTransform))]
public class FloatingWorldOrigin : MonoBehaviour
{
    public static FloatingWorldOrigin Instance { get; private set; }

    public ScaledRigidbody scaledRigidbody;
    public ScaledTransform scaledTransform;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            scaledRigidbody = GetComponent<ScaledRigidbody>();
            scaledTransform = GetComponent<ScaledTransform>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Vector3d GetRealCameraPosition()
    {
        return Camera.main.transform.position.ToVector3d() + scaledTransform.realPosition;        
    }
}
