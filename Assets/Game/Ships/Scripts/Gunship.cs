using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SpaceStuff;
using TMPro;

public class Gunship : Ship
{
    private TurretSystem turretSystem;
    private TorpedoSystem torpedoSystem;

    [SerializeField, Header("Gunship")] private Material modelMaterial;
    [SerializeField] private Transform targetModelParent;
    [SerializeField] private TextMeshProUGUI targetName;
    [SerializeField] private Transform targetDirectionPivot;
    private Transform targetModel;
    private RadarTarget lockedTarget;

    protected override void Start()
    {
        base.Start();
        turretSystem = GetComponent<TurretSystem>();
        torpedoSystem = GetComponent<TorpedoSystem>();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (isPilot && (IsOwner || GameManager.Instance.offlineMode))
        {
            if (turretSystem.manualControl || torpedoSystem.torpedoBayDoorOpen)
            {
                if (!_HUDSystem.combatPanel.gameObject.activeSelf)
                {
                    _HUDSystem.combatPanel.gameObject.SetActive(true);
                }
            }
            else
            {
                if (_HUDSystem.combatPanel.gameObject.activeSelf)
                {
                    _HUDSystem.combatPanel.gameObject.SetActive(false);
                }
            }

            if (lockedTarget != null)
            {
                targetName.text = lockedTarget.name;
                targetModel.rotation = lockedTarget.transform.rotation;

                if(lockedTarget.doubleRigidbody != null)
                {
                    if (lockedTarget.doubleRigidbody.velocity != Vector3d.zero)
                        targetDirectionPivot.rotation = Quaternion.LookRotation(lockedTarget.doubleRigidbody.velocity.ToVector3(), lockedTarget.transform.up);
                }
                else if (lockedTarget.attachedRB != null)
                {
                    if (lockedTarget.attachedRB.linearVelocity != Vector3.zero)
                        targetDirectionPivot.rotation = Quaternion.LookRotation(lockedTarget.attachedRB.linearVelocity, lockedTarget.transform.up);
                }

                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_HUDSystem.combatPanel, Camera.main.WorldToScreenPoint(lockedTarget.transform.position), Camera.main, out Vector3 crosshairPosition))
                {
                    _HUDSystem.mainCrosshair.transform.position = crosshairPosition;
                }
                _HUDSystem.mainCrosshair.transform.rotation = Quaternion.LookRotation(pilotPoint.position - _HUDSystem.mainCrosshair.transform.position, Camera.main.transform.up);
            }
            else if (targetModel != null)
            {
                Destroy(targetModel.gameObject);
                targetName.text = "NO TARGET";
                targetModelParent.gameObject.SetActive(false);
            }
        }
    }

    public void LockTarget()
    {
        if (targetModel != null)
            Destroy(targetModel.gameObject);
        targetName.text = "NO TARGET";
        if (_HUDSystem.combatPanel.gameObject.activeSelf)
        {
            Vector3 crosshairDirection = _HUDSystem.mainCrosshair.transform.position - pilotPoint.position;
            if (Physics.Raycast(_HUDSystem.mainCrosshair.transform.position, crosshairDirection, out RaycastHit hit, Mathf.Infinity, ~ignoreLayers))
            {
                hit.transform.TryGetComponent<RadarTarget>(out lockedTarget);
                Debug.DrawLine(_HUDSystem.mainCrosshair.transform.position, hit.point, Color.green, 0.1f);
            }
            targetModelParent.gameObject.SetActive(lockedTarget != null);
            if (lockedTarget != null)
            {
                _HUDSystem.mainCrosshair.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
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
                newTargetModel = CustomMethods.GenerateModel(lockedTarget.gameObject, 2, modelMaterial, 1); // 2 is Ignore Raycast layer
                newTargetModel.transform.localScale = new Vector3(5, 5, 5);
            }

            newTargetModel.name = "Target Model";
            targetModel = newTargetModel.transform;
            targetModel.SetParent(targetModelParent);
            targetModel.localPosition = Vector3.zero;
        }
        else
        {
            lockedTarget = null;
            targetModelParent.gameObject.SetActive(false);
        }
    }

    public void LaunchTorpedo()
    {
        if (lockedTarget != null)
            torpedoSystem.FireTorpedo(lockedTarget.transform);
    }
}
