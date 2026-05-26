using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class HoverInteractable : MonoBehaviour
{
    [SerializeField] private Transform meshTransform;

    private bool hovered = false;
    [SerializeField, Tooltip("0 to maxState states inclusive")] private int maxState = 1;
    private int state = 0;
    [SerializeField] private Vector3[] statePositions;
    [SerializeField] private Vector3[] stateEulerAngles;

    [SerializeField] private InputActionReference interactAction;

    [SerializeField] private UnityEvent onHoverEnter;
    [SerializeField] private UnityEvent onHoverExit;
    [SerializeField] private UnityEvent<int> onInteract;

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed += Interact;
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= Interact;
            interactAction.action.Disable();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (meshTransform == null)
        {
            meshTransform = transform;
        }

        if (statePositions.Length == 0)
        {
            statePositions = new Vector3[maxState + 1];
            for (int i = 0; i <= maxState; i++)
            {
                statePositions[i] = meshTransform.localPosition;
            }
        }
        if (stateEulerAngles.Length == 0)
        {
            stateEulerAngles = new Vector3[maxState + 1];
            for (int i = 0; i <= maxState; i++)
            {
                stateEulerAngles[i] = meshTransform.localEulerAngles;
            }
        }

        if (statePositions.Length != maxState + 1)
        {
            Debug.LogWarning($"{transform.name} HoverInteractable: number of state positions ({statePositions.Length}) does not match max state ({maxState})");
        }
        if (stateEulerAngles.Length != maxState + 1)
        {
            Debug.LogWarning($"{transform.name} HoverInteractable: number of state positions ({stateEulerAngles.Length}) does not match max state ({maxState})");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (hovered && GameManager.Instance.inputActions.UI.Click.WasPressedThisFrame())
        {
            Interact(default);
        }
    }

    private void Interact(InputAction.CallbackContext context)
    {
        state = (state + 1) % (maxState + 1);
        meshTransform.localPosition = statePositions[state];
        meshTransform.localEulerAngles = stateEulerAngles[state];
        onInteract?.Invoke(state);
    }

    public void OnHoverEnter()
    {
        hovered = true;
        onHoverEnter?.Invoke();
    }

    public void OnHoverExit()
    {
        hovered = false;
        onHoverExit?.Invoke();
    }

    public void ToggleGameObjectActive(GameObject target)
    {
        target.SetActive(!target.activeSelf);
    }
}
