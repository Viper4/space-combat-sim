using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformChange : MonoBehaviour
{
    public delegate void TransformChangeEvent();
    public event TransformChangeEvent OnTransformChange;
    public event TransformChangeEvent OnPositionChange;
    public event TransformChangeEvent OnRotationChange;

    [SerializeField] private bool trackPosition;
    [SerializeField] private bool trackRotation;

    private Vector3 lastPosition;
    private Quaternion lastRotation;

    public virtual void TransformChanged()
    {
        OnTransformChange?.Invoke();
    }

    public virtual void PositionChanged()
    {
        OnPositionChange?.Invoke();
    }

    public virtual void RotationChanged()
    {
        OnRotationChange?.Invoke();
    }

    void Start()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        bool differentPosition = trackPosition && (transform.position - lastPosition).sqrMagnitude > 0.001f;
        bool differentRotation = trackRotation && transform.rotation != lastRotation;
        if(differentPosition || differentRotation)
        {
            TransformChanged();
        }
        if(differentPosition)
        {
            lastPosition = transform.position;
            PositionChanged();
        }
        if(differentRotation)
        {
            lastRotation = transform.rotation;
            RotationChanged();
        }
    }
}
