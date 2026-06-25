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

        while (!IsPercentComplete())
        {
            yield return null;
        }

        manualOperation = null;
        loadingPanel.SetActive(false);
    }

    public override void LoadStart(LoadQueueData queueData)
    {
        base.LoadStart(queueData);
        loadingPanel.SetActive(true);
    }

    public override void LoadEnd(LoadQueueData queueData)
    {
        base.LoadEnd(queueData);
        loadingPanel.SetActive(false);
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
