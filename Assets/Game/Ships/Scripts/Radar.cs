using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;

public class Radar : MonoBehaviour
{
    [SerializeField] private Ship ship;
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

                if (radarTarget.doubleRigidbody.scaledTransform != null)
                {
                    double sqrDistance = (radarTarget.doubleRigidbody.scaledTransform.realPosition - ship.doubleRigidbody.scaledTransform.realPosition).sqrMagnitude;
                    if (sqrDistance > (radarRanges[rangeIndex] * radarRanges[rangeIndex]))
                    {
                        continue;
                    }
                }

                Vector3d relativePosition;
                // Floating world origin only updates scaled transform's position on origin shifts, so need to calculate offset manually
                if (radarTarget.doubleRigidbody.scaledTransform != null)
                {
                    relativePosition = radarTarget.doubleRigidbody.scaledTransform.realPosition - ship.doubleRigidbody.scaledTransform.realPosition;
                }
                else
                {
                    relativePosition = radarTarget.transform.position.ToVector3d() - ship.doubleRigidbody.scaledTransform.realPosition;
                }
                double distance = relativePosition.magnitude;
                Vector3 direction = (relativePosition / distance).ToVector3();

                RadarTarget.Metrics shipMetrics = ship.GetMetrics();
                RadarTarget.Metrics targetMetrics = radarTarget.GetMetrics();

                Vector3 relativeVelocity = ship.doubleRigidbody.velocity.ToVector3() - radarTarget.doubleRigidbody.velocity.ToVector3();
                Vector3 relativeAcceleration = shipMetrics.acceleration.ToVector3() - targetMetrics.acceleration.ToVector3();
                // Positive closing => getting closer, Negative closing => moving away
                float closingSpeed = Vector3.Dot(relativeVelocity, direction);
                float closingAcceleration = Vector3.Dot(relativeAcceleration, direction);

                float arrivalTime = SpaceMath.CalculateArrivalTime(closingAcceleration, closingSpeed, (float)distance);

                // Update metrics for use in other places like the HUD
                float speed = (float)radarTarget.doubleRigidbody.velocity.magnitude;
                radarTarget.UpdateMetrics(direction, (float)distance, speed, closingSpeed, arrivalTime);

                string ETA = arrivalTime < 0.0001f ? "Never" : SpaceMath.SecondsToFormattedString(arrivalTime, 2);
                string details = "<b>" + radarTarget.name + "</b>" +
                    "\nDST " + SpaceMath.DistanceToFormattedString(distance, 2) +
                    "\nSPD " + SpaceMath.SpeedToFormattedString(speed, 2) +
                    "\nREL " + SpaceMath.SpeedToFormattedString(closingSpeed, 2) +
                    "\nETA " + ETA;

                if (hologramActive)
                {
                    // Display on radar hologram
                    Vector3 offset = direction * (float)(distance / radarRanges[rangeIndex] * 0.5);
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
                                newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
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
                ship.UIAnimator.SetTrigger("CloseRadar");
                break;
            case 1:
                hologramActive = true;
                iconParent.SetActive(true);
                active = true;
                ship.UIAnimator.SetTrigger("OpenRadar");
                break;
        }
        triggerCollider.radius = radarRanges[rangeIndex];
        ship.radarIcon.model.localScale = 2 * iconRadii[rangeIndex] * Vector3.one;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.isTrigger && other.attachedRigidbody.TryGetComponent(out RadarTarget otherRadarTarget) && !targetsInTrigger.Contains(otherRadarTarget.GetID()))
        {
            targetsInTrigger.Add(otherRadarTarget.GetID());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.isTrigger && other.attachedRigidbody.TryGetComponent(out RadarTarget otherRadarTarget))
        {
            targetsInTrigger.Remove(otherRadarTarget.GetID());
        }
    }
}
