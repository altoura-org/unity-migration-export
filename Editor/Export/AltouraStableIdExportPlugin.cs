using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Plugins;

namespace Altoura.Migration.Editor
{
    public class AltouraStableIdExportPlugin : GLTFExportPlugin
    {
        public override string DisplayName => "Altoura Stable ID";
        public override string Description => "Writes altouraStableId into glTF node extras for vNext import.";
        public override bool EnabledByDefault => true;
        public override bool AlwaysEnabled => true;

        public override GLTFExportPluginContext CreateInstance(ExportContext context)
        {
            return new AltouraStableIdExportPluginContext();
        }
    }

    public class AltouraStableIdExportPluginContext : GLTFExportPluginContext
    {
        public override void BeforeNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node)
        {
            if (transform == null || node == null)
            {
                return;
            }

            var stableId = StableIdRegistry.GetStableId(transform.gameObject);
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }

            NodeExtrasUtility.SetExtra(node, "altouraStableId", stableId);

            // glTF has no node visibility; record Unity's initial active state so
            // the vNext importer can hide timeline-activated objects on load.
            // Written only when inactive to keep the common case compact.
            if (!transform.gameObject.activeSelf)
            {
                NodeExtrasUtility.SetExtra(node, "altouraInitiallyActive", false);
            }

            // A disabled MeshRenderer/SkinnedMeshRenderer means the object is
            // active but its mesh is not drawn. UnityGLTF still exports the mesh
            // (ExportDisabledGameObjects is on), and glTF has no per-renderer
            // visibility, so record the disabled state separately from active
            // state; the vNext importer hides these via a node override.
            Renderer renderer = transform.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = transform.GetComponent<SkinnedMeshRenderer>();
            }

            if (renderer != null && !renderer.enabled)
            {
                NodeExtrasUtility.SetExtra(node, "altouraRendererEnabled", false);
            }
        }
    }

    internal static class NodeExtrasUtility
    {
        public static void SetExtra(Node node, string key, JToken value)
        {
            if (node == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (node.Extras is JObject extras)
            {
                extras[key] = value;
                return;
            }

            node.Extras = new JObject
            {
                [key] = value
            };
        }
    }
}
