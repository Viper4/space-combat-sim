using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SpaceStuff;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Ship)), RequireComponent(typeof(TargetingSystem))]
public class TorpedoSystem : MonoBehaviour
{
    private Ship ship;
    private TargetingSystem targetingSystem;
    [SerializeField] private AudioManipulation launchAudio;

    [SerializeField] private TorpedoPoint[] torpedoPoints;
    [SerializeField] private Image[] torpedoIcons;
    [SerializeField] private Color baseUIColor;
    [SerializeField] private Animation bayDoorAnimation;
    [SerializeField] private float launchCooldown = 0.25f;
    private bool torpedoBayDoorOpen = false;
    private bool canLaunch = true;

    private Torpedo[] launchedTorpedoes;

    private void Start()
    {
        ship = GetComponent<Ship>();
        targetingSystem = GetComponent<TargetingSystem>();
        targetingSystem.OnTargetChange += ChangeTarget;
        launchedTorpedoes = new Torpedo[torpedoPoints.Length];
        GameManager.Instance.inputActions.Player.LaunchTorpedo.performed += LaunchTorpedo;
    }

    public void TorpedoBaySwitch(int state)
    {
        torpedoBayDoorOpen = state == 1;
        if (torpedoBayDoorOpen)
        {
            bayDoorAnimation.Play("OpenTorpedoBay");
        }
        else
        {
            bayDoorAnimation.Play("CloseTorpedoBay");
        }
    }

    public void LaunchTorpedo(InputAction.CallbackContext context)
    {
        if (!torpedoBayDoorOpen || !canLaunch)
            return;

        Debug.Log("Fire Torpedo");
        for (int i = 0; i < torpedoPoints.Length; i++)
        {
            if (torpedoPoints[i].hasTorpedo)
            {
                launchAudio.ResetPlay(true);
                Vector3d launchPosition = ship.doubleRigidbody.scaledTransform.GetChildRealPosition(torpedoPoints[i].transform.position);
                launchedTorpedoes[i] = torpedoPoints[i].LaunchTorpedo(launchPosition, ship.doubleRigidbody.velocity, targetingSystem.lockedTarget, i, ship.team);
                UpdateTorpedoUI(i, false);
                break;
            }
        }
        StartCoroutine(LaunchCooldown());
    }

    private IEnumerator LaunchCooldown()
    {
        canLaunch = false;
        yield return new WaitForSeconds(launchCooldown);
        canLaunch = true;
    }

    public void UpdateTorpedoUI(int torpedoIndex, bool active)
    {
        torpedoIcons[torpedoIndex].color = active ? baseUIColor : Color.black;
    }

    public void ChangeTarget()
    {
        for (int i = 0; i < launchedTorpedoes.Length; i++)
        {
            if (launchedTorpedoes[i] == null)
                continue;
            launchedTorpedoes[i].SetTarget(targetingSystem.lockedTarget);
        }
    }

    private void OnDestroy()
    {
        GameManager.Instance.inputActions.Player.LaunchTorpedo.performed -= LaunchTorpedo;
    }
}
