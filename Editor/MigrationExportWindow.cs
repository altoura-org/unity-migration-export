using System.IO;
using UnityEditor;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    public class MigrationExportWindow : EditorWindow
    {
        private GameObject prefab;
        private string timelineNameFilter = "door_open_timeline";
        private bool exportAllTimelines = true;
        private bool compressMeshesWithDraco = true;
        private string outputDirectory;
        private Vector2 scrollPosition;

        [MenuItem("Altoura/Migration Export")]
        public static void ShowWindow()
        {
            GetWindow<MigrationExportWindow>("Altoura Migration");
        }

        [MenuItem("Altoura/Export Selected To vNext Package")]
        public static void ExportSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Altoura Migration", "Select a root GameObject or assign a prefab in the Migration Export window.", "OK");
                return;
            }

            var outputPath = EditorUtility.SaveFilePanel(
                "Export vNext Package",
                GetDefaultOutputDirectory(),
                selected.name + ".altpkg.zip",
                "zip");

            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                var zipPath = MigrationPackageExporter.Export(new MigrationExportOptions
                {
                    ExportRoot = selected,
                    SourceType = "hierarchy",
                    SourceAssetPath = GetAssetPathForObject(selected),
                    OutputZipPath = outputPath,
                    ExportAllTimelines = true
                });

                EditorUtility.DisplayDialog("Altoura Migration", "Export complete:\n" + zipPath, "OK");
                EditorUtility.RevealInFinder(zipPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Altoura Migration", "Export failed:\n" + ex.Message, "OK");
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Unity to vNext Migration Export", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Exports GLB geometry with altouraStableId, converted OTIO timelines, lossless Unity timeline sidecars, particles, and audio into a .altpkg.zip file.",
                MessageType.Info);

            EditorGUILayout.Space();
            prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);

            exportAllTimelines = EditorGUILayout.Toggle("Export All Timelines", exportAllTimelines);
            using (new EditorGUI.DisabledScope(exportAllTimelines))
            {
                timelineNameFilter = EditorGUILayout.TextField("Timeline Name Filter", timelineNameFilter);
            }

            compressMeshesWithDraco = EditorGUILayout.Toggle(
                new GUIContent("Draco Compression", "Compress GLB meshes with KHR_draco_mesh_compression via gltf-transform (requires Node.js)."),
                compressMeshesWithDraco);

            outputDirectory = EditorGUILayout.TextField("Output Directory", string.IsNullOrEmpty(outputDirectory) ? GetDefaultOutputDirectory() : outputDirectory);

            EditorGUILayout.Space();

            if (GUILayout.Button("Export Combo Room Realscale (Scale-Up)", GUILayout.Height(28)))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combo Room/Realscale.prefab");
                exportAllTimelines = true;
                ExportPackage();
            }

            if (GUILayout.Button("Export Vertical Slice (door_open_timeline)", GUILayout.Height(28)))
            {
                exportAllTimelines = false;
                timelineNameFilter = "door_open_timeline";
                ExportPackage();
            }

            if (GUILayout.Button("Export Package", GUILayout.Height(32)))
            {
                ExportPackage();
            }

            EditorGUILayout.EndScrollView();
        }

        private void ExportPackage()
        {
            var selectedRoot = Selection.activeGameObject;
            var exportRoot = MigrationPackageExporter.InstantiateExportRoot(prefab, selectedRoot);

            if (exportRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Altoura Migration",
                    "Assign a prefab or select a root GameObject in the Hierarchy.",
                    "OK");
                return;
            }

            var defaultName = exportRoot.name + ".altpkg.zip";
            var outputPath = EditorUtility.SaveFilePanel(
                "Export vNext Package",
                string.IsNullOrEmpty(outputDirectory) ? GetDefaultOutputDirectory() : outputDirectory,
                defaultName,
                "zip");

            if (string.IsNullOrEmpty(outputPath))
            {
                MigrationPackageExporter.DestroyTemporaryInstance(exportRoot, prefab, selectedRoot);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Altoura Migration", "Exporting package...", 0.5f);

                var sourcePath = !string.IsNullOrEmpty(GetAssetPathForObject(prefab))
                    ? GetAssetPathForObject(prefab)
                    : GetAssetPathForObject(exportRoot);

                var zipPath = MigrationPackageExporter.Export(new MigrationExportOptions
                {
                    ExportRoot = exportRoot,
                    SourceAssetPath = sourcePath,
                    SourceType = prefab != null ? "prefab" : "hierarchy",
                    OutputZipPath = outputPath,
                    TimelineNameFilter = timelineNameFilter,
                    ExportAllTimelines = exportAllTimelines,
                    CompressMeshesWithDraco = compressMeshesWithDraco
                });

                EditorUtility.DisplayDialog("Altoura Migration", "Export complete:\n" + zipPath, "OK");
                EditorUtility.RevealInFinder(zipPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Altoura Migration", "Export failed:\n" + ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                MigrationPackageExporter.DestroyTemporaryInstance(exportRoot, prefab, selectedRoot);
            }
        }

        private static string GetDefaultOutputDirectory()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Exports");
        }

        private static string GetAssetPathForObject(UnityEngine.Object unityObject)
        {
            return unityObject == null ? string.Empty : AssetDatabase.GetAssetPath(unityObject);
        }
    }
}
