using UnityEngine;

namespace GPUParticles
{
    public static class ShurikenConverter
    {
        public static void Convert(GameObject owner)
        {
            if (owner == null)
            {
                Debug.LogError("Cannot convert Shuriken particle system without a valid GameObject reference.");
                return;
            }

            var ps = owner.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Debug.LogError("No ParticleSystem found for conversion.", owner);
                return;
            }

            var gpu = owner.GetComponent<GPUParticleSystem>();
            if (gpu == null) gpu = owner.AddComponent<GPUParticleSystem>();

            var main = ps.main;
            var emission = ps.emission;
            var colOver = ps.colorOverLifetime;
            var sizeOver = ps.sizeOverLifetime;
            var shape = ps.shape;
            var psr = owner.GetComponent<ParticleSystemRenderer>();

            // ---- Main ----
            gpu.maxParticles = main.maxParticles;
            gpu.simulationSpace = main.simulationSpace == ParticleSystemSimulationSpace.World ? SimulationSpace.World : SimulationSpace.Local;

            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant) gpu.startLifetime = main.startLifetime.constant;
            else Debug.LogWarning("StartLifetime is not constant; using main.startLifetime.constant.", owner);

            if (main.startSpeed.mode == ParticleSystemCurveMode.Constant) gpu.startSpeed = main.startSpeed.constant;
            else Debug.LogWarning("StartSpeed is not constant; using main.startSpeed.constant.", owner);

            if (main.startSize.mode == ParticleSystemCurveMode.Constant) gpu.startSize = main.startSize.constant;
            else Debug.LogWarning("StartSize is not constant; using main.startSize.constant.", owner);

            if (main.startColor.mode == ParticleSystemGradientMode.Color) gpu.startColor = main.startColor.color;
            else Debug.LogWarning("StartColor is not a single color; using main.startColor.color.", owner);

            if (main.gravityModifier.mode == ParticleSystemCurveMode.Constant) gpu.gravityModifier = main.gravityModifier.constant;
            else Debug.LogWarning("GravityModifier is not constant; using main.gravityModifier.constant.", owner);

            if (emission.rateOverTime.mode == ParticleSystemCurveMode.Constant) gpu.emissionRateOverTime = emission.rateOverTime.constant;
            else Debug.LogWarning("Emission.rateOverTime not constant; using constant value.", owner);

            // ---- Shape TRS ----
            gpu.shapeLocalPosition = shape.position;
            gpu.shapeLocalRotationEuler = shape.rotation;
            gpu.shapeLocalScale = shape.scale;

            // ---- Shape mapping ----
            gpu.shapeType = ShapeTypeGPU.Point;
            gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
            gpu.alignToDirection = shape.alignToDirection; // default false in Unity

            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Cone:
                case ParticleSystemShapeType.ConeShell:
                    gpu.shapeType = ShapeTypeGPU.Cone;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    gpu.shapeConeRadius = shape.radius;
                    gpu.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    gpu.shapeConeAngle = shape.angle;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.ConeVolume:
                case ParticleSystemShapeType.ConeVolumeShell:
                    gpu.shapeType = ShapeTypeGPU.Cone;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpu.shapeConeRadius = shape.radius;
                    gpu.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    gpu.shapeConeAngle = shape.angle;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                    gpu.shapeType = ShapeTypeGPU.Box;
                    gpu.shapeEmitFrom = (shape.shapeType == ParticleSystemShapeType.Box) ? ShapeEmitFromGPU.Volume : ShapeEmitFromGPU.Surface;
                    gpu.shapeBoxSize = shape.scale;
                    break;

                case ParticleSystemShapeType.Sphere:
                case ParticleSystemShapeType.SphereShell:
                    gpu.shapeType = ShapeTypeGPU.Sphere;
                    {
                        float avg = (shape.scale.x + shape.scale.y + shape.scale.z) / 3f;
                        gpu.shapeSphereRadius = Mathf.Max(0f, shape.radius * avg);
                    }
                    gpu.shapeRadiusThickness = (shape.shapeType == ParticleSystemShapeType.SphereShell) ? 0f : shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.Hemisphere:
                case ParticleSystemShapeType.HemisphereShell:
                    gpu.shapeType = ShapeTypeGPU.Hemisphere;
                    {
                        float avg = (shape.scale.x + shape.scale.y + shape.scale.z) / 3f;
                        gpu.shapeSphereRadius = Mathf.Max(0f, shape.radius * avg);
                    }
                    gpu.shapeRadiusThickness = (shape.shapeType == ParticleSystemShapeType.HemisphereShell) ? 0f : shape.radiusThickness;
                    break;
            }

            // ---- Renderer Module mapping ----
            if (psr != null)
            {
                switch (psr.renderMode)
                {
                    case ParticleSystemRenderMode.Billboard:           gpu.renderMode = GPURenderMode.Billboard; break;
                    case ParticleSystemRenderMode.Stretch:             gpu.renderMode = GPURenderMode.StretchedBillboard; break;
                    case ParticleSystemRenderMode.HorizontalBillboard: gpu.renderMode = GPURenderMode.HorizontalBillboard; break;
                    case ParticleSystemRenderMode.VerticalBillboard:   gpu.renderMode = GPURenderMode.VerticalBillboard; break;
                    case ParticleSystemRenderMode.Mesh:
                        Debug.LogWarning("RendererMode=Mesh is not supported in MVP (ignored).", owner);
                        break;
                }

                // Alignment（Stretched 内部忽略，但保留字段）
                switch (psr.alignment)
                {
                    case ParticleSystemRenderSpace.View:    gpu.renderAlignment = GPUAlignment.View;    break;
                    case ParticleSystemRenderSpace.Facing:  gpu.renderAlignment = GPUAlignment.Facing;  break;
                    case ParticleSystemRenderSpace.World:   gpu.renderAlignment = GPUAlignment.World;   break;
                    case ParticleSystemRenderSpace.Local:   gpu.renderAlignment = GPUAlignment.Local;   break;
                    case ParticleSystemRenderSpace.Velocity:gpu.renderAlignment = GPUAlignment.Velocity;break;
                }

                gpu.allowRoll = psr.allowRoll;
                gpu.pivot = new Vector2(psr.pivot.x, psr.pivot.y);
                gpu.normalDirection = psr.normalDirection;

                // Stretched-only
                gpu.stretchedLengthScale = psr.lengthScale;
                gpu.stretchedVelocityScale = psr.velocityScale;
                gpu.stretchedCameraVelocityScale = psr.cameraVelocityScale;
                gpu.freeformStretching = psr.freeformStretching;
                gpu.rotateWithStretchDirection = psr.rotateWithStretchDirection;
            }

            // ---- COL/Size over lifetime LUTs ----
            if (colOver.enabled)
            {
                Gradient g = colOver.color.gradient != null ? colOver.color.gradient : colOver.color.gradientMax;
                var lut = GradientLUTBuilder.Build(g, 256);
                gpu.colorOverLifetimeLUT = lut;
            }
            else gpu.colorOverLifetimeLUT = Texture2D.whiteTexture;

            if (sizeOver.enabled)
            {
                var mmc = sizeOver.size;
                AnimationCurve curveToBake = null;
                switch (mmc.mode)
                {
                    case ParticleSystemCurveMode.Constant:    curveToBake = AnimationCurve.Linear(0f, mmc.constant,    1f, mmc.constant); break;
                    case ParticleSystemCurveMode.Curve:       curveToBake = mmc.curve; break;
                    case ParticleSystemCurveMode.TwoConstants:curveToBake = AnimationCurve.Linear(0f, mmc.constantMax, 1f, mmc.constantMax); break;
                    case ParticleSystemCurveMode.TwoCurves:   curveToBake = mmc.curveMax != null ? mmc.curveMax : mmc.curve; break;
                }
                var lut = CurveLUTBuilder.Build(curveToBake, 256);
                gpu.sizeOverLifetimeLUT = lut;
            }
            else gpu.sizeOverLifetimeLUT = Texture2D.whiteTexture;

            Debug.Log("Shuriken → GPU conversion complete (Shapes + Renderer mapping).", owner);

            // Base map (texture) from ParticleSystemRenderer material
            if (psr != null)
            {
                Texture2D baseTex = null;
                // Prefer sharedMaterial to avoid instantiating
                var mat = psr.sharedMaterial;
                baseTex = TryGetBaseMap(mat);
                if (baseTex == null && psr.sharedMaterials != null)
                {
                    foreach (var m in psr.sharedMaterials)
                    {
                        baseTex = TryGetBaseMap(m);
                        if (baseTex != null) break;
                    }
                }
                if (baseTex != null)
                {
                    gpu.baseMap = baseTex;
                    // Optional: note assignment for clarity during conversion
                    Debug.Log($"Assigned baseMap from material '{mat?.name ?? "<array>"}': {baseTex.name}", owner);
                }
            }

            Debug.Log("Shuriken → GPU (MRT) basic conversion complete.", owner);
        }

        public static void Convert(ParticleSystem particleSystem)
        {
            Convert(particleSystem != null ? particleSystem.gameObject : null);
        }

        private static Texture2D TryGetBaseMap(Material m)
        {
            if (m == null) return null;
            // URP Lit/Unlit commonly use _BaseMap; legacy uses _MainTex
            if (m.HasProperty("_BaseMap"))
            {
                var t = m.GetTexture("_BaseMap") as Texture2D;
                if (t != null) return t;
            }
            if (m.HasProperty("_MainTex"))
            {
                var t = m.GetTexture("_MainTex") as Texture2D;
                if (t != null) return t;
            }
            return m.mainTexture as Texture2D;
        }
    }
}
