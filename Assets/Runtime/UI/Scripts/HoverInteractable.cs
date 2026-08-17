using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class HoverInteractable : MonoBehaviour
{
    [SerializeField] private Transform meshTransform;

    [SerializeField] private bool clickable = true;
    private bool hovered = false;
    [SerializeField, Tooltip("0-maxState states inclusive")] private int maxState = 1;
    [SerializeField] private int state = 0;
    [SerializeField, Tooltip("State wraps around to the opposite end once it goes over either end.")] private bool wrapState;
    [SerializeField, Tooltip("Whether state will decrement on the next toggle.")] private bool decrement = false;
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

        OnInteract(true); // Update with current initialized state
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.Instance.IsPaused || !clickable || !hovered)
            return;
        if (GameManager.Instance.inputActions.UI.Click.WasPressedThisFrame())
        {
            IncrementState(1);
        }
        if (GameManager.Instance.inputActions.UI.RightClick.WasPressedThisFrame())
        {
            IncrementState(-1);
        }
    }
    
    private void OnInteract(bool silent)
    {
        meshTransform.localPosition = statePositions[state];
        meshTransform.localEulerAngles = stateEulerAngles[state];
        if (!silent)
            onInteract?.Invoke(state);
    }

    private void IncrementState(int add)
    {
        state += add;
        if (wrapState)
        {
            if (state < 0)
            {
                state = maxState;
            }
            else if (state > maxState)
            {
                state = 0;
            }
            OnInteract(false);
        }
        else if (state < 0 || state > maxState)
        {
            state -= add;
        }
        else
        {
            OnInteract(false);
        }
    }

    private void Interact(InputAction.CallbackContext context)
    {
        if (GameManager.Instance.IsPaused)
            return;
        if (state == maxState)
            decrement = true;
        else if (state == 0)
            decrement = false;
        IncrementState(decrement ? -1 : 1);
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

    public void SetState(int newState)
    {
        if (newState < 0 || newState > maxState)
            return;
        state = newState;
        OnInteract(false);
    }

    public void SetDecrement(bool value)
    {
        decrement = value;
    }
}
