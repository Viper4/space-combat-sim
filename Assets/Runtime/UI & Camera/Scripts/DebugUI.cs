using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI detailsText;
    [SerializeField, Tooltip("How often to update the details text.")] private float pollingTime = 0.5f;

    [Header("Settings")]
    [SerializeField] private int maxUniqueLogs = 200;

    private StringBuilder logStringBuilder = new(4096);

    private readonly Dictionary<string, LogData> logs = new();

    private static readonly Regex ColonRegex = new(@"].*:", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\d+(\.\d+)?", RegexOptions.Compiled);

    private float time;
    private int frameCount;

    private class LogData
    {
        public string LatestMessage;
        public int Count;
        public LogType Type;
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;

        if (GameManager.Instance != null)
            GameManager.Instance.inputActions.UI.Debug.performed += TogglePanel;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;

        if (GameManager.Instance != null)
            GameManager.Instance.inputActions.UI.Debug.performed -= TogglePanel;
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        frameCount++;

        if (time >= pollingTime)
        {
            int frameRate = Mathf.RoundToInt(frameCount / time);
            detailsText.text = $"{frameRate} FPS";
            if (InstanceFinder.IsOffline)
            {
                detailsText.text += "\nOffline";
            }
            else
            {
                if (LobbyManager.Instance.IsHosting)
                {
                    detailsText.text += "\nHosting";
                }
                else
                {
                    detailsText.text += "\nConnected";
                }
            }

            time -= pollingTime;
            frameCount = 0;
        }
    }

    private void TogglePanel(InputAction.CallbackContext context)
    {
        panel.SetActive(!panel.activeSelf);
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        string key = GenerateTemplateKey(condition);

        if (logs.TryGetValue(key, out LogData existing))
        {
            existing.LatestMessage = condition;
            existing.Count++;
        }
        else
        {
            if (logs.Count >= maxUniqueLogs)
                return;

            logs[key] = new LogData
            {
                LatestMessage = condition,
                Count = 1,
                Type = type
            };
        }

        RefreshLog();
    }

    private string GenerateTemplateKey(string message)
    {
        Match colonMatch = ColonRegex.Match(message);
        if (colonMatch.Success)
        {
            return colonMatch.Value;
        }
        return message;
    }

    private void RefreshLog()
    {
        logStringBuilder.Clear();

        foreach (var pair in logs)
        {
            LogData log = pair.Value;

            string color = log.Type switch
            {
                LogType.Warning => "#FFD700",
                LogType.Error => "#FF5555",
                LogType.Exception => "#FF3333",
                LogType.Assert => "#FF8800",
                _ => "#FFFFFF"
            };

            string logMessage = $"<color={color}>{log.LatestMessage}</color>";

            if (log.Count > 1)
                logMessage += $" <color=#88FF88>(x{log.Count})</color>";

            logStringBuilder.Append(logMessage + "\n");
        }
        logText.text = logStringBuilder.ToString();
    }

    public void ClearLogs()
    {
        logs.Clear();
        logText.text = string.Empty;
    }
}