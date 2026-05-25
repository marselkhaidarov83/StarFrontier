#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

public static class ValidationRunner
{
    public static List<T> LoadAllConfigs<T>() where T : BaseConfig
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        var result = new List<T>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            result.Add(asset);
        }

        return result;
    }
}
#endif