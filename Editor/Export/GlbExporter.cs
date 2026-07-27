using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityGLTF;

namespace Altoura.Migration.Editor
{
    public static class GlbExporter
    {
        public static string ExportRoot(GameObject root, string outputDirectory, string fileNameWithoutExtension)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            StableIdRegistry.BuildFromHierarchy(root);
            var settings = GLTFSettings.GetOrCreateSettings();
            RegisterExportPlugin(settings);

            // Training content is largely inactive until a timeline activates it;
            // those objects must still exist as GLB nodes for bindings to resolve.
            settings.ExportDisabledGameObjects = true;
            settings.UseMainCameraVisibility = false;

            var exportContext = new ExportContext();
            using (new QuadMeshTriangulator(root))
            {
                var exporter = new GLTFSceneExporter(new[] { root.transform }, exportContext);
                exporter.SaveGLB(outputDirectory, fileNameWithoutExtension);
            }

            var glbPath = Path.Combine(outputDirectory, fileNameWithoutExtension + ".glb");
            if (!File.Exists(glbPath))
            {
                throw new FileNotFoundException("GLB export failed to produce output file.", glbPath);
            }

            return glbPath;
        }

        private static void RegisterExportPlugin(GLTFSettings settings)
        {
            var existing = settings.ExportPlugins.Find(plugin => plugin is AltouraStableIdExportPlugin);
            if (existing == null)
            {
                var plugin = ScriptableObject.CreateInstance<AltouraStableIdExportPlugin>();
                settings.ExportPlugins.Add(plugin);
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
