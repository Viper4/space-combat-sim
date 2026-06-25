using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class SaveSystem
{
    private static readonly string SAVE_FOLDER = Application.dataPath + "/SaveData/";
    private static readonly string SETTINGS_FOLDER = SAVE_FOLDER + "Settings/";
    private static readonly string gameSettingsFile = "settings.dat";

    public static readonly GameSettings defaultGameSettings = new GameSettings
    {
        rangedSettings = new List<RangedSetting>()
        {
            new()
            {
                name = "Sensitivity",
                value = 0.5f,
                min = 0.0f,
                max = 1.0f,
                integer = false
            }
            ,
            new()
            {
                name = "Field of View",
                value = 60f,
                min = 15f,
                max = 90f,
                integer = false
            }
            ,
            new()
            {
                name = "Target Frame Rate",
                value = 60f,
                min = 1f,
                max = 300f,
                integer = true
            }
            ,
            new()
            {
                name = "Master Volume",
                value = 1.0f,
                min = 0.0f,
                max = 1.0f,
                integer = false
            }
        },
        bindingOverridesJson = null,
    };

    public static void Init()
    {
        Directory.CreateDirectory(SAVE_FOLDER);
        Directory.CreateDirectory(SETTINGS_FOLDER);
    }

    public static void SaveGameSettings(GameSettings fromSettings)
    {
        string json = JsonUtility.ToJson(fromSettings, prettyPrint: true);
        Debug.Log("[SaveSystem] Saved settings.");

        File.WriteAllText(SETTINGS_FOLDER + gameSettingsFile, json);
    }

    public static GameSettings LoadGameSettings()
    {
        string path = SETTINGS_FOLDER + gameSettingsFile;
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);

            if (!string.IsNullOrWhiteSpace(json))
            {
                Debug.Log("[SaveSystem] Loaded settings.");
                return JsonUtility.FromJson<GameSettings>(json);
            }
        }

        Debug.LogWarning("[SaveSystem] Could not find file, or file was empty '" + SETTINGS_FOLDER + gameSettingsFile + "', saving and loading defaults.");
        SaveGameSettings(defaultGameSettings);
        return defaultGameSettings;
    }

    public static void DeleteFile(string fullFileName)
    {
        if (File.Exists(SAVE_FOLDER + fullFileName))
        {
            File.Delete(SAVE_FOLDER + fullFileName);
        }
    }

    public static string LatestFileInSaveFolder(bool returnExtension, string fileExtension = "")
    {
        FileInfo latestFile = new DirectoryInfo(SAVE_FOLDER).GetFiles("*" + fileExtension, SearchOption.AllDirectories).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();

        if (latestFile != null)
        {
            if (returnExtension)
            {
                return latestFile.Name;
            }
            else
            {
                return Path.GetFileNameWithoutExtension(latestFile.Name);
            }
        }
        else
        {
            return null;
        }
    }

    public static IEnumerable<string> FilesInSaveFolder(bool returnExtension, string fileExtension = "")
    {
        if (returnExtension)
        {
            return Directory.EnumerateFiles(SAVE_FOLDER, "*" + fileExtension, SearchOption.AllDirectories).Select(Path.GetFileName);
        }
        else
        {
            return Directory.EnumerateFiles(SAVE_FOLDER, "*" + fileExtension, SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension);
        }
    }
}
