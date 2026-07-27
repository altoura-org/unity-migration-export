using UnityEngine;

namespace Altoura.Migration.Editor
{
    /// <summary>
    /// Converts Unity transforms to glTF / Three.js coordinate space.
    ///
    /// These MUST match UnityGLTF's per-node local TRS conversion exactly, or
    /// timeline keyframes replay in a different frame than the baked GLB mesh
    /// (object jumps to a wrong pose). UnityGLTF (SchemaExtensions.SetUnityTransform)
    /// uses CoordinateSpaceConversionScale = (-1, 1, 1):
    ///   position -> (-x, y, z)
    ///   rotation -> (x, -y, -z, w)   (ToGltfQuaternionConvert)
    ///   scale    -> unchanged
    /// </summary>
    public static class CoordinateConverter
    {
        public static Vector3 ToGltfPosition(Vector3 unityPosition)
        {
            return new Vector3(-unityPosition.x, unityPosition.y, unityPosition.z);
        }

        public static Quaternion ToGltfRotation(Quaternion unityRotation)
        {
            return new Quaternion(unityRotation.x, -unityRotation.y, -unityRotation.z, unityRotation.w);
        }

        public static Vector3 ToGltfScale(Vector3 unityScale)
        {
            return unityScale;
        }

        public static float[] ToGltfPositionArray(Vector3 unityPosition)
        {
            var converted = ToGltfPosition(unityPosition);
            return new[] { converted.x, converted.y, converted.z };
        }

        public static float[] ToGltfRotationArray(Quaternion unityRotation)
        {
            var converted = ToGltfRotation(unityRotation);
            return new[] { converted.x, converted.y, converted.z, converted.w };
        }

        public static float[] ToGltfScaleArray(Vector3 unityScale)
        {
            var converted = ToGltfScale(unityScale);
            return new[] { converted.x, converted.y, converted.z };
        }
    }
}
