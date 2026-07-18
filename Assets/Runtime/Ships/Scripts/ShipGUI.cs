using System;
using System.Collections;
using UnityEngine;

public class ShipGUI : MonoBehaviour
{
    private static readonly int OpenHash = Animator.StringToHash("Open");
    private static readonly int CloseHash = Animator.StringToHash("Close");
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
        if (!GameManager.Instance.IsPaused && GameManager.Instance.inputActions.Player.GUIToggle.WasPressedThisFrame())
        {
            if (animationRoutine != null)
                StopCoroutine(animationRoutine);
            animationRoutine = StartCoroutine(ToggleGUI());
        }
    }

    private IEnumerator ToggleGUI()
    {
        open = !open;
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
        radarActive = value;
    }
}
