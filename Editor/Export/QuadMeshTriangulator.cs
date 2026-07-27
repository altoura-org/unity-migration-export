using System;
using System.Collections.Generic;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    /// <summary>
    /// glTF only supports point, line, and triangle primitives; UnityGLTF throws
    /// on quad-topology meshes. This scans the export hierarchy, swaps any mesh
    /// containing a quad submesh for a triangulated temporary copy, and restores
    /// the originals on Dispose. Source mesh assets are never modified.
    /// </summary>
    public sealed class QuadMeshTriangulator : IDisposable
    {
        private readonly List<KeyValuePair<MeshFilter, Mesh>> swappedFilters = new List<KeyValuePair<MeshFilter, Mesh>>();
        private readonly List<KeyValuePair<SkinnedMeshRenderer, Mesh>> swappedSkinned = new List<KeyValuePair<SkinnedMeshRenderer, Mesh>>();
        private readonly Dictionary<Mesh, Mesh> convertedByOriginal = new Dictionary<Mesh, Mesh>();

        public QuadMeshTriangulator(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var converted = GetOrCreateTriangulated(filter.sharedMesh, filter.gameObject);
                if (converted != null)
                {
                    swappedFilters.Add(new KeyValuePair<MeshFilter, Mesh>(filter, filter.sharedMesh));
                    filter.sharedMesh = converted;
                }
            }

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var converted = GetOrCreateTriangulated(renderer.sharedMesh, renderer.gameObject);
                if (converted != null)
                {
                    swappedSkinned.Add(new KeyValuePair<SkinnedMeshRenderer, Mesh>(renderer, renderer.sharedMesh));
                    renderer.sharedMesh = converted;
                }
            }
        }

        public void Dispose()
        {
            foreach (var entry in swappedFilters)
            {
                if (entry.Key != null)
                {
                    entry.Key.sharedMesh = entry.Value;
                }
            }

            foreach (var entry in swappedSkinned)
            {
                if (entry.Key != null)
                {
                    entry.Key.sharedMesh = entry.Value;
                }
            }

            foreach (var converted in convertedByOriginal.Values)
            {
                if (converted != null)
                {
                    UnityEngine.Object.DestroyImmediate(converted);
                }
            }

            swappedFilters.Clear();
            swappedSkinned.Clear();
            convertedByOriginal.Clear();
        }

        private Mesh GetOrCreateTriangulated(Mesh mesh, GameObject owner)
        {
            if (mesh == null || !HasQuadSubmesh(mesh))
            {
                return null;
            }

            if (convertedByOriginal.TryGetValue(mesh, out var existing))
            {
                return existing;
            }

            var copy = UnityEngine.Object.Instantiate(mesh);
            copy.name = mesh.name + "_triangulated";

            for (var i = 0; i < copy.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) != MeshTopology.Quads)
                {
                    continue;
                }

                var quads = mesh.GetIndices(i);
                var triangles = new int[quads.Length / 4 * 6];
                var t = 0;
                for (var q = 0; q + 3 < quads.Length; q += 4)
                {
                    triangles[t++] = quads[q];
                    triangles[t++] = quads[q + 1];
                    triangles[t++] = quads[q + 2];
                    triangles[t++] = quads[q];
                    triangles[t++] = quads[q + 2];
                    triangles[t++] = quads[q + 3];
                }

                copy.SetIndices(triangles, MeshTopology.Triangles, i, false);
            }

            convertedByOriginal[mesh] = copy;
            Debug.Log("[AltouraMigration] Triangulated quad mesh '" + mesh.name + "' on '" +
                      StableIdRegistry.GetHierarchyPath(owner, null) + "' for GLB export.");
            return copy;
        }

        private static bool HasQuadSubmesh(Mesh mesh)
        {
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Quads)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
