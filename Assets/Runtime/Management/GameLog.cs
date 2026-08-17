using UnityEngine;

public static class GameLog
{
    public static string ObjectLog(Object source, string message)
    {
        return $"[{source.name}: {source.GetType().Name}] {message}";
    }

    public static string OperationFailed(Object source, string operation)
    {
        return ObjectLog(source, $"Failed to {operation}");
    }

    public static string NetworkObjectNotFound(Object source, string operation, int objectId)
    {
        string message = $"{OperationFailed(source, operation)}; Cannot find NetworkObject associated with ID {objectId}.";
        return message;
    }

    public static string ComponentNotFound(Object source, string operation, Object target, string componentName)
    {
        string message = $"{OperationFailed(source, operation)}; Cannot find attached {componentName} component on {target.name}.";
        return message;
    }

    public static string DictionaryValueNotFound(Object source, string operation, string keyName, string valueName, string dictionaryName)
    {
        string message = $"{OperationFailed(source, operation)}; Cannot find value {valueName} associated with key {keyName} in {dictionaryName}.";
        return message;
    }

    public static string ListItemNotFound(Object source, string operation, string targetName, string searchedName)
    {
        string message = $"{OperationFailed(source, operation)}; Cannot find {targetName} in {searchedName}.";
        return message;
    }
}