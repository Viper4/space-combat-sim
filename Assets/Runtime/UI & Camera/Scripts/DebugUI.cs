using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI displayText;

    [Header("Settings")]
    [SerializeField] private int maxUniqueLogs = 200;

    private readonly Dictionary<string, LogData> logs = new();

    private static readonly Regex NumberRegex = new(@"\d+(\.\d+)?", RegexOptions.Compiled);

    private class LogData
    {
        public string LatestMessage;
        public int Count;
        public LogType Type;
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
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

        RefreshDisplay();
    }

    private string GenerateTemplateKey(string message)
    {
        return NumberRegex.Replace(message, "X");
    }

    private void RefreshDisplay()
    {
        displayText.text = string.Empty;

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

            displayText.text +=
                $"<color={color}>{log.LatestMessage}</color>";

            if (log.Count > 1)
                displayText.text += $" <color=#88FF88>(x{log.Count})</color>";

            displayText.text += "\n";
        }
    }

    public void ClearLogs()
    {
        logs.Clear();
        displayText.text = string.Empty;
    }
}