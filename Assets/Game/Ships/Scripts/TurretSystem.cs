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
    [SerializeField] private GameObject turretCrosshairPrefab;
    private Transform[] turretCrosshairs;
    private Image[] turretCrosshairImages;

    public bool manualControl = false;

    [SerializeField] private Color crosshairNormalColor;
    [SerializeField] private Color crosshairHoverColor;
    [SerializeField] private Color crosshairTriggerColor;
    private bool triggerHeld = false;
    [SerializeField] private Color turretNormalColor;
    [SerializeField] private Color turretHoverColor;
    [SerializeField] private Color turretBlockedColor;

    [SerializeField] private Color targetingHUDColor;

    private List<uint> targetsInRange = new List<uint>();

    private void Start()
    {
        ship = GetComponent<Ship>();

        turrets = new Turret[turretPoints.Length];
        turretCrosshairs = new Transform[turretPoints.Length];
        turretCrosshairImages = new Image[turretPoints.Length];
        if (ship.shields != null)
        {
            Collider shieldCollider = ship.shields.colliderObject.GetComponent<Collider>();
            for (int i = 0; i < turretPoints.Length; i++)
            {
                turrets[i] = turretPoints[i].GetChild(0).GetComponent<Turret>();
                turrets[i].IgnoreCollider(shieldCollider);
                GameObject newCrosshair = Instantiate(turretCrosshairPrefab, _HUDSystem.combatPanel);
                turretCrosshairs[i] = newCrosshair.transform;
                turretCrosshairImages[i] = newCrosshair.GetComponent<Image>();
            }
        }
        else
        {
            for (int i = 0; i < turretPoints.Length; i++)
            {
                turrets[i] = turretPoints[i].GetChild(0).GetComponent<Turret>();
                GameObject newCrosshair = Instantiate(turretCrosshairPrefab, _HUDSystem.combatPanel);
                turretCrosshairs[i] = newCrosshair.transform;
                turretCrosshairImages[i] = newCrosshair.GetComponent<Image>();
            }
        }
    }

    private void FixedUpdate()
    {
        if (manualControl)
        {
            if (_HUDSystem.combatHudActive)
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
                    Vector3 turretHitPoint;
                    if (turret.GetObstruction(out RaycastHit turretHit))
                    {
                        if (turretHit.transform != transform)
                            turretCrosshairImages[i].color = turretHoverColor;
                        else
                            turretCrosshairImages[i].color = turretBlockedColor;
                        turretHitPoint = turretHit.point;
                    }
                    else
                    {
                        turretCrosshairImages[i].color = turretNormalColor;
                        turretHitPoint = turret.firePoint.position + turret.firePoint.forward * 5000;
                    }

                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_HUDSystem.combatPanel, Camera.main.WorldToScreenPoint(turretHitPoint), Camera.main, out Vector3 turretCrosshairPos))
                    {
                        turretCrosshairs[i].position = turretCrosshairPos;
                    }
                }
            }
        }
        else
        {
            for (int i = targetsInRange.Count - 1; i >= 0; i--)
            {
                uint targetID = targetsInRange[i];
                if (!RadarRegistry.TryGet(targetsInRange[i], out var radarTarget))
                {
                    targetsInRange.RemoveAt(i);
                    continue;
                }

                if (radarTarget.turretsTargeting <= 0)
                {
                    if (_HUDSystem.TryGetValue(targetID, out HUDObject targetHUDObject))
                    {
                        targetHUDObject.SetTargetText("");
                        targetHUDObject.SetColor(radarTarget.originalHUDColor);
                    }
                    if (radarTarget.radarIcon != null)
                        radarTarget.radarIcon.SetColor(radarTarget.originalRadarColor, radarTarget.originalRadarEmission);
                    continue;
                }

                /*Vector3 relativeVelocity = ship.velocity - radarTarget.velocity;
                Vector3 relativeAcceleration = ship.acceleration - radarTarget.acceleration;
                float closingSpeed = Vector3.Dot(relativeVelocity, radarTarget.direction);
                float closingAcceleration = Vector3.Dot(relativeAcceleration, radarTarget.direction);

                float arrivalTime = CustomMethods.CalculateArrivalTime(closingAcceleration, closingSpeed, radarTarget.distance);
                string ETA = arrivalTime < 0 ? "Never" : CustomMethods.SecondsToFormattedString(arrivalTime, 2);
                string details = "Distance: " + CustomMethods.DistanceToFormattedString(radarTarget.distance, 2) +
                    "\nSpeed: " + CustomMethods.SpeedToFormattedString(radarTarget.velocity.magnitude, 2) +
                    "\nClosing Speed: " + CustomMethods.SpeedToFormattedString(closingSpeed, 2) +
                    "\nETA: " + ETA;*/

                if (_HUDSystem.TryGetValue(targetID, out HUDObject _targetHUDObject))
                {
                    //_HUDSystem.UpdateObject(targetID, radarTarget.transform, radarTarget.transform.name, details);
                    _targetHUDObject.SetTargetText(radarTarget.turretsTargeting.ToString());
                    if (radarTarget.originalHUDColor == Color.clear)
                        radarTarget.originalHUDColor = _targetHUDObject.GetColor();

                    _targetHUDObject.SetColor(targetingHUDColor);
                }
                /*else
                {
                    HUDObject newHUDObject = _HUDSystem.CreateObject(targetID, radarTarget.transform, radarTarget.transform.name, details);
                    newHUDObject.SetTargetText(radarTarget.turretsTargeting.ToString());
                    if (radarTarget.originalHUDColor == Color.clear)
                        radarTarget.originalHUDColor = newHUDObject.GetColor();
                    newHUDObject.SetColor(targetingHUDColor);
                }*/

                if (radarTarget.originalRadarColor == Color.clear)
                {
                    radarTarget.originalRadarColor = radarTarget.radarIcon.GetColor();
                    radarTarget.originalRadarEmission = radarTarget.radarIcon.GetEmission();
                }
                radarTarget.radarIcon.SetColor(targetingHUDColor, targetingHUDColor);
            }

            if (_HUDSystem.combatHudActive)
            {
                for (int i = 0; i < turrets.Length; i++)
                {
                    Turret turret = turrets[i];
                    Vector3 turretHitPoint;
                    if (turret.GetObstruction(out RaycastHit turretHit))
                    {
                        if (turretHit.transform != transform)
                            turretCrosshairImages[i].color = turretHoverColor;
                        else
                            turretCrosshairImages[i].color = turretBlockedColor;
                        turretHitPoint = turretHit.point;
                    }
                    else
                    {
                        turretCrosshairImages[i].color = turretNormalColor;
                        turretHitPoint = turret.firePoint.position + turret.firePoint.forward * 1000;
                    }

                    Vector3 screenHit = Camera.main.WorldToScreenPoint(turretHitPoint);
                    if (screenHit.z <= 0)
                    {
                        turretCrosshairImages[i].color = Color.clear;
                        continue;
                    }

                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_HUDSystem.combatPanel, screenHit, Camera.main, out Vector3 turretCrosshairPos))
                    {
                        turretCrosshairs[i].position = turretCrosshairPos;
                    }
                }
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
        if (!other.isTrigger && other.TryGetComponent<RadarTarget>(out var otherRadarTarget) && otherRadarTarget.GetID() != ship.GetID())
        {
            if (otherRadarTarget.team == ship.team)
                return;
            uint targetID = otherRadarTarget.GetID();
            targetsInRange.Add(targetID);
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
            uint targetID = otherRadarTarget.GetID();
            targetsInRange.Remove(targetID);
            for (int i = 0; i < turrets.Length; i++)
            {
                turrets[i].RemoveTarget(otherRadarTarget);
            }
        }
    }
}
