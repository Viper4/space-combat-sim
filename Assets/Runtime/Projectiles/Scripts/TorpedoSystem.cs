using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SpaceStuff;

[RequireComponent(typeof(Ship))]
public class TorpedoSystem : MonoBehaviour
{
    private Ship ship;
    private TurretSystem turretSystem;

    [SerializeField] private TorpedoPoint[] torpedoPoints;
    [SerializeField] private Image[] torpedoIcons;
    [SerializeField] private Color baseUIColor;
    [SerializeField] private Animation bayDoorAnimation;
    public bool torpedoBayDoorOpen = false;

    void Start()
    {
        ship = GetComponent<Ship>();
        TryGetComponent(out turretSystem);
    }

    void Update()
    {
        
    }

    public void TorpedoBaySwitch(int state)
    {
        torpedoBayDoorOpen = state == 1;
        if (torpedoBayDoorOpen)
        {
            bayDoorAnimation.Play("CloseTorpedoBay");
        }
        else
        {
            bayDoorAnimation.Play("OpenTorpedoBay");
        }
    }

    public void FireTorpedo(RadarTarget target)
    {
        if (torpedoBayDoorOpen)
        {
            Debug.Log("Fire Torpedo");
            for (int i = 0; i < torpedoPoints.Length; i++)
            {
                if (torpedoPoints[i].hasTorpedo)
                {
                    torpedoPoints[i].LaunchTorpedo(ship.doubleRigidbody.velocity.ToVector3(), target, i, ship.team);
                    UpdateTorpedoUI(i, false);
                    break;
                }
            }
        }
    }

    public void UpdateTorpedoUI(int torpedoIndex, bool active)
    {
        torpedoIcons[torpedoIndex].color = active ? baseUIColor : Color.black;
    }
}
