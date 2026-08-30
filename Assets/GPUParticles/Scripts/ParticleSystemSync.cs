using UnityEngine;
using UnityEngine.Rendering;

namespace GPUParticles
{
    /// <summary>
    /// 运行时同步组件，监听ParticleSystem参数变化并同步到GPUParticleSystem
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ParticleSystemSync : MonoBehaviour
    {
        private ParticleSystem sourceParticleSystem;
        private GPUParticleSystem targetGPUParticleSystem;
        private ParticleSystemRenderer sourceRenderer;
        private Texture2D cachedBaseMap;
        private Color cachedMaterialBaseColor;
        private GPUParticleColorMode cachedMaterialColorMode;
        private bool cachedTextureSheetFrameBlending;

        // 缓存上次的值以检测变化
        private float cachedStartLifetime;
        private float cachedStartSpeed;
        private float cachedStartSize;
        private Color cachedStartColor;
        private float cachedGravityModifier;
        private ParticleSystemGravitySource cachedGravitySource;
        private float cachedSimulationSpeed;
        private bool cachedUseUnscaledTime;
        private bool cachedPlayOnAwake;
        private bool cachedPrewarm;
        private ParticleSystemStopAction cachedStopAction;
        private float cachedStartRotation;
        private float cachedFlipRotation;
        private float cachedRotationOverLifetime;
        private bool cachedEmissionEnabled;
        private int cachedMaxParticles;
        private ParticleSystemSimulationSpace cachedSimulationSpace;
        private Transform cachedCustomSimulationSpace;
        private ParticleSystemScalingMode cachedScalingMode;
        private ParticleSystemEmitterVelocityMode cachedEmitterVelocityMode;
        private Vector3 cachedCustomEmitterVelocity;
        private ParticleSystemCullingMode cachedCullingMode;
        private PlaybackSyncState cachedPlaybackState =
            PlaybackSyncState.Unknown;

        private enum PlaybackSyncState
        {
            Unknown,
            Playing,
            Paused,
            StoppedEmitting,
            StoppedAndClear
        }

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
        private ParticleSystemShapeMultiModeValue cachedShapeArcMode;
        private float cachedShapeArcSpread;
        private float cachedShapeDonutRadius;
        private bool cachedShapeEnabled;
        private bool cachedShapeAlignToDirection;
        private float cachedShapeRandomDirectionAmount;
        private float cachedShapeSphericalDirectionAmount;
        private float cachedShapeRandomPositionAmount;

        // Renderer缓存
        private ParticleSystemRenderMode cachedRenderMode;
        private ParticleSystemRenderSpace cachedAlignment;
        private bool cachedAllowRoll;
        private Vector3 cachedPivot;
        private float cachedNormalDirection;
        private float cachedMinParticleSize;
        private float cachedMaxParticleSize;
        private float cachedLengthScale;
        private float cachedVelocityScale;
        private float cachedCameraVelocityScale;
        private bool cachedFreeformStretching;
        private bool cachedRotateWithStretchDirection;
        private Bounds cachedRendererLocalBounds;

        // LUT缓存（避免每帧重建）
        private float lastColorLUTUpdate = 0f;
        private float lastStartColorLUTUpdate = 0f;
        private float lastStartLifetimeLUTUpdate = 0f;
        private float lastStartSpeedLUTUpdate = 0f;
        private float lastStartSizeLUTUpdate = 0f;
        private float lastGravityModifierLUTUpdate = 0f;
        private float lastStartRotationLUTUpdate = 0f;
        private float lastSizeLUTUpdate = 0f;
        private float lastForceLUTUpdate = 0f;
        private float lastVelocityLUTUpdate = 0f;
        private float lastLimitVelocityLUTUpdate = 0f;
        private float lastInheritVelocityLUTUpdate = 0f;
        private float lastLifetimeByEmitterSpeedLUTUpdate = 0f;
        private float lastNoiseLUTUpdate = 0f;
        private float lastCollisionLUTUpdate = 0f;
        private float lastTextureSheetLUTUpdate = 0f;
        private float lastColorBySpeedLUTUpdate = 0f;
        private float lastSizeBySpeedLUTUpdate = 0f;
        private float lastRotationLUTUpdate = 0f;
        private float lastRotationBySpeedLUTUpdate = 0f;
        private float lastEmissionTimelineUpdate = 0f;
        private float lastShapeArcLUTUpdate = 0f;
        private Texture2D generatedForceLUT;
        private Texture2D generatedVelocityLUT;
        private Texture2D generatedVelocityOrbitalLUT;
        private Texture2D generatedVelocityOrbitalOffsetLUT;
        private Texture2D generatedLimitVelocityLUT;
        private Texture2D generatedInheritVelocityLUT;
        private Texture2D generatedLifetimeByEmitterSpeedLUT;
        private Texture2D generatedNoiseStrengthLUT;
        private Texture2D generatedNoiseAmountsLUT;
        private Texture2D generatedNoiseRemapLUT;
        private Texture2D generatedCollisionParametersLUT;
        private Texture2D generatedTextureSheetFrameLUT;
        private Texture2D generatedTextureSheetStartLUT;
        private Texture2D generatedColorLUT;
        private Texture2D generatedStartColorLUT;
        private Texture2D generatedStartLifetimeLUT;
        private Texture2D generatedStartSpeedLUT;
        private Texture2D generatedStartSizeLUT;
        private Texture2D generatedStartSizeYLUT;
        private Texture2D generatedGravityModifierLUT;
        private Texture2D generatedStartRotationLUT;
        private Texture2D generatedSizeLUT;
        private Texture2D generatedSizeYLUT;
        private Texture2D generatedColorBySpeedLUT;
        private Texture2D generatedSizeBySpeedLUT;
        private Texture2D generatedSizeBySpeedYLUT;
        private Texture2D generatedRotationLUT;
        private Texture2D generatedRotationBySpeedLUT;
        private Texture2D generatedShapeArcSpeedLUT;
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
            SyncLimitVelocityOverLifetime(true);
            SyncInheritVelocity(true);
            SyncLifetimeByEmitterSpeed(true);
            SyncNoise(true);
            SyncCollision(true);
            SyncTextureSheetAnimation(true);
            SyncRendererParameters(true);
            SyncRotationParameters(true);
            SyncMaterialParameters(true);
            SyncColorOverLifetime(true);
            SyncSizeOverLifetime(true);
            SyncColorBySpeed(true);
            SyncSizeBySpeed(true);
            SyncRotationBySpeed(true);
            targetGPUParticleSystem.InitializePlaybackFromSettings();
            isInitialized = true;
        }

        void Start()
        {
            if (isInitialized)
            {
                SyncPlaybackState(true);
            }
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
            cachedGravitySource = main.gravitySource;
            cachedSimulationSpeed = main.simulationSpeed;
            cachedUseUnscaledTime = main.useUnscaledTime;
            cachedPlayOnAwake = main.playOnAwake;
            cachedPrewarm = main.prewarm;
            cachedStopAction = main.stopAction;
            cachedStartRotation = main.startRotation.mode == ParticleSystemCurveMode.Constant ? main.startRotation.constant : cachedStartRotation;
            cachedFlipRotation = main.flipRotation;
            cachedEmissionEnabled = emission.enabled;
            cachedMaxParticles = main.maxParticles;
            cachedSimulationSpace = main.simulationSpace;
            cachedCustomSimulationSpace = main.customSimulationSpace;
            cachedScalingMode = main.scalingMode;
            cachedEmitterVelocityMode = main.emitterVelocityMode;
            cachedCustomEmitterVelocity = main.emitterVelocity;
            cachedCullingMode = main.cullingMode;
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
            cachedShapeArcMode = shape.arcMode;
            cachedShapeArcSpread = shape.arcSpread;
            cachedShapeDonutRadius = shape.donutRadius;
            cachedShapeEnabled = shape.enabled;
            cachedShapeAlignToDirection = shape.alignToDirection;
            cachedShapeRandomDirectionAmount = shape.randomDirectionAmount;
            cachedShapeSphericalDirectionAmount =
                shape.sphericalDirectionAmount;
            cachedShapeRandomPositionAmount = shape.randomPositionAmount;

            if (sourceRenderer != null)
            {
                cachedRenderMode = sourceRenderer.renderMode;
                cachedAlignment = sourceRenderer.alignment;
                CacheRendererValues();
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
            SyncLimitVelocityOverLifetime();
            SyncInheritVelocity();
            SyncLifetimeByEmitterSpeed();
            SyncNoise();
            SyncCollision();
            SyncTextureSheetAnimation();
            SyncRendererParameters();
            SyncRotationParameters();
            SyncMaterialParameters();
            SyncColorOverLifetime();
            SyncSizeOverLifetime();
            SyncColorBySpeed();
            SyncSizeBySpeed();
            SyncRotationBySpeed();
            SyncPlaybackState();
        }

        void SyncMainParameters(bool force = false)
        {
            var main = sourceParticleSystem.main;

            if (force ||
                targetGPUParticleSystem.scalingSource !=
                sourceParticleSystem.transform)
            {
                targetGPUParticleSystem.scalingSource =
                    sourceParticleSystem.transform;
            }

            // Max Particles
            if (force || main.maxParticles != cachedMaxParticles)
            {
                targetGPUParticleSystem.maxParticles = main.maxParticles;
                cachedMaxParticles = main.maxParticles;
            }

            // Simulation Space
            if (force ||
                main.simulationSpace != cachedSimulationSpace ||
                main.customSimulationSpace != cachedCustomSimulationSpace)
            {
                targetGPUParticleSystem.simulationSpace =
                    main.simulationSpace == ParticleSystemSimulationSpace.World
                        ? SimulationSpace.World
                        : main.simulationSpace ==
                          ParticleSystemSimulationSpace.Custom
                            ? SimulationSpace.Custom
                            : SimulationSpace.Local;
                targetGPUParticleSystem.customSimulationSpace =
                    main.customSimulationSpace;
                cachedSimulationSpace = main.simulationSpace;
                cachedCustomSimulationSpace = main.customSimulationSpace;
            }

            if (force || main.scalingMode != cachedScalingMode)
            {
                targetGPUParticleSystem.scalingMode = main.scalingMode;
                cachedScalingMode = main.scalingMode;
            }

            if (force || main.cullingMode != cachedCullingMode)
            {
                targetGPUParticleSystem.cullingMode = main.cullingMode;
                cachedCullingMode = main.cullingMode;
            }

            if (force ||
                targetGPUParticleSystem.emitterVelocitySource !=
                sourceParticleSystem)
            {
                targetGPUParticleSystem.emitterVelocitySource =
                    sourceParticleSystem;
            }

            if (force ||
                main.emitterVelocityMode != cachedEmitterVelocityMode)
            {
                targetGPUParticleSystem.emitterVelocityMode =
                    main.emitterVelocityMode;
                cachedEmitterVelocityMode = main.emitterVelocityMode;
            }

            if (main.emitterVelocityMode ==
                ParticleSystemEmitterVelocityMode.Custom)
            {
                Vector3 customEmitterVelocity = main.emitterVelocity;
                if (force ||
                    (customEmitterVelocity - cachedCustomEmitterVelocity)
                    .sqrMagnitude > 1e-8f)
                {
                    targetGPUParticleSystem.customEmitterVelocity =
                        customEmitterVelocity;
                    cachedCustomEmitterVelocity = customEmitterVelocity;
                }
            }

            if (force || main.playOnAwake != cachedPlayOnAwake)
            {
                targetGPUParticleSystem.playOnAwake = main.playOnAwake;
                cachedPlayOnAwake = main.playOnAwake;
            }

            if (force || main.prewarm != cachedPrewarm)
            {
                targetGPUParticleSystem.prewarm = main.prewarm;
                cachedPrewarm = main.prewarm;
            }

            if (force ||
                main.stopAction != cachedStopAction ||
                targetGPUParticleSystem.stopActionTarget !=
                sourceParticleSystem.gameObject)
            {
                targetGPUParticleSystem.stopAction = main.stopAction;
                targetGPUParticleSystem.stopActionTarget =
                    sourceParticleSystem.gameObject;
                cachedStopAction = main.stopAction;
            }

            ParticleSystem.MinMaxCurve startLifetime = main.startLifetime;
            ShurikenMinMaxUtility.TryGetConstantRange(
                startLifetime, out float minimum, out float maximum);
            targetGPUParticleSystem.SetStartLifetimeRange(minimum, maximum);
            targetGPUParticleSystem.startLifetimeMode = startLifetime.mode;
            bool updateStartLifetimeLUT = force ||
                Time.realtimeSinceStartup - lastStartLifetimeLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (updateStartLifetimeLUT)
            {
                DestroyGeneratedTexture(ref generatedStartLifetimeLUT);
                if (IsCurveMode(startLifetime.mode))
                {
                    generatedStartLifetimeLUT = CurveLUTBuilder.BuildHighPrecision(
                        startLifetime,
                        assetName: "StartLifetime_LUT");
                    targetGPUParticleSystem.startLifetimeLUT =
                        generatedStartLifetimeLUT;
                }
                else
                {
                    targetGPUParticleSystem.startLifetimeLUT =
                        CurveLUTBuilder.GetDefaultUnitLUT();
                }
                lastStartLifetimeLUTUpdate = Time.realtimeSinceStartup;
            }

            ShurikenMinMaxUtility.TryGetConstantRange(
                main.startSpeed, out minimum, out maximum);
            targetGPUParticleSystem.SetStartSpeedRange(minimum, maximum);
            targetGPUParticleSystem.startSpeedMode = main.startSpeed.mode;
            bool updateStartSpeedLUT = force ||
                Time.realtimeSinceStartup - lastStartSpeedLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (updateStartSpeedLUT)
            {
                DestroyGeneratedTexture(ref generatedStartSpeedLUT);
                if (IsCurveMode(main.startSpeed.mode))
                {
                    generatedStartSpeedLUT = CurveLUTBuilder.BuildSigned(
                        main.startSpeed,
                        assetName: "StartSpeed_LUT");
                    targetGPUParticleSystem.startSpeedLUT =
                        generatedStartSpeedLUT;
                }
                else
                {
                    targetGPUParticleSystem.startSpeedLUT =
                        CurveLUTBuilder.GetDefaultZeroLUT();
                }
                lastStartSpeedLUTUpdate = Time.realtimeSinceStartup;
            }

            ParticleSystem.MinMaxCurve startSize = main.startSize3D ? main.startSizeX : main.startSize;
            ShurikenMinMaxUtility.TryGetConstantRange(startSize, out minimum, out maximum);
            targetGPUParticleSystem.SetStartSizeRange(minimum, maximum);
            targetGPUParticleSystem.startSizeMode = startSize.mode;
            targetGPUParticleSystem.startSize3D = main.startSize3D;
            ParticleSystem.MinMaxCurve startSizeY = main.startSize3D
                ? main.startSizeY
                : main.startSize;
            ShurikenMinMaxUtility.TryGetConstantRange(
                startSizeY, out minimum, out maximum);
            targetGPUParticleSystem.SetStartSizeYRange(minimum, maximum);
            targetGPUParticleSystem.startSizeYMode = startSizeY.mode;
            bool updateStartSizeLUT = force ||
                Time.realtimeSinceStartup - lastStartSizeLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (updateStartSizeLUT)
            {
                DestroyGeneratedTexture(ref generatedStartSizeLUT);
                DestroyGeneratedTexture(ref generatedStartSizeYLUT);
                if (IsCurveMode(startSize.mode))
                {
                    generatedStartSizeLUT = CurveLUTBuilder.Build(
                        startSize,
                        assetName: "StartSize_LUT");
                    targetGPUParticleSystem.startSizeLUT =
                        generatedStartSizeLUT;
                }
                else
                {
                    targetGPUParticleSystem.startSizeLUT =
                        CurveLUTBuilder.GetDefaultUnitLUT();
                }
                if (main.startSize3D && IsCurveMode(startSizeY.mode))
                {
                    generatedStartSizeYLUT = CurveLUTBuilder.Build(
                        startSizeY,
                        assetName: "StartSizeY_LUT");
                    targetGPUParticleSystem.startSizeYLUT =
                        generatedStartSizeYLUT;
                }
                else
                {
                    targetGPUParticleSystem.startSizeYLUT =
                        CurveLUTBuilder.GetDefaultUnitLUT();
                }
                lastStartSizeLUTUpdate = Time.realtimeSinceStartup;
            }

            ShurikenMinMaxUtility.TryGetColorRange(
                main.startColor, out Color minimumColor, out Color maximumColor);
            targetGPUParticleSystem.SetStartColorRange(minimumColor, maximumColor,
                main.startColor.mode == ParticleSystemGradientMode.TwoColors);
            targetGPUParticleSystem.startColorMode = main.startColor.mode;
            bool updateStartColorLUT = force ||
                Time.realtimeSinceStartup - lastStartColorLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (updateStartColorLUT)
            {
                DestroyGeneratedTexture(ref generatedStartColorLUT);
                if (IsStartColorGradientMode(main.startColor.mode))
                {
                    generatedStartColorLUT = GradientLUTBuilder.Build(
                        main.startColor,
                        assetName: "StartColor_LUT");
                    targetGPUParticleSystem.startColorLUT =
                        generatedStartColorLUT;
                }
                else
                {
                    targetGPUParticleSystem.startColorLUT =
                        GradientLUTBuilder.GetDefaultWhiteLUT();
                }
                lastStartColorLUTUpdate = Time.realtimeSinceStartup;
            }

            ParticleSystem.MinMaxCurve gravityModifier = main.gravityModifier;
            ShurikenMinMaxUtility.TryGetConstantRange(
                gravityModifier, out minimum, out maximum);
            targetGPUParticleSystem.SetGravityModifierRange(minimum, maximum);
            targetGPUParticleSystem.gravityModifierMode = gravityModifier.mode;
            bool updateGravityModifierLUT = force ||
                Time.realtimeSinceStartup - lastGravityModifierLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (updateGravityModifierLUT)
            {
                DestroyGeneratedTexture(ref generatedGravityModifierLUT);
                if (IsCurveMode(gravityModifier.mode))
                {
                    generatedGravityModifierLUT = CurveLUTBuilder.BuildSigned(
                        gravityModifier,
                        assetName: "GravityModifier_LUT");
                    targetGPUParticleSystem.gravityModifierLUT =
                        generatedGravityModifierLUT;
                }
                else
                {
                    targetGPUParticleSystem.gravityModifierLUT =
                        CurveLUTBuilder.GetDefaultZeroLUT();
                }
                lastGravityModifierLUTUpdate = Time.realtimeSinceStartup;
            }

            if (force || main.gravitySource != cachedGravitySource)
            {
                targetGPUParticleSystem.gravitySource = main.gravitySource;
                cachedGravitySource = main.gravitySource;
            }

            // Simulation Speed
            float newSimulationSpeed = main.simulationSpeed;
            if (force || Mathf.Abs(newSimulationSpeed - cachedSimulationSpeed) > 0.001f)
            {
                targetGPUParticleSystem.simulationSpeed = newSimulationSpeed;
                cachedSimulationSpeed = newSimulationSpeed;
            }

            if (force || main.useUnscaledTime != cachedUseUnscaledTime)
            {
                targetGPUParticleSystem.useUnscaledTime = main.useUnscaledTime;
                cachedUseUnscaledTime = main.useUnscaledTime;
            }

            ParticleSystem.MinMaxCurve startRotation = main.startRotation3D
                ? main.startRotationZ
                : main.startRotation;
            ShurikenMinMaxUtility.TryGetConstantRange(startRotation, out minimum, out maximum);
            targetGPUParticleSystem.SetStartRotationRange(minimum, maximum);
            targetGPUParticleSystem.startRotationMode = startRotation.mode;
            bool updateStartRotationLUT = force ||
                Time.realtimeSinceStartup - lastStartRotationLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (updateStartRotationLUT)
            {
                DestroyGeneratedTexture(ref generatedStartRotationLUT);
                if (IsCurveMode(startRotation.mode))
                {
                    generatedStartRotationLUT = CurveLUTBuilder.BuildSigned(
                        startRotation,
                        assetName: "StartRotation_LUT");
                    targetGPUParticleSystem.startRotationLUT =
                        generatedStartRotationLUT;
                }
                else
                {
                    targetGPUParticleSystem.startRotationLUT =
                        CurveLUTBuilder.GetDefaultZeroLUT();
                }
                lastStartRotationLUTUpdate = Time.realtimeSinceStartup;
            }

            if (force ||
                Mathf.Abs(main.flipRotation - cachedFlipRotation) > 0.0001f)
            {
                targetGPUParticleSystem.flipRotation =
                    Mathf.Clamp01(main.flipRotation);
                cachedFlipRotation = main.flipRotation;
            }
        }

        void SyncPlaybackState(bool force = false)
        {
            PlaybackSyncState state = ResolvePlaybackState();
            if (!force && state == cachedPlaybackState) return;

            switch (state)
            {
                case PlaybackSyncState.Playing:
                    targetGPUParticleSystem.Play(false);
                    break;

                case PlaybackSyncState.Paused:
                    targetGPUParticleSystem.Pause(false);
                    break;

                case PlaybackSyncState.StoppedEmitting:
                    targetGPUParticleSystem.Stop(
                        false,
                        ParticleSystemStopBehavior.StopEmitting);
                    break;

                case PlaybackSyncState.StoppedAndClear:
                    targetGPUParticleSystem.Stop(
                        false,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                    break;
            }

            cachedPlaybackState = state;
        }

        PlaybackSyncState ResolvePlaybackState()
        {
            if (sourceParticleSystem.isPaused)
            {
                return PlaybackSyncState.Paused;
            }

            if (sourceParticleSystem.isPlaying)
            {
                if (!sourceParticleSystem.isEmitting &&
                    SourceWasStoppedEmitting())
                {
                    return PlaybackSyncState.StoppedEmitting;
                }
                return PlaybackSyncState.Playing;
            }

            return sourceParticleSystem.particleCount > 0
                ? PlaybackSyncState.StoppedEmitting
                : PlaybackSyncState.StoppedAndClear;
        }

        bool SourceWasStoppedEmitting()
        {
            var emission = sourceParticleSystem.emission;
            if (!emission.enabled) return false;

            var main = sourceParticleSystem.main;
            if (main.loop) return true;

            float naturalEmissionEnd =
                Mathf.Max(0f, main.startDelay.constantMax) +
                Mathf.Max(0.05f, main.duration);
            return sourceParticleSystem.time < naturalEmissionEnd - 1e-4f;
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

            if (force ||
                shape.alignToDirection != cachedShapeAlignToDirection)
            {
                cachedShapeAlignToDirection = shape.alignToDirection;
                shapeChanged = true;
            }

            if (force || Mathf.Abs(
                    shape.randomDirectionAmount -
                    cachedShapeRandomDirectionAmount) > 0.001f)
            {
                cachedShapeRandomDirectionAmount =
                    shape.randomDirectionAmount;
                shapeChanged = true;
            }

            if (force || Mathf.Abs(
                    shape.sphericalDirectionAmount -
                    cachedShapeSphericalDirectionAmount) > 0.001f)
            {
                cachedShapeSphericalDirectionAmount =
                    shape.sphericalDirectionAmount;
                shapeChanged = true;
            }

            if (force || Mathf.Abs(
                    shape.randomPositionAmount -
                    cachedShapeRandomPositionAmount) > 0.001f)
            {
                cachedShapeRandomPositionAmount =
                    shape.randomPositionAmount;
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

            if (force || shape.arcMode != cachedShapeArcMode)
            {
                cachedShapeArcMode = shape.arcMode;
                shapeChanged = true;
            }

            if (force || Mathf.Abs(
                    shape.arcSpread - cachedShapeArcSpread) > 0.001f)
            {
                cachedShapeArcSpread = shape.arcSpread;
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

            bool timeToUpdateArcSpeed = force ||
                Time.realtimeSinceStartup - lastShapeArcLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (timeToUpdateArcSpeed)
            {
                DestroyGeneratedTexture(ref generatedShapeArcSpeedLUT);
                targetGPUParticleSystem.shapeArcSpeedMode =
                    shape.arcSpeed.mode;
                bool animatedArc =
                    shape.arcMode ==
                        ParticleSystemShapeMultiModeValue.Loop ||
                    shape.arcMode ==
                        ParticleSystemShapeMultiModeValue.PingPong;
                if (animatedArc)
                {
                    generatedShapeArcSpeedLUT =
                        CurveLUTBuilder.BuildIntegral(shape.arcSpeed);
                    targetGPUParticleSystem.shapeArcSpeedIntegralLUT =
                        generatedShapeArcSpeedLUT;
                }
                else
                {
                    targetGPUParticleSystem.shapeArcSpeedIntegralLUT =
                        CurveLUTBuilder.GetDefaultLinear01LUT();
                }
                lastShapeArcLUTUpdate = Time.realtimeSinceStartup;
            }
        }

        void ApplyShapeMapping(ParticleSystem.ShapeModule shape)
        {
            targetGPUParticleSystem.shapeRandomDirectionAmount =
                shape.randomDirectionAmount;
            targetGPUParticleSystem.shapeSphericalDirectionAmount =
                shape.sphericalDirectionAmount;
            targetGPUParticleSystem.shapeRandomPositionAmount =
                shape.randomPositionAmount;
            targetGPUParticleSystem.shapeArcMode =
                ConvertShapeArcMode(shape.arcMode);
            targetGPUParticleSystem.shapeArcSpread =
                Mathf.Clamp01(shape.arcSpread);

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
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeSphereRadius = shape.radius;
                    targetGPUParticleSystem.shapeRadiusThickness =
                        shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.Hemisphere:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Hemisphere;
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeSphereRadius = shape.radius;
                    targetGPUParticleSystem.shapeRadiusThickness =
                        shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.Cone:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Cone;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Base;
                    targetGPUParticleSystem.shapeConeRadius = shape.radius;
                    targetGPUParticleSystem.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    targetGPUParticleSystem.shapeConeAngle = shape.angle;
                    targetGPUParticleSystem.shapeRadiusThickness =
                        shape.radiusThickness;
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.ConeVolume:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Cone;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeConeRadius = shape.radius;
                    targetGPUParticleSystem.shapeConeLength = shape.length > 0f ? shape.length : 1f;
                    targetGPUParticleSystem.shapeConeAngle = shape.angle;
                    targetGPUParticleSystem.shapeRadiusThickness =
                        shape.radiusThickness;
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.Donut:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Donut;
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.radiusThickness <= 0.001f
                            ? ShapeEmitFromGPU.Surface
                            : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeDonutRadius = Mathf.Max(0f, shape.radius);
                    targetGPUParticleSystem.shapeDonutThickness = Mathf.Max(0f, shape.donutRadius);
                    targetGPUParticleSystem.shapeRadiusThickness =
                        shape.radiusThickness;
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    break;

                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Box;
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.shapeType == ParticleSystemShapeType.Box
                            ? ShapeEmitFromGPU.Volume
                            : shape.shapeType == ParticleSystemShapeType.BoxEdge
                                ? ShapeEmitFromGPU.Edge
                                : ShapeEmitFromGPU.Surface;
                    targetGPUParticleSystem.shapeBoxSize = Vector3.one;
                    break;

                case ParticleSystemShapeType.Circle:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Circle;
                    targetGPUParticleSystem.shapeEmitFrom =
                        shape.radiusThickness <= 0.001f
                        ? ShapeEmitFromGPU.Surface
                        : ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeCircleRadius = Mathf.Max(0f, shape.radius);
                    targetGPUParticleSystem.shapeConeArcDeg = shape.arc;
                    targetGPUParticleSystem.shapeRadiusThickness =
                        shape.radiusThickness;
                    break;

                case ParticleSystemShapeType.SingleSidedEdge:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Edge;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Edge;
                    targetGPUParticleSystem.shapeEdgeLength =
                        Mathf.Max(0f, 2f * shape.radius);
                    break;

                case ParticleSystemShapeType.Rectangle:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Rectangle;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    targetGPUParticleSystem.shapeRectangleSize = Vector2.one;
                    break;

                default:
                    targetGPUParticleSystem.shapeType = ShapeTypeGPU.Point;
                    targetGPUParticleSystem.shapeEmitFrom = ShapeEmitFromGPU.Base;
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
            targetGPUParticleSystem.velocityOverLifetimeSpeedModifierEnabled =
                velocity.enabled;
            targetGPUParticleSystem.velocityOverLifetimeSpace =
                velocity.space == ParticleSystemSimulationSpace.World
                    ? SimulationSpace.World
                    : SimulationSpace.Local;
            targetGPUParticleSystem.velocityOverLifetimeOrbitalEnabled =
                velocity.enabled &&
                (HasNonZeroCurve(velocity.orbitalX) ||
                 HasNonZeroCurve(velocity.orbitalY) ||
                 HasNonZeroCurve(velocity.orbitalZ) ||
                 HasNonZeroCurve(velocity.radial));

            DestroyGeneratedVelocityLUT();
            if (velocity.enabled)
            {
                generatedVelocityLUT = MinMaxCurveVector3LUTBuilder.Build(
                    velocity.x,
                    velocity.y,
                    velocity.z,
                    velocity.speedModifier);
                targetGPUParticleSystem.velocityOverLifetimeLUT = generatedVelocityLUT;
                if (targetGPUParticleSystem.velocityOverLifetimeOrbitalEnabled)
                {
                    generatedVelocityOrbitalLUT =
                        MinMaxCurveVector3LUTBuilder.Build(
                            velocity.orbitalX,
                            velocity.orbitalY,
                            velocity.orbitalZ,
                            velocity.radial);
                    generatedVelocityOrbitalOffsetLUT =
                        MinMaxCurveVector3LUTBuilder.Build(
                            velocity.orbitalOffsetX,
                            velocity.orbitalOffsetY,
                            velocity.orbitalOffsetZ);
                    targetGPUParticleSystem.velocityOverLifetimeOrbitalLUT =
                        generatedVelocityOrbitalLUT;
                    targetGPUParticleSystem.velocityOverLifetimeOrbitalOffsetLUT =
                        generatedVelocityOrbitalOffsetLUT;
                }
                else
                {
                    targetGPUParticleSystem.velocityOverLifetimeOrbitalLUT =
                        MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
                    targetGPUParticleSystem.velocityOverLifetimeOrbitalOffsetLUT =
                        MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
                }
            }
            else
            {
                targetGPUParticleSystem.velocityOverLifetimeLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultVelocityLUT();
                targetGPUParticleSystem.velocityOverLifetimeOrbitalLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
                targetGPUParticleSystem.velocityOverLifetimeOrbitalOffsetLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            }

            lastVelocityLUTUpdate = Time.realtimeSinceStartup;
        }

        static bool HasNonZeroCurve(ParticleSystem.MinMaxCurve curve)
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

        void SyncLimitVelocityOverLifetime(bool forceUpdate = false)
        {
            var limit = sourceParticleSystem.limitVelocityOverLifetime;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastLimitVelocityLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.limitVelocityOverLifetimeEnabled =
                limit.enabled;
            targetGPUParticleSystem.limitVelocityOverLifetimeSeparateAxes =
                limit.separateAxes;
            targetGPUParticleSystem.limitVelocityOverLifetimeSpace =
                limit.space == ParticleSystemSimulationSpace.World
                    ? SimulationSpace.World
                    : SimulationSpace.Local;
            targetGPUParticleSystem.limitVelocityOverLifetimeDampen =
                Mathf.Clamp01(limit.dampen);
            targetGPUParticleSystem.limitVelocityMultiplyDragBySize =
                limit.multiplyDragByParticleSize;
            targetGPUParticleSystem.limitVelocityMultiplyDragByVelocity =
                limit.multiplyDragByParticleVelocity;

            DestroyGeneratedLimitVelocityLUT();
            if (limit.enabled)
            {
                generatedLimitVelocityLUT =
                    LimitVelocityLUTBuilder.Build(limit);
                targetGPUParticleSystem.limitVelocityOverLifetimeLUT =
                    generatedLimitVelocityLUT;
            }
            else
            {
                targetGPUParticleSystem.limitVelocityOverLifetimeLUT =
                    LimitVelocityLUTBuilder.GetDefaultZeroLUT();
            }

            lastLimitVelocityLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncInheritVelocity(bool forceUpdate = false)
        {
            var inherit = sourceParticleSystem.inheritVelocity;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastInheritVelocityLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.inheritVelocityEnabled = inherit.enabled;
            targetGPUParticleSystem.inheritVelocityMode = inherit.mode;

            DestroyGeneratedInheritVelocityLUT();
            if (inherit.enabled)
            {
                generatedInheritVelocityLUT =
                    CurveLUTBuilder.BuildSigned(inherit.curve);
                targetGPUParticleSystem.inheritVelocityLUT =
                    generatedInheritVelocityLUT;
            }
            else
            {
                targetGPUParticleSystem.inheritVelocityLUT =
                    CurveLUTBuilder.GetDefaultZeroLUT();
            }

            lastInheritVelocityLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncLifetimeByEmitterSpeed(bool forceUpdate = false)
        {
            var lifetime = sourceParticleSystem.lifetimeByEmitterSpeed;
            bool timeToUpdate =
                Time.realtimeSinceStartup -
                lastLifetimeByEmitterSpeedLUTUpdate > LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.lifetimeByEmitterSpeedEnabled =
                lifetime.enabled;
            targetGPUParticleSystem.SetLifetimeByEmitterSpeedRange(
                lifetime.range);

            DestroyGeneratedLifetimeByEmitterSpeedLUT();
            if (lifetime.enabled)
            {
                generatedLifetimeByEmitterSpeedLUT =
                    CurveLUTBuilder.Build(
                        lifetime.curve,
                        assetName: "LifetimeByEmitterSpeed_LUT");
                targetGPUParticleSystem.lifetimeByEmitterSpeedLUT =
                    generatedLifetimeByEmitterSpeedLUT;
            }
            else
            {
                targetGPUParticleSystem.lifetimeByEmitterSpeedLUT =
                    CurveLUTBuilder.GetDefaultUnitLUT();
            }

            lastLifetimeByEmitterSpeedLUTUpdate =
                Time.realtimeSinceStartup;
        }

        void SyncNoise(bool forceUpdate = false)
        {
            var noise = sourceParticleSystem.noise;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastNoiseLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.noiseEnabled = noise.enabled;
            targetGPUParticleSystem.noiseSeparateAxes = noise.separateAxes;
            targetGPUParticleSystem.noiseFrequency =
                Mathf.Max(0.0001f, noise.frequency);
            targetGPUParticleSystem.noiseDamping = noise.damping;
            targetGPUParticleSystem.noiseQuality = noise.quality;
            targetGPUParticleSystem.noiseOctaveCount =
                Mathf.Clamp(noise.octaveCount, 1, 4);
            targetGPUParticleSystem.noiseOctaveMultiplier =
                Mathf.Max(0f, noise.octaveMultiplier);
            targetGPUParticleSystem.noiseOctaveScale =
                Mathf.Max(1f, noise.octaveScale);
            targetGPUParticleSystem.noiseRemapEnabled =
                noise.enabled && noise.remapEnabled;

            DestroyGeneratedTexture(ref generatedNoiseStrengthLUT);
            DestroyGeneratedTexture(ref generatedNoiseAmountsLUT);
            DestroyGeneratedTexture(ref generatedNoiseRemapLUT);
            if (!noise.enabled)
            {
                targetGPUParticleSystem.noiseStrengthLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultUnitVectorLUT();
                targetGPUParticleSystem.noiseAmountsLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultNoiseAmountsLUT();
                targetGPUParticleSystem.noiseRemapLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultSignedIdentityLUT();
                lastNoiseLUTUpdate = Time.realtimeSinceStartup;
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
            generatedNoiseStrengthLUT =
                MinMaxCurveVector3LUTBuilder.Build(
                    strengthX,
                    strengthY,
                    strengthZ,
                    assetName: "NoiseStrength_LUT");
            generatedNoiseAmountsLUT =
                MinMaxCurveVector3LUTBuilder.Build(
                    noise.positionAmount,
                    noise.rotationAmount,
                    noise.sizeAmount,
                    noise.scrollSpeed,
                    assetName: "NoiseAmounts_LUT");
            targetGPUParticleSystem.noiseStrengthLUT =
                generatedNoiseStrengthLUT;
            targetGPUParticleSystem.noiseAmountsLUT =
                generatedNoiseAmountsLUT;

            if (noise.remapEnabled)
            {
                ParticleSystem.MinMaxCurve remapX = noise.separateAxes
                    ? noise.remapX
                    : noise.remap;
                ParticleSystem.MinMaxCurve remapY = noise.separateAxes
                    ? noise.remapY
                    : noise.remap;
                ParticleSystem.MinMaxCurve remapZ = noise.separateAxes
                    ? noise.remapZ
                    : noise.remap;
                generatedNoiseRemapLUT =
                    MinMaxCurveVector3LUTBuilder.Build(
                        remapX,
                        remapY,
                        remapZ,
                        assetName: "NoiseRemap_LUT");
                targetGPUParticleSystem.noiseRemapLUT =
                    generatedNoiseRemapLUT;
            }
            else
            {
                targetGPUParticleSystem.noiseRemapLUT =
                    MinMaxCurveVector3LUTBuilder.GetDefaultSignedIdentityLUT();
            }

            lastNoiseLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncCollision(bool forceUpdate = false)
        {
            const int maxSupportedPlanes = 6;
            ParticleSystem.CollisionModule collision =
                sourceParticleSystem.collision;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastCollisionLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.collisionEnabled = collision.enabled;
            targetGPUParticleSystem.collisionType = collision.type;
            targetGPUParticleSystem.collisionMinKillSpeed =
                Mathf.Max(0f, collision.minKillSpeed);
            targetGPUParticleSystem.collisionMaxKillSpeed = Mathf.Max(
                targetGPUParticleSystem.collisionMinKillSpeed,
                collision.maxKillSpeed);
            targetGPUParticleSystem.collisionRadiusScale =
                Mathf.Max(0f, collision.radiusScale);

            if (collision.enabled &&
                collision.type == ParticleSystemCollisionType.Planes)
            {
                int planeCount = Mathf.Min(
                    collision.planeCount,
                    maxSupportedPlanes);
                var planes = new Transform[planeCount];
                for (int i = 0; i < planeCount; i++)
                {
                    planes[i] = collision.GetPlane(i);
                }
                targetGPUParticleSystem.collisionPlanes = planes;
            }
            else
            {
                targetGPUParticleSystem.collisionPlanes =
                    System.Array.Empty<Transform>();
            }

            DestroyGeneratedTexture(ref generatedCollisionParametersLUT);
            if (collision.enabled)
            {
                generatedCollisionParametersLUT =
                    MinMaxCurveVector3LUTBuilder.Build(
                        collision.dampen,
                        collision.bounce,
                        collision.lifetimeLoss,
                        assetName: "CollisionParameters_LUT");
                targetGPUParticleSystem.collisionParametersLUT =
                    generatedCollisionParametersLUT;
            }
            else
            {
                targetGPUParticleSystem.collisionParametersLUT =
                    MinMaxCurveVector3LUTBuilder
                        .GetDefaultCollisionParametersLUT();
            }

            lastCollisionLUTUpdate = Time.realtimeSinceStartup;
        }

        void SyncTextureSheetAnimation(bool forceUpdate = false)
        {
            var textureSheet = sourceParticleSystem.textureSheetAnimation;
            bool timeToUpdate =
                Time.realtimeSinceStartup - lastTextureSheetLUTUpdate >
                LUT_UPDATE_INTERVAL;
            if (!forceUpdate && !timeToUpdate) return;

            targetGPUParticleSystem.textureSheetMode = textureSheet.mode;
            targetGPUParticleSystem.textureSheetAnimation =
                textureSheet.animation;
            targetGPUParticleSystem.textureSheetTimeMode = textureSheet.timeMode;
            targetGPUParticleSystem.textureSheetRowMode = textureSheet.rowMode;
            targetGPUParticleSystem.textureSheetUVChannelMask =
                textureSheet.uvChannelMask;
            targetGPUParticleSystem.textureSheetTilesX =
                Mathf.Max(1, textureSheet.numTilesX);
            targetGPUParticleSystem.textureSheetTilesY =
                Mathf.Max(1, textureSheet.numTilesY);
            targetGPUParticleSystem.textureSheetRowIndex = Mathf.Clamp(
                textureSheet.rowIndex,
                0,
                targetGPUParticleSystem.textureSheetTilesY - 1);
            targetGPUParticleSystem.textureSheetCycleCount =
                Mathf.Max(1, textureSheet.cycleCount);
            targetGPUParticleSystem.textureSheetFps =
                Mathf.Max(0f, textureSheet.fps);
            targetGPUParticleSystem.SetTextureSheetSpeedRange(
                textureSheet.speedRange);

            bool gridMode = textureSheet.mode == ParticleSystemAnimationMode.Grid;
            bool affectsUV0 =
                (textureSheet.uvChannelMask & UVChannelFlags.UV0) != 0;
            targetGPUParticleSystem.textureSheetAnimationEnabled =
                textureSheet.enabled && gridMode && affectsUV0;
            if (textureSheet.animation == ParticleSystemAnimationType.SingleRow &&
                textureSheet.rowMode == ParticleSystemAnimationRowMode.MeshIndex)
            {
                targetGPUParticleSystem.textureSheetRowMode =
                    ParticleSystemAnimationRowMode.Custom;
            }

            DestroyGeneratedTextureSheetLUTs();
            if (textureSheet.enabled && gridMode)
            {
                generatedTextureSheetFrameLUT = CurveLUTBuilder.BuildSigned(
                    textureSheet.frameOverTime,
                    assetName: "TextureSheetFrameOverTime_LUT");
                generatedTextureSheetStartLUT = CurveLUTBuilder.BuildSigned(
                    textureSheet.startFrame,
                    resolution: 2,
                    assetName: "TextureSheetStartFrame_LUT");
                targetGPUParticleSystem.textureSheetFrameOverTimeLUT =
                    generatedTextureSheetFrameLUT;
                targetGPUParticleSystem.textureSheetStartFrameLUT =
                    generatedTextureSheetStartLUT;
            }
            else
            {
                targetGPUParticleSystem.textureSheetFrameOverTimeLUT =
                    CurveLUTBuilder.GetDefaultLinear01LUT();
                targetGPUParticleSystem.textureSheetStartFrameLUT =
                    CurveLUTBuilder.GetDefaultZeroLUT();
            }

            lastTextureSheetLUTUpdate = Time.realtimeSinceStartup;
        }

        void OnDestroy()
        {
            DestroyGeneratedTexture(ref generatedNoiseStrengthLUT);
            DestroyGeneratedTexture(ref generatedNoiseAmountsLUT);
            DestroyGeneratedTexture(ref generatedNoiseRemapLUT);
            DestroyGeneratedTexture(ref generatedCollisionParametersLUT);
            DestroyGeneratedTexture(ref generatedShapeArcSpeedLUT);
            DestroyGeneratedForceLUT();
            DestroyGeneratedVelocityLUT();
            DestroyGeneratedLimitVelocityLUT();
            DestroyGeneratedInheritVelocityLUT();
            DestroyGeneratedLifetimeByEmitterSpeedLUT();
            DestroyGeneratedTextureSheetLUTs();
            DestroyGeneratedTexture(ref generatedStartColorLUT);
            DestroyGeneratedTexture(ref generatedStartLifetimeLUT);
            DestroyGeneratedTexture(ref generatedStartSpeedLUT);
            DestroyGeneratedTexture(ref generatedStartSizeLUT);
            DestroyGeneratedTexture(ref generatedStartSizeYLUT);
            DestroyGeneratedTexture(ref generatedGravityModifierLUT);
            DestroyGeneratedTexture(ref generatedStartRotationLUT);
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
            DestroyGeneratedTexture(ref generatedVelocityLUT);
            DestroyGeneratedTexture(ref generatedVelocityOrbitalLUT);
            DestroyGeneratedTexture(ref generatedVelocityOrbitalOffsetLUT);
        }

        void DestroyGeneratedLimitVelocityLUT()
        {
            if (generatedLimitVelocityLUT == null) return;

            if (Application.isPlaying) Destroy(generatedLimitVelocityLUT);
            else DestroyImmediate(generatedLimitVelocityLUT);
            generatedLimitVelocityLUT = null;
        }

        void DestroyGeneratedInheritVelocityLUT()
        {
            if (generatedInheritVelocityLUT == null) return;

            if (Application.isPlaying) Destroy(generatedInheritVelocityLUT);
            else DestroyImmediate(generatedInheritVelocityLUT);
            generatedInheritVelocityLUT = null;
        }

        void DestroyGeneratedLifetimeByEmitterSpeedLUT()
        {
            if (generatedLifetimeByEmitterSpeedLUT == null) return;

            if (Application.isPlaying)
            {
                Destroy(generatedLifetimeByEmitterSpeedLUT);
            }
            else
            {
                DestroyImmediate(generatedLifetimeByEmitterSpeedLUT);
            }
            generatedLifetimeByEmitterSpeedLUT = null;
        }

        void DestroyGeneratedTextureSheetLUTs()
        {
            DestroyGeneratedTexture(
                ref generatedTextureSheetFrameLUT);
            DestroyGeneratedTexture(
                ref generatedTextureSheetStartLUT);
        }

        void DestroyGeneratedTexture(ref Texture2D texture)
        {
            if (texture == null) return;

            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
            texture = null;
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

        void DestroyGeneratedColorLUT()
        {
            if (generatedColorLUT == null) return;

            if (Application.isPlaying) Destroy(generatedColorLUT);
            else DestroyImmediate(generatedColorLUT);
            generatedColorLUT = null;
        }

        void DestroyGeneratedSizeLUT()
        {
            DestroyGeneratedTexture(ref generatedSizeLUT);
            DestroyGeneratedTexture(ref generatedSizeYLUT);
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
            DestroyGeneratedTexture(ref generatedSizeBySpeedLUT);
            DestroyGeneratedTexture(ref generatedSizeBySpeedYLUT);
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
            bool rendererValuesChanged =
                sourceRenderer.allowRoll != cachedAllowRoll ||
                sourceRenderer.pivot != cachedPivot ||
                !Mathf.Approximately(
                    sourceRenderer.normalDirection,
                    cachedNormalDirection) ||
                !Mathf.Approximately(
                    sourceRenderer.minParticleSize,
                    cachedMinParticleSize) ||
                !Mathf.Approximately(
                    sourceRenderer.maxParticleSize,
                    cachedMaxParticleSize) ||
                !Mathf.Approximately(
                    sourceRenderer.lengthScale,
                    cachedLengthScale) ||
                !Mathf.Approximately(
                    sourceRenderer.velocityScale,
                    cachedVelocityScale) ||
                !Mathf.Approximately(
                    sourceRenderer.cameraVelocityScale,
                    cachedCameraVelocityScale) ||
                sourceRenderer.freeformStretching !=
                    cachedFreeformStretching ||
                sourceRenderer.rotateWithStretchDirection !=
                    cachedRotateWithStretchDirection ||
                sourceRenderer.localBounds != cachedRendererLocalBounds;

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

            if (force || rendererChanged || rendererValuesChanged)
            {
                targetGPUParticleSystem.allowRoll = sourceRenderer.allowRoll;
                targetGPUParticleSystem.pivot = new Vector2(sourceRenderer.pivot.x, sourceRenderer.pivot.y);
                targetGPUParticleSystem.normalDirection = sourceRenderer.normalDirection;
                targetGPUParticleSystem.screenSpaceSizeClampEnabled = true;
                targetGPUParticleSystem.minParticleSize =
                    sourceRenderer.minParticleSize;
                targetGPUParticleSystem.maxParticleSize =
                    sourceRenderer.maxParticleSize;
                targetGPUParticleSystem.stretchedLengthScale = sourceRenderer.lengthScale;
                targetGPUParticleSystem.stretchedVelocityScale = sourceRenderer.velocityScale;
                targetGPUParticleSystem.stretchedCameraVelocityScale = sourceRenderer.cameraVelocityScale;
                targetGPUParticleSystem.freeformStretching = sourceRenderer.freeformStretching;
                targetGPUParticleSystem.rotateWithStretchDirection = sourceRenderer.rotateWithStretchDirection;
                targetGPUParticleSystem.localCullingBounds =
                    sourceRenderer.localBounds;
                CacheRendererValues();
            }
        }

        void CacheRendererValues()
        {
            if (sourceRenderer == null) return;

            cachedAllowRoll = sourceRenderer.allowRoll;
            cachedPivot = sourceRenderer.pivot;
            cachedNormalDirection = sourceRenderer.normalDirection;
            cachedMinParticleSize = sourceRenderer.minParticleSize;
            cachedMaxParticleSize = sourceRenderer.maxParticleSize;
            cachedLengthScale = sourceRenderer.lengthScale;
            cachedVelocityScale = sourceRenderer.velocityScale;
            cachedCameraVelocityScale = sourceRenderer.cameraVelocityScale;
            cachedFreeformStretching = sourceRenderer.freeformStretching;
            cachedRotateWithStretchDirection =
                sourceRenderer.rotateWithStretchDirection;
            cachedRendererLocalBounds = sourceRenderer.localBounds;
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

            Material material = ShurikenConverter.GetPrimaryMaterial(
                sourceRenderer);
            Texture2D baseMap = ShurikenConverter.TryGetBaseMap(
                sourceRenderer);
            if (force || baseMap != cachedBaseMap)
            {
                targetGPUParticleSystem.baseMap = baseMap != null ? baseMap : Texture2D.whiteTexture;
                cachedBaseMap = baseMap;
            }

            Color baseColor = ShurikenConverter.GetMaterialBaseColor(material);
            if (force || baseColor != cachedMaterialBaseColor)
            {
                targetGPUParticleSystem.materialBaseColor = baseColor;
                cachedMaterialBaseColor = baseColor;
            }

            GPUParticleColorMode colorMode =
                ShurikenConverter.GetMaterialColorMode(material);
            if (force || colorMode != cachedMaterialColorMode)
            {
                targetGPUParticleSystem.materialColorMode = colorMode;
                cachedMaterialColorMode = colorMode;
            }

            bool frameBlending =
                ShurikenConverter.UsesFlipbookBlending(material);
            if (force ||
                frameBlending != cachedTextureSheetFrameBlending)
            {
                targetGPUParticleSystem.textureSheetFrameBlending =
                    frameBlending;
                cachedTextureSheetFrameBlending = frameBlending;
            }
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

            targetGPUParticleSystem.sizeOverLifetimeSeparateAxes =
                size.enabled && size.separateAxes;
            DestroyGeneratedSizeLUT();
            if (size.enabled)
            {
                ParticleSystem.MinMaxCurve curve = size.separateAxes
                    ? size.x
                    : size.size;
                generatedSizeLUT = CurveLUTBuilder.Build(curve);
                targetGPUParticleSystem.sizeOverLifetimeLUT = generatedSizeLUT;
                if (size.separateAxes)
                {
                    generatedSizeYLUT = CurveLUTBuilder.Build(
                        size.y,
                        assetName: "SizeOverLifetimeY_LUT");
                    targetGPUParticleSystem.sizeOverLifetimeYLUT =
                        generatedSizeYLUT;
                }
                else
                {
                    targetGPUParticleSystem.sizeOverLifetimeYLUT =
                        CurveLUTBuilder.GetDefaultUnitLUT();
                }
            }
            else
            {
                targetGPUParticleSystem.sizeOverLifetimeLUT =
                    CurveLUTBuilder.GetDefaultUnitLUT();
                targetGPUParticleSystem.sizeOverLifetimeYLUT =
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
            targetGPUParticleSystem.sizeBySpeedSeparateAxes =
                size.enabled && size.separateAxes;
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
                if (size.separateAxes)
                {
                    generatedSizeBySpeedYLUT = CurveLUTBuilder.Build(
                        size.y,
                        assetName: "SizeBySpeedY_LUT");
                    targetGPUParticleSystem.sizeBySpeedYLUT =
                        generatedSizeBySpeedYLUT;
                }
                else
                {
                    targetGPUParticleSystem.sizeBySpeedYLUT =
                        CurveLUTBuilder.GetDefaultUnitLUT();
                }
            }
            else
            {
                targetGPUParticleSystem.sizeBySpeedLUT =
                    CurveLUTBuilder.GetDefaultUnitLUT();
                targetGPUParticleSystem.sizeBySpeedYLUT =
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

