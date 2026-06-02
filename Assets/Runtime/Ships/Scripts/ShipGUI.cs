using System;
using System.Collections;
using UnityEngine;

public class ShipGUI : MonoBehaviour
{
    private static readonly int OpenHash = Animator.StringToHash("Open");
    private static readonly int CloseHash = Animator.StringToHash("Close");
    private static readonly int OpenRadar = Animator.StringToHash("OpenRadar");
    private static readonly int CloseRadar = Animator.StringToHash("CloseRadar");
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject canvas;

    private bool open;

    private bool radarActive;

    private Coroutine animationRoutine;

    private void Start()
    {
        canvas.SetActive(open && !radarActive);
    }

    private void Update()
    {
        if (GameManager.Instance.inputActions.Player.CursorLock.WasPressedThisFrame())
        {
            if (animationRoutine != null)
                StopCoroutine(animationRoutine);
            animationRoutine = StartCoroutine(ToggleGUI());
        }
    }

    private IEnumerator ToggleGUI()
    {
        open = !open;
        if (radarActive)
            yield break;
        if (open)
        {
            canvas.SetActive(true);
            animator.SetTrigger(OpenHash);
        }
        else
        {
            animator.SetTrigger(CloseHash);

            yield return new WaitUntil(() =>
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

                return state.IsName("Close") && state.normalizedTime >= 1.0f;
            });

            canvas.SetActive(false);
        }
    }

    public void ToggleRadarActive(bool value)
    {
        if (radarActive && !value)
        {
            animator.SetTrigger(CloseRadar);
        }
        else if (open && !radarActive && value)
        {
            animator.SetTrigger(OpenRadar);
        }
        radarActive = value;
        canvas.SetActive(open && !radarActive);
    }
}
