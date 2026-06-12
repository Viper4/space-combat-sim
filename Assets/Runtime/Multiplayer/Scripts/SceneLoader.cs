using FishNet.Managing.Scened;
using UnityEngine;

public class SceneLoader : DefaultSceneProcessor
{
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private SliderIndicator progressIndicator;

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
