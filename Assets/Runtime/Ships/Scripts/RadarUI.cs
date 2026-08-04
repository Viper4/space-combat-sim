using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

public class RadarUI : MonoBehaviour
{
    [SerializeField] private Radar radar;
    [SerializeField] private Ship ship;

    [SerializeField] private float[] iconRadii;
    [SerializeField] private GameObject iconParent;
    [SerializeField] private GameObject passiveEmissionIndicator;
    [SerializeField] private GameObject activeRadarIndicator;
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

    [SerializeField] private float activeAnimationSpeed = 0.5f;

    private void Start()
    {
        if (ship != null)
        {
            RadarIcon newIcon = Instantiate(shipIcon, iconParent.transform).GetComponent<RadarIcon>();
            newIcon.Init(iconParent.transform.position, transform.rotation, friendlyShipColor, friendlyShipEmission, "", true);
            ship.attachedRadarTarget.radarIcon = newIcon;
        }
    }

    private void LateUpdate()
    {
        if (!radar.IsEnabled)
        {
            if (iconParent.activeSelf)
                iconParent.SetActive(false);
            return;
        }

        if (!iconParent.activeSelf)
            iconParent.SetActive(true);
        
        double radarRange = radar.GetCurrentRange();
        double passiveEmissionRadius = ship.attachedRadarTarget.GetEmissionTriggerRadius();
        if (passiveEmissionRadius <= 0.0 && passiveEmissionIndicator.activeSelf)
        {
            passiveEmissionIndicator.SetActive(false);
        }
        else
        {
            if (!passiveEmissionIndicator.activeSelf)
                passiveEmissionIndicator.SetActive(true);
            passiveEmissionIndicator.transform.localScale = (float)(passiveEmissionRadius / radarRange) * Vector3.one;
        }

        if (radar.IsActive)
        {
            activeRadarIndicator.transform.localScale += activeAnimationSpeed * Time.deltaTime * Vector3.one;
            if (activeRadarIndicator.transform.localScale.sqrMagnitude > 3) // (1, 1, 1)
                activeRadarIndicator.transform.localScale = Vector3.zero;
        }

        foreach(RadarTarget radarTarget in radar.GetAllDetectedTargets())
        {
            Vector3d relativePosition = radarTarget.scaledRigidbody.scaledTransform.realPosition - ship.scaledRigidbody.scaledTransform.realPosition;
            double distance = relativePosition.magnitude;
            Vector3d direction = relativePosition / distance;
            
            // Display on radar hologram
            Vector3 offset = direction.ToVector3() * (float)(distance / radarRange * 0.5);
            offset.x *= hologramScale.x;
            offset.y *= hologramScale.y;
            offset.z *= hologramScale.z;
            Vector3 iconScale = 2 * iconRadii[radar.emitLevel] * Vector3.one;
            string iconText = "";

            if (radarTarget.radarIcon != null)
            {
                switch (radarTarget.tag)
                {
                    case "Ship":
                        iconText = radarTarget.transform.name + "\n" + SpaceMath.DistanceToFormattedString(distance, "F2");
                        break;
                    case "Projectile":
                        iconText = "PRJ\n" + SpaceMath.DistanceToFormattedString(distance, "F2");
                        break;
                    case "Torpedo":
                        iconText = "TRP\n" + SpaceMath.DistanceToFormattedString(distance, "F2");
                        break;
                    case "CelestialBody":
                        Vector3d realScale = radarTarget.scaledRigidbody.scaledTransform.realScale;
                        iconScale = new Vector3(
                            (float)(realScale.x / radarRange * hologramScale.x),
                            (float)(realScale.y / radarRange * hologramScale.y),
                            (float)(realScale.z / radarRange * hologramScale.z)
                        );
                        break;
                }
                radarTarget.radarIcon.UpdateIcon(
                    iconParent.transform.position + offset, 
                    radarTarget.transform.rotation, 
                    iconText);
                radarTarget.radarIcon.model.localScale = iconScale;
            }
            else
            {
                RadarIcon newIcon;
                Color iconColor;
                Color iconEmission;
                switch (radarTarget.tag)
                {
                    case "Ship":
                        iconText = radarTarget.transform.name + "\n" + SpaceMath.DistanceToFormattedString(distance, "F2");
                        newIcon = Instantiate(shipIcon, iconParent.transform).GetComponent<RadarIcon>();
                        if (radarTarget.team == ship.attachedRadarTarget.team)
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
                        iconText = "PRJ\n" + SpaceMath.DistanceToFormattedString(distance, "F2");
                        newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                        if (radarTarget.team == ship.attachedRadarTarget.team)
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
                        iconText = "TRP\n" + SpaceMath.DistanceToFormattedString(distance, "F2");
                        newIcon = Instantiate(pointIcon, iconParent.transform).GetComponent<RadarIcon>();
                        if (radarTarget.team == ship.attachedRadarTarget.team)
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
                        Vector3d realScale = radarTarget.scaledRigidbody.scaledTransform.realScale;
                        iconScale = new Vector3(
                            (float)(realScale.x / radarRange * hologramScale.x),
                            (float)(realScale.y / radarRange * hologramScale.y),
                            (float)(realScale.z / radarRange * hologramScale.z)
                        );
                        break;
                    default:
                        newIcon = Instantiate(realScaleIcon, iconParent.transform).GetComponent<RadarIcon>();
                        iconColor = Color.white;
                        iconEmission = Color.white;
                        double realRadius = radarTarget.scaledRigidbody.scaledTransform.realRadius;
                        iconScale = new Vector3(
                            (float)(realRadius / radarRange * hologramScale.x),
                            (float)(realRadius / radarRange * hologramScale.y),
                            (float)(realRadius / radarRange * hologramScale.z)
                        );
                        break;
                }
                newIcon.model.localScale = iconScale;

                newIcon.Init(
                    iconParent.transform.position + offset, 
                    radarTarget.transform.rotation, 
                    iconColor, 
                    iconEmission, 
                    iconText,
                    false
                );
                radarTarget.radarIcon = newIcon;
            }

            if (HUDSystem.Instance.radarHudActive)
            {
                Vector3d relativeAcceleration = radarTarget.acceleration - ship.attachedRadarTarget.acceleration;
                Vector3d relativeVelocity = radarTarget.scaledRigidbody.velocity - ship.scaledRigidbody.velocity;
                // Negative closing => moving away, Positive closing => coming closer
                double closingVelocity = -Vector3d.Dot(relativeVelocity, direction);
                double closingAcceleration = -Vector3d.Dot(relativeAcceleration, direction);

                double arrivalTime = SpaceMath.CalculateArrivalTime(distance, closingVelocity, closingAcceleration);

                string ETA = arrivalTime < 0.0 ? "Never" : SpaceMath.SecondsToFormattedString(arrivalTime, "F2");
                string details = "<b>" + radarTarget.name + "</b>" +
                    "\nDST " + SpaceMath.DistanceToFormattedString(distance, "F2") +
                    "\nSPD " + SpaceMath.SpeedToFormattedString((float)radarTarget.scaledRigidbody.velocity.magnitude, "F2") +
                    "\nCLS " + SpaceMath.SpeedToFormattedString(closingVelocity, "F2") +
                    "\nETA " + ETA;

                double predictTime = arrivalTime < 0.0 ? distance * 0.0025f : arrivalTime;
                Vector3d predictedPosition = radarTarget.scaledRigidbody.scaledTransform.realPosition + radarTarget.scaledRigidbody.velocity * predictTime + 0.5 * predictTime * predictTime * radarTarget.acceleration;

                if (!HUDSystem.Instance.UpdateObject(radarTarget, details, predictedPosition))
                {
                    HUDObject newHUDObject = HUDSystem.Instance.CreateObject(radarTarget, details, predictedPosition);
                    switch (radarTarget.transform.tag)
                    {
                        case "Ship":
                            if (radarTarget.team == ship.attachedRadarTarget.team)
                            {
                                newHUDObject.SetColor(friendlyShipColor);
                            }
                            else
                            {
                                newHUDObject.SetColor(hostileShipColor);
                            }
                            break;
                        case "Projectile":
                            if (radarTarget.team == ship.attachedRadarTarget.team)
                            {
                                newHUDObject.SetColor(friendlyProjectileColor);
                            }
                            else
                            {
                                newHUDObject.SetColor(hostileProjectileColor);
                            }
                            break;
                        case "Torpedo":
                            if (radarTarget.team == ship.attachedRadarTarget.team)
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

    public void SetRange()
    {
        activeRadarIndicator.SetActive(radar.IsActive);
        ship.attachedRadarTarget.radarIcon.model.gameObject.SetActive(radar.IsActive);
        ship.attachedRadarTarget.radarIcon.model.localScale = 2 * iconRadii[radar.emitLevel] * Vector3.one;
    }
}
