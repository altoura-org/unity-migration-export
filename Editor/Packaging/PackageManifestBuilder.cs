using System;
using System.Collections.Generic;

namespace Altoura.Migration.Editor
{
    [Serializable]
    public class MigrationManifest
    {
        public string packageVersion = "1.0.0";
        public string toolVersion = "0.1.0";
        public string unityVersion;
        public string exportedAt;
        public MigrationSourceInfo source = new MigrationSourceInfo();
        public string coordinateSystem = "glTF_Y_UP_RIGHT_HANDED";
        public List<MigrationModelEntry> models = new List<MigrationModelEntry>();
        public List<MigrationTimelineEntry> timelines = new List<MigrationTimelineEntry>();
        public List<MigrationParticleEntry> particles = new List<MigrationParticleEntry>();
        public List<MigrationAudioEntry> audio = new List<MigrationAudioEntry>();
        public List<MigrationTextureEntry> textures = new List<MigrationTextureEntry>();
    }

    [Serializable]
    public class MigrationSourceInfo
    {
        public string name;
        public string type;
        public string assetPath;
    }

    [Serializable]
    public class MigrationModelEntry
    {
        public string file;
        public string name;
        public string rootStableId;
    }

    [Serializable]
    public class MigrationTimelineEntry
    {
        public string name;
        public string otioFile;
        public string unityFile;
        public string directorHierarchyPath;
        public string playableAssetPath;
    }

    [Serializable]
    public class MigrationParticleEntry
    {
        public string name;
        public string file;
        public string altouraStableId;
        public string hierarchyPath;
    }

    [Serializable]
    public class MigrationAudioEntry
    {
        public string file;
        public string originalPath;
    }

    [Serializable]
    public class MigrationTextureEntry
    {
        public string file;
        public string originalPath;
    }

    public static class PackageManifestBuilder
    {
        public static MigrationManifest Create(string sourceName, string sourceType, string assetPath, string rootStableId, string modelRelativePath)
        {
            return new MigrationManifest
            {
                unityVersion = UnityEngine.Application.unityVersion,
                exportedAt = DateTime.UtcNow.ToString("o"),
                source = new MigrationSourceInfo
                {
                    name = sourceName,
                    type = sourceType,
                    assetPath = assetPath
                },
                models = new List<MigrationModelEntry>
                {
                    new MigrationModelEntry
                    {
                        file = modelRelativePath,
                        name = sourceName,
                        rootStableId = rootStableId
                    }
                }
            };
        }
    }
}
