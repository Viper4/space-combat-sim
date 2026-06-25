using System.Collections.Generic;

[System.Serializable]
public class GameSettings
{
    public List<RangedSetting> rangedSettings;
    public string bindingOverridesJson;
}

[System.Serializable]
public struct RangedSetting
{
    public string name;
    public float value;
    public float min;
    public float max;
    public bool integer;
}