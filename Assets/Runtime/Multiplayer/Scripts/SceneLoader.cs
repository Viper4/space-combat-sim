using System;
using System.Collections;
using FishNet.Managing.Scened;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public class SceneLoader : DefaultSceneProcessor
{
    public static SceneLoader Instance;

    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private SliderIndicator progressIndicator;

    private AsyncOperation manualOperation;

    public Action OnStartSceneLoad;
    public Action OnEndSceneLoad;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void BeginOfflineLoad(string sceneName, string message = null)
    {
        StartCoroutine(LoadOfflineSceneAsync(sceneName, message));
    }

    private IEnumerator LoadOfflineSceneAsync(string sceneName, string message = null)
    {
        loadingPanel.SetActive(true);
        Debug.Log(GameLog.ObjectLog(this, $"Started offline scene load for {sceneName}."));

        AsyncOperation op = UnitySceneManager.LoadSceneAsync(sceneName);

        manualOperation = op;
        OnStartSceneLoad?.Invoke();

        while (manualOperation.progress < 1f) // 1f since we want to wait while scene activates
        {
            progressIndicator.UpdateUI((manualOperation.progress / 0.9f) * 100f, 100f);
            yield return null;
        }

        manualOperation = null;
        loadingPanel.SetActive(false);

        if (sceneName == "StartScene")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        OnEndSceneLoad?.Invoke();
        if (message != null)
        {
            LobbyManager.Instance.InvokeConnectionFail(message);
        }
        Debug.Log(GameLog.ObjectLog(this, $"Ended offline scene load for {sceneName}."));
    }

    public override void LoadStart(LoadQueueData queueData)
    {
        loadingPanel.SetActive(true);
        base.LoadStart(queueData);
        OnStartSceneLoad?.Invoke();
        string sceneNames = "";
        for(int i = 0; i < queueData.SceneLoadData.SceneLookupDatas.Length; i++)
        {
            if (i == queueData.SceneLoadData.SceneLookupDatas.Length - 1)
            {
                sceneNames += queueData.SceneLoadData.SceneLookupDatas[i].Name;
            }
            else
            {
                sceneNames += queueData.SceneLoadData.SceneLookupDatas[i].Name + ", ";
            }
        }
        Debug.Log(GameLog.ObjectLog(this, $"Started online scene load for {sceneNames}."));
    }

    public override void LoadEnd(LoadQueueData queueData)
    {
        loadingPanel.SetActive(false);
        base.LoadEnd(queueData);
        OnEndSceneLoad?.Invoke();
        string sceneNames = "";
        for(int i = 0; i < queueData.SceneLoadData.SceneLookupDatas.Length; i++)
        {
            if (i == queueData.SceneLoadData.SceneLookupDatas.Length - 1)
            {
                sceneNames += queueData.SceneLoadData.SceneLookupDatas[i].Name;
            }
            else
            {
                sceneNames += queueData.SceneLoadData.SceneLookupDatas[i].Name + ", ";
            }
        }
        Debug.Log(GameLog.ObjectLog(this, $"Ended online scene load for {sceneNames}."));
    }

    public override bool IsPercentComplete()
    {
        return GetPercentComplete() >= 1f;;
    }

    /// <summary>
    /// Returns the progress on the current scene load or unload.
    /// </summary>
    /// <returns></returns>
    public override float GetPercentComplete()
    {
        float progress = CurrentAsyncOperation == null ? 1f : (CurrentAsyncOperation.progress / 0.9f);
        progressIndicator.UpdateUI(progress * 100f, 100f);
        return progress;
    }
}
