using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    /// <summary>
    /// Validates that exported GLB files contain altouraStableId in node extras.
    /// Menu: Altoura / Validate GLB Stable IDs
    /// </summary>
    public static class GlbExtrasValidator
    {
        [MenuItem("Altoura/Validate GLB Stable IDs")]
        public static void ValidateFromFilePicker()
        {
            var glbPath = EditorUtility.OpenFilePanel("Select GLB", Application.dataPath, "glb");
            if (string.IsNullOrEmpty(glbPath))
            {
                return;
            }

            var result = ValidateGlbFile(glbPath);
            EditorUtility.DisplayDialog(
                "GLB Stable ID Validation",
                result.Summary,
                "OK");
        }

        public static GlbValidationResult ValidateGlbFile(string glbPath)
        {
            var json = ExtractGlbJsonChunk(glbPath);
            if (string.IsNullOrEmpty(json))
            {
                return new GlbValidationResult
                {
                    Summary = "Failed to read GLB JSON chunk from: " + glbPath
                };
            }

            var nodeCount = 0;
            var nodesWithStableId = 0;
            var searchIndex = 0;

            while (true)
            {
                var extrasIndex = json.IndexOf("\"extras\"", searchIndex, StringComparison.Ordinal);
                if (extrasIndex < 0)
                {
                    break;
                }

                nodeCount++;
                var stableIdIndex = json.IndexOf("\"altouraStableId\"", extrasIndex, StringComparison.Ordinal);
                if (stableIdIndex >= 0 && stableIdIndex < extrasIndex + 500)
                {
                    nodesWithStableId++;
                }

                searchIndex = extrasIndex + 8;
            }

            return new GlbValidationResult
            {
                GlbPath = glbPath,
                NodesWithExtras = nodeCount,
                NodesWithStableId = nodesWithStableId,
                Summary = "GLB: " + Path.GetFileName(glbPath) +
                          "\nNodes with extras: " + nodeCount +
                          "\nNodes with altouraStableId: " + nodesWithStableId +
                          (nodesWithStableId > 0 ? "\n\nValidation PASSED." : "\n\nValidation FAILED - no altouraStableId found.")
            };
        }

        private static string ExtractGlbJsonChunk(string glbPath)
        {
            var bytes = File.ReadAllBytes(glbPath);
            if (bytes.Length < 20)
            {
                return null;
            }

            var chunkLength = BitConverter.ToInt32(bytes, 12);
            var chunkType = Encoding.ASCII.GetString(bytes, 16, 4);
            if (chunkType != "JSON")
            {
                return null;
            }

            return Encoding.UTF8.GetString(bytes, 20, chunkLength);
        }
    }

    public class GlbValidationResult
    {
        public string GlbPath;
        public int NodesWithExtras;
        public int NodesWithStableId;
        public string Summary;
    }
}
