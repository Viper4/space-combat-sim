using SpaceStuff;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Ship))]
public class TurretSystem : MonoBehaviour
{
    [HideInInspector] public Ship ship;
    [SerializeField] private HUDSystem _HUDSystem;
    [SerializeField] private Radar radar;

    [SerializeField] private Transform[] turretPoints;
    [HideInInspector] public Turret[] turrets;
    [SerializeField] private GameObject turretCrosshair;

    public bool manualControl = false;

    [SerializeField] private Color crosshairNormalColor;
    [SerializeField] private Color crosshairHoverColor;
    [SerializeField] private Color crosshairTriggerColor;
    private bool triggerHeld = false;
    [SerializeField] private Color turretNormalColor;
    [SerializeField] private Color turretBlockedColor;

    [SerializeField] private Color targetingHUDColor;

    private List<RadarTarget> targetsInRange = new List<RadarTarget>();

    private void Start()
    {
        ship = GetComponent<Ship>();

        turrets = new Turret[turretPoints.Length];
        if (ship.shields != null)
        {
            Collider shieldCollider = ship.shields.colliderObject.GetComponent<Collider>();
            for (int i = 0; i < turretPoints.Length; i++)
            {
                turrets[i] = turretPoints[i].GetChild(0).GetComponent<Turret>();
                turrets[i].IgnoreCollider(shieldCollider);
                Instantiate(turretCrosshair, _HUDSystem.combatPanel);
            }
        }
        else
        {
            for (int i = 0; i < turretPoints.Length; i++)
            {
                turrets[i] = turretPoints[i].GetChild(0).GetComponent<Turret>();
                Instantiate(turretCrosshair, _HUDSystem.combatPanel);
            }
        }
    }

    private void FixedUpdate()
    {
        if (manualControl)
        {
            Vector3 crosshairDirection = _HUDSystem.mainCrosshair.transform.position - ship.pilotPoint.position;

            if (Physics.Raycast(_HUDSystem.mainCrosshair.transform.position, crosshairDirection, out RaycastHit hit, Mathf.Infinity, ~ship.ignoreLayers))
            {
                if (!triggerHeld)
                    _HUDSystem.mainCrosshair.color = crosshairHoverColor;
            }
            else
            {
                if (!triggerHeld)
                    _HUDSystem.mainCrosshair.color = crosshairNormalColor;
            }

            for (int i = 0; i < turrets.Length; i++)
            {
                Turret turret = turrets[i];
                turret.aimDirection = hit.point - turret.firePoint.position;
                Transform turretCrosshair = _HUDSystem.combatPanel.GetChild(i + 1);
                Vector3 turretHitPoint;
                if (turret.GetObstruction(out RaycastHit turretHit))
                {
                    if (turretHit.transform != transform)
                        turretCrosshair.GetComponent<Image>().color = turretNormalColor;
                    else
                        turretCrosshair.GetComponent<Image>().color = turretBlockedColor;
                    turretHitPoint = turretHit.point;
                }
                else
                {
                    turretCrosshair.GetComponent<Image>().color = turretNormalColor;
                    turretHitPoint = turret.firePoint.position + turret.firePoint.forward * 1000;
                }

                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_HUDSystem.combatPanel, Camera.main.WorldToScreenPoint(turretHitPoint), Camera.main, out Vector3 turretCrosshairPos))
                {
                    turretCrosshair.position = turretCrosshairPos;
                }
            }
        }
        else
        {
            for (int i = targetsInRange.Count - 1; i >= 0; i--)
            {
                if (targetsInRange[i] == null)
                {
                    targetsInRange.RemoveAt(i);
                    continue;
                }
                uint targetID = targetsInRange[i].GetID();

                if (targetsInRange[i].turretsTargeting <= 0)
                {
                    if (_HUDSystem.TryGetValue(targetID, out HUDObject targetHUDObject))
                    {
                        targetHUDObject.SetTargetText("");
                        targetHUDObject.SetColor(targetsInRange[i].originalHUDColor);
                    }
                    if (targetsInRange[i].radarIcon != null)
                        targetsInRange[i].radarIcon.SetColor(targetsInRange[i].originalRadarColor, targetsInRange[i].originalRadarEmission);
                    continue;
                }

                /*Vector3 relativeVelocity = ship.velocity - targetsInRange[i].velocity;
                Vector3 relativeAcceleration = ship.acceleration - targetsInRange[i].acceleration;
                float closingSpeed = Vector3.Dot(relativeVelocity, targetsInRange[i].direction);
                float closingAcceleration = Vector3.Dot(relativeAcceleration, targetsInRange[i].direction);

                float arrivalTime = CustomMethods.CalculateArrivalTime(closingAcceleration, closingSpeed, targetsInRange[i].distance);
                string ETA = arrivalTime < 0 ? "Never" : CustomMethods.SecondsToFormattedString(arrivalTime, 2);
                string details = "Distance: " + CustomMethods.DistanceToFormattedString(targetsInRange[i].distance, 2) +
                    "\nSpeed: " + CustomMethods.SpeedToFormattedString(targetsInRange[i].velocity.magnitude, 2) +
                    "\nClosing Speed: " + CustomMethods.SpeedToFormattedString(closingSpeed, 2) +
                    "\nETA: " + ETA;*/

                if (_HUDSystem.TryGetValue(targetID, out HUDObject _targetHUDObject))
                {
                    //_HUDSystem.UpdateObject(targetID, targetsInRange[i].transform, targetsInRange[i].transform.name, details);
                    _targetHUDObject.SetTargetText(targetsInRange[i].turretsTargeting.ToString());
                    if (targetsInRange[i].originalHUDColor == Color.clear)
                        targetsInRange[i].originalHUDColor = _targetHUDObject.GetColor();

                    _targetHUDObject.SetColor(targetingHUDColor);
                }
                /*else
                {
                    HUDObject newHUDObject = _HUDSystem.CreateObject(targetID, targetsInRange[i].transform, targetsInRange[i].transform.name, details);
                    newHUDObject.SetTargetText(targetsInRange[i].turretsTargeting.ToString());
                    if (targetsInRange[i].originalHUDColor == Color.clear)
                        targetsInRange[i].originalHUDColor = newHUDObject.GetColor();
                    newHUDObject.SetColor(targetingHUDColor);
                }*/

                if (targetsInRange[i].originalRadarColor == Color.clear)
                {
                    targetsInRange[i].originalRadarColor = targetsInRange[i].radarIcon.GetColor();
                    targetsInRange[i].originalRadarEmission = targetsInRange[i].radarIcon.GetEmission();
                }
                targetsInRange[i].radarIcon.SetColor(targetingHUDColor, targetingHUDColor);
            }
        }
    }

    public void TriggerStart()
    {
        if (manualControl)
        {
            triggerHeld = true;
            _HUDSystem.mainCrosshair.color = crosshairTriggerColor;
            for (int i = 0; i < turrets.Length; i++)
            {
                Turret turret = turrets[i];
                if (!turret.GetObstruction(out RaycastHit turretHit) || turretHit.transform != transform)
                {
                    turret.fire = true;
                }
            }
        }
    }

    public void TriggerEnd()
    {
        if (manualControl)
        {
            triggerHeld = false;
            for (int i = 0; i < turrets.Length; i++)
            {
                turrets[i].fire = false;
            }
        }
    }

    public void ToggleManualControl()
    {
        manualControl = !manualControl;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.isTrigger && other.TryGetComponent<RadarTarget>(out var otherRadarTarget))
        {
            if (otherRadarTarget.team == ship.team)
                return;
            targetsInRange.Add(otherRadarTarget);
            for (int i = 0; i < turrets.Length; i++)
            {
                turrets[i].AddTarget(otherRadarTarget);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.isTrigger && other.TryGetComponent<RadarTarget>(out var otherRadarTarget))
        {
            targetsInRange.Remove(otherRadarTarget);
            for (int i = 0; i < turrets.Length; i++)
            {
                turrets[i].RemoveTarget(otherRadarTarget);
            }
        }
    }
}
