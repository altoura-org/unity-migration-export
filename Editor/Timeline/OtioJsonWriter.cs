using System.Globalization;
using System.IO;
using System.Text;

namespace Altoura.Migration.Editor
{
    public static class OtioJsonWriter
    {
        public static void WriteCollection(string filePath, OtioSerializableCollectionDocument collection)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"OTIO_SCHEMA\": \"SerializableCollection.1\",");
            builder.AppendLine("  \"name\": " + Quote(collection.name) + ",");
            builder.AppendLine("  \"children\": [");

            for (var i = 0; i < collection.children.Count; i++)
            {
                WriteTimeline(builder, collection.children[i], "    ");
                if (i < collection.children.Count - 1)
                {
                    builder.AppendLine(",");
                }
                else
                {
                    builder.AppendLine();
                }
            }

            builder.AppendLine("  ]");
            builder.Append("}");

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, builder.ToString());
        }

        public static void WriteTimelineFile(string filePath, OtioTimelineDocument timeline)
        {
            var collection = new OtioSerializableCollectionDocument
            {
                children = new System.Collections.Generic.List<OtioTimelineDocument> { timeline }
            };
            WriteCollection(filePath, collection);
        }

        private static void WriteTimeline(StringBuilder builder, OtioTimelineDocument timeline, string indent)
        {
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("  \"OTIO_SCHEMA\": \"Timeline.1\",");
            builder.Append(indent).Append("  \"name\": ").Append(Quote(timeline.name)).AppendLine(",");
            builder.Append(indent).Append("  \"timelineId\": ").Append(Quote(timeline.timelineId)).AppendLine(",");
            builder.Append(indent).AppendLine("  \"metadata\": {");
            builder.Append(indent).Append("    \"fps\": ").Append(timeline.metadata.fps.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append(indent).Append("    \"duration\": ").Append(timeline.metadata.duration.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append(indent).Append("    \"contentDuration\": ").Append(timeline.metadata.contentDuration.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append(indent).Append("    \"loopEnabled\": ").Append(timeline.metadata.loopEnabled ? "true" : "false").AppendLine();
            builder.Append(indent).AppendLine("  },");
            builder.Append(indent).AppendLine("  \"tracks\": {");
            builder.Append(indent).AppendLine("    \"OTIO_SCHEMA\": \"Stack.1\",");
            builder.Append(indent).Append("    \"name\": ").Append(Quote(timeline.tracks.name)).AppendLine(",");
            builder.Append(indent).AppendLine("    \"children\": [");

            for (var i = 0; i < timeline.tracks.children.Count; i++)
            {
                WriteSequence(builder, timeline.tracks.children[i], indent + "      ");
                if (i < timeline.tracks.children.Count - 1)
                {
                    builder.AppendLine(",");
                }
                else
                {
                    builder.AppendLine();
                }
            }

            builder.Append(indent).AppendLine("    ]");
            builder.Append(indent).AppendLine("  }");
            builder.Append(indent).Append("}");
        }

        private static void WriteSequence(StringBuilder builder, OtioSequenceDocument sequence, string indent)
        {
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("  \"OTIO_SCHEMA\": \"Sequence.1\",");
            builder.Append(indent).Append("  \"name\": ").Append(Quote(sequence.name)).AppendLine(",");
            builder.Append(indent).Append("  \"kind\": ").Append(Quote(sequence.kind)).AppendLine(",");
            builder.Append(indent).AppendLine("  \"metadata\": {");
            builder.Append(indent).AppendLine("    \"channels\": [");

            for (var i = 0; i < sequence.metadata.channels.Count; i++)
            {
                WriteChannel(builder, sequence.metadata.channels[i], indent + "      ");
                if (i < sequence.metadata.channels.Count - 1)
                {
                    builder.AppendLine(",");
                }
                else
                {
                    builder.AppendLine();
                }
            }

            builder.Append(indent).AppendLine("    ]");
            builder.Append(indent).AppendLine("  },");
            builder.Append(indent).AppendLine("  \"children\": []");
            builder.Append(indent).Append("}");
        }

        private static void WriteChannel(StringBuilder builder, OtioChannelDocument channel, string indent)
        {
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("  \"trackId\": ").Append(Quote(channel.trackId)).AppendLine(",");
            builder.Append(indent).Append("  \"assetInstanceStableId\": ").Append(Quote(channel.assetInstanceStableId)).AppendLine(",");
            builder.Append(indent).Append("  \"childObjectStableId\": ").Append(Quote(channel.childObjectStableId ?? string.Empty)).AppendLine(",");
            builder.Append(indent).Append("  \"objectName\": ").Append(Quote(channel.objectName)).AppendLine(",");
            builder.Append(indent).Append("  \"property\": ").Append(Quote(channel.property)).AppendLine(",");
            builder.Append(indent).Append("  \"visible\": ").Append(channel.visible ? "true" : "false").AppendLine(",");
            builder.Append(indent).Append("  \"locked\": ").Append(channel.locked ? "true" : "false").AppendLine(",");
            builder.Append(indent).Append("  \"type\": ").Append(Quote(channel.type)).AppendLine(",");

            if (!string.IsNullOrEmpty(channel.audioFileName))
            {
                builder.Append(indent).Append("  \"audioFileName\": ").Append(Quote(channel.audioFileName)).AppendLine(",");
            }

            builder.Append(indent).AppendLine("  \"keyframes\": [");
            for (var i = 0; i < channel.keyframes.Count; i++)
            {
                WriteKeyframe(builder, channel.keyframes[i], indent + "    ");
                if (i < channel.keyframes.Count - 1)
                {
                    builder.AppendLine(",");
                }
                else
                {
                    builder.AppendLine();
                }
            }

            builder.Append(indent).AppendLine("  ]");
            builder.Append(indent).Append("}");
        }

        private static void WriteKeyframe(StringBuilder builder, OtioKeyframeDocument keyframe, string indent)
        {
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("  \"id\": ").Append(Quote(keyframe.id)).AppendLine(",");
            builder.Append(indent).AppendLine("  \"time\": {");
            builder.Append(indent).AppendLine("    \"OTIO_SCHEMA\": \"RationalTime.1\",");
            builder.Append(indent).Append("    \"value\": ").Append(keyframe.time.value.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append(indent).Append("    \"rate\": ").Append(keyframe.time.rate.ToString(CultureInfo.InvariantCulture)).AppendLine();
            builder.Append(indent).AppendLine("  },");
            builder.Append(indent).Append("  \"value\": ").Append(keyframe.valueJson).AppendLine(",");
            builder.Append(indent).Append("  \"interpolation\": ").Append(Quote(keyframe.interpolation)).AppendLine(",");
            builder.Append(indent).Append("  \"isBaked\": ").Append(keyframe.isBaked ? "true" : "false").AppendLine();
            builder.Append(indent).Append("}");
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "null";
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
