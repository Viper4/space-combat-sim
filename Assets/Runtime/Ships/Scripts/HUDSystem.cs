using System.Collections;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;
using UnityEngine.UI;

public class HUDSystem : MonoBehaviour
{
    public bool radarHudActive = false;
    public bool combatHudActive = false;
    [SerializeField] private GameObject radarHudObjectPrefab;
    [SerializeField] private GameObject radarHudParent;
    [SerializeField] private float RadarHUDDistance = 1.5f;
    public RectTransform combatPanel;
    [SerializeField] private float combatHUDDistance = 1.0f;
    public Image targetCrosshair;

    private Dictionary<uint, HUDObject> radarIDHUDPair = new Dictionary<uint, HUDObject>();

    private void Start()
    {
        radarHudParent.SetActive(radarHudActive);
        combatPanel.gameObject.SetActive(combatHudActive);
        targetCrosshair.gameObject.SetActive(false);
    }

    private Vector3 CalculateHUDPosition(Vector3d realPosition, string tag)
    {
        Vector3d camRealPos = FloatingWorldOrigin.Instance.worldOriginPosition + Camera.main.transform.position.ToVector3d();
        Vector3 direction = (realPosition - camRealPos).normalized.ToVector3();
        var distanceOffset = tag switch
        {
            "Projectile" => -0.2f, // Projectiles highest priority
            "Ship" => -0.1f,
            _ => 0.0f,
        };
        Vector3 position = Camera.main.transform.position + direction * (RadarHUDDistance + distanceOffset);
        return position;
    }

    public HUDObject CreateObject(uint ID, RadarTarget target, string details)
    {
        Vector3 position = CalculateHUDPosition(target.doubleRigidbody.scaledTransform.realPosition, target.tag);
        HUDObject newHUDObject = Instantiate(radarHudObjectPrefab, radarHudParent.transform).GetComponent<HUDObject>();
        if (!target.doubleRigidbody.scaledTransform.visible)
        {
            newHUDObject.Init(this, position, ID, details);
            return newHUDObject;
        }

        Quadrilateral quad;
        if (target.useScaleForBounds)
        {
            // Use ellipse based on lossy scale of target's transform and its rotation
            quad = SpaceGeometry.GetEllipsoidBoundingBox(target.transform.position, target.transform.lossyScale, target.transform.rotation, Camera.main);
        }
        else
        {
            // Calculate bounding box based on renderers
            quad = SpaceGeometry.GetMinimumBoundingBox(target.boundsRenderers, Camera.main);
        }
        newHUDObject.Init(this, position, quad, ID, details);
        return newHUDObject;
    }

    public bool UpdateObject(uint ID, RadarTarget target, string details)
    {
        if (radarIDHUDPair.TryGetValue(ID, out HUDObject HUDObject))
        {
            Vector3 position = CalculateHUDPosition(target.doubleRigidbody.scaledTransform.realPosition, target.tag);

            if (!target.doubleRigidbody.scaledTransform.visible)
            {
                HUDObject.UpdateObject(position, details);
                return true;
            }

            Quadrilateral quad;
            if (target.useScaleForBounds)
            {
                // Use ellipse based on lossy scale of target's transform and its rotation
                quad = SpaceGeometry.GetEllipsoidBoundingBox(target.transform.position, target.transform.lossyScale, target.transform.rotation, Camera.main);
            }
            else
            {
                // Calculate bounding box based on renderers
                quad = SpaceGeometry.GetMinimumBoundingBox(target.boundsRenderers, Camera.main);
            }

            HUDObject.UpdateObject(position, quad, details);
            return true;
        }
        return false;
    }

    public bool TryGetValue(uint ID, out HUDObject HUDObject)
    {
        return radarIDHUDPair.TryGetValue(ID, out HUDObject);
    }

    public void Add(uint ID, HUDObject HUDObject)
    {
        radarIDHUDPair.Add(ID, HUDObject);
    }

    public void Remove(uint ID)
    {
        radarIDHUDPair.Remove(ID);
    }

    public void ToggleRadarHUD(int state)
    {
        radarHudActive = state == 1;
        radarHudParent.SetActive(radarHudActive);
    }

    public void ToggleCombatHUD(int state)
    {
        combatHudActive = state == 1;
        combatPanel.gameObject.SetActive(combatHudActive);
    }
}
