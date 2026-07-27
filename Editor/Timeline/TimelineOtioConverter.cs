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
                    AddAnimationChannels(timeline, trackCapture, director, exportRoot, rootStableId);
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
            PlayableDirector director,
            GameObject exportRoot,
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
                SampleTransformClip(timeline, trackCapture, boundObject, director, rootStableId, clip);
            }
        }

        private static void SampleTransformClip(
            OtioTimelineDocument timeline,
            UnityTimelineTrackCapture trackCapture,
            GameObject boundObject,
            PlayableDirector director,
            string rootStableId,
            UnityTimelineClipCapture clip)
        {
            var positionKeys = new List<OtioKeyframeDocument>();
            var rotationKeys = new List<OtioKeyframeDocument>();
            var scaleKeys = new List<OtioKeyframeDocument>();

            var originalTime = director.time;
            var originalUpdateMode = director.timeUpdateMode;
            director.timeUpdateMode = DirectorUpdateMode.Manual;

            // In Edit Mode the director's graph is never built (it only builds on
            // Play), and Evaluate() on an unbuilt graph is a no-op — every sample
            // would read the same static pose. Build it explicitly for sampling.
            var graphWasValid = director.playableGraph.IsValid();
            if (!graphWasValid)
            {
                director.RebuildGraph();
            }

            var sampleCount = Math.Max(2, (int)Math.Ceiling(clip.duration * SampleFps) + 1);

            // The vNext timeline hides fully-baked tracks, so mark the sample
            // nearest each source keyframe as a non-baked "anchor" (editable dot).
            // Falls back to first+last when the clip carried no anchor times.
            var anchorIndices = BuildAnchorIndices(clip, sampleCount);

            for (var i = 0; i < sampleCount; i++)
            {
                var localTime = Math.Min(clip.duration, i / SampleFps);
                var timelineTime = clip.start + localTime;
                director.time = timelineTime;
                director.Evaluate();

                var isBaked = !anchorIndices.Contains(i);
                var transform = boundObject.transform;
                positionKeys.Add(CreateLinearKeyframe(timelineTime, CoordinateConverter.ToGltfPositionArray(transform.localPosition), isBaked));
                rotationKeys.Add(CreateLinearKeyframe(timelineTime, CoordinateConverter.ToGltfRotationArray(transform.localRotation), isBaked));
                scaleKeys.Add(CreateLinearKeyframe(timelineTime, CoordinateConverter.ToGltfScaleArray(transform.localScale), isBaked));
            }

            director.time = originalTime;
            director.timeUpdateMode = originalUpdateMode;
            director.Evaluate();

            // Restore edit-mode state: destroy the graph we built for sampling.
            if (!graphWasValid && director.playableGraph.IsValid())
            {
                director.playableGraph.Destroy();
            }

            if (IsConstant(positionKeys) && IsConstant(rotationKeys) && IsConstant(scaleKeys))
            {
                Debug.LogWarning(
                    "[AltouraMigration] Sampled transform never changed for track '" + trackCapture.trackName +
                    "' bound to '" + trackCapture.binding.hierarchyPath +
                    "'. The director may not be animating in Edit Mode (inactive object or disabled Animator?).");
            }

            timeline.tracks.children.Add(CreateSequence(
                trackCapture.trackName + "-position",
                trackCapture.binding.altouraStableId,
                rootStableId,
                trackCapture.binding.hierarchyPath,
                "position",
                "default",
                positionKeys));

            timeline.tracks.children.Add(CreateSequence(
                trackCapture.trackName + "-rotation",
                trackCapture.binding.altouraStableId,
                rootStableId,
                trackCapture.binding.hierarchyPath,
                "rotation",
                "default",
                rotationKeys));

            timeline.tracks.children.Add(CreateSequence(
                trackCapture.trackName + "-scale",
                trackCapture.binding.altouraStableId,
                rootStableId,
                trackCapture.binding.hierarchyPath,
                "scale",
                "default",
                scaleKeys));
        }

        // Maps each source keyframe time to the index of the closest baked sample.
        // These indices become non-baked anchors. When the clip has no recorded
        // anchor times, anchors the first and last sample so the track is visible.
        private static HashSet<int> BuildAnchorIndices(UnityTimelineClipCapture clip, int sampleCount)
        {
            var indices = new HashSet<int>();
            if (clip.anchorTimes != null)
            {
                foreach (var anchorTime in clip.anchorTimes)
                {
                    var localTime = anchorTime - clip.start;
                    if (localTime < 0 || localTime > clip.duration)
                    {
                        continue;
                    }

                    var index = (int)Math.Round(localTime * SampleFps);
                    if (index < 0)
                    {
                        index = 0;
                    }
                    else if (index >= sampleCount)
                    {
                        index = sampleCount - 1;
                    }

                    indices.Add(index);
                }
            }

            if (indices.Count == 0)
            {
                indices.Add(0);
                indices.Add(sampleCount - 1);
            }

            return indices;
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
