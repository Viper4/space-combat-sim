using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool offlineMode = false;

    //public Dictionary<ulong, NetworkObject> trackedNetworkObjects = new Dictionary<ulong, NetworkObject>();

    public InputActions inputActions;
    public PlayerSettings playerSettings;
    public float sensitivityScale = 0.02f;
    [SerializeField] private bool loadSettings = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            inputActions = new InputActions();
            inputActions.Enable();

            SaveSystem.Init();
            if (loadSettings)
                playerSettings = SaveSystem.LoadPlayerSettings("profile1");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        inputActions?.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        if (inputActions.UI.Fullscreen.triggered)
        {
            Screen.fullScreen = !Screen.fullScreen;
            if (Screen.fullScreen)
            {
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            }
            else
            {
                Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
            }
        }
    }
}
