using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    public static class AudioAssetExtractor
    {
        public static void ExtractFromTimelineCaptures(
            string outputDirectory,
            List<UnityTimelineCaptureDocument> captures,
            MigrationManifest manifest)
        {
            if (captures == null || captures.Count == 0)
            {
                return;
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var copiedAssets = new HashSet<string>();
            foreach (var capture in captures)
            {
                foreach (var track in capture.tracks)
                {
                    if (track.trackType == null || track.trackType.IndexOf("AudioTrack", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    foreach (var clip in track.clips)
                    {
                        if (string.IsNullOrEmpty(clip.displayName))
                        {
                            continue;
                        }

                        var audioClip = FindAudioClipByName(clip.displayName);
                        if (audioClip == null)
                        {
                            continue;
                        }

                        var sourcePath = AssetDatabase.GetAssetPath(audioClip);
                        if (string.IsNullOrEmpty(sourcePath) || copiedAssets.Contains(sourcePath))
                        {
                            continue;
                        }

                        var fileName = Path.GetFileName(sourcePath);
                        var destinationPath = Path.Combine(outputDirectory, fileName);
                        if (!File.Exists(sourcePath))
                        {
                            continue;
                        }

                        File.Copy(sourcePath, destinationPath, true);
                        copiedAssets.Add(sourcePath);

                        manifest.audio.Add(new MigrationAudioEntry
                        {
                            file = "audio/" + fileName,
                            originalPath = sourcePath
                        });
                    }
                }
            }
        }

        private static AudioClip FindAudioClipByName(string clipName)
        {
            var guids = AssetDatabase.FindAssets(clipName + " t:AudioClip");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null && clip.name == clipName)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
