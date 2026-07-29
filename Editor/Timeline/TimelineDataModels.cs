using System;
using System.Collections.Generic;

namespace Altoura.Migration.Editor
{
    [Serializable]
    public class UnityTimelineCaptureDocument
    {
        public string schemaVersion = "1.0.0";
        public string timelineName;
        public double duration;
        public double fps = 60;
        public string directorHierarchyPath;
        public string playableAssetPath;
        public string timelineId;
        public List<UnityTimelineTrackCapture> tracks = new List<UnityTimelineTrackCapture>();
    }

    [Serializable]
    public class UnityTimelineTrackCapture
    {
        public string trackType;
        public string trackName;
        public UnityTimelineBindingCapture binding;
        public List<UnityTimelineClipCapture> clips = new List<UnityTimelineClipCapture>();
    }

    [Serializable]
    public class UnityTimelineBindingCapture
    {
        public string hierarchyPath;
        public string altouraStableId;
        public string bindingType;
    }

    [Serializable]
    public class UnityTimelineClipCapture
    {
        public string displayName;
        public string clipAssetType;
        public double start;
        public double duration;
        public double clipIn;
        public double easeInDuration;
        public double easeOutDuration;
        public bool hasEmbeddedAnimationClip;
        public string animationClipName;
        public bool isInfiniteClip;

        // Timeline-space times (seconds) of the source AnimationClip's real
        // keyframes. OTIO samples only these anchors (plus clip boundaries)
        // instead of baking every frame. Empty => clip boundaries only.
        public List<double> anchorTimes = new List<double>();

        // Timeline applies a rigid offset on top of the clip's animated root
        // pose (AnimationTrack.position/rotation composed with the clip's own
        // or the track's infinite-clip offset). Sampling the AnimationClip
        // directly bypasses that offset, so it is recorded here and re-composed
        // when OTIO keyframes are written. Only meaningful when the clip
        // animates the bound object's own transform.
        public bool animatesRootTransform;
        public UnityEngine.Vector3 offsetPosition;
        public UnityEngine.Quaternion offsetRotation = UnityEngine.Quaternion.identity;

        // Editor-only source used during the same export pass. It is excluded
        // from the lossless .unity.json sidecar and never leaves Unity.
        [NonSerialized]
        public UnityEngine.AnimationClip sourceAnimationClip;
    }

    [Serializable]
    public class OtioSerializableCollectionDocument
    {
        public string OTIO_SCHEMA = "SerializableCollection.1";
        public string name = "Unity Migration Timelines";
        public List<OtioTimelineDocument> children = new List<OtioTimelineDocument>();
    }

    [Serializable]
    public class OtioTimelineDocument
    {
        public string OTIO_SCHEMA = "Timeline.1";
        public string name;
        public string timelineId;
        public OtioTimelineMetadata metadata = new OtioTimelineMetadata();
        public OtioStackDocument tracks = new OtioStackDocument();
    }

    [Serializable]
    public class OtioTimelineMetadata
    {
        public double fps = 60;
        public double duration;
        public double contentDuration;
        public bool loopEnabled;
    }

    [Serializable]
    public class OtioStackDocument
    {
        public string OTIO_SCHEMA = "Stack.1";
        public string name;
        public List<OtioSequenceDocument> children = new List<OtioSequenceDocument>();
    }

    [Serializable]
    public class OtioSequenceDocument
    {
        public string OTIO_SCHEMA = "Sequence.1";
        public string name;
        public string kind = "Animation";
        public OtioSequenceMetadata metadata = new OtioSequenceMetadata();
        public List<object> children = new List<object>();
    }

    [Serializable]
    public class OtioSequenceMetadata
    {
        public List<OtioChannelDocument> channels = new List<OtioChannelDocument>();
    }

    [Serializable]
    public class OtioChannelDocument
    {
        public string trackId;
        public string assetInstanceStableId;
        public string childObjectStableId;
        public string objectName;
        public string property;
        public bool visible = true;
        public bool locked;
        public string type = "default";
        public string audioFileName;
        public List<OtioKeyframeDocument> keyframes = new List<OtioKeyframeDocument>();
    }

    [Serializable]
    public class OtioKeyframeDocument
    {
        public string id;
        public OtioRationalTimeDocument time = new OtioRationalTimeDocument();
        public string valueJson;
        public string interpolation = "linear";
        public bool isBaked = true;
    }

    [Serializable]
    public class OtioRationalTimeDocument
    {
        public string OTIO_SCHEMA = "RationalTime.1";
        public double value;
        public double rate = 1;
    }

    [Serializable]
    public class OtioAudioClipValueDocument
    {
        public double clipStartOffset;
        public double clipEndOffset;
        public double volume = 1;
        public double originalDuration;
        public double startTimeInTimeline;
    }
}
