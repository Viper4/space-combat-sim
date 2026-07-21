using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class ShipGUI : MonoBehaviour
{
    private static readonly int OpenHash = Animator.StringToHash("OpenGUI");
    private static readonly int CloseHash = Animator.StringToHash("CloseGUI");
    [SerializeField] private Ship ship;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject canvas;
    [SerializeField] private TextMeshProUGUI startupText;

    private bool open;

    private Coroutine animationRoutine;

    private void Start()
    {
        canvas.SetActive(open);
    }

    private void Update()
    {
        if (ship.isShutdown)
            return;
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
            startupText.text = "LOADING...";
        }
        else
        {
            animator.SetTrigger(CloseHash);
            startupText.text = "EXITING...";

            yield return new WaitUntil(() =>
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

                return state.IsName("CloseGUI") && state.normalizedTime >= 1.0f;
            });

            canvas.SetActive(false);
        }
    }
}
