using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Altoura.Migration.Editor
{
    public static class TimelineOtioConverter
    {
        private const double SampleFps = 60.0;

        public static OtioTimelineDocument Convert(UnityTimelineCaptureDocument capture, PlayableDirector director, GameObject exportRoot, string rootStableId)
        {
            if (capture == null || director == null)
            {
                return null;
            }

            var timeline = new OtioTimelineDocument
            {
                name = capture.timelineName,
                timelineId = capture.timelineId,
                metadata = new OtioTimelineMetadata
                {
                    fps = SampleFps,
                    duration = capture.duration,
                    contentDuration = capture.duration,
                    loopEnabled = false
                },
                tracks = new OtioStackDocument
                {
                    name = capture.timelineName + " Tracks"
                }
            };

            foreach (var trackCapture in capture.tracks)
            {
                if (trackCapture.trackType != null && trackCapture.trackType.IndexOf("ActivationTrack", StringComparison.Ordinal) >= 0)
                {
                    AddActivationChannels(timeline, trackCapture, rootStableId);
                    continue;
                }

                if (trackCapture.trackType != null && trackCapture.trackType.IndexOf("AnimationTrack", StringComparison.Ordinal) >= 0)
                {
                    AddAnimationChannels(timeline, trackCapture, rootStableId);
                    continue;
                }

                if (trackCapture.trackType != null && trackCapture.trackType.IndexOf("AudioTrack", StringComparison.Ordinal) >= 0)
                {
                    AddAudioChannels(timeline, trackCapture, rootStableId);
                }
            }

            return timeline;
        }

        private static void AddActivationChannels(OtioTimelineDocument timeline, UnityTimelineTrackCapture trackCapture, string rootStableId)
        {
            if (trackCapture.binding == null || string.IsNullOrEmpty(trackCapture.binding.altouraStableId))
            {
                return;
            }

            var keyframes = new List<OtioKeyframeDocument>();
            foreach (var clip in trackCapture.clips)
            {
                keyframes.Add(CreateStepKeyframe(clip.start, true));
                keyframes.Add(CreateStepKeyframe(clip.start + clip.duration, false));
            }

            if (keyframes.Count == 0)
            {
                return;
            }

            timeline.tracks.children.Add(CreateSequence(
                trackCapture.trackName + "-visible",
                trackCapture.binding.altouraStableId,
                rootStableId,
                trackCapture.binding.hierarchyPath,
                "visible",
                "visibility",
                keyframes));
        }

        private static void AddAnimationChannels(
            OtioTimelineDocument timeline,
            UnityTimelineTrackCapture trackCapture,
            string rootStableId)
        {
            if (trackCapture.binding == null || string.IsNullOrEmpty(trackCapture.binding.altouraStableId))
            {
                return;
            }

            var boundObject = StableIdRegistry.GetGameObject(trackCapture.binding.altouraStableId);
            if (boundObject == null)
            {
                return;
            }

            foreach (var clip in trackCapture.clips)
            {
                SampleTransformClip(timeline, trackCapture, boundObject, rootStableId, clip);
            }
        }

        private static void SampleTransformClip(
            OtioTimelineDocument timeline,
            UnityTimelineTrackCapture trackCapture,
            GameObject boundObject,
            string rootStableId,
            UnityTimelineClipCapture clip)
        {
            if (clip.sourceAnimationClip == null)
            {
                Debug.LogWarning(
                    "[AltouraMigration] Skipping animation clip '" + clip.displayName +
                    "' because its source AnimationClip is unavailable.");
                return;
            }

            var positionKeys = new List<OtioKeyframeDocument>();
            var rotationKeys = new List<OtioKeyframeDocument>();
            var scaleKeys = new List<OtioKeyframeDocument>();

            // Sample only source key times. The previous implementation baked at
            // 60 FPS, turning a two-key clip into hundreds of redundant OTIO keys.
            // AnimationClip.SampleAnimation works in Edit Mode without depending
            // on a PlayableDirector graph.
            var sampleTimes = BuildSampleTimes(clip);
            var snapshots = CaptureTransformSnapshots(boundObject);
            try
            {
                foreach (var timelineTime in sampleTimes)
                {
                    var sourceTime = timelineTime - clip.start + clip.clipIn;
                    sourceTime = Math.Max(0, Math.Min(clip.sourceAnimationClip.length, sourceTime));
                    clip.sourceAnimationClip.SampleAnimation(boundObject, (float)sourceTime);

                    var transform = boundObject.transform;
                    var localPosition = transform.localPosition;
                    var localRotation = transform.localRotation;

                    // SampleAnimation writes the clip's raw pose; Timeline additionally
                    // applies the track/clip offset recorded at capture time. Without
                    // this the object snaps away from its GLB pose as soon as the first
                    // keyframe is applied, and rotated offsets skew its motion axes.
                    if (clip.animatesRootTransform)
                    {
                        localPosition = clip.offsetPosition + clip.offsetRotation * localPosition;
                        localRotation = clip.offsetRotation * localRotation;
                    }

                    positionKeys.Add(CreateLinearKeyframe(
                        timelineTime,
                        CoordinateConverter.ToGltfPositionArray(localPosition),
                        false));
                    rotationKeys.Add(CreateLinearKeyframe(
                        timelineTime,
                        CoordinateConverter.ToGltfRotationArray(localRotation),
                        false));
                    scaleKeys.Add(CreateLinearKeyframe(
                        timelineTime,
                        CoordinateConverter.ToGltfScaleArray(transform.localScale),
                        false));
                }
            }
            finally
            {
                RestoreTransformSnapshots(snapshots);
            }

            var emittedChannels = 0;
            if (!IsConstant(positionKeys))
            {
                timeline.tracks.children.Add(CreateSequence(
                    trackCapture.trackName + "-position",
                    trackCapture.binding.altouraStableId,
                    rootStableId,
                    trackCapture.binding.hierarchyPath,
                    "position",
                    "default",
                    positionKeys));
                emittedChannels++;
            }

            if (!IsConstant(rotationKeys))
            {
                timeline.tracks.children.Add(CreateSequence(
                    trackCapture.trackName + "-rotation",
                    trackCapture.binding.altouraStableId,
                    rootStableId,
                    trackCapture.binding.hierarchyPath,
                    "rotation",
                    "default",
                    rotationKeys));
                emittedChannels++;
            }

            if (!IsConstant(scaleKeys))
            {
                timeline.tracks.children.Add(CreateSequence(
                    trackCapture.trackName + "-scale",
                    trackCapture.binding.altouraStableId,
                    rootStableId,
                    trackCapture.binding.hierarchyPath,
                    "scale",
                    "default",
                    scaleKeys));
                emittedChannels++;
            }

            if (emittedChannels == 0)
            {
                Debug.LogWarning(
                    "[AltouraMigration] Animation clip '" + clip.displayName +
                    "' produced no transform changes for '" + trackCapture.binding.hierarchyPath + "'.");
            }
        }

        private static List<double> BuildSampleTimes(UnityTimelineClipCapture clip)
        {
            var times = new SortedSet<double>();
            var start = clip.start;
            var end = clip.start + clip.duration;
            times.Add(start);
            times.Add(end);

            if (clip.anchorTimes != null)
            {
                foreach (var anchorTime in clip.anchorTimes)
                {
                    if (anchorTime < start - 0.000001 || anchorTime > end + 0.000001)
                    {
                        continue;
                    }

                    times.Add(Math.Max(start, Math.Min(end, anchorTime)));
                }
            }

            return new List<double>(times);
        }

        private static List<TransformSnapshot> CaptureTransformSnapshots(GameObject root)
        {
            var snapshots = new List<TransformSnapshot>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                snapshots.Add(new TransformSnapshot(transform));
            }

            return snapshots;
        }

        private static void RestoreTransformSnapshots(List<TransformSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }
        }

        private sealed class TransformSnapshot
        {
            private readonly Transform transform;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;

            public TransformSnapshot(Transform transform)
            {
                this.transform = transform;
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
            }

            public void Restore()
            {
                if (transform == null)
                {
                    return;
                }

                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = localScale;
            }
        }

        private static void AddAudioChannels(OtioTimelineDocument timeline, UnityTimelineTrackCapture trackCapture, string rootStableId)
        {
            if (trackCapture.binding == null || string.IsNullOrEmpty(trackCapture.binding.altouraStableId))
            {
                return;
            }

            foreach (var clip in trackCapture.clips)
            {
                var audioValue = new OtioAudioClipValueDocument
                {
                    clipStartOffset = clip.clipIn,
                    clipEndOffset = clip.clipIn + clip.duration,
                    volume = 1,
                    originalDuration = clip.duration,
                    startTimeInTimeline = clip.start
                };

                var keyframes = new List<OtioKeyframeDocument>
                {
                    new OtioKeyframeDocument
                    {
                        id = Guid.NewGuid().ToString(),
                        interpolation = "step",
                        isBaked = true,
                        time = new OtioRationalTimeDocument { value = clip.start, rate = 1 },
                        valueJson = JsonUtility.ToJson(audioValue)
                    }
                };

                timeline.tracks.children.Add(CreateSequence(
                    trackCapture.trackName + "-audio",
                    trackCapture.binding.altouraStableId,
                    rootStableId,
                    trackCapture.binding.hierarchyPath,
                    "audioClip",
                    "audio",
                    keyframes,
                    clip.displayName));
            }
        }

        private static OtioSequenceDocument CreateSequence(
            string sequenceName,
            string childStableId,
            string rootStableId,
            string objectName,
            string property,
            string channelType,
            List<OtioKeyframeDocument> keyframes,
            string audioFileName = null)
        {
            var channel = new OtioChannelDocument
            {
                trackId = Guid.NewGuid().ToString(),
                assetInstanceStableId = rootStableId,
                childObjectStableId = childStableId == rootStableId ? string.Empty : childStableId,
                objectName = objectName,
                property = property,
                visible = true,
                locked = false,
                type = channelType,
                audioFileName = audioFileName,
                keyframes = keyframes
            };

            return new OtioSequenceDocument
            {
                name = sequenceName,
                kind = channelType == "audio" ? "Audio" : "Animation",
                metadata = new OtioSequenceMetadata
                {
                    channels = new List<OtioChannelDocument> { channel }
                }
            };
        }

        private static bool IsConstant(List<OtioKeyframeDocument> keyframes)
        {
            if (keyframes == null || keyframes.Count < 2)
            {
                return true;
            }

            var first = keyframes[0].valueJson;
            for (var i = 1; i < keyframes.Count; i++)
            {
                if (!string.Equals(keyframes[i].valueJson, first, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static OtioKeyframeDocument CreateStepKeyframe(double time, bool visible)
        {
            return new OtioKeyframeDocument
            {
                id = Guid.NewGuid().ToString(),
                interpolation = "step",
                isBaked = true,
                time = new OtioRationalTimeDocument { value = time, rate = 1 },
                valueJson = visible ? "true" : "false"
            };
        }

        private static OtioKeyframeDocument CreateLinearKeyframe(double time, float[] vectorValue, bool isBaked = true)
        {
            return new OtioKeyframeDocument
            {
                id = Guid.NewGuid().ToString(),
                interpolation = "linear",
                isBaked = isBaked,
                time = new OtioRationalTimeDocument { value = time, rate = 1 },
                valueJson = FloatArrayToJson(vectorValue)
            };
        }

        private static string FloatArrayToJson(float[] values)
        {
            var parts = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                parts[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            }

            return "[" + string.Join(",", parts) + "]";
        }
    }
}
