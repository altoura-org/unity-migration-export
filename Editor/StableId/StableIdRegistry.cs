using System.Collections.Generic;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    /// <summary>
    /// In-memory map of GameObjects to altouraStableId for the current export session.
    /// </summary>
    public static class StableIdRegistry
    {
        private static readonly Dictionary<int, string> InstanceIdToStableId = new Dictionary<int, string>();
        private static readonly Dictionary<string, GameObject> StableIdToGameObject = new Dictionary<string, GameObject>();

        public static void Clear()
        {
            InstanceIdToStableId.Clear();
            StableIdToGameObject.Clear();
        }

        public static void BuildFromHierarchy(GameObject root)
        {
            Clear();
            if (root == null)
            {
                return;
            }

            RegisterGameObject(root);
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                RegisterGameObject(transform.gameObject);
            }
        }

        public static void RegisterGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            var instanceId = gameObject.GetInstanceID();
            if (InstanceIdToStableId.ContainsKey(instanceId))
            {
                return;
            }

            var stableId = StableIdService.GetStableId(gameObject);
            InstanceIdToStableId[instanceId] = stableId;
            StableIdToGameObject[stableId] = gameObject;
        }

        public static string GetStableId(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            var instanceId = gameObject.GetInstanceID();
            if (InstanceIdToStableId.TryGetValue(instanceId, out var stableId))
            {
                return stableId;
            }

            stableId = StableIdService.GetStableId(gameObject);
            InstanceIdToStableId[instanceId] = stableId;
            StableIdToGameObject[stableId] = gameObject;
            return stableId;
        }

        public static GameObject GetGameObject(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return null;
            }

            StableIdToGameObject.TryGetValue(stableId, out var gameObject);
            return gameObject;
        }

        public static string GetHierarchyPath(GameObject gameObject, GameObject root)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                segments.Add(current.name);
                if (root != null && current.gameObject == root)
                {
                    break;
                }

                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
