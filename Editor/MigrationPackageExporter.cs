using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Altoura.Migration.Editor
{
    public class MigrationExportOptions
    {
        public GameObject ExportRoot;
        public string SourceAssetPath;
        public string SourceType = "prefab";
        public string OutputZipPath;
        public string TimelineNameFilter;
        public bool ExportAllTimelines = true;
        public bool CompressMeshesWithDraco = true;
    }

    public static class MigrationPackageExporter
    {
        public static string Export(MigrationExportOptions options)
        {
            if (options == null || options.ExportRoot == null)
            {
                throw new ArgumentException("Export root is required.");
            }

            var root = options.ExportRoot;
            var sourceName = root.name;
            var stagingDirectory = Path.Combine(Path.GetTempPath(), "AltouraMigration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                StableIdRegistry.BuildFromHierarchy(root);
                var rootStableId = StableIdRegistry.GetStableId(root);

                var modelsDirectory = Path.Combine(stagingDirectory, "models");
                var glbFileName = TimelineCaptureService.SanitizeFileName(sourceName);
                var glbPath = GlbExporter.ExportRoot(root, modelsDirectory, glbFileName);

                if (options.CompressMeshesWithDraco)
                {
                    EditorUtility.DisplayProgressBar("Altoura Migration", "Compressing GLB (Draco)...", 0.7f);
                    DracoGlbCompressor.TryCompressInPlace(glbPath);
                }

                var manifest = PackageManifestBuilder.Create(
                    sourceName,
                    options.SourceType,
                    options.SourceAssetPath,
                    rootStableId,
                    "models/" + glbFileName + ".glb");

                var timelinesDirectory = Path.Combine(stagingDirectory, "timelines");
                Directory.CreateDirectory(timelinesDirectory);

                var directors = TimelineCaptureService.FindDirectors(
                    root,
                    options.ExportAllTimelines ? null : options.TimelineNameFilter);

                var timelineCaptures = new List<UnityTimelineCaptureDocument>();
                var usedTimelineFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var director in directors)
                {
                    var capture = TimelineCaptureService.CaptureDirector(director, root);
                    if (capture == null)
                    {
                        continue;
                    }

                    timelineCaptures.Add(capture);

                    // Multiple directors can reference identically named timeline
                    // assets; suffix duplicates so no files are overwritten.
                    var safeName = TimelineCaptureService.SanitizeFileName(capture.timelineName);
                    var uniqueName = safeName;
                    var suffix = 2;
                    while (!usedTimelineFileNames.Add(uniqueName))
                    {
                        uniqueName = safeName + "_" + suffix;
                        suffix++;
                    }

                    TimelineCaptureService.WriteCapture(timelinesDirectory, capture, uniqueName);

                    var otioTimeline = TimelineOtioConverter.Convert(capture, director, root, rootStableId);
                    if (otioTimeline != null)
                    {
                        var otioPath = Path.Combine(timelinesDirectory, uniqueName + ".otio.json");
                        OtioJsonWriter.WriteTimelineFile(otioPath, otioTimeline);
                    }

                    manifest.timelines.Add(new MigrationTimelineEntry
                    {
                        name = capture.timelineName,
                        otioFile = "timelines/" + uniqueName + ".otio.json",
                        unityFile = "timelines/" + uniqueName + ".unity.json",
                        directorHierarchyPath = capture.directorHierarchyPath,
                        playableAssetPath = capture.playableAssetPath
                    });
                }

                var particlesDirectory = Path.Combine(stagingDirectory, "particles");
                var particleCaptures = ParticleCaptureService.CaptureFromHierarchy(root);
                ParticleCaptureService.WriteCaptures(particlesDirectory, particleCaptures, manifest);

                var audioDirectory = Path.Combine(stagingDirectory, "audio");
                AudioAssetExtractor.ExtractFromTimelineCaptures(audioDirectory, timelineCaptures, manifest);

                var manifestPath = Path.Combine(stagingDirectory, "manifest.json");
                JsonFileWriter.WriteJson(manifestPath, manifest);

                var outputZipPath = options.OutputZipPath;
                if (string.IsNullOrEmpty(outputZipPath))
                {
                    outputZipPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        sourceName + ".altpkg.zip");
                }

                ZipPackageWriter.CreatePackage(stagingDirectory, outputZipPath);
                Debug.Log("[AltouraMigration] Exported package: " + outputZipPath);
                return outputZipPath;
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, true);
                }

                StableIdRegistry.Clear();
            }
        }

        public static GameObject InstantiateExportRoot(GameObject prefab, GameObject selectedRoot)
        {
            if (selectedRoot != null)
            {
                return selectedRoot;
            }

            if (prefab == null)
            {
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.name = prefab.name;
            }

            return instance;
        }

        public static void DestroyTemporaryInstance(GameObject instance, GameObject prefab, GameObject selectedRoot)
        {
            if (instance == null || selectedRoot != null)
            {
                return;
            }

            if (prefab != null && instance != prefab)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
