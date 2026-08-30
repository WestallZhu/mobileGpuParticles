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
        private bool cachedEmissionEnabled;
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
        private bool cachedShapeEnabled;

        // Renderer缓存
        private ParticleSystemRenderMode cachedRenderMode;
        private ParticleSystemRenderSpace cachedAlignment;

        // LUT缓存（避免每帧重建）
        private float lastColorLUTUpdate = 0f;
        private float lastSizeLUTUpdate = 0f;
        private float lastForceLUTUpdate = 0f;
        private float lastVelocityLUTUpdate = 0f;
        private float lastColorBySpeedLUTUpdate = 0f;
        private float lastSizeBySpeedLUTUpdate = 0f;
        private float lastRotationLUTUpdate = 0f;
        private float lastRotationBySpeedLUTUpdate = 0f;
        private float lastEmissionTimelineUpdate = 0f;
        private Texture2D generatedForceLUT;
        private Texture2D generatedVelocityLUT;
        private Texture2D generatedColorLUT;
        private Texture2D generatedSizeLUT;
        private Texture2D generatedColorBySpeedLUT;
        private Texture2D generatedSizeBySpeedLUT;
        private Texture2D generatedRotationLUT;
        private Texture2D generatedRotationBySpeedLUT;
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
            SyncForceOverLifetime(true);
            SyncVelocityOverLifetime(true);
            SyncRendererParameters(true);
            SyncRotationParameters(true);
            SyncMaterialParameters(true);
            SyncColorOverLifetime(true);
            SyncSizeOverLifetime(true);
            SyncColorBySpeed(true);
            SyncSizeBySpeed(true);
            SyncRotationBySpeed(true);
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
            cachedEmissionEnabled = emission.enabled;
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
            cachedShapeEnabled = shape.enabled;

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
            SyncForceOverLifetime();
            SyncVelocityOverLifetime();
            SyncRendererParameters();
            SyncRotationParameters();
            SyncMaterialParameters();
            SyncColorOverLifetime();
            SyncSizeOverLifetime();
            SyncColorBySpeed();
            SyncSizeBySpeed();
            SyncRotationBySpeed();
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

            ShurikenMinMaxUtility.TryGetConstantRange(
                main.startLifetime, out float minimum, out float maximum);
            targetGPUParticleSystem.SetStartLifetimeRange(minimum, maximum);

            ShurikenMinMaxUtility.TryGetConstantRange(
                main.startSpeed, out minimum, out maximum);
            targetGPUParticleSystem.SetStartSpeedRange(minimum, maximum);

            ParticleSystem.MinMaxCurve startSize = main.startSize3D ? main.startSizeX : main.startSize;
            ShurikenMinMaxUtility.TryGetConstantRange(startSize, out minimum, out maximum);
            targetGPUParticleSystem.SetStartSizeRange(minimum, maximum);

            ShurikenMinMaxUtility.TryGetColorRange(
                main.startColor, out Color minimumColor, out Color maximumColor);
            targetGPUParticleSystem.SetStartColorRange(minimumColor, maximumColor,
                main.startColor.mode == ParticleSystemGradientMode.TwoColors);

            ShurikenMinMaxUtility.TryGetConstantRange(
                main.gravityModifier, out minimum, out maximum);
            targetGPUParticleSystem.SetGravityModifierRange(minimum, maximum);

            // Simulation Speed
            float newSimulationSpeed = main.simulationSpeed;
            if (force || Mathf.Abs(newSimulationSpeed - cachedSimulationSpeed) > 0.001f)
            {
                targetGPUParticleSystem.simulationSpeed = newSimulationSpeed;
                cachedSimulationSpeed = newSimulationSpeed;
            }

            ParticleSystem.MinMaxCurve startRotation = main.startRotation3D
                ? main.startRotationZ
                : main.startRotation;
            ShurikenMinMaxUtility.TryGetConstantRange(startRotation, out minimum, out maximum);
            targetGPUParticleSystem.SetStartRotationRange(minimum, maximum);
        }

        void SyncEmissionParameters(bool force = false)
        {
            var emission = sourceParticleSystem.emission;
            if (force || emission.enabled != cachedEmissionEnabled)
            {
                targetGPUParticleSystem.emissionEnabled = emission.enabled;
                cachedEmissionEnabled = emission.enabled;
            }

            bool timeToUpdate = Time.realtimeSinceStartup - lastEmissionTimelineUpdate >
                                LUT_UPDATE_INTERVAL;
            if (!force && !timeToUpdate) return;

            var main = sourceParticleSystem.main;
            targetGPUParticleSystem.emissionDuration = Mathf.Max(0.05f, main.duration);
            targetGPUParticleSystem.emissionLooping = main.loop;
            targetGPUParticleSystem.emissionRandomSeed = sourceParticleSystem.randomSeed == 0u
                ? 1u
                : sourceParticleSystem.randomSeed;
            targetGPUParticleSystem.SetEmissionRateOverTime(emission.rateOverTime);
            targetGPUParticleSystem.SetEmissionRateOverDistance(emission.rateOverDistance);

            ShurikenMinMaxUtility.TryGetConstantRange(
                main.startDelay, out float minimumDelay, out float maximumDelay);
            targetGPUParticleSystem.SetEmissionStartDelayRange(minimumDelay, maximumDelay);

            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            targetGPUParticleSystem.SetEmissionBursts(bursts);
            lastEmissionTimelineUpdate = Time.realtimeSinceStartup;
        }

        void SyncShapeParameters(bool force = false)
        {
            var shape = sourceParticleSystem.shape;
            bool shapeChanged = false;

            if (force || shape.enabled != cachedShapeEnabled)
            {
                cachedShapeEnabled = shape.enabled;
                shapeChanged = true;
            }

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
                targetGPUParticleSystem.alignToDirection = shape.enabled && shape.alignToDirection;
            }
        }

        void ApplyShapeMapping(ParticleSystem.ShapeModule shape)
        {
            if (!shape.enabled)
            {
                targetGPUParticleSystem.shapeType = ShapeTypeGPU.Point;
                targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Base;
                targetGPUParticleSystem.shapeLocalPosition = Vector3.zero;
                targetGPUParticleSystem.shapeLocalRotationEuler = Vector3.zero;
                targetGPUParticleSystem.shapeLocalScale = Vector3.one;
                return;
            }

            targetGPUParticleSystem.shapeLocalPosition = shape.position;
            targetGPUParticleSystem.shapeLocalRotationEuler = shape.rotation;
            targetGPUParticleSystem.shapeLocalScale = shape.scale;

            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Sphere:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Sphere;
                    targetGPUParticleSystem.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeSphereRadius = shape.radius;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.Hemisphere:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Hemisphere;
                    targetGPUParticleSystem.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeSphereRadius = shape.radius;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.Cone:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Cone;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    targetGPUParticleSystem.shapeConeRadius = shape.radius;
                    targetGPUParticleSystem.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    targetGPUParticleSystem.shapeConeAngle = shape.angle;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.ConeVolume:
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
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
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
                    targetGPUParticleSystem.shapeEmitFrom = shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeCircleRadius = Mathf.Max(0f, shape.radius);
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    targetGPUParticleSystem.shapeRadiusThickness = shape.radiusThickness;
                    break;
            }
        }

        void SyncForceOverLifetime(bool forceUpdate = false)
        {
            var force = sourceParticleSystem.forceOverLifetime;
            bool timeToUpdate = Time.realtimeSinceStartup - lastForceLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.forceOverLifetimeEnabled = force.enabled;
            targetGPUParticleSystem.forceOverLifetimeSpace =
                force.space == ParticleSystemSimulationSpace.World
                    ? SimulationSpace.World
                    : SimulationSpace.Local;
            targetGPUParticleSystem.forceOverLifetimeRandomized = force.randomized;

            DestroyGeneratedForceLUT();
            if (force.enabled)
            {
                generatedForceLUT = MinMaxCurveVector3LUTBuilder.Build(force.x, force.y, force.z);
                targetGPUParticleSystem.forceOverLifetimeLUT = generatedForceLUT;
            }
            else
            {
                targetGPUParticleSystem.forceOverLifetimeLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            }

            lastForceLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncVelocityOverLifetime(bool forceUpdate = false)
        {
            var velocity = sourceParticleSystem.velocityOverLifetime;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastVelocityLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.velocityOverLifetimeEnabled = velocity.enabled;
            targetGPUParticleSystem.velocityOverLifetimeSpace =
                velocity.space == ParticleSystemSimulationSpace.World
                    ? SimulationSpace.World
                    : SimulationSpace.Local;

            DestroyGeneratedVelocityLUT();
            if (velocity.enabled)
            {
                generatedVelocityLUT = MinMaxCurveVector3LUTBuilder.Build(
                    velocity.x, velocity.y, velocity.z);
                targetGPUParticleSystem.velocityOverLifetimeLUT = generatedVelocityLUT;
            }
            else
            {
                targetGPUParticleSystem.velocityOverLifetimeLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            }

            lastVelocityLUTUpdate = Time.realtimeSinceStartup;
        }

        void OnDestroy()
        {
            DestroyGeneratedForceLUT();
            DestroyGeneratedVelocityLUT();
            DestroyGeneratedColorLUT();
            DestroyGeneratedSizeLUT();
            DestroyGeneratedColorBySpeedLUT();
            DestroyGeneratedSizeBySpeedLUT();
            DestroyGeneratedRotationLUT();
            DestroyGeneratedRotationBySpeedLUT();
        }

        void DestroyGeneratedForceLUT()
        {
            if (generatedForceLUT == null) return;

            if (Application.isPlaying) Destroy(generatedForceLUT);
            else DestroyImmediate(generatedForceLUT);
            generatedForceLUT = null;
        }

        void DestroyGeneratedVelocityLUT()
        {
            if (generatedVelocityLUT == null) return;

            if (Application.isPlaying) Destroy(generatedVelocityLUT);
            else DestroyImmediate(generatedVelocityLUT);
            generatedVelocityLUT = null;
        }

        void DestroyGeneratedColorLUT()
        {
            if (generatedColorLUT == null) return;

            if (Application.isPlaying) Destroy(generatedColorLUT);
            else DestroyImmediate(generatedColorLUT);
            generatedColorLUT = null;
        }

        void DestroyGeneratedSizeLUT()
        {
            if (generatedSizeLUT == null) return;

            if (Application.isPlaying) Destroy(generatedSizeLUT);
            else DestroyImmediate(generatedSizeLUT);
            generatedSizeLUT = null;
        }

        void DestroyGeneratedColorBySpeedLUT()
        {
            if (generatedColorBySpeedLUT == null) return;

            if (Application.isPlaying) Destroy(generatedColorBySpeedLUT);
            else DestroyImmediate(generatedColorBySpeedLUT);
            generatedColorBySpeedLUT = null;
        }

        void DestroyGeneratedSizeBySpeedLUT()
        {
            if (generatedSizeBySpeedLUT == null) return;

            if (Application.isPlaying) Destroy(generatedSizeBySpeedLUT);
            else DestroyImmediate(generatedSizeBySpeedLUT);
            generatedSizeBySpeedLUT = null;
        }

        void DestroyGeneratedRotationLUT()
        {
            if (generatedRotationLUT == null) return;

            if (Application.isPlaying) Destroy(generatedRotationLUT);
            else DestroyImmediate(generatedRotationLUT);
            generatedRotationLUT = null;
        }

        void DestroyGeneratedRotationBySpeedLUT()
        {
            if (generatedRotationBySpeedLUT == null) return;

            if (Application.isPlaying) Destroy(generatedRotationBySpeedLUT);
            else DestroyImmediate(generatedRotationBySpeedLUT);
            generatedRotationBySpeedLUT = null;
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
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastRotationLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!force && !timeToUpdate) return;

            DestroyGeneratedRotationLUT();
            if (!rotationOverLifetime.enabled)
            {
                targetGPUParticleSystem.SetRotationOverLifetimeRange(0f, 0f);
                targetGPUParticleSystem.rotationOverLifetimeIntegralLUT =
                    CurveLUTBuilder.GetDefaultZeroLUT();
                lastRotationLUTUpdate = Time.realtimeSinceStartup;
                return;
            }

            ParticleSystem.MinMaxCurve curve = rotationOverLifetime.z;
            targetGPUParticleSystem.SetRotationOverLifetimeRange(
                curve.Evaluate(0f, 0f),
                curve.Evaluate(0f, 1f));
            generatedRotationLUT = CurveLUTBuilder.BuildIntegral(curve);
            targetGPUParticleSystem.rotationOverLifetimeIntegralLUT =
                generatedRotationLUT;
            lastRotationLUTUpdate = Time.realtimeSinceStartup;
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
            var color = sourceParticleSystem.colorOverLifetime;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastColorLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!force && !timeToUpdate) return;

            targetGPUParticleSystem.colorOverLifetimeMode = color.enabled
                ? color.color.mode
                : ParticleSystemGradientMode.Gradient;
            DestroyGeneratedColorLUT();
            if (color.enabled)
            {
                generatedColorLUT = GradientLUTBuilder.Build(color.color);
                targetGPUParticleSystem.colorOverLifetimeLUT = generatedColorLUT;
            }
            else
            {
                targetGPUParticleSystem.colorOverLifetimeLUT =
                    GradientLUTBuilder.GetDefaultWhiteLUT();
            }

            lastColorLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncSizeOverLifetime(bool force = false)
        {
            var size = sourceParticleSystem.sizeOverLifetime;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastSizeLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!force && !timeToUpdate) return;

            DestroyGeneratedSizeLUT();
            if (size.enabled)
            {
                ParticleSystem.MinMaxCurve curve = size.separateAxes
                    ? size.x
                    : size.size;
                generatedSizeLUT = CurveLUTBuilder.Build(curve);
                targetGPUParticleSystem.sizeOverLifetimeLUT = generatedSizeLUT;
            }
            else
            {
                targetGPUParticleSystem.sizeOverLifetimeLUT =
                    CurveLUTBuilder.GetDefaultUnitLUT();
            }

            lastSizeLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncColorBySpeed(bool force = false)
        {
            var color = sourceParticleSystem.colorBySpeed;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastColorBySpeedLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!force && !timeToUpdate) return;

            targetGPUParticleSystem.colorBySpeedEnabled = color.enabled;
            targetGPUParticleSystem.colorBySpeedMode = color.enabled
                ? color.color.mode
                : ParticleSystemGradientMode.Gradient;
            targetGPUParticleSystem.SetColorBySpeedRange(color.range);
            DestroyGeneratedColorBySpeedLUT();
            if (color.enabled)
            {
                generatedColorBySpeedLUT = GradientLUTBuilder.Build(
                    color.color,
                    assetName: "ColorBySpeed_LUT");
                targetGPUParticleSystem.colorBySpeedLUT = generatedColorBySpeedLUT;
            }
            else
            {
                targetGPUParticleSystem.colorBySpeedLUT =
                    GradientLUTBuilder.GetDefaultWhiteLUT();
            }

            lastColorBySpeedLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncSizeBySpeed(bool force = false)
        {
            var size = sourceParticleSystem.sizeBySpeed;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastSizeBySpeedLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!force && !timeToUpdate) return;

            targetGPUParticleSystem.sizeBySpeedEnabled = size.enabled;
            targetGPUParticleSystem.SetSizeBySpeedRange(size.range);
            DestroyGeneratedSizeBySpeedLUT();
            if (size.enabled)
            {
                ParticleSystem.MinMaxCurve curve = size.separateAxes
                    ? size.x
                    : size.size;
                generatedSizeBySpeedLUT = CurveLUTBuilder.Build(
                    curve,
                    assetName: "SizeBySpeed_LUT");
                targetGPUParticleSystem.sizeBySpeedLUT = generatedSizeBySpeedLUT;
            }
            else
            {
                targetGPUParticleSystem.sizeBySpeedLUT =
                    CurveLUTBuilder.GetDefaultUnitLUT();
            }

            lastSizeBySpeedLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncRotationBySpeed(bool force = false)
        {
            var rotation = sourceParticleSystem.rotationBySpeed;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastRotationBySpeedLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (!force && !timeToUpdate) return;

            targetGPUParticleSystem.rotationBySpeedEnabled = rotation.enabled;
            targetGPUParticleSystem.SetRotationBySpeedRange(rotation.range);
            DestroyGeneratedRotationBySpeedLUT();
            if (rotation.enabled)
            {
                generatedRotationBySpeedLUT = CurveLUTBuilder.BuildSigned(
                    rotation.z,
                    assetName: "RotationBySpeed_LUT");
                targetGPUParticleSystem.rotationBySpeedLUT =
                    generatedRotationBySpeedLUT;
            }
            else
            {
                targetGPUParticleSystem.rotationBySpeedLUT =
                    CurveLUTBuilder.GetDefaultZeroLUT();
            }

            lastRotationBySpeedLUTUpdate = Time.realtimeSinceStartup;
        }
    }
}

