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

    public void BeginOfflineLoad(string sceneName)
    {
        StartCoroutine(LoadOfflineSceneAsync(sceneName));
    }

    private IEnumerator LoadOfflineSceneAsync(string sceneName)
    {
        AsyncOperation op = UnitySceneManager.LoadSceneAsync(sceneName);

        manualOperation = op;
        loadingPanel.SetActive(true);
        OnStartSceneLoad?.Invoke();

        while (!IsPercentComplete())
        {
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
    }

    public override void LoadStart(LoadQueueData queueData)
    {
        base.LoadStart(queueData);
        loadingPanel.SetActive(true);
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
        Debug.Log($"[SceneLoader] Started scene load for {sceneNames}");
    }

    public override void LoadEnd(LoadQueueData queueData)
    {
        base.LoadEnd(queueData);
        loadingPanel.SetActive(false);
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
        Debug.Log($"[SceneLoader] Ended scene load for {sceneNames}");
    }

    /// <summary>
    /// Returns the progress on the current scene load or unload.
    /// </summary>
    /// <returns></returns>
    public override float GetPercentComplete()
    {
        float progress = CurrentAsyncOperation == null ? 1f : CurrentAsyncOperation.progress;
        progressIndicator.UpdateUI(progress * 100f, 100f);
        return progress;
    }
}
