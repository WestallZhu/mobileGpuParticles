using UnityEngine;
using UnityEngine.Rendering;

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
            var shape = ps.shape;
            var psr = owner.GetComponent<ParticleSystemRenderer>();

            // ---- Main ----
            gpu.maxParticles = main.maxParticles;
            gpu.simulationSpace = ConvertSimulationSpace(main.simulationSpace);
            gpu.customSimulationSpace = main.customSimulationSpace;
            gpu.scalingMode = main.scalingMode;
            gpu.scalingSource = null;
            ApplyEmitterVelocity(ps, gpu);
            gpu.cullingMode = main.cullingMode;
            ApplyMainRanges(ps, gpu, owner);
            gpu.simulationSpeed = main.simulationSpeed;
            gpu.useUnscaledTime = main.useUnscaledTime;
            gpu.playOnAwake = main.playOnAwake;
            gpu.prewarm = main.prewarm;
            gpu.stopAction = main.stopAction;
            gpu.stopActionTarget = null;
            gpu.ringBufferMode = main.ringBufferMode;
            gpu.SetRingBufferLoopRange(main.ringBufferLoopRange);

            ApplyForceOverLifetime(ps, gpu, owner);
            ApplyVelocityOverLifetime(ps, gpu, owner);
            ApplyLimitVelocityOverLifetime(ps, gpu);
            ApplyInheritVelocity(ps, gpu);
            ApplyLifetimeByEmitterSpeed(ps, gpu);
            ApplyNoise(ps, gpu);
            ApplyCollision(ps, gpu, owner);
            ApplyMaterialParameters(psr, gpu);
            ApplyTextureSheetAnimation(ps, gpu, owner);

            ApplyEmission(ps, gpu);
            ApplyColorAndSizeOverLifetime(ps, gpu, owner);
            ApplyColorAndSizeBySpeed(ps, gpu, owner);
            ApplyRotationBySpeed(ps, gpu, owner);

            // ---- Shape TRS ----
            gpu.shapeLocalPosition = shape.position;
            gpu.shapeLocalRotationEuler = shape.rotation;
            gpu.shapeLocalScale = shape.scale;
            gpu.shapeRandomDirectionAmount = shape.randomDirectionAmount;
            gpu.shapeSphericalDirectionAmount =
                shape.sphericalDirectionAmount;
            gpu.shapeRandomPositionAmount = shape.randomPositionAmount;
            ApplyShapeArc(shape, gpu);

            // ---- Shape mapping ----
            gpu.shapeType = ShapeTypeGPU.Cone; // 默认使用Cone
            gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
            gpu.alignToDirection = shape.alignToDirection; // default false in Unity

            if (!shape.enabled)
            {
                gpu.shapeType = ShapeTypeGPU.Point;
                gpu.shapeEmitFrom = ShapeEmitFromGPU.Base;
                gpu.alignToDirection = false;
                gpu.shapeLocalPosition = Vector3.zero;
                gpu.shapeLocalRotationEuler = Vector3.zero;
                gpu.shapeLocalScale = Vector3.one;
            }
            else
            switch (shape.shapeType)
            {
                // 1. Sphere
                case ParticleSystemShapeType.Sphere:
                    gpu.shapeType = ShapeTypeGPU.Sphere;
                    gpu.shapeSphereRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    break;

                // 2. Hemisphere
                case ParticleSystemShapeType.Hemisphere:
                    gpu.shapeType = ShapeTypeGPU.Hemisphere;
                    gpu.shapeSphereRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    break;

                // 3. Cone
                case ParticleSystemShapeType.Cone:
                    gpu.shapeType = ShapeTypeGPU.Cone;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    gpu.shapeConeRadius = shape.radius;
                    gpu.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    gpu.shapeConeAngle = shape.angle;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.ConeVolume:
                    gpu.shapeType = ShapeTypeGPU.Cone;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpu.shapeConeRadius = shape.radius;
                    gpu.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    gpu.shapeConeAngle = shape.angle;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    break;

                // 4. Donut
                case ParticleSystemShapeType.Donut:
                    gpu.shapeType = ShapeTypeGPU.Donut;
                    gpu.shapeDonutRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeDonutThickness = Mathf.Max(0f, shape.donutRadius);
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    break;

                // 5. Box
                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                    gpu.shapeType = ShapeTypeGPU.Box;
                    gpu.shapeEmitFrom =
                        shape.shapeType == ParticleSystemShapeType.Box
                            ? ShapeEmitFromGPU.Volume
                            : shape.shapeType == ParticleSystemShapeType.BoxEdge
                                ? ShapeEmitFromGPU.Edge
                                : ShapeEmitFromGPU.Surface;
                    gpu.shapeBoxSize = Vector3.one;
                    break;

                // 6. Circle
                case ParticleSystemShapeType.Circle:
                    gpu.shapeType = ShapeTypeGPU.Circle;
                    gpu.shapeCircleRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeConeArcDeg = shape.arc;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    break;

                case ParticleSystemShapeType.SingleSidedEdge:
                    gpu.shapeType = ShapeTypeGPU.Edge;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Edge;
                    gpu.shapeEdgeLength = Mathf.Max(0f, 2f * shape.radius);
                    break;

                case ParticleSystemShapeType.Rectangle:
                    gpu.shapeType = ShapeTypeGPU.Rectangle;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpu.shapeRectangleSize = Vector2.one;
                    break;

                default:
                    gpu.shapeType = ShapeTypeGPU.Point;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    Debug.LogWarning(
                        $"Shape type {shape.shapeType} is not supported; using Point emission.",
                        gpu);
                    break;
            }

            if (shape.enabled && shape.alignToDirection)
            {
                Debug.LogWarning(
                    "Shape Align to Direction changes particle orientation only. " +
                    "Emission position and velocity were mapped, but 3D shape-aligned " +
                    "billboard orientation is not supported.",
                    gpu);
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
                gpu.pivotDepth = psr.pivot.z;
                gpu.normalDirection = psr.normalDirection;
                gpu.rendererFlip = Clamp01(psr.flip);
                gpu.screenSpaceSizeClampEnabled = true;
                gpu.minParticleSize = psr.minParticleSize;
                gpu.maxParticleSize = psr.maxParticleSize;
                gpu.localCullingBounds = psr.localBounds;

                // Stretched-only
                gpu.stretchedLengthScale = psr.lengthScale;
                gpu.stretchedVelocityScale = psr.velocityScale;
                gpu.stretchedCameraVelocityScale = psr.cameraVelocityScale;
                gpu.freeformStretching = psr.freeformStretching;
                gpu.rotateWithStretchDirection = psr.rotateWithStretchDirection;
            }

            Debug.Log("Shuriken → GPU conversion complete (Shapes + Renderer mapping).", owner);

            gpu.InitializePlaybackFromSettings();
            Debug.Log("Shuriken → GPU (MRT) basic conversion complete.", owner);
        }

        public static void Convert(ParticleSystem particleSystem)
        {
            Convert(particleSystem != null ? particleSystem.gameObject : null);
        }

        /// <summary>
        /// 转换ParticleSystem到新的子节点，并在新节点上添加GPUParticleSystem组件
        /// </summary>
        public static void ConvertToNewChild(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                Debug.LogError("Cannot convert null ParticleSystem.");
                return;
            }

            GameObject originalOwner = particleSystem.gameObject;
            Transform parentTransform = originalOwner.transform;

            // 创建新的子节点
            GameObject gpuChild = new GameObject(originalOwner.name + "_GPU");
            gpuChild.transform.SetParent(parentTransform);
            gpuChild.transform.localPosition = new Vector3(5f, 0f, 0f); // X轴偏移5
            gpuChild.transform.localRotation = Quaternion.identity;
            gpuChild.transform.localScale = Vector3.one;

            // 添加GPUParticleSystem组件
            var gpu = gpuChild.AddComponent<GPUParticleSystem>();

            // 使用现有的Convert逻辑来初始化GPU粒子系统
            var main = particleSystem.main;
            var shape = particleSystem.shape;
            var psr = originalOwner.GetComponent<ParticleSystemRenderer>();

            // ---- Main ----
            gpu.maxParticles = main.maxParticles;
            gpu.simulationSpace = ConvertSimulationSpace(main.simulationSpace);
            gpu.customSimulationSpace = main.customSimulationSpace;
            gpu.scalingMode = main.scalingMode;
            gpu.scalingSource = originalOwner.transform;
            ApplyEmitterVelocity(particleSystem, gpu);
            gpu.cullingMode = main.cullingMode;
            ApplyMainRanges(particleSystem, gpu, gpuChild);
            gpu.simulationSpeed = main.simulationSpeed;
            gpu.useUnscaledTime = main.useUnscaledTime;
            gpu.playOnAwake = main.playOnAwake;
            gpu.prewarm = main.prewarm;
            gpu.stopAction = main.stopAction;
            gpu.stopActionTarget = originalOwner;
            gpu.ringBufferMode = main.ringBufferMode;
            gpu.SetRingBufferLoopRange(main.ringBufferLoopRange);

            ApplyForceOverLifetime(particleSystem, gpu, gpuChild);
            ApplyVelocityOverLifetime(particleSystem, gpu, gpuChild);
            ApplyLimitVelocityOverLifetime(particleSystem, gpu);
            ApplyInheritVelocity(particleSystem, gpu);
            ApplyLifetimeByEmitterSpeed(particleSystem, gpu);
            ApplyNoise(particleSystem, gpu);
            ApplyCollision(particleSystem, gpu, gpuChild);
            ApplyMaterialParameters(psr, gpu);
            ApplyTextureSheetAnimation(particleSystem, gpu, gpuChild);

            ApplyEmission(particleSystem, gpu);
            ApplyColorAndSizeOverLifetime(particleSystem, gpu, gpuChild);
            ApplyColorAndSizeBySpeed(particleSystem, gpu, gpuChild);
            ApplyRotationBySpeed(particleSystem, gpu, gpuChild);

            // ---- Shape TRS ----
            gpu.shapeLocalPosition = shape.position;
            gpu.shapeLocalRotationEuler = shape.rotation;
            gpu.shapeLocalScale = shape.scale;
            gpu.shapeRandomDirectionAmount = shape.randomDirectionAmount;
            gpu.shapeSphericalDirectionAmount =
                shape.sphericalDirectionAmount;
            gpu.shapeRandomPositionAmount = shape.randomPositionAmount;
            ApplyShapeArc(shape, gpu);

            // ---- Shape mapping ----
            gpu.shapeType = ShapeTypeGPU.Cone; // 默认使用Cone
            gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
            gpu.alignToDirection = shape.alignToDirection;

            if (!shape.enabled)
            {
                gpu.shapeType = ShapeTypeGPU.Point;
                gpu.shapeEmitFrom = ShapeEmitFromGPU.Base;
                gpu.alignToDirection = false;
                gpu.shapeLocalPosition = Vector3.zero;
                gpu.shapeLocalRotationEuler = Vector3.zero;
                gpu.shapeLocalScale = Vector3.one;
            }
            else
            switch (shape.shapeType)
            {
                // 1. Sphere
                case ParticleSystemShapeType.Sphere:
                    gpu.shapeType = ShapeTypeGPU.Sphere;
                    gpu.shapeSphereRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    break;

                // 2. Hemisphere
                case ParticleSystemShapeType.Hemisphere:
                    gpu.shapeType = ShapeTypeGPU.Hemisphere;
                    gpu.shapeSphereRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    break;

                // 3. Cone
                case ParticleSystemShapeType.Cone:
                    gpu.shapeType = ShapeTypeGPU.Cone;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    gpu.shapeConeRadius = shape.radius;
                    gpu.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    gpu.shapeConeAngle = shape.angle;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.ConeVolume:
                    gpu.shapeType = ShapeTypeGPU.Cone;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpu.shapeConeRadius = shape.radius;
                    gpu.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    gpu.shapeConeAngle = shape.angle;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    break;

                // 4. Donut
                case ParticleSystemShapeType.Donut:
                    gpu.shapeType = ShapeTypeGPU.Donut;
                    gpu.shapeDonutRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeDonutThickness = Mathf.Max(0f, shape.donutRadius);
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeConeArcDeg = shape.arc;
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    break;

                // 5. Box
                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                    gpu.shapeType = ShapeTypeGPU.Box;
                    gpu.shapeEmitFrom =
                        shape.shapeType == ParticleSystemShapeType.Box
                            ? ShapeEmitFromGPU.Volume
                            : shape.shapeType == ParticleSystemShapeType.BoxEdge
                                ? ShapeEmitFromGPU.Edge
                                : ShapeEmitFromGPU.Surface;
                    gpu.shapeBoxSize = Vector3.one;
                    break;

                // 6. Circle
                case ParticleSystemShapeType.Circle:
                    gpu.shapeType = ShapeTypeGPU.Circle;
                    gpu.shapeCircleRadius = Mathf.Max(0f, shape.radius);
                    gpu.shapeConeArcDeg = shape.arc;
                    gpu.shapeRadiusThickness = shape.radiusThickness;
                    gpu.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    break;

                case ParticleSystemShapeType.SingleSidedEdge:
                    gpu.shapeType = ShapeTypeGPU.Edge;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Edge;
                    gpu.shapeEdgeLength = Mathf.Max(0f, 2f * shape.radius);
                    break;

                case ParticleSystemShapeType.Rectangle:
                    gpu.shapeType = ShapeTypeGPU.Rectangle;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpu.shapeRectangleSize = Vector2.one;
                    break;

                default:
                    gpu.shapeType = ShapeTypeGPU.Point;
                    gpu.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    Debug.LogWarning(
                        $"Shape type {shape.shapeType} is not supported; using Point emission.",
                        gpu);
                    break;
            }

            if (shape.enabled && shape.alignToDirection)
            {
                Debug.LogWarning(
                    "Shape Align to Direction changes particle orientation only. " +
                    "Emission position and velocity were mapped, but 3D shape-aligned " +
                    "billboard orientation is not supported.",
                    gpu);
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
                        Debug.LogWarning("RendererMode=Mesh is not supported in MVP (ignored).", gpuChild);
                        break;
                }

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
                gpu.pivotDepth = psr.pivot.z;
                gpu.normalDirection = psr.normalDirection;
                gpu.rendererFlip = Clamp01(psr.flip);
                gpu.screenSpaceSizeClampEnabled = true;
                gpu.minParticleSize = psr.minParticleSize;
                gpu.maxParticleSize = psr.maxParticleSize;
                gpu.localCullingBounds = psr.localBounds;

                gpu.stretchedLengthScale = psr.lengthScale;
                gpu.stretchedVelocityScale = psr.velocityScale;
                gpu.stretchedCameraVelocityScale = psr.cameraVelocityScale;
                gpu.freeformStretching = psr.freeformStretching;
                gpu.rotateWithStretchDirection = psr.rotateWithStretchDirection;
            }

            // 添加运行时同步组件（会在Awake中自动初始化）
            gpuChild.AddComponent<ParticleSystemSync>();
            gpu.InitializePlaybackFromSettings();

            Debug.Log($"Shuriken → GPU conversion complete on new child node: {gpuChild.name}", gpuChild);
        }

        public static void ApplyMaterialParameters(
            ParticleSystemRenderer renderer,
            GPUParticleSystem gpu)
        {
            if (gpu == null) return;

            Material material = GetPrimaryMaterial(renderer);
            gpu.baseMap = TryGetBaseMap(renderer) ?? Texture2D.whiteTexture;
            gpu.materialBaseColor = GetMaterialBaseColor(material);
            gpu.materialColorMode = GetMaterialColorMode(material);
            gpu.materialBlendOperation = GetMaterialBlendOperation(material);
            gpu.materialSourceBlend = GetMaterialBlendFactor(
                material, "_SrcBlend", BlendMode.SrcAlpha);
            gpu.materialDestinationBlend = GetMaterialBlendFactor(
                material,
                "_DstBlend",
                BlendMode.OneMinusSrcAlpha);
            gpu.materialSourceBlendAlpha = GetMaterialBlendFactor(
                material, "_SrcBlendAlpha", BlendMode.One);
            gpu.materialDestinationBlendAlpha = GetMaterialBlendFactor(
                material,
                "_DstBlendAlpha",
                BlendMode.OneMinusSrcAlpha);
            gpu.materialAlphaPremultiply = material != null &&
                material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");
            gpu.materialAlphaModulate = material != null &&
                material.IsKeywordEnabled("_ALPHAMODULATE_ON");
            gpu.materialZWrite = material != null &&
                material.HasProperty("_ZWrite") &&
                material.GetFloat("_ZWrite") > 0.5f;
            gpu.materialAlphaClip = UsesAlphaClipping(material);
            gpu.materialAlphaCutoff = GetMaterialAlphaCutoff(material);
            gpu.materialSoftParticles = UsesSoftParticles(material);
            gpu.materialSoftParticleFadeParams =
                GetMaterialSoftParticleFadeParams(material);
            gpu.materialCameraFading = UsesCameraFading(material);
            gpu.materialCameraFadeParams = gpu.materialCameraFading
                ? GetMaterialCameraFadeParams(material)
                : Vector2.zero;
            gpu.textureSheetFrameBlending = UsesFlipbookBlending(material);
        }

        internal static Material GetPrimaryMaterial(
            ParticleSystemRenderer renderer)
        {
            if (renderer == null) return null;
            Material material = renderer.sharedMaterial;
            if (material != null) return material;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null) return null;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null) return materials[i];
            }
            return null;
        }

        internal static Texture2D TryGetBaseMap(
            ParticleSystemRenderer renderer)
        {
            if (renderer == null) return null;
            Texture2D texture = TryGetBaseMap(renderer.sharedMaterial);
            if (texture != null) return texture;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null) return null;
            for (int i = 0; i < materials.Length; i++)
            {
                texture = TryGetBaseMap(materials[i]);
                if (texture != null) return texture;
            }
            return null;
        }

        internal static Texture2D TryGetBaseMap(Material material)
        {
            if (material == null) return null;
            // URP Lit/Unlit commonly use _BaseMap; legacy uses _MainTex.
            if (material.HasProperty("_BaseMap"))
            {
                var texture = material.GetTexture("_BaseMap") as Texture2D;
                if (texture != null) return texture;
            }
            if (material.HasProperty("_MainTex"))
            {
                var texture = material.GetTexture("_MainTex") as Texture2D;
                if (texture != null) return texture;
            }
            return material.mainTexture as Texture2D;
        }

        internal static Color GetMaterialBaseColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }
            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }
            if (material.HasProperty("_TintColor"))
            {
                return material.GetColor("_TintColor");
            }
            return Color.white;
        }

        internal static GPUParticleColorMode GetMaterialColorMode(
            Material material)
        {
            if (material == null)
            {
                return GPUParticleColorMode.Multiply;
            }
            if (material.IsKeywordEnabled("_COLOROVERLAY_ON"))
            {
                return GPUParticleColorMode.Overlay;
            }
            if (material.IsKeywordEnabled("_COLORCOLOR_ON"))
            {
                return GPUParticleColorMode.Color;
            }
            if (!material.IsKeywordEnabled("_COLORADDSUBDIFF_ON"))
            {
                return GPUParticleColorMode.Multiply;
            }

            Vector4 operation = material.HasProperty("_BaseColorAddSubDiff")
                ? material.GetVector("_BaseColorAddSubDiff")
                : Vector4.zero;
            if (operation.y > 0.5f)
            {
                return GPUParticleColorMode.Difference;
            }
            return operation.x >= 0f
                ? GPUParticleColorMode.Additive
                : GPUParticleColorMode.Subtractive;
        }

        internal static BlendOp GetMaterialBlendOperation(Material material)
        {
            return material != null && material.HasProperty("_BlendOp")
                ? (BlendOp)Mathf.RoundToInt(material.GetFloat("_BlendOp"))
                : BlendOp.Add;
        }

        internal static BlendMode GetMaterialBlendFactor(
            Material material,
            string propertyName,
            BlendMode fallback)
        {
            return material != null && material.HasProperty(propertyName)
                ? (BlendMode)Mathf.RoundToInt(
                    material.GetFloat(propertyName))
                : fallback;
        }

        internal static bool UsesAlphaClipping(Material material)
        {
            return material != null &&
                material.IsKeywordEnabled("_ALPHATEST_ON");
        }

        internal static float GetMaterialAlphaCutoff(Material material)
        {
            return material != null && material.HasProperty("_Cutoff")
                ? Mathf.Clamp01(material.GetFloat("_Cutoff"))
                : 0.5f;
        }

        internal static bool UsesSoftParticles(Material material)
        {
            return material != null &&
                material.IsKeywordEnabled("_SOFTPARTICLES_ON");
        }

        internal static Vector2 GetMaterialSoftParticleFadeParams(
            Material material)
        {
            if (material == null ||
                !material.HasProperty("_SoftParticleFadeParams"))
            {
                return Vector2.zero;
            }

            Vector4 parameters = material.GetVector(
                "_SoftParticleFadeParams");
            return new Vector2(parameters.x, parameters.y);
        }

        internal static bool UsesCameraFading(Material material)
        {
            return material != null &&
                material.HasProperty("_CameraFadingEnabled") &&
                material.GetFloat("_CameraFadingEnabled") > 0.5f &&
                material.IsKeywordEnabled("_FADING_ON");
        }

        internal static Vector2 GetMaterialCameraFadeParams(
            Material material)
        {
            if (material == null ||
                !material.HasProperty("_CameraFadeParams"))
            {
                return Vector2.zero;
            }

            Vector4 parameters = material.GetVector("_CameraFadeParams");
            return new Vector2(parameters.x, parameters.y);
        }

        internal static bool UsesFlipbookBlending(Material material)
        {
            if (material == null) return false;
            return (material.HasProperty("_FlipbookBlending") &&
                    material.GetFloat("_FlipbookBlending") > 0.5f) ||
                   material.IsKeywordEnabled("_FLIPBOOKBLENDING_ON");
        }

        static void ApplyMainRanges(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            var main = particleSystem.main;

            ParticleSystem.MinMaxCurve startLifetime = main.startLifetime;
            ShurikenMinMaxUtility.TryGetConstantRange(
                startLifetime, out float minimum, out float maximum);
            gpu.SetStartLifetimeRange(minimum, maximum);
            gpu.startLifetimeMode = startLifetime.mode;
            gpu.startLifetimeLUT = IsCurveMode(startLifetime.mode)
                ? CurveLUTBuilder.BuildHighPrecision(
                    startLifetime,
                    saveAsAsset: true,
                    assetName: "StartLifetime_LUT")
                : CurveLUTBuilder.GetDefaultUnitLUT();

            ParticleSystem.MinMaxCurve startSpeed = main.startSpeed;
            ShurikenMinMaxUtility.TryGetConstantRange(
                startSpeed, out minimum, out maximum);
            gpu.SetStartSpeedRange(minimum, maximum);
            gpu.startSpeedMode = startSpeed.mode;
            gpu.startSpeedLUT = IsCurveMode(startSpeed.mode)
                ? CurveLUTBuilder.BuildSigned(
                    startSpeed,
                    saveAsAsset: true,
                    assetName: "StartSpeed_LUT")
                : CurveLUTBuilder.GetDefaultZeroLUT();

            ParticleSystem.MinMaxCurve startSize = main.startSize3D ? main.startSizeX : main.startSize;
            ShurikenMinMaxUtility.TryGetConstantRange(
                startSize, out minimum, out maximum);
            gpu.SetStartSizeRange(minimum, maximum);
            gpu.startSizeMode = startSize.mode;
            gpu.startSizeLUT = IsCurveMode(startSize.mode)
                ? CurveLUTBuilder.Build(
                    startSize,
                    saveAsAsset: true,
                    assetName: "StartSize_LUT")
                : CurveLUTBuilder.GetDefaultUnitLUT();
            gpu.startSize3D = main.startSize3D;
            ParticleSystem.MinMaxCurve startSizeY = main.startSize3D
                ? main.startSizeY
                : main.startSize;
            ShurikenMinMaxUtility.TryGetConstantRange(
                startSizeY, out minimum, out maximum);
            gpu.SetStartSizeYRange(minimum, maximum);
            gpu.startSizeYMode = startSizeY.mode;
            gpu.startSizeYLUT = main.startSize3D &&
                                IsCurveMode(startSizeY.mode)
                ? CurveLUTBuilder.Build(
                    startSizeY,
                    saveAsAsset: true,
                    assetName: "StartSizeY_LUT")
                : CurveLUTBuilder.GetDefaultUnitLUT();

            ShurikenMinMaxUtility.TryGetColorRange(
                main.startColor, out Color minimumColor, out Color maximumColor);
            gpu.SetStartColorRange(minimumColor, maximumColor,
                main.startColor.mode == ParticleSystemGradientMode.TwoColors);
            gpu.startColorMode = main.startColor.mode;
            gpu.startColorLUT = IsStartColorGradientMode(main.startColor.mode)
                ? GradientLUTBuilder.Build(
                    main.startColor,
                    saveAsAsset: true,
                    assetName: "StartColor_LUT")
                : GradientLUTBuilder.GetDefaultWhiteLUT();

            ParticleSystem.MinMaxCurve gravityModifier = main.gravityModifier;
            ShurikenMinMaxUtility.TryGetConstantRange(
                gravityModifier, out minimum, out maximum);
            gpu.SetGravityModifierRange(minimum, maximum);
            gpu.gravityModifierMode = gravityModifier.mode;
            gpu.gravityModifierLUT = IsCurveMode(gravityModifier.mode)
                ? CurveLUTBuilder.BuildSigned(
                    gravityModifier,
                    saveAsAsset: true,
                    assetName: "GravityModifier_LUT")
                : CurveLUTBuilder.GetDefaultZeroLUT();
            gpu.gravitySource = main.gravitySource;

            ParticleSystem.MinMaxCurve startRotation = main.startRotation3D
                ? main.startRotationZ
                : main.startRotation;
            if (main.startRotation3D)
            {
                Debug.LogWarning(
                    "Start Rotation 3D X/Y axes are ignored for GPU billboards; mapping Z roll.",
                    context);
            }
            ShurikenMinMaxUtility.TryGetConstantRange(
                startRotation, out minimum, out maximum);
            gpu.SetStartRotationRange(minimum, maximum);
            gpu.startRotationMode = startRotation.mode;
            gpu.startRotationLUT = IsCurveMode(startRotation.mode)
                ? CurveLUTBuilder.BuildSigned(
                    startRotation,
                    saveAsAsset: true,
                    assetName: "StartRotation_LUT")
                : CurveLUTBuilder.GetDefaultZeroLUT();
            gpu.flipRotation = Mathf.Clamp01(main.flipRotation);

            var rotationOverLifetime = particleSystem.rotationOverLifetime;
            if (!rotationOverLifetime.enabled)
            {
                gpu.SetRotationOverLifetimeRange(0f, 0f);
                gpu.rotationOverLifetimeIntegralLUT =
                    CurveLUTBuilder.GetDefaultZeroLUT();
            }
            else
            {
                if (rotationOverLifetime.separateAxes)
                {
                    Debug.LogWarning(
                        "Rotation over Lifetime X/Y axes are ignored for GPU billboards; mapping Z roll.",
                        context);
                }
                ParticleSystem.MinMaxCurve rotationCurve = rotationOverLifetime.z;
                gpu.SetRotationOverLifetimeRange(
                    rotationCurve.Evaluate(1f, 0f),
                    rotationCurve.Evaluate(1f, 1f));
                gpu.rotationOverLifetimeIntegralLUT = CurveLUTBuilder.BuildIntegral(
                    rotationCurve,
                    saveAsAsset: true,
                    assetName: "RotationOverLife_IntegralLUT");
            }
        }

        static bool IsStartColorGradientMode(
            ParticleSystemGradientMode mode)
        {
            return mode == ParticleSystemGradientMode.Gradient ||
                   mode == ParticleSystemGradientMode.TwoGradients ||
                   mode == ParticleSystemGradientMode.RandomColor;
        }

        static bool IsCurveMode(ParticleSystemCurveMode mode)
        {
            return mode == ParticleSystemCurveMode.Curve ||
                   mode == ParticleSystemCurveMode.TwoCurves;
        }

        static void ApplyForceOverLifetime(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            var force = particleSystem.forceOverLifetime;
            gpu.forceOverLifetimeEnabled = force.enabled;
            gpu.forceOverLifetimeSpace = force.space == ParticleSystemSimulationSpace.World
                ? SimulationSpace.World
                : SimulationSpace.Local;
            gpu.forceOverLifetimeRandomized = force.randomized;

            if (force.space == ParticleSystemSimulationSpace.Custom)
            {
                Debug.LogWarning(
                    "Force over Lifetime Custom space is not supported; using emitter Local space.",
                    context);
            }

            gpu.forceOverLifetimeLUT = force.enabled
                ? MinMaxCurveVector3LUTBuilder.Build(
                    force.x, force.y, force.z, saveAsAsset: true,
                    assetName: "ForceOverLife_LUT")
                : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
        }

        static void ApplyColorAndSizeOverLifetime(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            var color = particleSystem.colorOverLifetime;
            gpu.colorOverLifetimeMode = color.enabled
                ? color.color.mode
                : ParticleSystemGradientMode.Gradient;
            gpu.colorOverLifetimeLUT = color.enabled
                ? GradientLUTBuilder.Build(
                    color.color, saveAsAsset: true)
                : GradientLUTBuilder.GetDefaultWhiteLUT();

            var size = particleSystem.sizeOverLifetime;
            ParticleSystem.MinMaxCurve sizeCurve = size.separateAxes
                ? size.x
                : size.size;
            gpu.sizeOverLifetimeSeparateAxes =
                size.enabled && size.separateAxes;
            gpu.sizeOverLifetimeLUT = size.enabled
                ? CurveLUTBuilder.Build(sizeCurve, saveAsAsset: true)
                : CurveLUTBuilder.GetDefaultUnitLUT();
            gpu.sizeOverLifetimeYLUT =
                size.enabled && size.separateAxes
                    ? CurveLUTBuilder.Build(
                        size.y,
                        saveAsAsset: true,
                        assetName: "SizeOverLifetimeY_LUT")
                    : CurveLUTBuilder.GetDefaultUnitLUT();
        }

        static void ApplyColorAndSizeBySpeed(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            var color = particleSystem.colorBySpeed;
            gpu.colorBySpeedEnabled = color.enabled;
            gpu.colorBySpeedMode = color.enabled
                ? color.color.mode
                : ParticleSystemGradientMode.Gradient;
            gpu.SetColorBySpeedRange(color.range);
            gpu.colorBySpeedLUT = color.enabled
                ? GradientLUTBuilder.Build(
                    color.color,
                    saveAsAsset: true,
                    assetName: "ColorBySpeed_LUT")
                : GradientLUTBuilder.GetDefaultWhiteLUT();

            var size = particleSystem.sizeBySpeed;
            gpu.sizeBySpeedEnabled = size.enabled;
            gpu.sizeBySpeedSeparateAxes =
                size.enabled && size.separateAxes;
            gpu.SetSizeBySpeedRange(size.range);
            ParticleSystem.MinMaxCurve sizeCurve = size.separateAxes
                ? size.x
                : size.size;
            gpu.sizeBySpeedLUT = size.enabled
                ? CurveLUTBuilder.Build(
                    sizeCurve,
                    saveAsAsset: true,
                    assetName: "SizeBySpeed_LUT")
                : CurveLUTBuilder.GetDefaultUnitLUT();
            gpu.sizeBySpeedYLUT = size.enabled && size.separateAxes
                ? CurveLUTBuilder.Build(
                    size.y,
                    saveAsAsset: true,
                    assetName: "SizeBySpeedY_LUT")
                : CurveLUTBuilder.GetDefaultUnitLUT();
        }

        static void ApplyRotationBySpeed(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            var rotation = particleSystem.rotationBySpeed;
            gpu.rotationBySpeedEnabled = rotation.enabled;
            gpu.SetRotationBySpeedRange(rotation.range);

            if (rotation.enabled && rotation.separateAxes)
            {
                Debug.LogWarning(
                    "Rotation by Speed X/Y axes are ignored for GPU billboards; mapping Z roll.",
                    context);
            }

            gpu.rotationBySpeedLUT = rotation.enabled
                ? CurveLUTBuilder.BuildSigned(
                    rotation.z,
                    saveAsAsset: true,
                    assetName: "RotationBySpeed_LUT")
                : CurveLUTBuilder.GetDefaultZeroLUT();
        }

        static void ApplyVelocityOverLifetime(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            var velocity = particleSystem.velocityOverLifetime;
            gpu.velocityOverLifetimeEnabled = velocity.enabled;
            gpu.velocityOverLifetimeSpeedModifierEnabled = velocity.enabled;
            gpu.velocityOverLifetimeSpace =
                velocity.space == ParticleSystemSimulationSpace.World
                    ? SimulationSpace.World
                    : SimulationSpace.Local;

            bool hasOrbitalOrRadial =
                HasNonZeroRate(velocity.orbitalX) ||
                HasNonZeroRate(velocity.orbitalY) ||
                HasNonZeroRate(velocity.orbitalZ) ||
                HasNonZeroRate(velocity.radial);
            gpu.velocityOverLifetimeOrbitalEnabled =
                velocity.enabled && hasOrbitalOrRadial;

            if (velocity.space == ParticleSystemSimulationSpace.Custom)
            {
                Debug.LogWarning(
                    "Velocity over Lifetime Custom space is not supported; using emitter Local space.",
                    context);
            }

            gpu.velocityOverLifetimeLUT = velocity.enabled
                ? MinMaxCurveVector3LUTBuilder.Build(
                    velocity.x,
                    velocity.y,
                    velocity.z,
                    velocity.speedModifier,
                    saveAsAsset: true,
                    assetName: "VelocityOverLife_LUT")
                : MinMaxCurveVector3LUTBuilder.GetDefaultVelocityLUT();
            gpu.velocityOverLifetimeOrbitalLUT =
                gpu.velocityOverLifetimeOrbitalEnabled
                ? MinMaxCurveVector3LUTBuilder.Build(
                    velocity.orbitalX,
                    velocity.orbitalY,
                    velocity.orbitalZ,
                    velocity.radial,
                    saveAsAsset: true,
                    assetName: "VelocityOverLifeOrbital_LUT")
                : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpu.velocityOverLifetimeOrbitalOffsetLUT =
                gpu.velocityOverLifetimeOrbitalEnabled
                ? MinMaxCurveVector3LUTBuilder.Build(
                    velocity.orbitalOffsetX,
                    velocity.orbitalOffsetY,
                    velocity.orbitalOffsetZ,
                    saveAsAsset: true,
                    assetName: "VelocityOverLifeOrbitalOffset_LUT")
                : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
        }

        static void ApplyLimitVelocityOverLifetime(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu)
        {
            var limit = particleSystem.limitVelocityOverLifetime;
            gpu.limitVelocityOverLifetimeEnabled = limit.enabled;
            gpu.limitVelocityOverLifetimeSeparateAxes = limit.separateAxes;
            gpu.limitVelocityOverLifetimeSpace =
                limit.space == ParticleSystemSimulationSpace.World
                    ? SimulationSpace.World
                    : SimulationSpace.Local;
            gpu.limitVelocityOverLifetimeDampen = Mathf.Clamp01(limit.dampen);
            gpu.limitVelocityMultiplyDragBySize =
                limit.multiplyDragByParticleSize;
            gpu.limitVelocityMultiplyDragByVelocity =
                limit.multiplyDragByParticleVelocity;
            gpu.limitVelocityOverLifetimeLUT = limit.enabled
                ? LimitVelocityLUTBuilder.Build(
                    limit,
                    saveAsAsset: true,
                    assetName: "LimitVelocityOverLifetime_LUT")
                : LimitVelocityLUTBuilder.GetDefaultZeroLUT();
        }

        static void ApplyEmitterVelocity(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu)
        {
            var main = particleSystem.main;
            gpu.emitterVelocityMode = main.emitterVelocityMode;
            gpu.customEmitterVelocity = main.emitterVelocity;
            gpu.emitterVelocitySource = particleSystem;
        }

        static void ApplyInheritVelocity(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu)
        {
            var inherit = particleSystem.inheritVelocity;
            gpu.inheritVelocityEnabled = inherit.enabled;
            gpu.inheritVelocityMode = inherit.mode;
            gpu.inheritVelocityLUT = inherit.enabled
                ? CurveLUTBuilder.BuildSigned(
                    inherit.curve,
                    saveAsAsset: true,
                    assetName: "InheritVelocity_LUT")
                : CurveLUTBuilder.GetDefaultZeroLUT();
        }

        static void ApplyLifetimeByEmitterSpeed(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu)
        {
            var lifetime = particleSystem.lifetimeByEmitterSpeed;
            gpu.lifetimeByEmitterSpeedEnabled = lifetime.enabled;
            gpu.SetLifetimeByEmitterSpeedRange(lifetime.range);
            gpu.lifetimeByEmitterSpeedLUT = lifetime.enabled
                ? CurveLUTBuilder.Build(
                    lifetime.curve,
                    saveAsAsset: true,
                    assetName: "LifetimeByEmitterSpeed_LUT")
                : CurveLUTBuilder.GetDefaultUnitLUT();
        }

        static void ApplyNoise(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu)
        {
            var noise = particleSystem.noise;
            gpu.noiseEnabled = noise.enabled;
            gpu.noiseSeparateAxes = noise.separateAxes;
            gpu.noiseFrequency = Mathf.Max(0.0001f, noise.frequency);
            gpu.noiseDamping = noise.damping;
            gpu.noiseQuality = noise.quality;
            gpu.noiseOctaveCount = Mathf.Clamp(noise.octaveCount, 1, 4);
            gpu.noiseOctaveMultiplier = Mathf.Max(0f, noise.octaveMultiplier);
            gpu.noiseOctaveScale = Mathf.Max(1f, noise.octaveScale);
            gpu.noiseRemapEnabled = noise.enabled && noise.remapEnabled;

            if (!noise.enabled)
            {
                gpu.noiseStrengthLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultUnitVectorLUT();
                gpu.noiseAmountsLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultNoiseAmountsLUT();
                gpu.noiseRemapLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultSignedIdentityLUT();
                return;
            }

            ParticleSystem.MinMaxCurve strengthX = noise.separateAxes
                ? noise.strengthX
                : noise.strength;
            ParticleSystem.MinMaxCurve strengthY = noise.separateAxes
                ? noise.strengthY
                : noise.strength;
            ParticleSystem.MinMaxCurve strengthZ = noise.separateAxes
                ? noise.strengthZ
                : noise.strength;
            gpu.noiseStrengthLUT = MinMaxCurveVector3LUTBuilder.Build(
                strengthX,
                strengthY,
                strengthZ,
                saveAsAsset: true,
                assetName: "NoiseStrength_LUT");
            gpu.noiseAmountsLUT = MinMaxCurveVector3LUTBuilder.Build(
                noise.positionAmount,
                noise.rotationAmount,
                noise.sizeAmount,
                noise.scrollSpeed,
                saveAsAsset: true,
                assetName: "NoiseAmounts_LUT");

            ParticleSystem.MinMaxCurve remapX = noise.separateAxes
                ? noise.remapX
                : noise.remap;
            ParticleSystem.MinMaxCurve remapY = noise.separateAxes
                ? noise.remapY
                : noise.remap;
            ParticleSystem.MinMaxCurve remapZ = noise.separateAxes
                ? noise.remapZ
                : noise.remap;
            gpu.noiseRemapLUT = noise.remapEnabled
                ? MinMaxCurveVector3LUTBuilder.Build(
                    remapX,
                    remapY,
                    remapZ,
                    saveAsAsset: true,
                    assetName: "NoiseRemap_LUT")
                : MinMaxCurveVector3LUTBuilder.GetDefaultSignedIdentityLUT();
        }

        static void ApplyCollision(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            const int maxSupportedPlanes = 6;
            ParticleSystem.CollisionModule collision =
                particleSystem.collision;
            gpu.collisionEnabled = collision.enabled;
            gpu.collisionType = collision.type;
            gpu.collisionMinKillSpeed = Mathf.Max(0f, collision.minKillSpeed);
            gpu.collisionMaxKillSpeed = Mathf.Max(
                gpu.collisionMinKillSpeed,
                collision.maxKillSpeed);
            gpu.collisionRadiusScale = Mathf.Max(0f, collision.radiusScale);

            if (!collision.enabled)
            {
                gpu.collisionPlanes = System.Array.Empty<Transform>();
                gpu.collisionParametersLUT = MinMaxCurveVector3LUTBuilder
                    .GetDefaultCollisionParametersLUT();
                return;
            }

            gpu.collisionParametersLUT = MinMaxCurveVector3LUTBuilder.Build(
                collision.dampen,
                collision.bounce,
                collision.lifetimeLoss,
                saveAsAsset: true,
                assetName: "CollisionParameters_LUT");

            if (collision.type != ParticleSystemCollisionType.Planes)
            {
                gpu.collisionPlanes = System.Array.Empty<Transform>();
                Debug.LogWarning(
                    "Collision World mode is not supported by the GPU simulator; " +
                    "use Collision Planes mode.",
                    context);
            }
            else
            {
                int planeCount = Mathf.Min(
                    collision.planeCount,
                    maxSupportedPlanes);
                gpu.collisionPlanes = new Transform[planeCount];
                for (int i = 0; i < planeCount; i++)
                {
                    gpu.collisionPlanes[i] = collision.GetPlane(i);
                }

                if (collision.planeCount > maxSupportedPlanes)
                {
                    Debug.LogWarning(
                        $"Collision Planes supports the first " +
                        $"{maxSupportedPlanes} planes; extra planes were ignored.",
                        context);
                }
            }

            if (collision.sendCollisionMessages)
            {
                Debug.LogWarning(
                    "GPU Collision Planes does not emit OnParticleCollision messages.",
                    context);
            }
        }

        static void ApplyTextureSheetAnimation(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu,
            Object context)
        {
            var textureSheet = particleSystem.textureSheetAnimation;
            bool usesFrameBlending = gpu.textureSheetFrameBlending;
            gpu.textureSheetMode = textureSheet.mode;
            gpu.textureSheetAnimation = textureSheet.animation;
            gpu.textureSheetTimeMode = textureSheet.timeMode;
            gpu.textureSheetRowMode = textureSheet.rowMode;
            gpu.textureSheetUVChannelMask = textureSheet.uvChannelMask;
            gpu.textureSheetTilesX = Mathf.Max(1, textureSheet.numTilesX);
            gpu.textureSheetTilesY = Mathf.Max(1, textureSheet.numTilesY);
            gpu.textureSheetRowIndex = Mathf.Clamp(
                textureSheet.rowIndex, 0, gpu.textureSheetTilesY - 1);
            gpu.textureSheetCycleCount = Mathf.Max(1, textureSheet.cycleCount);
            gpu.textureSheetFps = Mathf.Max(0f, textureSheet.fps);
            gpu.SetTextureSheetSpeedRange(textureSheet.speedRange);

            if (!textureSheet.enabled)
            {
                gpu.textureSheetAnimationEnabled = false;
                gpu.textureSheetFrameOverTimeLUT =
                    CurveLUTBuilder.GetDefaultLinear01LUT();
                gpu.textureSheetStartFrameLUT =
                    CurveLUTBuilder.GetDefaultZeroLUT();
                return;
            }

            if (textureSheet.mode != ParticleSystemAnimationMode.Grid)
            {
                gpu.textureSheetAnimationEnabled = false;
                gpu.textureSheetFrameOverTimeLUT =
                    CurveLUTBuilder.GetDefaultLinear01LUT();
                gpu.textureSheetStartFrameLUT =
                    CurveLUTBuilder.GetDefaultZeroLUT();
                Debug.LogWarning(
                    "Texture Sheet Animation Sprites mode is not supported; " +
                    "the GPU renderer currently supports Grid mode.",
                    context);
                return;
            }

            bool affectsUV0 =
                (textureSheet.uvChannelMask & UVChannelFlags.UV0) != 0;
            gpu.textureSheetAnimationEnabled = affectsUV0;
            if (!affectsUV0)
            {
                Debug.LogWarning(
                    "Texture Sheet Animation does not target UV0, so it does not " +
                    "affect the GPU renderer's Base Map.",
                    context);
            }

            if (textureSheet.animation == ParticleSystemAnimationType.SingleRow &&
                textureSheet.rowMode == ParticleSystemAnimationRowMode.MeshIndex)
            {
                gpu.textureSheetRowMode = ParticleSystemAnimationRowMode.Custom;
                Debug.LogWarning(
                    "Texture Sheet Animation MeshIndex row selection requires Mesh " +
                    "particle rendering; using the configured Custom row instead.",
                    context);
            }

            UVChannelFlags supportedChannels = UVChannelFlags.UV0;
            if (usesFrameBlending)
            {
                supportedChannels |= UVChannelFlags.UV1;
            }
            UVChannelFlags unsupportedChannels =
                textureSheet.uvChannelMask & ~supportedChannels;
            if (unsupportedChannels != 0)
            {
                Debug.LogWarning(
                    "Texture Sheet Animation UV channels not consumed by the " +
                    $"GPU Base Map were ignored: {unsupportedChannels}.",
                    context);
            }

            gpu.textureSheetFrameOverTimeLUT = CurveLUTBuilder.BuildSigned(
                textureSheet.frameOverTime,
                saveAsAsset: true,
                assetName: "TextureSheetFrameOverTime_LUT");
            gpu.textureSheetStartFrameLUT = CurveLUTBuilder.BuildSigned(
                textureSheet.startFrame,
                resolution: 2,
                saveAsAsset: true,
                assetName: "TextureSheetStartFrame_LUT");
        }

        static Vector3 Clamp01(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp01(value.x),
                Mathf.Clamp01(value.y),
                Mathf.Clamp01(value.z));
        }

        static void ApplyEmission(
            ParticleSystem particleSystem,
            GPUParticleSystem gpu)
        {
            var main = particleSystem.main;
            var emission = particleSystem.emission;

            gpu.emissionEnabled = emission.enabled;
            gpu.emissionDuration = Mathf.Max(0.05f, main.duration);
            gpu.emissionLooping = main.loop;
            gpu.emissionRandomSeed = particleSystem.randomSeed == 0u
                ? 1u
                : particleSystem.randomSeed;
            gpu.SetEmissionRateOverTime(emission.rateOverTime);
            gpu.SetEmissionRateOverDistance(emission.rateOverDistance);

            ShurikenMinMaxUtility.GetRangeAtTime(
                main.startDelay,
                0f,
                out float minimumDelay,
                out float maximumDelay);
            gpu.SetEmissionStartDelayRange(minimumDelay, maximumDelay);

            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpu.SetEmissionBursts(bursts);
        }

        static SimulationSpace ConvertSimulationSpace(
            ParticleSystemSimulationSpace source)
        {
            switch (source)
            {
                case ParticleSystemSimulationSpace.World:
                    return SimulationSpace.World;
                case ParticleSystemSimulationSpace.Custom:
                    return SimulationSpace.Custom;
                default:
                    return SimulationSpace.Local;
            }
        }

        static void ApplyShapeArc(
            ParticleSystem.ShapeModule shape,
            GPUParticleSystem gpu)
        {
            gpu.shapeArcMode = ConvertShapeArcMode(shape.arcMode);
            gpu.shapeArcSpread = Mathf.Clamp01(shape.arcSpread);
            gpu.shapeArcSpeedMode = shape.arcSpeed.mode;
            bool animatedArc =
                shape.arcMode == ParticleSystemShapeMultiModeValue.Loop ||
                shape.arcMode == ParticleSystemShapeMultiModeValue.PingPong;
            gpu.shapeArcSpeedIntegralLUT = animatedArc
                ? CurveLUTBuilder.BuildIntegral(
                    shape.arcSpeed,
                    saveAsAsset: true,
                    assetName: "ShapeArcSpeedIntegral_LUT")
                : CurveLUTBuilder.GetDefaultLinear01LUT();
        }

        static ShapeArcModeGPU ConvertShapeArcMode(
            ParticleSystemShapeMultiModeValue source)
        {
            switch (source)
            {
                case ParticleSystemShapeMultiModeValue.Loop:
                    return ShapeArcModeGPU.Loop;
                case ParticleSystemShapeMultiModeValue.PingPong:
                    return ShapeArcModeGPU.PingPong;
                case ParticleSystemShapeMultiModeValue.BurstSpread:
                    return ShapeArcModeGPU.BurstSpread;
                default:
                    return ShapeArcModeGPU.Random;
            }
        }

        static bool HasNonZeroRate(ParticleSystem.MinMaxCurve curve)
        {
            for (int i = 0; i <= 16; i++)
            {
                float time = i / 16f;
                if (Mathf.Abs(curve.Evaluate(time, 0f)) > 1e-5f ||
                    Mathf.Abs(curve.Evaluate(time, 1f)) > 1e-5f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
