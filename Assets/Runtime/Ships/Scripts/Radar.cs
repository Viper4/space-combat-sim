using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

public class Radar : MonoBehaviour
{
    [SerializeField] private Ship ship;
    [SerializeField] private ShipGUI shipGUI;
    [SerializeField] private HUDSystem _HUDSystem;

    public bool active = true;
    [SerializeField] private float[] radarRanges;
    private int rangeIndex = 0;
    [SerializeField] private float[] iconRadii;
    private List<uint> targetsInTrigger = new List<uint>();
    [SerializeField] private SphereCollider triggerCollider;

    [SerializeField] private GameObject iconParent;
    [SerializeField] private Vector3 hologramScale;

    [SerializeField] private GameObject shipIcon;
    [SerializeField] private Color friendlyShipColor;
    [SerializeField] private Color friendlyShipEmission;
    [SerializeField] private Color hostileShipColor;
    [SerializeField] private Color hostileShipEmission;

    [SerializeField] private GameObject pointIcon;

    [SerializeField] private Color friendlyProjectileColor;
    [SerializeField] private Color friendlyProjectileEmission;
    [SerializeField] private Color hostileProjectileColor;
    [SerializeField] private Color hostileProjectileEmission;

    [SerializeField] private GameObject realScaleIcon;
    [SerializeField] private Color celestialBodyColor;
    [SerializeField] private Color celestialBodyEmission;

    private bool hologramActive = false;

    private void Start()
    {
        if(ship != null)
        {
            RadarIcon newIcon = Instantiate(shipIcon, iconParent.transform).GetComponent<RadarIcon>();
            newIcon.Init(iconParent.transform.position, transform.rotation, friendlyShipColor, friendlyShipEmission, "", true);
            ship.radarIcon = newIcon;
        }
    }

    private void FixedUpdate()
    {
        if (active)
        {
            for (int i = targetsInTrigger.Count - 1; i >= 0; i--)
            {
                uint targetID = targetsInTrigger[i];
                if (targetID == ship.GetID())
                {
                    targetsInTrigger.RemoveAt(i);
                    continue;
                }
                if (!RadarRegistry.TryGet(targetID, out var radarTarget))
                {
                    targetsInTrigger.RemoveAt(i);
                    continue;
                }
                Vector3d relativePosition = radarTarget.doubleRigidbody.scaledTransform.realPosition - ship.doubleRigidbody.scaledTransform.realPosition;
                double sqrDistance = relativePosition.sqrMagnitude;

                if (sqrDistance > (radarRanges[rangeIndex] * radarRanges[rangeIndex])
                || (radarTarget.stealthDistance >= 0f && sqrDistance > radarTarget.stealthDistance * radarTarget.stealthDistance))
                {
                    continue;
                }

                double distance = Math.Sqrt(sqrDistance);
                Vector3d direction = relativePosition / distance;
                Vector3d relativeAcceleration = radarTarget.acceleration - ship.acceleration;
                Vector3d relativeVelocity = radarTarget.doubleRigidbody.velocity - ship.doubleRigidbody.velocity;
                // Negative closing => moving away, Positive closing => coming closer
                double closingSpeed = -Vector3d.Dot(relativeVelocity, direction);
                double closingAcceleration = -Vector3d.Dot(relativeAcceleration, direction);

                double arrivalTime = SpaceMath.CalculateArrivalTime(distance, closingSpeed, closingAcceleration);

                // Update metrics for use in other places like the HUD
                float speed = (float)radarTarget.doubleRigidbody.velocity.magnitude;
                radarTarget.UpdateMetrics(direction.ToVector3(), distance, speed, closingSpeed, arrivalTime);

                string ETA = arrivalTime < 0.0001f ? "Never" : SpaceMath.SecondsToFormattedString(arrivalTime, 2);
                string details = "<b>" + radarTarget.name + "</b>" +
                    "\nDST " + SpaceMath.DistanceToFormattedString(distance, 2) +
                    "\nSPD " + SpaceMath.SpeedToFormattedString(speed, 2) +
                    "\nREL " + SpaceMath.SpeedToFormattedString(closingSpeed, 2) +
                    "\nETA " + ETA;

                if (hologramActive)
                {
                    // Display on radar hologram
                    Vector3 offset = direction.ToVector3() * (float)(distance / radarRanges[rangeIndex] * 0.5);
                    offset.x *= hologramScale.x;
                    offset.y *= hologramScale.y;
                    offset.z *= hologramScale.z;
                    Vector3 iconScale = 2 * iconRadii[rangeIndex] * Vector3.one;

                    if (radarTarget.radarIcon != null)
                    {
                        radarTarget.radarIcon.UpdateIcon(iconParent.transform.position + offset, radarTarget.transform.rotation, radarTarget.transform.name + "\n" + SpaceMath.DistanceToFormattedString(distance, 2));
                        if (radarTarget.transform.CompareTag("CelestialBody"))
                        {
                            Vector3d realScale = radarTarget.doubleRigidbody.scaledTransform.realScale;
                            iconScale = new Vector3(
                                (float)(realScale.x / radarRanges[rangeIndex] * hologramScale.x),
                                (float)(realScale.y / radarRanges[rangeIndex] * hologramScale.y),
                                (float)(realScale.z / radarRanges[rangeIndex] * hologramScale.z)
                            );
                        }
                        radarTarget.radarIcon.model.localScale = iconScale;
                    }
                    else
                    {
                        RadarIcon newIcon;
                        Color iconColor;
                        Color iconEmission;
                        switch (radarTarget.transform.tag)
                        {
                            case "Ship":
                                newIcon = Instantiate(shipIcon, iconParent.transform).GetComponent<RadarIcon>();
                                if (radarTarget.team == ship.team)
                                {
                                    iconColor = friendlyShipColor;
                                    iconEmission = friendlyShipEmission;
                                }
                                else
                                {
                                    iconColor = hostileShipColor;
                                    iconEmission = hostileShipEmission;
                                }
                                break;
                            case "Projectile":
                                newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                                if (radarTarget.team == ship.team)
                                {
                                    iconColor = friendlyProjectileColor;
                                    iconEmission = friendlyProjectileEmission;
                                }
                                else
                                {
                                    iconColor = hostileProjectileColor;
                                    iconEmission = hostileProjectileEmission;
                                }
                                break;
                            case "Torpedo":
                                newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                                if (radarTarget.team == ship.team)
                                {
                                    iconColor = friendlyProjectileColor;
                                    iconEmission = friendlyProjectileEmission;
                                }
                                else
                                {
                                    iconColor = hostileProjectileColor;
                                    iconEmission = hostileProjectileEmission;
                                }
                                break;
                            case "CelestialBody":
                                newIcon = Instantiate(realScaleIcon, iconParent.transform).GetComponent<RadarIcon>();
                                iconColor = celestialBodyColor;
                                iconEmission = celestialBodyEmission;
                                Vector3d realScale = radarTarget.doubleRigidbody.scaledTransform.realScale;
                                iconScale = new Vector3(
                                    (float)(realScale.x / radarRanges[rangeIndex] * hologramScale.x),
                                    (float)(realScale.y / radarRanges[rangeIndex] * hologramScale.y),
                                    (float)(realScale.z / radarRanges[rangeIndex] * hologramScale.z)
                                );
                                break;
                            default:
                                newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                                iconColor = Color.white;
                                iconEmission = Color.white;
                                break;
                        }
                        newIcon.model.localScale = iconScale;

                        newIcon.Init(
                            iconParent.transform.position + offset, 
                            radarTarget.transform.rotation, 
                            iconColor, 
                            iconEmission, 
                            radarTarget.transform.name + "\n" + SpaceMath.DistanceToFormattedString(distance, 2),
                            false
                        );
                        radarTarget.radarIcon = newIcon;
                    }
                }

                if (_HUDSystem.radarHudActive)
                {
                    if (!_HUDSystem.UpdateObject(targetID, radarTarget, details))
                    {
                        HUDObject newHUDObject = _HUDSystem.CreateObject(targetID, radarTarget, details);
                        switch (radarTarget.transform.tag)
                        {
                            case "Ship":
                                if (radarTarget.team == ship.team)
                                {
                                    newHUDObject.SetColor(friendlyShipColor);
                                }
                                else
                                {
                                    newHUDObject.SetColor(hostileShipColor);
                                }
                                break;
                            case "Projectile":
                                if (radarTarget.team == ship.team)
                                {
                                    newHUDObject.SetColor(friendlyProjectileColor);
                                }
                                else
                                {
                                    newHUDObject.SetColor(hostileProjectileColor);
                                }
                                break;
                            case "Torpedo":
                                if (radarTarget.team == ship.team)
                                {
                                    newHUDObject.SetColor(friendlyProjectileColor);
                                }
                                else
                                {
                                    newHUDObject.SetColor(hostileProjectileColor);
                                }
                                break;
                            case "CelestialBody":
                                newHUDObject.SetColor(celestialBodyColor);
                                break;
                            default:
                                newHUDObject.SetColor(Color.white);
                                break;
                        }
                    }
                }
            }
        }
    }

    public void ToggleScale(int state)
    {
        rangeIndex = state;
        switch (state)
        {
            case 0:
                hologramActive = false;
                iconParent.SetActive(false);
                active = false;
                shipGUI.ToggleRadarActive(false);
                break;
            case 1:
                hologramActive = true;
                iconParent.SetActive(true);
                active = true;
                shipGUI.ToggleRadarActive(true);
                break;
        }
        triggerCollider.radius = radarRanges[rangeIndex];
        ship.radarIcon.model.localScale = 2 * iconRadii[rangeIndex] * Vector3.one;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody.TryGetComponent(out RadarTarget otherRadarTarget) && !targetsInTrigger.Contains(otherRadarTarget.GetID()))
        {
            targetsInTrigger.Add(otherRadarTarget.GetID());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody.TryGetComponent(out RadarTarget otherRadarTarget))
        {
            targetsInTrigger.Remove(otherRadarTarget.GetID());
        }
    }
}
