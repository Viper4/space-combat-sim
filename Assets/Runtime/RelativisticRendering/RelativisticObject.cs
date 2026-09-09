using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(ScaledRigidbody))]
public class RelativisticObject : MonoBehaviour
{
    private ScaledRigidbody _scaledRigidbody;
    private MaterialPropertyBlock _block;

    private void Start()
    {
        _scaledRigidbody = GetComponent<ScaledRigidbody>();
        _block = new MaterialPropertyBlock();
    }

    private void FixedUpdate()
    {
        if (FloatingWorldOrigin.Instance == null || Camera.main == null || !_scaledRigidbody.scaledTransform.visible)
            return;

        foreach (Renderer renderer in _scaledRigidbody.scaledTransform.trackedRenderers)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetVector(ScaledSpaceVisuals.SourceVelocityID, _scaledRigidbody.velocity.ToVector3());
            _block.SetVector(ScaledSpaceVisuals.ObserverVelocityID, FloatingWorldOrigin.Instance.scaledRigidbody.velocity.ToVector3());
            renderer.SetPropertyBlock(_block);
        }
    }
}