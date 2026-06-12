using UnityEngine;
using SpaceStuff;
using TMPro;
using UnityEngine.InputSystem;
using System;

public class TargetingSystem : MonoBehaviour
{
    [SerializeField] private Transform centralCrosshair;
    [SerializeField] private LayerMask ignoreLayers;
    [SerializeField] private HUDSystem _HUDSystem;
    [SerializeField] private Material modelMaterial;
    [SerializeField] private Transform targetModelParent;
    [SerializeField] private TextMeshProUGUI targetName;
    [SerializeField] private Transform targetDirectionPivot;
    private Transform targetModel;
    public RadarTarget lockedTarget {get; private set;}

    private Vector3 aimPoint;
    private bool crosshairHovering;

    public Action OnTargetChange;

    private void Start()
    {
        GameManager.Instance.inputActions.Player.LockTarget.performed += LockTarget;
    }

    private void FixedUpdate()
    {
        Vector3 crosshairDirection = centralCrosshair.position - Camera.main.transform.position;

        if (Physics.Raycast(centralCrosshair.position, crosshairDirection, out RaycastHit crosshairHit, Mathf.Infinity, ~ignoreLayers, QueryTriggerInteraction.Ignore))
        {
            if (crosshairHit.transform.TryGetComponent<ScaledTransform>(out var scaledTransform))
            {
                // Convert hit point to real position and back to transform position (position relative to world origin)
                aimPoint = (scaledTransform.GetChildRealPosition(crosshairHit.point) - FloatingWorldOrigin.Instance.worldOriginPosition).ToVector3();
            }
            else
            {
                aimPoint = crosshairHit.point;
            }
            crosshairHovering = true;
        }
        else
        {
            aimPoint = Camera.main.transform.position + crosshairDirection.normalized * 5000f;
            crosshairHovering = false;
        }

        if (lockedTarget != null)
        {
            targetName.text = lockedTarget.name;
            targetModel.rotation = lockedTarget.transform.rotation;
            if (lockedTarget.doubleRigidbody.velocity != Vector3d.zero)
                targetDirectionPivot.rotation = Quaternion.LookRotation(lockedTarget.doubleRigidbody.velocity.ToVector3(), transform.up);

            _HUDSystem.UpdateTargetDirectionMarker(lockedTarget.doubleRigidbody.scaledTransform.realPosition);

        }
        else if (targetModel != null)
        {
            RemoveTarget();
        }
    }

    private void RemoveTarget()
    {
        if (lockedTarget != null)
        {
            if (lockedTarget.alertSystem != null)
                lockedTarget.alertSystem.RemoveRadarLock();
            lockedTarget = null;
            _HUDSystem.SetTargetDirectionMarkerActive(false);
        }
        if (targetModel != null)
        {
            Destroy(targetModel.gameObject);
            targetName.text = "NO TARGET";
            targetModelParent.gameObject.SetActive(false);
            targetModel = null;
        }
    }

    private void LockTarget(InputAction.CallbackContext context)
    {
        if (!_HUDSystem.radarHudActive)
            return;

        RadarTarget newTarget = _HUDSystem.GetClosestTarget();
        if (newTarget == null || newTarget == lockedTarget)
        {
            RemoveTarget();
            OnTargetChange?.Invoke();
            return;
        }
        RemoveTarget();
        lockedTarget = newTarget;
        if (lockedTarget.alertSystem != null)
            lockedTarget.alertSystem.AddRadarLock();
        targetModelParent.gameObject.SetActive(lockedTarget != null);

        GameObject newTargetModel;
        if (lockedTarget.CompareTag("Ship"))
        {
            newTargetModel = Instantiate(lockedTarget.GetComponent<Ship>().hologramPrefab, targetModelParent, false);
        }
        else
        {
            newTargetModel = SpaceMath.GenerateModel(lockedTarget.gameObject, 2, modelMaterial, 1); // 2 is Ignore Raycast layer
            newTargetModel.transform.localScale = lockedTarget.transform.localScale.normalized * 0.05f;
        }

        newTargetModel.name = "Target Model";
        targetModel = newTargetModel.transform;
        targetModel.SetParent(targetModelParent);
        targetModel.localPosition = Vector3.zero;
        targetName.text = lockedTarget.name;
        OnTargetChange?.Invoke();
    }

    private void OnDestroy()
    {
        GameManager.Instance.inputActions.Player.LockTarget.performed -= LockTarget;
    }

    public void SetCrosshairActive(bool active)
    {
        centralCrosshair.gameObject.SetActive(active);
    }

    public Vector3 GetAimPoint()
    {
        return aimPoint;
    }

    public bool CrosshairIsHovering()
    {
        return crosshairHovering;
    }
}
