using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    /// <summary>
    /// Derives deterministic altouraStableId values from Unity GlobalObjectId.
    /// </summary>
    public static class StableIdService
    {
        private static readonly Guid NamespaceUuid = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        public static string GetStableId(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            if (globalId.identifierType != 0)
            {
                return CreateDeterministicUuid(globalId.ToString());
            }

            return CreateDeterministicUuid("instance:" + gameObject.GetInstanceID());
        }

        public static string GetStableId(Component component)
        {
            return component == null ? null : GetStableId(component.gameObject);
        }

        public static string CreateDeterministicUuid(string seed)
        {
            if (string.IsNullOrEmpty(seed))
            {
                return Guid.NewGuid().ToString();
            }

            var namespaceBytes = NamespaceUuid.ToByteArray();
            SwapGuidByteOrder(namespaceBytes);

            var seedBytes = Encoding.UTF8.GetBytes(seed);
            var combined = new byte[namespaceBytes.Length + seedBytes.Length];
            Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
            Buffer.BlockCopy(seedBytes, 0, combined, namespaceBytes.Length, seedBytes.Length);

            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(combined);

                // SHA-1 yields 20 bytes; a GUID needs exactly 16.
                var guidBytes = new byte[16];
                Buffer.BlockCopy(hash, 0, guidBytes, 0, 16);

                guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
                guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
                SwapGuidByteOrder(guidBytes);
                return new Guid(guidBytes).ToString();
            }
        }

        private static void SwapGuidByteOrder(byte[] guidBytes)
        {
            if (guidBytes == null || guidBytes.Length != 16)
            {
                return;
            }

            Swap(guidBytes, 0, 3);
            Swap(guidBytes, 1, 2);
            Swap(guidBytes, 4, 5);
            Swap(guidBytes, 6, 7);
        }

        private static void Swap(byte[] bytes, int a, int b)
        {
            var temp = bytes[a];
            bytes[a] = bytes[b];
            bytes[b] = temp;
        }
    }
}
