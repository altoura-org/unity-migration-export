using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Altoura.Migration.Editor
{
    public static class TimelineCaptureService
    {
        public static UnityTimelineCaptureDocument CaptureDirector(PlayableDirector director, GameObject exportRoot)
        {
            if (director == null)
            {
                return null;
            }

            var timelineAsset = director.playableAsset as TimelineAsset;
            if (timelineAsset == null)
            {
                return null;
            }

            var playableAssetPath = AssetDatabase.GetAssetPath(timelineAsset);
            var document = new UnityTimelineCaptureDocument
            {
                timelineName = timelineAsset.name,
                duration = timelineAsset.duration,
                fps = 60,
                directorHierarchyPath = StableIdRegistry.GetHierarchyPath(director.gameObject, exportRoot),
                playableAssetPath = playableAssetPath,
                timelineId = StableIdService.CreateDeterministicUuid(playableAssetPath + ":" + timelineAsset.name)
            };

            foreach (var output in timelineAsset.outputs)
            {
                var track = output.sourceObject as TrackAsset;
                if (track == null)
                {
                    continue;
                }

                var trackCapture = new UnityTimelineTrackCapture
                {
                    trackType = track.GetType().FullName,
                    trackName = track.name
                };

                var binding = director.GetGenericBinding(track);
                trackCapture.binding = CreateBindingCapture(binding, exportRoot);

                foreach (var clip in track.GetClips())
                {
                    var clipCapture = new UnityTimelineClipCapture
                    {
                        displayName = clip.displayName,
                        clipAssetType = clip.asset != null ? clip.asset.GetType().FullName : null,
                        start = clip.start,
                        duration = clip.duration,
                        clipIn = clip.clipIn,
                        easeInDuration = clip.easeInDuration,
                        easeOutDuration = clip.easeOutDuration
                    };

                    if (clip.asset is AnimationPlayableAsset animationPlayableAsset && animationPlayableAsset.clip != null)
                    {
                        clipCapture.hasEmbeddedAnimationClip = true;
                        clipCapture.animationClipName = animationPlayableAsset.clip.name;
                        clipCapture.anchorTimes = ExtractAnchorTimes(animationPlayableAsset.clip, clip.start, clip.clipIn);
                        clipCapture.sourceAnimationClip = animationPlayableAsset.clip;
                        CaptureOffsets(
                            clipCapture,
                            track as AnimationTrack,
                            animationPlayableAsset.clip,
                            animationPlayableAsset.position,
                            animationPlayableAsset.rotation);
                    }

                    trackCapture.clips.Add(clipCapture);
                }

                // Recorded (infinite) clips live outside the clip list; synthesize a
                // clip spanning the recording so the OTIO sampler picks them up.
                if (trackCapture.clips.Count == 0 &&
                    track is AnimationTrack animationTrack &&
                    animationTrack.infiniteClip != null)
                {
                    var infiniteCapture = new UnityTimelineClipCapture
                    {
                        displayName = animationTrack.infiniteClip.name,
                        clipAssetType = "InfiniteClip",
                        start = 0,
                        duration = animationTrack.infiniteClip.length,
                        clipIn = 0,
                        hasEmbeddedAnimationClip = true,
                        animationClipName = animationTrack.infiniteClip.name,
                        isInfiniteClip = true,
                        anchorTimes = ExtractAnchorTimes(animationTrack.infiniteClip, 0, 0),
                        sourceAnimationClip = animationTrack.infiniteClip
                    };

                    CaptureOffsets(
                        infiniteCapture,
                        animationTrack,
                        animationTrack.infiniteClip,
                        animationTrack.infiniteClipOffsetPosition,
                        animationTrack.infiniteClipOffsetRotation);

                    trackCapture.clips.Add(infiniteCapture);
                }

                document.tracks.Add(trackCapture);
            }

            return document;
        }

        public static List<PlayableDirector> FindDirectors(GameObject root, string timelineNameFilter)
        {
            var directors = new List<PlayableDirector>();
            if (root == null)
            {
                return directors;
            }

            var allDirectors = root.GetComponentsInChildren<PlayableDirector>(true);
            foreach (var director in allDirectors)
            {
                if (director.playableAsset == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(timelineNameFilter) &&
                    director.playableAsset.name.IndexOf(timelineNameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                directors.Add(director);
            }

            return directors;
        }

        public static void WriteCapture(string outputDirectory, UnityTimelineCaptureDocument document, string fileNameWithoutExtension)
        {
            if (document == null)
            {
                return;
            }

            var filePath = Path.Combine(outputDirectory, fileNameWithoutExtension + ".unity.json");
            JsonFileWriter.WriteJson(filePath, document);
        }

        // Collects the distinct timeline-space times of every real keyframe across
        // all of the AnimationClip's curves (the union, so any property's keyframe
        // becomes an anchor). Clip-local key times are shifted by the clip's start
        // and clipIn; clip timeScale is assumed to be 1 (the migration norm).
        private static List<double> ExtractAnchorTimes(AnimationClip clip, double clipStart, double clipIn)
        {
            var times = new SortedSet<double>();
            if (clip == null)
            {
                return new List<double>();
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                {
                    continue;
                }

                foreach (var key in curve.keys)
                {
                    times.Add(clipStart + (key.time - clipIn));
                }
            }

            return new List<double>(times);
        }

        // Mirrors Timeline's own offset composition (TimelineRecording.GetLocalToTrack):
        // the track offset is applied first, then the clip's offset, and the result
        // is a rigid transform in the bound object's parent space. Offsets only take
        // effect when the clip drives the bound object's own transform, which is why
        // that is recorded alongside them.
        private static void CaptureOffsets(
            UnityTimelineClipCapture capture,
            AnimationTrack track,
            AnimationClip clip,
            Vector3 clipOffsetPosition,
            Quaternion clipOffsetRotation)
        {
            if (track == null)
            {
                return;
            }

            capture.animatesRootTransform = AnimatesRootTransform(clip);
            if (!capture.animatesRootTransform)
            {
                return;
            }

            var trackPosition = Vector3.zero;
            var trackRotation = Quaternion.identity;

            if (track.trackOffset == TrackOffset.ApplyTransformOffsets)
            {
                trackPosition = track.position;
                trackRotation = track.rotation;
            }
            else
            {
                // Scene-offset modes derive the starting pose from the live scene at
                // preview time, which is not reachable from the asset. The clip offset
                // is still exported; only the track portion is dropped.
                Debug.LogWarning(
                    "[AltouraMigration] Track '" + track.name + "' uses " + track.trackOffset +
                    "; only its clip offset is exported.");
            }

            capture.offsetPosition = trackPosition + trackRotation * clipOffsetPosition;
            capture.offsetRotation = trackRotation * clipOffsetRotation;
        }

        private static bool AnimatesRootTransform(AnimationClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (string.IsNullOrEmpty(binding.path) && binding.type == typeof(Transform))
                {
                    return true;
                }
            }

            return false;
        }

        private static UnityTimelineBindingCapture CreateBindingCapture(UnityEngine.Object binding, GameObject exportRoot)
        {
            if (binding == null)
            {
                return null;
            }

            GameObject boundObject = null;
            if (binding is GameObject gameObject)
            {
                boundObject = gameObject;
            }
            else if (binding is Component component)
            {
                boundObject = component.gameObject;
            }

            if (boundObject == null)
            {
                return new UnityTimelineBindingCapture
                {
                    bindingType = binding.GetType().FullName
                };
            }

            return new UnityTimelineBindingCapture
            {
                hierarchyPath = StableIdRegistry.GetHierarchyPath(boundObject, exportRoot),
                altouraStableId = StableIdRegistry.GetStableId(boundObject),
                bindingType = binding.GetType().FullName
            };
        }

        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "timeline";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }
    }
}
