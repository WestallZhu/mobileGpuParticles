using UnityEngine;

namespace GPUParticles
{
    /// <summary>
    /// 运行时同步组件，监听ParticleSystem参数变化并同步到GPUParticleSystem
    /// </summary>
    public class ParticleSystemSync : MonoBehaviour
    {
        private ParticleSystem sourceParticleSystem;
        private GPUParticleSystem targetGPUParticleSystem;
        private ParticleSystemRenderer sourceRenderer;
        private Texture2D cachedBaseMap;

        // 缓存上次的值以检测变化
        private float cachedStartLifetime;
        private float cachedStartSpeed;
        private float cachedStartSize;
        private Color cachedStartColor;
        private float cachedGravityModifier;
        private float cachedSimulationSpeed;
        private float cachedStartRotation;
        private float cachedRotationOverLifetime;
        private float cachedEmissionRate;
        private int cachedMaxParticles;
        private ParticleSystemSimulationSpace cachedSimulationSpace;

        // Shape缓存
        private ParticleSystemShapeType cachedShapeType;
        private Vector3 cachedShapePosition;
        private Vector3 cachedShapeRotation;
        private Vector3 cachedShapeScale;
        private float cachedShapeRadius;
        private float cachedShapeAngle;
        private float cachedShapeLength;
        private float cachedShapeRadiusThickness;
        private float cachedShapeArc;
        private float cachedShapeDonutRadius;

        // Renderer缓存
        private ParticleSystemRenderMode cachedRenderMode;
        private ParticleSystemRenderSpace cachedAlignment;

        // LUT缓存（避免每帧重建）
        private Gradient cachedColorGradient;
        private AnimationCurve cachedSizeCurve;
        private float lastColorLUTUpdate = 0f;
        private float lastSizeLUTUpdate = 0f;
        private const float LUT_UPDATE_INTERVAL = 0.1f; // 每0.1秒更新一次LUT

        private bool isInitialized = false;

        /// <summary>
        /// 在Awake中自动初始化，查找父节点的ParticleSystem和自身的GPUParticleSystem
        /// </summary>
        void Awake()
        {
            // 查找自身的GPUParticleSystem组件
            targetGPUParticleSystem = GetComponent<GPUParticleSystem>();
            
            // 查找父节点的ParticleSystem组件
            Transform parent = transform.parent;
            if (parent != null)
            {
                sourceParticleSystem = parent.GetComponent<ParticleSystem>();
                if (sourceParticleSystem != null)
                {
                    sourceRenderer = sourceParticleSystem.GetComponent<ParticleSystemRenderer>();
                }
            }

            if (sourceParticleSystem == null || targetGPUParticleSystem == null)
            {
                Debug.LogError($"ParticleSystemSync: 无法找到源ParticleSystem或目标GPUParticleSystem！\n" +
                              $"Source: {(sourceParticleSystem != null ? "Found" : "Missing")}, " +
                              $"Target: {(targetGPUParticleSystem != null ? "Found" : "Missing")}", this);
                enabled = false;
                return;
            }

            // 初始化缓存值
            CacheCurrentValues();

            SyncMainParameters(true);
            SyncEmissionParameters(true);
            SyncShapeParameters(true);
            SyncRendererParameters(true);
            SyncRotationParameters(true);
            SyncMaterialParameters(true);
            SyncColorOverLifetime(true);
            SyncSizeOverLifetime(true);
            isInitialized = true;
        }

        void CacheCurrentValues()
        {
            if (sourceParticleSystem == null || targetGPUParticleSystem == null) return;

            var main = sourceParticleSystem.main;
            var emission = sourceParticleSystem.emission;
            var shape = sourceParticleSystem.shape;

            cachedStartLifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant ? main.startLifetime.constant : cachedStartLifetime;
            cachedStartSpeed = main.startSpeed.mode == ParticleSystemCurveMode.Constant ? main.startSpeed.constant : cachedStartSpeed;
            cachedStartSize = main.startSize.mode == ParticleSystemCurveMode.Constant ? main.startSize.constant : cachedStartSize;
            cachedStartColor = main.startColor.mode == ParticleSystemGradientMode.Color ? main.startColor.color : cachedStartColor;
            cachedGravityModifier = main.gravityModifier.mode == ParticleSystemCurveMode.Constant ? main.gravityModifier.constant : cachedGravityModifier;
            cachedSimulationSpeed = main.simulationSpeed;
            cachedStartRotation = main.startRotation.mode == ParticleSystemCurveMode.Constant ? main.startRotation.constant : cachedStartRotation;
            cachedEmissionRate = emission.rateOverTime.mode == ParticleSystemCurveMode.Constant ? emission.rateOverTime.constant : cachedEmissionRate;
            cachedMaxParticles = main.maxParticles;
            cachedSimulationSpace = main.simulationSpace;
            var rotationOverLifetime = sourceParticleSystem.rotationOverLifetime;
            cachedRotationOverLifetime =
                rotationOverLifetime.enabled &&
                !rotationOverLifetime.separateAxes &&
                rotationOverLifetime.z.mode == ParticleSystemCurveMode.Constant
                    ? rotationOverLifetime.z.constant
                    : cachedRotationOverLifetime;

            cachedShapeType = shape.shapeType;
            cachedShapePosition = shape.position;
            cachedShapeRotation = shape.rotation;
            cachedShapeScale = shape.scale;
            cachedShapeRadius = shape.radius;
            cachedShapeAngle = shape.angle;
            cachedShapeLength = shape.length;
            cachedShapeRadiusThickness = shape.radiusThickness;
            cachedShapeArc = shape.arc;
            cachedShapeDonutRadius = shape.donutRadius;

            if (sourceRenderer != null)
            {
                cachedRenderMode = sourceRenderer.renderMode;
                cachedAlignment = sourceRenderer.alignment;
            }
        }

        void Update()
        {
            if (!isInitialized || !Application.isPlaying) return;
            if (sourceParticleSystem == null || targetGPUParticleSystem == null) return;

            SyncMainParameters();
            SyncEmissionParameters();
            SyncShapeParameters();
            SyncRendererParameters();
            SyncRotationParameters();
            SyncMaterialParameters();
            SyncColorOverLifetime();
            SyncSizeOverLifetime();
        }

        void SyncMainParameters(bool force = false)
        {
            var main = sourceParticleSystem.main;

            // Max Particles
            if (force || main.maxParticles != cachedMaxParticles)
            {
                targetGPUParticleSystem.maxParticles = main.maxParticles;
                cachedMaxParticles = main.maxParticles;
            }

            // Simulation Space
            if (force || main.simulationSpace != cachedSimulationSpace)
            {
                targetGPUParticleSystem.simulationSpace = main.simulationSpace == ParticleSystemSimulationSpace.World 
                    ? SimulationSpace.World 
                    : SimulationSpace.Local;
                cachedSimulationSpace = main.simulationSpace;
            }

            // Start Lifetime
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            {
                float newLifetime = main.startLifetime.constant;
                if (force || Mathf.Abs(newLifetime - cachedStartLifetime) > 0.001f)
                {
                    targetGPUParticleSystem.startLifetime = newLifetime;
                    cachedStartLifetime = newLifetime;
                }
            }

            // Start Speed
            if (main.startSpeed.mode == ParticleSystemCurveMode.Constant)
            {
                float newSpeed = main.startSpeed.constant;
                if (force || Mathf.Abs(newSpeed - cachedStartSpeed) > 0.001f)
                {
                    targetGPUParticleSystem.startSpeed = newSpeed;
                    cachedStartSpeed = newSpeed;
                }
            }

            // Start Size
            if (main.startSize.mode == ParticleSystemCurveMode.Constant)
            {
                float newSize = main.startSize.constant;
                if (force || Mathf.Abs(newSize - cachedStartSize) > 0.001f)
                {
                    targetGPUParticleSystem.startSize = newSize;
                    cachedStartSize = newSize;
                }
            }

            // Start Color
            if (main.startColor.mode == ParticleSystemGradientMode.Color)
            {
                Color newColor = main.startColor.color;
                if (force || newColor != cachedStartColor)
                {
                    targetGPUParticleSystem.startColor = newColor;
                    cachedStartColor = newColor;
                }
            }

            // Gravity Modifier
            if (main.gravityModifier.mode == ParticleSystemCurveMode.Constant)
            {
                float newGravity = main.gravityModifier.constant;
                if (force || Mathf.Abs(newGravity - cachedGravityModifier) > 0.001f)
                {
                    targetGPUParticleSystem.gravityModifier = newGravity;
                    cachedGravityModifier = newGravity;
                }
            }

            // Simulation Speed
            float newSimulationSpeed = main.simulationSpeed;
            if (force || Mathf.Abs(newSimulationSpeed - cachedSimulationSpeed) > 0.001f)
            {
                targetGPUParticleSystem.simulationSpeed = newSimulationSpeed;
                cachedSimulationSpeed = newSimulationSpeed;
            }

            if (main.startRotation.mode == ParticleSystemCurveMode.Constant)
            {
                float newStartRotation = main.startRotation.constant;
                if (force || Mathf.Abs(newStartRotation - cachedStartRotation) > 0.001f)
                {
                    targetGPUParticleSystem.startRotation = newStartRotation;
                    cachedStartRotation = newStartRotation;
                }
            }
        }

        void SyncEmissionParameters(bool force = false)
        {
            var emission = sourceParticleSystem.emission;
            if (emission.rateOverTime.mode == ParticleSystemCurveMode.Constant)
            {
                float newRate = emission.rateOverTime.constant;
                if (force || Mathf.Abs(newRate - cachedEmissionRate) > 0.001f)
                {
                    targetGPUParticleSystem.emissionRateOverTime = newRate;
                    cachedEmissionRate = newRate;
                }
            }
        }

        void SyncShapeParameters(bool force = false)
        {
            var shape = sourceParticleSystem.shape;
            bool shapeChanged = false;

            // Shape Type
            if (force || shape.shapeType != cachedShapeType)
            {
                // 形状类型变化需要完整重新映射（这里简化处理，只更新基本参数）
                cachedShapeType = shape.shapeType;
                shapeChanged = true;
            }

            // Shape Position
            if (force || shape.position != cachedShapePosition)
            {
                targetGPUParticleSystem.shapeLocalPosition = shape.position;
                cachedShapePosition = shape.position;
                shapeChanged = true;
            }

            // Shape Rotation
            if (force || shape.rotation != cachedShapeRotation)
            {
                targetGPUParticleSystem.shapeLocalRotationEuler = shape.rotation;
                cachedShapeRotation = shape.rotation;
                shapeChanged = true;
            }

            // Shape Scale
            if (force || shape.scale != cachedShapeScale)
            {
                targetGPUParticleSystem.shapeLocalScale = shape.scale;
                cachedShapeScale = shape.scale;
                shapeChanged = true;
            }

            // Shape Radius
            if (force || Mathf.Abs(shape.radius - cachedShapeRadius) > 0.001f)
            {
                targetGPUParticleSystem.shapeConeRadius = shape.radius;
                targetGPUParticleSystem.shapeSphereRadius = shape.radius;
                cachedShapeRadius = shape.radius;
                shapeChanged = true;
            }

            // Shape Angle
            if (force || Mathf.Abs(shape.angle - cachedShapeAngle) > 0.001f)
            {
                cachedShapeAngle = shape.angle;
                shapeChanged = true;
            }

            // Shape Length
            if (force || Mathf.Abs(shape.length - cachedShapeLength) > 0.001f)
            {
                cachedShapeLength = shape.length;
                shapeChanged = true;
            }

            // Shape Radius Thickness
            if (force || Mathf.Abs(shape.radiusThickness - cachedShapeRadiusThickness) > 0.001f)
            {
                cachedShapeRadiusThickness = shape.radiusThickness;
                shapeChanged = true;
            }

            // Shape Arc
            if (force || Mathf.Abs(shape.arc - cachedShapeArc) > 0.001f)
            {
                cachedShapeArc = shape.arc;
                shapeChanged = true;
            }

            // Donut Thickness
            if (force || Mathf.Abs(shape.donutRadius - cachedShapeDonutRadius) > 0.001f)
            {
                cachedShapeDonutRadius = shape.donutRadius;
                shapeChanged = true;
            }

            // 其他shape参数根据类型更新
            if (force || shapeChanged)
            {
                ApplyShapeMapping(shape);
                targetGPUParticleSystem.alignToDirection = shape.alignToDirection;
            }
        }

        void ApplyShapeMapping(ParticleSystem.ShapeModule shape)
        {
            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Sphere:
                case ParticleSystemShapeType.SphereShell:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Sphere;
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.shapeType == ParticleSystemShapeType.SphereShell ? ShapeEmitFromGPU.Surface : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeSphereRadius = shape.radius;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.Hemisphere:
                case ParticleSystemShapeType.HemisphereShell:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Hemisphere;
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.shapeType == ParticleSystemShapeType.HemisphereShell ? ShapeEmitFromGPU.Surface : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeSphereRadius = shape.radius;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.Cone:
                case ParticleSystemShapeType.ConeShell:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Cone;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    targetGPUParticleSystem.shapeConeRadius = shape.radius;
                    targetGPUParticleSystem.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    targetGPUParticleSystem.shapeConeAngle = shape.angle;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.ConeVolume:
                case ParticleSystemShapeType.ConeVolumeShell:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Cone;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeConeRadius = shape.radius;
                    targetGPUParticleSystem.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    targetGPUParticleSystem.shapeConeAngle = shape.angle;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.Donut:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Donut;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeDonutRadius = Mathf.Max(0f, shape.radius);
                    targetGPUParticleSystem.shapeDonutThickness = Mathf.Max(0f, shape.donutRadius);
                    break;

                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Box;
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.shapeType == ParticleSystemShapeType.Box ? ShapeEmitFromGPU.Volume : ShapeEmitFromGPU.Surface;
                    targetGPUParticleSystem.shapeBoxSize = Vector3.one;
                    break;

                case ParticleSystemShapeType.Circle:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Circle;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeCircleRadius = Mathf.Max(0f, shape.radius);
                    break;

                case ParticleSystemShapeType.CircleEdge:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Circle;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Surface;
                    targetGPUParticleSystem.shapeCircleRadius = Mathf.Max(0f, shape.radius);
                    break;
            }
        }

        void SyncRendererParameters(bool force = false)
        {
            if (sourceRenderer == null) return;

            bool rendererChanged = false;

            // Render Mode
            if (force || sourceRenderer.renderMode != cachedRenderMode)
            {
                switch (sourceRenderer.renderMode)
                {
                    case ParticleSystemRenderMode.Billboard:
                        targetGPUParticleSystem.renderMode = GPURenderMode.Billboard;
                        break;
                    case ParticleSystemRenderMode.Stretch:
                        targetGPUParticleSystem.renderMode = GPURenderMode.StretchedBillboard;
                        break;
                    case ParticleSystemRenderMode.HorizontalBillboard:
                        targetGPUParticleSystem.renderMode = GPURenderMode.HorizontalBillboard;
                        break;
                    case ParticleSystemRenderMode.VerticalBillboard:
                        targetGPUParticleSystem.renderMode = GPURenderMode.VerticalBillboard;
                        break;
                }
                cachedRenderMode = sourceRenderer.renderMode;
                rendererChanged = true;
            }

            // Alignment
            if (force || sourceRenderer.alignment != cachedAlignment)
            {
                switch (sourceRenderer.alignment)
                {
                    case ParticleSystemRenderSpace.View:
                        targetGPUParticleSystem.renderAlignment = GPUAlignment.View;
                        break;
                    case ParticleSystemRenderSpace.Facing:
                        targetGPUParticleSystem.renderAlignment = GPUAlignment.Facing;
                        break;
                    case ParticleSystemRenderSpace.World:
                        targetGPUParticleSystem.renderAlignment = GPUAlignment.World;
                        break;
                    case ParticleSystemRenderSpace.Local:
                        targetGPUParticleSystem.renderAlignment = GPUAlignment.Local;
                        break;
                    case ParticleSystemRenderSpace.Velocity:
                        targetGPUParticleSystem.renderAlignment = GPUAlignment.Velocity;
                        break;
                }
                cachedAlignment = sourceRenderer.alignment;
                rendererChanged = true;
            }

            if (force || rendererChanged)
            {
                targetGPUParticleSystem.allowRoll = sourceRenderer.allowRoll;
                targetGPUParticleSystem.pivot = new Vector2(sourceRenderer.pivot.x, sourceRenderer.pivot.y);
                targetGPUParticleSystem.normalDirection = sourceRenderer.normalDirection;
                targetGPUParticleSystem.stretchedLengthScale = sourceRenderer.lengthScale;
                targetGPUParticleSystem.stretchedVelocityScale = sourceRenderer.velocityScale;
                targetGPUParticleSystem.stretchedCameraVelocityScale = sourceRenderer.cameraVelocityScale;
                targetGPUParticleSystem.freeformStretching = sourceRenderer.freeformStretching;
                targetGPUParticleSystem.rotateWithStretchDirection = sourceRenderer.rotateWithStretchDirection;
            }
        }

        void SyncRotationParameters(bool force = false)
        {
            var rotationOverLifetime = sourceParticleSystem.rotationOverLifetime;
            float newRotationOverLifetime = 0f;

            if (rotationOverLifetime.enabled &&
                !rotationOverLifetime.separateAxes &&
                rotationOverLifetime.z.mode == ParticleSystemCurveMode.Constant)
            {
                newRotationOverLifetime = rotationOverLifetime.z.constant;
            }

            if (force || Mathf.Abs(newRotationOverLifetime - cachedRotationOverLifetime) > 0.001f)
            {
                targetGPUParticleSystem.rotationOverLifetime = newRotationOverLifetime;
                cachedRotationOverLifetime = newRotationOverLifetime;
            }
        }

        void SyncMaterialParameters(bool force = false)
        {
            if (sourceRenderer == null) return;

            Texture2D baseMap = TryGetBaseMap(sourceRenderer);
            if (force || baseMap != cachedBaseMap)
            {
                targetGPUParticleSystem.baseMap = baseMap != null ? baseMap : Texture2D.whiteTexture;
                cachedBaseMap = baseMap;
            }
        }

        static Texture2D TryGetBaseMap(ParticleSystemRenderer renderer)
        {
            if (renderer == null) return null;

            Texture2D baseMap = TryGetBaseMap(renderer.sharedMaterial);
            if (baseMap != null) return baseMap;

            var materials = renderer.sharedMaterials;
            if (materials == null) return null;

            foreach (var material in materials)
            {
                baseMap = TryGetBaseMap(material);
                if (baseMap != null) return baseMap;
            }

            return null;
        }

        static Texture2D TryGetBaseMap(Material material)
        {
            if (material == null) return null;

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

        void SyncColorOverLifetime(bool force = false)
        {
            var colOver = sourceParticleSystem.colorOverLifetime;
            if (colOver.enabled)
            {
                Gradient g = colOver.color.gradient != null ? colOver.color.gradient : colOver.color.gradientMax;
                
                // 只在渐变变化或时间间隔到达时更新LUT，或强制更新
                bool gradientChanged = g != cachedColorGradient;
                bool timeToUpdate = Time.time - lastColorLUTUpdate > LUT_UPDATE_INTERVAL;
                
                if (force || gradientChanged || timeToUpdate)
                {
                    if (g != null)
                    {
                        var lut = GradientLUTBuilder.Build(g, 256);
                        if (lut != null)
                        {
                            targetGPUParticleSystem.colorOverLifetimeLUT = lut;
                            cachedColorGradient = g;
                            lastColorLUTUpdate = Time.time;
                        }
                    }
                }
            }
            else
            {
                Texture2D defaultLut = GradientLUTBuilder.GetDefaultWhiteLUT();
                if (force || targetGPUParticleSystem.colorOverLifetimeLUT != defaultLut)
                {
                    targetGPUParticleSystem.colorOverLifetimeLUT = defaultLut;
                }
            }
        }

        void SyncSizeOverLifetime(bool force = false)
        {
            var sizeOver = sourceParticleSystem.sizeOverLifetime;
            if (sizeOver.enabled)
            {
                var mmc = sizeOver.size;
                AnimationCurve curveToBake = null;
                switch (mmc.mode)
                {
                    case ParticleSystemCurveMode.Constant:
                        curveToBake = AnimationCurve.Linear(0f, mmc.constant, 1f, mmc.constant);
                        break;
                    case ParticleSystemCurveMode.Curve:
                        curveToBake = mmc.curve;
                        break;
                    case ParticleSystemCurveMode.TwoConstants:
                        curveToBake = AnimationCurve.Linear(0f, mmc.constantMax, 1f, mmc.constantMax);
                        break;
                    case ParticleSystemCurveMode.TwoCurves:
                        curveToBake = mmc.curveMax != null ? mmc.curveMax : mmc.curve;
                        break;
                }

                // 只在曲线变化或时间间隔到达时更新LUT，或强制更新
                bool curveChanged = curveToBake != cachedSizeCurve;
                bool timeToUpdate = Time.time - lastSizeLUTUpdate > LUT_UPDATE_INTERVAL;
                
                if ((force || curveChanged || timeToUpdate) && curveToBake != null)
                {
                    var lut = CurveLUTBuilder.Build(curveToBake, 256);
                    if (lut != null)
                    {
                        targetGPUParticleSystem.sizeOverLifetimeLUT = lut;
                        cachedSizeCurve = curveToBake;
                        lastSizeLUTUpdate = Time.time;
                    }
                }
            }
            else
            {
                Texture2D defaultLut = CurveLUTBuilder.GetDefaultUnitLUT();
                if (force || targetGPUParticleSystem.sizeOverLifetimeLUT != defaultLut)
                {
                    targetGPUParticleSystem.sizeOverLifetimeLUT = defaultLut;
                }
            }
        }
    }
}

