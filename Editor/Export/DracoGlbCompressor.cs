using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    /// <summary>
    /// Compresses exported GLB meshes with KHR_draco_mesh_compression using the
    /// gltf-transform CLI (runs via npx; requires Node.js on the machine).
    /// UnityGLTF can only import Draco, not export it, so this runs as a
    /// post-export step. Node extras (altouraStableId) are preserved.
    /// </summary>
    public static class DracoGlbCompressor
    {
        public static bool TryCompressInPlace(string glbPath)
        {
            if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
            {
                return false;
            }

            var sizeBefore = new FileInfo(glbPath).Length;

            var startInfo = new ProcessStartInfo
            {
                FileName = Application.platform == RuntimePlatform.WindowsEditor ? "npx.cmd" : "npx",
                Arguments = "-y @gltf-transform/cli draco \"" + glbPath + "\" \"" + glbPath + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogWarning(
                            "[AltouraMigration] Draco compression failed (exit " + process.ExitCode +
                            "); keeping uncompressed GLB.\n" + stderr);
                        return false;
                    }

                    var sizeAfter = new FileInfo(glbPath).Length;
                    UnityEngine.Debug.Log(string.Format(
                        "[AltouraMigration] Draco compression: {0:N1} MB -> {1:N1} MB\n{2}",
                        sizeBefore / 1048576.0,
                        sizeAfter / 1048576.0,
                        stdout));
                    return true;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[AltouraMigration] Draco compression unavailable (is Node.js installed?); " +
                    "keeping uncompressed GLB. " + ex.Message);
                return false;
            }
        }
    }
}
