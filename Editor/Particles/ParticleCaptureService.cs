using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Altoura.Migration.Editor
{
    [Serializable]
    public class ParticleSystemCaptureDocument
    {
        public string schemaVersion = "1.0.0";
        public string name;
        public string altouraStableId;
        public string hierarchyPath;
        public bool isPlaying;
        public ParticleMainModuleCapture main = new ParticleMainModuleCapture();
        public ParticleEmissionModuleCapture emission = new ParticleEmissionModuleCapture();
        public ParticleShapeModuleCapture shape = new ParticleShapeModuleCapture();
    }

    [Serializable]
    public class ParticleMainModuleCapture
    {
        public float duration;
        public bool loop;
        public float startLifetime;
        public float startSpeed;
        public float startSize;
        public Color startColor;
        public float gravityModifier;
        public int maxParticles;
    }

    [Serializable]
    public class ParticleEmissionModuleCapture
    {
        public bool enabled;
        public float rateOverTime;
        public float rateOverDistance;
    }

    [Serializable]
    public class ParticleShapeModuleCapture
    {
        public bool enabled;
        public string shapeType;
        public float angle;
        public float radius;
    }

    public static class ParticleCaptureService
    {
        public static List<ParticleSystemCaptureDocument> CaptureFromHierarchy(GameObject root)
        {
            var captures = new List<ParticleSystemCaptureDocument>();
            if (root == null)
            {
                return captures;
            }

            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particleSystem in particleSystems)
            {
                captures.Add(CaptureParticleSystem(particleSystem, root));
            }

            return captures;
        }

        public static void WriteCaptures(string outputDirectory, List<ParticleSystemCaptureDocument> captures, MigrationManifest manifest)
        {
            if (captures == null || captures.Count == 0)
            {
                return;
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            foreach (var capture in captures)
            {
                var fileName = TimelineCaptureService.SanitizeFileName(capture.name) + ".json";
                var relativePath = "particles/" + fileName;
                var filePath = Path.Combine(outputDirectory, fileName);
                JsonFileWriter.WriteJson(filePath, capture);

                manifest.particles.Add(new MigrationParticleEntry
                {
                    name = capture.name,
                    file = relativePath,
                    altouraStableId = capture.altouraStableId,
                    hierarchyPath = capture.hierarchyPath
                });
            }
        }

        private static ParticleSystemCaptureDocument CaptureParticleSystem(ParticleSystem particleSystem, GameObject root)
        {
            var main = particleSystem.main;
            var emission = particleSystem.emission;
            var shape = particleSystem.shape;

            return new ParticleSystemCaptureDocument
            {
                name = particleSystem.gameObject.name,
                altouraStableId = StableIdRegistry.GetStableId(particleSystem.gameObject),
                hierarchyPath = StableIdRegistry.GetHierarchyPath(particleSystem.gameObject, root),
                isPlaying = particleSystem.isPlaying,
                main = new ParticleMainModuleCapture
                {
                    duration = main.duration,
                    loop = main.loop,
                    startLifetime = main.startLifetime.constant,
                    startSpeed = main.startSpeed.constant,
                    startSize = main.startSize.constant,
                    startColor = main.startColor.color,
                    gravityModifier = main.gravityModifier.constant,
                    maxParticles = main.maxParticles
                },
                emission = new ParticleEmissionModuleCapture
                {
                    enabled = emission.enabled,
                    rateOverTime = emission.rateOverTime.constant,
                    rateOverDistance = emission.rateOverDistance.constant
                },
                shape = new ParticleShapeModuleCapture
                {
                    enabled = shape.enabled,
                    shapeType = shape.shapeType.ToString(),
                    angle = shape.angle,
                    radius = shape.radius
                }
            };
        }
    }
}
