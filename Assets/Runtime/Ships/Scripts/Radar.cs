using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

public class Radar : MonoBehaviour
{
    private bool active = false;
    private bool hologramActive = false;

    [SerializeField] private Ship ship;
    [SerializeField] private ShipGUI shipGUI;
    [SerializeField] private HUDSystem _HUDSystem;
    [SerializeField] private AlertSystem alertSystem;

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

    [Serializable]
    public struct ConfigSetting
    {
        public string tag;
        public bool on;
        public double radius;
    }

    [SerializeField] private ConfigSetting[] detectInits;
    [SerializeField] private ConfigSetting[] alertInits;
    [SerializeField] private ConfigSetting[] killInits;

    private Dictionary<string, ConfigSetting> detectConfigs = new Dictionary<string, ConfigSetting>();
    private Dictionary<string, ConfigSetting> alertConfigs = new Dictionary<string, ConfigSetting>();
    private Dictionary<string, ConfigSetting> killConfigs = new Dictionary<string, ConfigSetting>();

    private void Start()
    {
        if(ship != null)
        {
            RadarIcon newIcon = Instantiate(shipIcon, iconParent.transform).GetComponent<RadarIcon>();
            newIcon.Init(iconParent.transform.position, transform.rotation, friendlyShipColor, friendlyShipEmission, "", true);
            ship.radarTarget.radarIcon = newIcon;
        }
        for(int i = 0; i < detectInits.Length; i++)
        {
            string tag = detectInits[i].tag;
            detectConfigs.Add(tag, detectInits[i]);
        }
        for(int i = 0; i < alertInits.Length; i++)
        {
            string tag = alertInits[i].tag;
            alertConfigs.Add(tag, alertInits[i]);
        }
        for(int i = 0; i < killInits.Length; i++)
        {
            string tag = killInits[i].tag;
            killConfigs.Add(tag, killInits[i]);
        }
    }

    private void FixedUpdate()
    {
        if (active)
        {
            for (int i = targetsInTrigger.Count - 1; i >= 0; i--)
            {
                uint targetID = targetsInTrigger[i];
                if (targetID == ship.radarTarget.GetID())
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
                    continue;

                if (detectConfigs.TryGetValue(radarTarget.tag, out var detectConfig) && !detectConfig.on)
                    continue;

                double distance = Math.Sqrt(sqrDistance);
                Vector3d direction = relativePosition / distance;

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
                        radarTarget.radarIcon.UpdateIcon(
                            iconParent.transform.position + offset, 
                            radarTarget.transform.rotation, 
                            radarTarget.transform.name + "\n" + SpaceMath.DistanceToFormattedString(distance, "F2"));
                        if (radarTarget.CompareTag("CelestialBody"))
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
                        if (alertConfigs.TryGetValue(radarTarget.tag, out var alertConfig) && alertConfig.on)
                        {
                            if (radarTarget.alertWhenTargeting)
                            {
                                alertSystem.NewContact();
                            }
                            else
                            {
                                alertSystem.NewSpecialContact();
                            }
                        }
                        switch (radarTarget.tag)
                        {
                            case "Ship":
                                newIcon = Instantiate(shipIcon, iconParent.transform).GetComponent<RadarIcon>();
                                if (radarTarget.team == ship.radarTarget.team)
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
                                if (radarTarget.team == ship.radarTarget.team)
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
                                if (radarTarget.team == ship.radarTarget.team)
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
                                if (radarTarget.alertWhenTargeting)
                                    alertSystem.NewContact();
                                else
                                    alertSystem.NewSpecialContact();
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
                            radarTarget.transform.name + "\n" + SpaceMath.DistanceToFormattedString(distance, "F2"),
                            false
                        );
                        radarTarget.radarIcon = newIcon;
                    }
                }

                if (_HUDSystem.radarHudActive)
                {
                    Vector3d relativeAcceleration = radarTarget.acceleration - ship.radarTarget.acceleration;
                    Vector3d relativeVelocity = radarTarget.doubleRigidbody.velocity - ship.doubleRigidbody.velocity;
                    // Negative closing => moving away, Positive closing => coming closer
                    double closingVelocity = -Vector3d.Dot(relativeVelocity, direction);
                    double closingAcceleration = -Vector3d.Dot(relativeAcceleration, direction);

                    double arrivalTime = SpaceMath.CalculateArrivalTime(distance, closingVelocity, closingAcceleration);

                    string ETA = arrivalTime < 0.0 ? "Never" : SpaceMath.SecondsToFormattedString(arrivalTime, "F2");
                    string details = "<b>" + radarTarget.name + "</b>" +
                        "\nDST " + SpaceMath.DistanceToFormattedString(distance, "F2") +
                        "\nSPD " + SpaceMath.SpeedToFormattedString((float)radarTarget.doubleRigidbody.velocity.magnitude, "F2") +
                        "\nCLS " + SpaceMath.SpeedToFormattedString(closingVelocity, "F2") +
                        "\nETA " + ETA;

                    double predictTime = arrivalTime < 0.0 ? distance / 25.0 : arrivalTime;
                    Vector3d predictedPosition = radarTarget.doubleRigidbody.scaledTransform.realPosition + radarTarget.doubleRigidbody.velocity * predictTime + 0.5 * predictTime * predictTime * radarTarget.acceleration;

                    if (!_HUDSystem.UpdateObject(radarTarget, details, predictedPosition))
                    {
                        HUDObject newHUDObject = _HUDSystem.CreateObject(radarTarget, details, predictedPosition);
                        switch (radarTarget.transform.tag)
                        {
                            case "Ship":
                                if (radarTarget.team == ship.radarTarget.team)
                                {
                                    newHUDObject.SetColor(friendlyShipColor);
                                }
                                else
                                {
                                    newHUDObject.SetColor(hostileShipColor);
                                }
                                break;
                            case "Projectile":
                                if (radarTarget.team == ship.radarTarget.team)
                                {
                                    newHUDObject.SetColor(friendlyProjectileColor);
                                }
                                else
                                {
                                    newHUDObject.SetColor(hostileProjectileColor);
                                }
                                break;
                            case "Torpedo":
                                if (radarTarget.team == ship.radarTarget.team)
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
        ship.radarTarget.radarIcon.model.localScale = 2 * iconRadii[rangeIndex] * Vector3.one;
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

    public void SetDetectOn(string tag)
    {
        if (!detectConfigs.TryGetValue(tag, out var config))
            return;
        config.on = true;
        detectConfigs[tag] = config;
    }

    public void SetDetectOff(string tag)
    {
        if (!detectConfigs.TryGetValue(tag, out var config))
            return;
        config.on = false;
        detectConfigs[tag] = config;
    }

    public void SetDetectRadius(string tag, double radius)
    {
        if (!detectConfigs.TryGetValue(tag, out var config))
            return;
        config.radius = radius;
        detectConfigs[tag] = config;
    }

    public void SetAlertOn(string tag)
    {
        if (!alertConfigs.TryGetValue(tag, out var config))
            return;
        config.on = true;
        alertConfigs[tag] = config;
    }

    public void SetAlertOff(string tag)
    {
        if (!alertConfigs.TryGetValue(tag, out var config))
            return;
        config.on = false;
        alertConfigs[tag] = config;
    }
}
