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
    [SerializeField] private int rangeIndex = 0;
    private List<RadarTarget> targetsInRange = new List<RadarTarget>();
    [SerializeField] private SphereCollider triggerCollider;

    [SerializeField] private GameObject iconParent;
    [SerializeField] private Vector3 hologramScale;

    [SerializeField] private GameObject shipIcon;
    [SerializeField] private Color friendlyShipColor;
    [SerializeField] private Color friendlyShipEmission;
    [SerializeField] private Color hostileShipColor;
    [SerializeField] private Color hostileShipEmission;

    [SerializeField] private GameObject pointIcon;
    [SerializeField] private Color projectileColor;
    [SerializeField] private Color projectileEmission;
    [SerializeField] private Color celestialBodyColor;
    [SerializeField] private Color celestialBodyEmission;

    private bool hologramActive = false;

    private void Start()
    {
        if(ship != null)
        {
            targetsInRange.Add(ship);
        }
    }

    private void FixedUpdate()
    {
        if (active)
        {
            for (int i = targetsInRange.Count - 1; i >= 0; i--)
            {
                RadarTarget radarTarget = targetsInRange[i];
                if (radarTarget == null)
                {
                    targetsInRange.RemoveAt(i);
                    continue;
                }

                if (radarTarget.scaledTransform != null && radarTarget.scaledTransform.inScaledSpace)
                {
                    double sqrDistance = (radarTarget.scaledTransform.realPosition - ship.scaledTransform.realPosition).sqrMagnitude;
                    if (sqrDistance > (radarRanges[rangeIndex] * radarRanges[rangeIndex]))
                    {
                        targetsInRange.RemoveAt(i);
                        continue;
                    }
                }

                uint targetID = radarTarget.GetID();
                if (targetID == ship.GetID())
                {
                    if (hologramActive)
                    {
                        if (ship.radarIcon != null)
                        {
                            ship.radarIcon.UpdateIcon(iconParent.transform.position, "You");
                        }
                        else
                        {
                            RadarIcon newIcon = Instantiate(shipIcon, iconParent.transform).GetComponent<RadarIcon>();
                            newIcon.Init(iconParent.transform.position, transform.rotation, friendlyShipColor, friendlyShipEmission, "You");
                            ship.radarIcon = newIcon;
                        }
                        //ship.radarIcon.model.rotation = transform.rotation;
                    }
                }
                else 
                {
                    Vector3d relativePosition;
                    // Floating world origin only updates scaled transform's position on origin shifts, so need to calculate offset manually
                    if (radarTarget.scaledTransform != null)
                    {
                        relativePosition = radarTarget.scaledTransform.realPosition - ship.scaledTransform.realPosition;
                    }
                    else
                    {
                        relativePosition = radarTarget.transform.position.ToVector3d() - ship.scaledTransform.realPosition;
                    }
                    double distance = relativePosition.magnitude;
                    Vector3 direction = (relativePosition / distance).ToVector3();

                    Vector3 relativeVelocity = ship.velocity - radarTarget.velocity;
                    Vector3 relativeAcceleration = ship.acceleration - radarTarget.acceleration;
                    float closingSpeed = Vector3.Dot(relativeVelocity, direction);
                    float closingAcceleration = Vector3.Dot(relativeAcceleration, direction);

                    float arrivalTime = CustomMethods.CalculateArrivalTime(closingAcceleration, closingSpeed, (float)distance);

                    // Update metrics for use in other places like the HUD
                    radarTarget.direction = direction;
                    radarTarget.arrivalTime = arrivalTime;
                    radarTarget.distance = (float)distance;
                    radarTarget.speed = radarTarget.velocity.magnitude;

                    string ETA = arrivalTime < 0.001f ? "Never" : CustomMethods.SecondsToFormattedString(arrivalTime, 2);
                    string details = "Distance: " + CustomMethods.DistanceToFormattedString(distance, 2) +
                        "\nSpeed: " + CustomMethods.SpeedToFormattedString(radarTarget.speed, 2) +
                        "\nClosing Speed: " + CustomMethods.SpeedToFormattedString(closingSpeed, 2) +
                        "\nETA: " + ETA;

                    if (!_HUDSystem.UpdateObject(targetID, radarTarget.transform, radarTarget.transform.name, details))
                    {
                        HUDObject newHUDObject = _HUDSystem.CreateObject(targetID, radarTarget.transform, radarTarget.transform.name, details);
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
                                newHUDObject.SetColor(projectileColor);
                                break;
                            case "Torpedo":
                                newHUDObject.SetColor(projectileColor);
                                break;
                            case "CelestialBody":
                                newHUDObject.SetColor(celestialBodyColor);
                                break;
                        }
                    }
                            
                    if(hologramActive)
                    {
                        // Display on radar hologram
                        Vector3 offset = direction * (float)(distance / radarRanges[rangeIndex] * 0.5);
                        offset.x *= hologramScale.x;
                        offset.y *= hologramScale.y;
                        offset.z *= hologramScale.z;

                        Debug.DrawRay(ship.transform.position, direction * (float)distance, Color.green, Time.fixedDeltaTime);

                        if (radarTarget.radarIcon != null)
                        {
                            radarTarget.radarIcon.UpdateIcon(iconParent.transform.position + offset, radarTarget.transform.rotation, radarTarget.transform.name + "\n" + CustomMethods.DistanceToFormattedString(distance, 2));
                        }
                        else
                        {
                            RadarIcon newIcon;
                            Color iconColor = friendlyShipColor;
                            Color iconEmission = friendlyShipEmission;
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
                                    iconColor = projectileColor;
                                    iconEmission = projectileEmission;
                                    break;
                                case "Torpedo":
                                    newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                                    iconColor = projectileColor;
                                    iconEmission = projectileEmission;
                                    break;
                                case "CelestialBody":
                                    newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                                    float iconRadius = (float)ship.scaledTransform.realScale.x / radarRanges[rangeIndex];
                                    if (iconRadius < 0.01f)
                                        iconRadius = 0.01f;
                                    newIcon.model.localScale = new Vector3(iconRadius, iconRadius, iconRadius);
                                    iconColor = celestialBodyColor;
                                    iconEmission = celestialBodyEmission;
                                    break;
                                default:
                                    newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                                    break;
                            }

                            newIcon.Init(iconParent.transform.position + offset, radarTarget.transform.rotation, iconColor, iconEmission, radarTarget.transform.name + "\n" + CustomMethods.DistanceToFormattedString(distance, 2));
                            radarTarget.radarIcon = newIcon;
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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.isTrigger && other.attachedRigidbody.TryGetComponent(out RadarTarget otherRadarTarget))
        {
            if (otherRadarTarget.scaledTransform != null && otherRadarTarget.scaledTransform.inScaledSpace)
            {
                double sqrDistance = (otherRadarTarget.scaledTransform.realPosition - ship.scaledTransform.realPosition).sqrMagnitude;
                Debug.Log($"Scaled space radar hit: {other.name} dst: {sqrDistance}");
                if (sqrDistance <= (radarRanges[rangeIndex] * radarRanges[rangeIndex]))
                {
                    targetsInRange.Add(otherRadarTarget);
                }
            }
            else
            {
                targetsInRange.Add(otherRadarTarget);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.isTrigger && other.attachedRigidbody.TryGetComponent(out RadarTarget otherRadarTarget))
        {
            targetsInRange.Remove(otherRadarTarget);
        }
    }
}
