using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using TMPro;

[RequireComponent(typeof(TurretSystem)), RequireComponent(typeof(TorpedoSystem))]
public class Gunship : Ship
{
    [SerializeField] private HUDSystem _HUDSystem;
    [SerializeField, Header("Gunship")] private Material modelMaterial;
    [SerializeField] private Transform targetModelParent;
    [SerializeField] private TextMeshProUGUI targetName;
    [SerializeField] private Transform targetDirectionPivot;
    private Transform targetModel;
    private RadarTarget lockedTarget;

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (isPilot && (IsOwner || GameManager.Instance.offlineMode))
        {
            if (lockedTarget != null)
            {
                targetName.text = lockedTarget.name;
                targetModel.rotation = lockedTarget.transform.rotation;
                if (lockedTarget.doubleRigidbody.velocity != Vector3d.zero)
                    targetDirectionPivot.rotation = Quaternion.LookRotation(lockedTarget.doubleRigidbody.velocity.ToVector3(), lockedTarget.transform.up);

                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_HUDSystem.combatPanel, Camera.main.WorldToScreenPoint(lockedTarget.transform.position), Camera.main, out Vector3 crosshairPosition))
                {
                    _HUDSystem.targetCrosshair.transform.position = crosshairPosition;
                }
                _HUDSystem.targetCrosshair.transform.rotation = Quaternion.LookRotation(pilotPoint.position - _HUDSystem.targetCrosshair.transform.position, Camera.main.transform.up);
            }
            else if (targetModel != null)
            {
                Destroy(targetModel.gameObject);
                targetName.text = "NO TARGET";
                targetModelParent.gameObject.SetActive(false);
                targetModel = null;
                _HUDSystem.targetCrosshair.gameObject.SetActive(false);
            }
        }
    }

    public void LockTarget()
    {
        if (targetModel != null)
            Destroy(targetModel.gameObject);
        targetName.text = "NO TARGET";
        if (_HUDSystem.combatHudActive)
        {
            _HUDSystem.targetCrosshair.gameObject.SetActive(true);
            Vector3 crosshairDirection = _HUDSystem.targetCrosshair.transform.position - pilotPoint.position;
            if (Physics.Raycast(_HUDSystem.targetCrosshair.transform.position, crosshairDirection, out RaycastHit hit, Mathf.Infinity, ~ignoreLayers))
            {
                hit.transform.TryGetComponent(out lockedTarget);
            }
            targetModelParent.gameObject.SetActive(lockedTarget != null);
            if (lockedTarget != null)
            {
                _HUDSystem.targetCrosshair.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            if (lockedTarget == null)
                return;
            GameObject newTargetModel;
            if (lockedTarget.transform.CompareTag("Ship"))
            {
                newTargetModel = Instantiate(lockedTarget.GetComponent<Ship>().radarModel, targetModelParent, false);
            }
            else
            {
                newTargetModel = SpaceMath.GenerateModel(lockedTarget.gameObject, 2, modelMaterial, 1); // 2 is Ignore Raycast layer
                newTargetModel.transform.localScale = new Vector3(5, 5, 5);
            }

            newTargetModel.name = "Target Model";
            targetModel = newTargetModel.transform;
            targetModel.SetParent(targetModelParent);
            targetModel.localPosition = Vector3.zero;
            targetName.text = lockedTarget.name;
        }
        else
        {
            lockedTarget = null;
            targetModelParent.gameObject.SetActive(false);
        }
    }
}
