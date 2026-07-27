using System;
using System.IO;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    public static class JsonFileWriter
    {
        public static void WriteJson(string filePath, object data)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
        }

        public static string ToJson(object data)
        {
            return JsonUtility.ToJson(data, true);
        }
    }

    [Serializable]
    public class StringListWrapper
    {
        public string[] items;
    }
}
