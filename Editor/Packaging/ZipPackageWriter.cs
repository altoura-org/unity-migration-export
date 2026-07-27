using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Altoura.Migration.Editor
{
    public static class ZipPackageWriter
    {
        public static void CreatePackage(string stagingDirectory, string outputZipPath)
        {
            if (!Directory.Exists(stagingDirectory))
            {
                throw new DirectoryNotFoundException("Staging directory does not exist: " + stagingDirectory);
            }

            var outputDirectory = Path.GetDirectoryName(outputZipPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            if (File.Exists(outputZipPath))
            {
                File.Delete(outputZipPath);
            }

            ZipFile.CreateFromDirectory(stagingDirectory, outputZipPath, CompressionLevel.Optimal, false);
        }

        public static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destinationPath = Path.Combine(destinationDirectory, relativePath);
                var destinationFolder = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationFolder) && !Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                File.Copy(file, destinationPath, true);
            }
        }
    }
}
