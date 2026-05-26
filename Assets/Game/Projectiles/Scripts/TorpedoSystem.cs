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
        if (torpedoBayDoorOpen)
        {
            torpedoBayDoorOpen = false;
            bayDoorAnimation.Play("CloseTorpedoBay");
        }
        else
        {
            torpedoBayDoorOpen = true;
            bayDoorAnimation.Play("OpenTorpedoBay");
        }
    }

    public void FireTorpedo(Transform target)
    {
        if (torpedoBayDoorOpen)
        {
            Debug.Log("Fire Torpedo");
            for (int i = 0; i < torpedoPoints.Length; i++)
            {
                if (torpedoPoints[i].hasTorpedo)
                {
                    Collider torpedoCollider = torpedoPoints[i].LaunchTorpedo(ship.doubleRigidbody.velocity.ToVector3(), target, i).GetComponent<Collider>();
                    UpdateTorpedoUI(i, false);
                    if(turretSystem != null)
                    {
                        // Prevent turrets from shooting at our own torpedos
                        foreach (Turret turret in turretSystem.turrets)
                        {
                            Physics.IgnoreCollision(turret.GetComponent<Collider>(), torpedoCollider);
                        }
                    }
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
