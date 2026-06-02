using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class SaveSystem
{
    private static readonly string SAVE_FOLDER = Application.dataPath + "/SaveData/";
    private static readonly string SETTINGS_FOLDER = SAVE_FOLDER + "Settings/";

    private static readonly string playerSettingsExtension = ".playersettings";

    public static readonly PlayerSettings defaultPlayerSettings = new PlayerSettings
    {
        sensitivity = 50,
        fieldOfView = 60,
        targetFramerate = 60,
        masterVolume = 100,
    };

    public static void Init()
    {
        Directory.CreateDirectory(SAVE_FOLDER);
        Directory.CreateDirectory(SETTINGS_FOLDER);
    }

    public static void SavePlayerSettings(this PlayerSettings fromSettings, string fileName)
    {
        string json = JsonUtility.ToJson(fromSettings, prettyPrint: true);

        File.WriteAllText(SETTINGS_FOLDER + fileName + playerSettingsExtension, json);
    }

    public static PlayerSettings LoadPlayerSettings(string fileName)
    {
        string path = SETTINGS_FOLDER + fileName + playerSettingsExtension;
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);

            if (!string.IsNullOrWhiteSpace(json))
            {
                return JsonUtility.FromJson<PlayerSettings>(json);
            }
        }

        Debug.LogWarning("Could not find file '" + SETTINGS_FOLDER + fileName + playerSettingsExtension + "', saving and loading defaults.");
        defaultPlayerSettings.SavePlayerSettings(fileName);
        return defaultPlayerSettings;
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
