using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace GPUParticles
{
    public enum ParticleABDisplayMode
    {
        Both,
        ShurikenOnly,
        GPUOnly
    }

    public enum ParticleABValidationProfile
    {
        BaselineCone,
        ForceOverLifetimePoint,
        RandomizedMainPoint,
        EmissionBurstPoint,
        EmissionRateCurvePoint,
        EmissionRateDistancePoint,
        VelocityOverLifetimePoint,
        LimitVelocityOverLifetimePoint,
        LimitVelocityOverLifetimeAxesPoint,
        InheritVelocityInitialPoint,
        InheritVelocityCurrentPoint,
        RotationOverLifetimeCurvePoint,
        RotationBySpeedCurvePoint,
        ColorSizeOverLifetimeRandomizedPoint,
        ColorSizeBySpeedRandomizedPoint,
        LifetimeByEmitterSpeedPoint,
        ShapeSpherePoint,
        ShapeCirclePoint,
        ShapeDonutPoint,
        ShapeEdgePoint,
        ShapeRectanglePoint,
        ShapeBoxEdgePoint,
        TextureSheetLifetimePoint,
        TextureSheetSpeedPoint,
        TextureSheetFPSPoint,
        TextureSheetSingleRowPoint,
        VelocitySpeedModifierPoint,
        StartColorGradientPoint,
        StartColorTwoGradientsPoint,
        StartColorRandomColorPoint,
        StartSpeedCurvePoint,
        StartSpeedTwoCurvesPoint,
        StartSizeCurvePoint,
        StartSizeTwoCurvesPoint,
        GravityModifierCurvePoint,
        GravityModifierTwoCurvesPoint,
        StartLifetimeCurvePoint,
        StartLifetimeTwoCurvesPoint,
        StartRotationCurvePoint,
        StartRotationTwoCurvesPoint,
        VelocityOrbitalRadialPoint,
        SizeSeparateAxesPoint,
        RendererScreenSizeClampPoint,
        UnscaledTimePoint,
        ScalingHierarchyPoint,
        ScalingLocalPoint,
        ScalingShapePoint,
        PlaybackLifecyclePoint,
        PrewarmPoint
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ParticleABValidationController : MonoBehaviour
    {
        [Header("A/B Systems")]
        public ParticleSystem shuriken;
        public GPUParticleSystem gpuParticles;
        public ParticleABDisplayMode displayMode = ParticleABDisplayMode.Both;
        public ParticleABValidationProfile validationProfile = ParticleABValidationProfile.BaselineCone;
        public uint randomSeed = 12345;

        [Header("Deterministic Playback")]
        [Min(1)] public int fixedFrameRate = 60;

        [Header("RT Capture")]
        public bool captureOnPlay = true;
        [Min(0.1f)] public float captureFrequency = 5f;
        [Min(0.1f)] public float captureDuration = 10f;
        [Min(64)] public int captureWidth = 1280;
        [Min(64)] public int captureHeight = 720;
        public string outputFolder = "TestResults/ParticleAB";
        public bool exitEditorWhenCaptureCompletes;

        Camera captureCamera;
        ParticleSystemRenderer shurikenRenderer;
        ParticleSystem.Particle[] shurikenParticles;
        RenderTexture cameraCaptureRT;
        int playbackFrame;
        int nextCaptureFrame;
        int finalCaptureFrame;
        int captureIndex;
        bool captureActive;
        string sessionFolder;
        string metricsPath;
        int maximumCountDelta;
        float maximumMeanAgeError;
        float maximumMeanLifetimeError;
        float maximumMeanStartRotationError;
        float maximumMeanSpeedError;
        float maximumMeanSizeError;
        float maximumMeanSizeYError;
        float maximumMeanVelocityError;
        float maximumMeanPositionError;
        float maximumShurikenConeError;
        float maximumGPUConeError;
        float maximumForceKinematicsError;
        float maximumVelocitySpeedModifierKinematicsError;
        float maximumShurikenColorBoundsError;
        float maximumGPUColorBoundsError;
        float maximumShurikenStartLifetimeBoundsError;
        float maximumGPUStartLifetimeBoundsError;
        float maximumShurikenStartSpeedBoundsError;
        float maximumGPUStartSpeedBoundsError;
        float maximumShurikenStartSizeBoundsError;
        float maximumGPUStartSizeBoundsError;
        float maximumShurikenGravityIntegralBoundsError;
        float maximumGPUGravityIntegralBoundsError;
        float maximumShurikenSizeBoundsError;
        float maximumGPUSizeBoundsError;
        float maximumShurikenRotationError;
        float maximumGPURotationError;
        float maximumShurikenLimitVelocityError;
        float maximumGPULimitVelocityError;
        float maximumShurikenShapeDirectionError;
        float maximumGPUShapeDirectionError;
        float maximumShurikenShapeGeometryError;
        float maximumGPUShapeGeometryError;
        int maximumShurikenParticleCount;
        int maximumGPUParticleCount;
        int textureSheetComparableSamples;
        int textureSheetFrameMismatches;
        int textureSheetClassificationFailures;
        int maximumTextureSheetFrameDelta;
        int shurikenTextureSheetFrameMask;
        int gpuTextureSheetFrameMask;
        float maximumScreenSizePixelError;
        int screenSizeClassificationFailures;
        int currentShurikenScreenSizePixels = -1;
        float maximumScalingWidthPixelError;
        float maximumScalingHeightPixelError;
        float maximumScalingSpawnOffsetPixelError;
        int scalingBoundsClassificationFailures;
        bool hasCurrentShurikenScalingBounds;
        MarkerPixelBounds currentShurikenScalingBounds;
        Vector2 currentShurikenScalingOffsetPixels;
        float maximumShurikenMeanAge;
        float maximumGPUMeanAge;
        int playbackStateMismatchCount;
        int playbackEmptyViolationCount;
        int playbackTransitionMask;
        bool playbackInitialStopped;
        bool playbackDrainObserved;
        bool playbackClearObserved;
        int maximumShurikenStoppedParticleCount;
        int maximumGPUStoppedParticleCount;
        bool prewarmFirstSnapshotObserved;
        int prewarmFirstShurikenCount;
        int prewarmFirstGPUCount;
        float prewarmFirstShurikenMeanAge;
        float prewarmFirstGPUMeanAge;
        bool prewarmRestartSnapshotObserved;
        int prewarmRestartShurikenCount;
        int prewarmRestartGPUCount;
        float prewarmRestartShurikenMeanAge;
        float prewarmRestartGPUMeanAge;
        Texture2D profileForceLUT;
        Texture2D profileVelocityLUT;
        Texture2D profileVelocityOrbitalLUT;
        Texture2D profileVelocityOrbitalOffsetLUT;
        Texture2D profileLimitVelocityLUT;
        Texture2D profileInheritVelocityLUT;
        Texture2D profileLifetimeByEmitterSpeedLUT;
        Texture2D profileTextureSheetFrameLUT;
        Texture2D profileTextureSheetStartLUT;
        Texture2D profileTextureSheetAtlas;
        Material profileTextureSheetMaterial;
        Texture2D profileColorLUT;
        Texture2D profileStartColorLUT;
        Texture2D profileStartLifetimeLUT;
        Texture2D profileStartSpeedLUT;
        Texture2D profileStartSizeLUT;
        Texture2D profileGravityModifierLUT;
        Texture2D profileStartRotationLUT;
        Texture2D profileSizeLUT;
        Texture2D profileSizeYLUT;
        Texture2D profileSizeBySpeedXLUT;
        Texture2D profileSizeBySpeedYLUT;
        Texture2D profileRotationLUT;
        Texture2D profileRotationBySpeedLUT;
        Gradient profileColorMinimumGradient;
        Gradient profileColorMaximumGradient;
        AnimationCurve profileStartLifetimeMinimumCurve;
        AnimationCurve profileStartLifetimeMaximumCurve;
        AnimationCurve profileStartSpeedMinimumCurve;
        AnimationCurve profileStartSpeedMaximumCurve;
        AnimationCurve profileStartSizeMinimumCurve;
        AnimationCurve profileStartSizeMaximumCurve;
        AnimationCurve profileGravityMinimumCurve;
        AnimationCurve profileGravityMaximumCurve;
        AnimationCurve profileStartRotationMinimumCurve;
        AnimationCurve profileStartRotationMaximumCurve;
        ObservedRange shurikenStartColorBlendRange;
        ObservedRange gpuStartColorBlendRange;
        ObservedRange shurikenStartLifetimeBlendRange;
        ObservedRange gpuStartLifetimeBlendRange;
        ObservedRange shurikenStartSpeedBlendRange;
        ObservedRange gpuStartSpeedBlendRange;
        ObservedRange shurikenStartSizeBlendRange;
        ObservedRange gpuStartSizeBlendRange;
        ObservedRange shurikenGravityBlendRange;
        ObservedRange gpuGravityBlendRange;
        ObservedRange shurikenStartRotationBlendRange;
        ObservedRange gpuStartRotationBlendRange;
        AnimationCurve profileSizeMinimumCurve;
        AnimationCurve profileSizeMaximumCurve;
        static readonly Vector3 ValidationForce = new Vector3(2f, -1f, 0.5f);
        static readonly Vector3 RotationBySpeedAcceleration = Vector3.right * 2f;
        static readonly Vector3 LimitVelocityAxesAcceleration =
            new Vector3(10f, 4f, -8f);
        static readonly Vector3 VelocitySpeedModifierRawVelocity =
            new Vector3(2f, 4f, 0f);
        const float VelocitySpeedModifierLifetime = 4f;
        const float StartColorProfileDuration = 2f;
        const float StartLifetimeProfileDuration = 2f;
        const float StartLifetimeCurveTickPhase = 0.2f;
        const float StartSpeedProfileDuration = 2f;
        const float StartSizeProfileDuration = 2f;
        const float GravityModifierProfileDuration = 2f;
        const float StartRotationProfileDuration = 2f;
        const float RotationProfileStartRotation = 0.25f;
        const float RendererClampMinimum = 0.04f;
        const float RendererClampMaximum = 0.12f;
        const float RendererClampTravel = 120f;
        const float RendererClampRangeTolerancePixels = 4f;
        const float RendererClampPairTolerancePixels = 2f;
        const float ScalingPairTolerancePixels = 5f;
        const float UnscaledTimeScale = 0f;
        const int PlaybackPlayFrame = 31;
        const int PlaybackPauseFrame = 77;
        const int PlaybackResumeFrame = 107;
        const int PlaybackStopEmittingFrame = 151;
        const int PlaybackDrainExpectedFrame = 230;
        const int PlaybackReplayFrame = 241;
        const int PlaybackClearFrame = 260;
        const int PrewarmRestartStopFrame = 31;
        const int PrewarmRestartPlayFrame = 32;
        const int PrewarmRestartCaptureFrame = 36;
        static readonly Color32 RendererClampMarker =
            new Color32(242, 31, 242, 255);
        static readonly Color32 ScalingModeMarker =
            new Color32(242, 31, 242, 255);
        static readonly Color32[] TextureSheetPalette =
        {
            new Color32(242, 31, 31, 255),
            new Color32(31, 242, 31, 255),
            new Color32(31, 31, 242, 255),
            new Color32(242, 242, 31, 255),
            new Color32(242, 31, 242, 255),
            new Color32(31, 242, 242, 255),
            new Color32(242, 112, 31, 255),
            new Color32(145, 31, 242, 255)
        };
        Vector3 shurikenBasePositionWS;
        Vector3 gpuBasePositionWS;
        ObservedRange shurikenLifetimeRange;
        ObservedRange gpuLifetimeRange;
        ObservedRange shurikenSpeedRange;
        ObservedRange gpuSpeedRange;
        ObservedRange shurikenSizeRange;
        ObservedRange gpuSizeRange;
        ObservedRange shurikenColorRedRange;
        ObservedRange gpuColorRedRange;
        ObservedRange shurikenStartRotationRange;
        ObservedRange gpuStartRotationRange;
        ObservedRange shurikenDistancePositionRange;
        ObservedRange gpuDistancePositionRange;
        ObservedRange shurikenShapeSpawnXRange;
        ObservedRange shurikenShapeSpawnYRange;
        ObservedRange shurikenShapeSpawnZRange;
        ObservedRange gpuShapeSpawnXRange;
        ObservedRange gpuShapeSpawnYRange;
        ObservedRange gpuShapeSpawnZRange;
        ObservedRange shurikenLifetimeColorBlendRange;
        ObservedRange gpuLifetimeColorBlendRange;
        ObservedRange shurikenLifetimeSizeBlendRange;
        ObservedRange gpuLifetimeSizeBlendRange;
        ObservedRange shurikenSpeedColorBlendRange;
        ObservedRange gpuSpeedColorBlendRange;
        ObservedRange shurikenSpeedSizeBlendRange;
        ObservedRange gpuSpeedSizeBlendRange;
        ObservedRange shurikenScreenSizePixelRange;
        ObservedRange gpuScreenSizePixelRange;
        ObservedRange shurikenScalingWidthPixelRange;
        ObservedRange gpuScalingWidthPixelRange;
        ObservedRange shurikenScalingHeightPixelRange;
        ObservedRange gpuScalingHeightPixelRange;
        ObservedRange shurikenScalingSpawnOffsetPixelRange;
        ObservedRange gpuScalingSpawnOffsetPixelRange;
        ObservedRange shurikenScalingBirthXRange;
        ObservedRange gpuScalingBirthXRange;
        ObservedRange shurikenPausedMeanAgeRange;
        ObservedRange gpuPausedMeanAgeRange;

        struct MarkerPixelBounds
        {
            public bool Valid;
            public int MinimumX;
            public int MinimumY;
            public int MaximumX;
            public int MaximumY;

            public float Width => Valid
                ? MaximumX - MinimumX + 1f
                : -1f;
            public float Height => Valid
                ? MaximumY - MinimumY + 1f
                : -1f;
            public Vector2 Center => Valid
                ? new Vector2(
                    (MinimumX + MaximumX) * 0.5f,
                    (MinimumY + MaximumY) * 0.5f)
                : Vector2.zero;
        }

        struct ObservedRange
        {
            public float Minimum;
            public float Maximum;
            public bool HasSamples;

            public void Reset()
            {
                Minimum = float.PositiveInfinity;
                Maximum = float.NegativeInfinity;
                HasSamples = false;
            }

            public void Observe(float value)
            {
                Minimum = Mathf.Min(Minimum, value);
                Maximum = Mathf.Max(Maximum, value);
                HasSamples = true;
            }

            public bool Covers(float expectedMinimum, float expectedMaximum)
            {
                const float boundsTolerance = 0.01f;
                float requiredSpread = (expectedMaximum - expectedMinimum) * 0.4f;
                return HasSamples &&
                       Minimum >= expectedMinimum - boundsTolerance &&
                       Maximum <= expectedMaximum + boundsTolerance &&
                       Maximum - Minimum >= requiredSpread;
            }
        }

        void Awake()
        {
            captureCamera = GetComponent<Camera>();
            shurikenRenderer = shuriken != null ? shuriken.GetComponent<ParticleSystemRenderer>() : null;
        }

        void Start()
        {
            fixedFrameRate = Mathf.Max(1, fixedFrameRate);
            Time.captureFramerate = IsUnscaledTimeProfile()
                ? 0
                : fixedFrameRate;
            Application.targetFrameRate = fixedFrameRate;

            shurikenBasePositionWS = shuriken != null
                ? shuriken.transform.position
                : Vector3.zero;
            gpuBasePositionWS = gpuParticles != null
                ? gpuParticles.transform.position
                : Vector3.zero;

            playbackInitialStopped = !IsPlaybackLifecycleProfile() ||
                (shuriken != null && gpuParticles != null &&
                 shuriken.isStopped && gpuParticles.isStopped);

            ConfigureValidationProfile();
            RestartPlayback();
            ApplyDisplayMode();

            if (captureOnPlay)
            {
                BeginCapture();
            }
        }

        void OnDestroy()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            if (profileVelocityLUT != null) Destroy(profileVelocityLUT);
            if (profileVelocityOrbitalLUT != null)
            {
                Destroy(profileVelocityOrbitalLUT);
            }
            if (profileVelocityOrbitalOffsetLUT != null)
            {
                Destroy(profileVelocityOrbitalOffsetLUT);
            }
            if (profileLimitVelocityLUT != null) Destroy(profileLimitVelocityLUT);
            if (profileInheritVelocityLUT != null) Destroy(profileInheritVelocityLUT);
            if (profileLifetimeByEmitterSpeedLUT != null)
            {
                Destroy(profileLifetimeByEmitterSpeedLUT);
            }
            if (profileTextureSheetFrameLUT != null)
            {
                Destroy(profileTextureSheetFrameLUT);
            }
            if (profileTextureSheetStartLUT != null)
            {
                Destroy(profileTextureSheetStartLUT);
            }
            if (profileTextureSheetAtlas != null)
            {
                Destroy(profileTextureSheetAtlas);
            }
            if (profileTextureSheetMaterial != null)
            {
                Destroy(profileTextureSheetMaterial);
            }
            if (profileColorLUT != null) Destroy(profileColorLUT);
            if (profileStartColorLUT != null) Destroy(profileStartColorLUT);
            if (profileStartLifetimeLUT != null) Destroy(profileStartLifetimeLUT);
            if (profileStartSpeedLUT != null) Destroy(profileStartSpeedLUT);
            if (profileStartSizeLUT != null) Destroy(profileStartSizeLUT);
            if (profileGravityModifierLUT != null) Destroy(profileGravityModifierLUT);
            if (profileStartRotationLUT != null) Destroy(profileStartRotationLUT);
            if (profileSizeLUT != null) Destroy(profileSizeLUT);
            if (profileSizeYLUT != null) Destroy(profileSizeYLUT);
            if (profileSizeBySpeedXLUT != null)
            {
                Destroy(profileSizeBySpeedXLUT);
            }
            if (profileSizeBySpeedYLUT != null)
            {
                Destroy(profileSizeBySpeedYLUT);
            }
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            if (profileRotationBySpeedLUT != null) Destroy(profileRotationBySpeedLUT);
            ReleaseCameraCaptureTarget();
        }

        void Update()
        {
            UpdatePlaybackLifecycle();
            UpdatePrewarmLifecycle();

            if (captureActive && UsesMovingEmitterProfile())
            {
                float nextSimulationTime = (playbackFrame + 1f) / fixedFrameRate;
                MoveValidationEmitters(nextSimulationTime);
            }

            if (Input.GetKeyDown(KeyCode.B)) SetDisplayMode(ParticleABDisplayMode.Both);
            if (Input.GetKeyDown(KeyCode.S)) SetDisplayMode(ParticleABDisplayMode.ShurikenOnly);
            if (Input.GetKeyDown(KeyCode.G)) SetDisplayMode(ParticleABDisplayMode.GPUOnly);
            if (Input.GetKeyDown(KeyCode.R)) RestartPlayback();
            if (Input.GetKeyDown(KeyCode.Space)) Time.timeScale = Time.timeScale > 0f ? 0f : 1f;
            if (Input.GetKeyDown(KeyCode.C) && !captureActive) BeginCapture();
        }

        void LateUpdate()
        {
            if (!captureActive) return;

            if (IsPlaybackLifecycleProfile())
            {
                ObservePlaybackState();
            }

            if (IsUnscaledTimeProfile() && playbackFrame < 3)
            {
                Debug.Log(
                    $"PARTICLE_UNSCALED_TIME_STEP:" +
                    $"frame={playbackFrame}; " +
                    $"timeScale={Time.timeScale:R}; " +
                    $"deltaTime={Time.deltaTime:R}; " +
                    $"unscaledDeltaTime={Time.unscaledDeltaTime:R}",
                    this);
            }

            // Batch mode has no regular GameView render, so a render-pass-driven
            // simulation would otherwise advance only on capture frames. Submit the
            // normal guarded simulation once per validation frame; Camera.Render then
            // only reads that state because the per-frame guard rejects a second step.
            if (gpuParticles != null)
            {
                CommandBuffer command = CommandBufferPool.Get("ParticleAB.SimulateFrame");
                gpuParticles.Simulate(command, captureCamera);
                Graphics.ExecuteCommandBuffer(command);
                CommandBufferPool.Release(command);
            }

            playbackFrame++;
            if (playbackFrame >= nextCaptureFrame)
            {
                float elapsed = (float)playbackFrame / fixedFrameRate;
                CaptureSnapshot(elapsed);
                captureIndex++;
                nextCaptureFrame = CaptureFrameForIndex(captureIndex);
            }

            if (playbackFrame >= finalCaptureFrame)
            {
                CompleteCapture();
            }
        }

        void ConfigureValidationProfile()
        {
            if (validationProfile == ParticleABValidationProfile.BaselineCone ||
                shuriken == null || gpuParticles == null)
            {
                return;
            }

            ResetBySpeedModules();
            ResetTextureSheetAnimation();

            if (IsPlaybackLifecycleProfile())
            {
                ConfigurePlaybackLifecycleProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.PrewarmPoint)
            {
                ConfigurePrewarmProfile();
                return;
            }

            if (IsShapeProfile())
            {
                ConfigureShapeProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.TextureSheetLifetimePoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.Lifetime);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.TextureSheetSpeedPoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.Speed);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.TextureSheetFPSPoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.FPS);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.TextureSheetSingleRowPoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.Lifetime);
                return;
            }

            if (validationProfile == ParticleABValidationProfile.RandomizedMainPoint)
            {
                ConfigureRandomizedMainProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartColorGradientPoint)
            {
                ConfigureStartColorProfile(
                    ParticleSystemGradientMode.Gradient);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartColorTwoGradientsPoint)
            {
                ConfigureStartColorProfile(
                    ParticleSystemGradientMode.TwoGradients);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartColorRandomColorPoint)
            {
                ConfigureStartColorProfile(
                    ParticleSystemGradientMode.RandomColor);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartLifetimeCurvePoint)
            {
                ConfigureStartLifetimeProfile(false);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartLifetimeTwoCurvesPoint)
            {
                ConfigureStartLifetimeProfile(true);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartRotationCurvePoint)
            {
                ConfigureStartRotationProfile(false);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartRotationTwoCurvesPoint)
            {
                ConfigureStartRotationProfile(true);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartSpeedCurvePoint)
            {
                ConfigureStartSpeedProfile(false);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartSpeedTwoCurvesPoint)
            {
                ConfigureStartSpeedProfile(true);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartSizeCurvePoint)
            {
                ConfigureStartSizeProfile(false);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StartSizeTwoCurvesPoint)
            {
                ConfigureStartSizeProfile(true);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.SizeSeparateAxesPoint)
            {
                ConfigureSizeSeparateAxesProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.RendererScreenSizeClampPoint)
            {
                ConfigureRendererScreenSizeClampProfile();
                return;
            }

            if (IsScalingModeProfile())
            {
                ConfigureScalingModeProfile(ScalingModeForProfile());
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.UnscaledTimePoint)
            {
                ConfigureUnscaledTimeProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.GravityModifierCurvePoint)
            {
                ConfigureGravityModifierProfile(false);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.GravityModifierTwoCurvesPoint)
            {
                ConfigureGravityModifierProfile(true);
                return;
            }

            if (validationProfile == ParticleABValidationProfile.EmissionBurstPoint)
            {
                ConfigureEmissionBurstProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.EmissionRateCurvePoint)
            {
                ConfigureEmissionRateCurveProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.EmissionRateDistancePoint)
            {
                ConfigureEmissionRateDistanceProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.VelocityOverLifetimePoint)
            {
                ConfigureVelocityOverLifetimeProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.VelocityOrbitalRadialPoint)
            {
                ConfigureVelocityOrbitalRadialProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.VelocitySpeedModifierPoint)
            {
                ConfigureVelocitySpeedModifierProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.LimitVelocityOverLifetimePoint)
            {
                ConfigureLimitVelocityOverLifetimeProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint)
            {
                ConfigureLimitVelocityOverLifetimeAxesProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.InheritVelocityInitialPoint)
            {
                ConfigureInheritVelocityProfile(
                    ParticleSystemInheritVelocityMode.Initial);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.InheritVelocityCurrentPoint)
            {
                ConfigureInheritVelocityProfile(
                    ParticleSystemInheritVelocityMode.Current);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.LifetimeByEmitterSpeedPoint)
            {
                ConfigureLifetimeByEmitterSpeedProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.RotationOverLifetimeCurvePoint)
            {
                ConfigureRotationOverLifetimeProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.RotationBySpeedCurvePoint)
            {
                ConfigureRotationBySpeedProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint)
            {
                ConfigureColorSizeOverLifetimeProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint)
            {
                ConfigureColorSizeBySpeedProfile();
                return;
            }

            var main = shuriken.main;
            main.maxParticles = 1000;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.startSize3D = false;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.startRotation3D = false;
            main.startRotation = 0f;
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = shuriken.shape;
            shape.enabled = false;

            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = false;
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = false;
            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = false;

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.randomized = false;
            force.x = ValidationForce.x;
            force.y = ValidationForce.y;
            force.z = ValidationForce.z;
            var velocityOverLifetime = shuriken.velocityOverLifetime;
            velocityOverLifetime.enabled = false;
            var limitVelocity = shuriken.limitVelocityOverLifetime;
            limitVelocity.enabled = false;
            var inheritVelocity = shuriken.inheritVelocity;
            inheritVelocity.enabled = false;
            var lifetimeByEmitterSpeed = shuriken.lifetimeByEmitterSpeed;
            lifetimeByEmitterSpeed.enabled = false;

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.emissionDuration = main.duration;
            gpuParticles.emissionLooping = main.loop;
            gpuParticles.SetEmissionStartDelayRange(0f, 0f);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.startLifetime = 5f;
            gpuParticles.startSpeed = 0f;
            gpuParticles.startSize = 1f;
            gpuParticles.startSize3D = false;
            gpuParticles.SetStartSizeYRange(1f, 1f);
            gpuParticles.startSizeYMode = ParticleSystemCurveMode.Constant;
            gpuParticles.startSizeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.startColor = Color.white;
            gpuParticles.startRotation = 0f;
            gpuParticles.rotationOverLifetime = 0f;
            gpuParticles.rotationOverLifetimeIntegralLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.gravityModifier = 0f;
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.colorOverLifetimeLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeOverLifetimeSeparateAxes = false;
            gpuParticles.sizeOverLifetimeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.sizeOverLifetimeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.shapeType = ShapeTypeGPU.Point;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.alignToDirection = false;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.forceOverLifetimeRandomized = false;

            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(force.x, force.y, force.z);
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;
            gpuParticles.velocityOverLifetimeEnabled = false;
            gpuParticles.velocityOverLifetimeSpeedModifierEnabled = false;
            gpuParticles.velocityOverLifetimeOrbitalEnabled = false;
            gpuParticles.velocityOverLifetimeLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultVelocityLUT();
            gpuParticles.velocityOverLifetimeOrbitalLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeOrbitalOffsetLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.limitVelocityOverLifetimeEnabled = false;
            gpuParticles.limitVelocityOverLifetimeSeparateAxes = false;
            gpuParticles.limitVelocityOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.limitVelocityOverLifetimeDampen = 0f;
            gpuParticles.limitVelocityMultiplyDragBySize = false;
            gpuParticles.limitVelocityMultiplyDragByVelocity = false;
            gpuParticles.limitVelocityOverLifetimeLUT =
                LimitVelocityLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.inheritVelocityEnabled = false;
            gpuParticles.inheritVelocityMode =
                ParticleSystemInheritVelocityMode.Initial;
            gpuParticles.inheritVelocityLUT = CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.lifetimeByEmitterSpeedEnabled = false;
            gpuParticles.SetLifetimeByEmitterSpeedRange(new Vector2(0f, 1f));
            gpuParticles.lifetimeByEmitterSpeedLUT =
                CurveLUTBuilder.GetDefaultUnitLUT();
        }

        void ConfigureRandomizedMainProfile()
        {
            var main = shuriken.main;
            main.maxParticles = 1000;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.red, Color.blue);
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = shuriken.shape;
            shape.enabled = false;

            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = false;
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = false;

            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = false;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

            var force = shuriken.forceOverLifetime;
            force.enabled = false;
            var velocityOverLifetime = shuriken.velocityOverLifetime;
            velocityOverLifetime.enabled = false;
            var inheritVelocity = shuriken.inheritVelocity;
            inheritVelocity.enabled = false;
            var lifetimeByEmitterSpeed = shuriken.lifetimeByEmitterSpeed;
            lifetimeByEmitterSpeed.enabled = false;

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.emissionDuration = main.duration;
            gpuParticles.emissionLooping = main.loop;
            gpuParticles.SetEmissionStartDelayRange(0f, 0f);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetStartLifetimeRange(3f, 5f);
            gpuParticles.SetStartSpeedRange(1f, 3f);
            gpuParticles.SetStartSizeRange(0.5f, 1.5f);
            gpuParticles.startSize3D = false;
            gpuParticles.SetStartSizeYRange(0.5f, 1.5f);
            gpuParticles.startSizeYMode = ParticleSystemCurveMode.TwoConstants;
            gpuParticles.startSizeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.SetStartColorRange(Color.red, Color.blue, true);
            gpuParticles.SetGravityModifierRange(0f, 0f);
            gpuParticles.SetStartRotationRange(-Mathf.PI, Mathf.PI);
            gpuParticles.SetRotationOverLifetimeRange(-1f, 1f);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(rotationOverLifetime.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.colorOverLifetimeLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeOverLifetimeSeparateAxes = false;
            gpuParticles.sizeOverLifetimeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.sizeOverLifetimeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.shapeType = ShapeTypeGPU.Point;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.alignToDirection = false;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.forceOverLifetimeEnabled = false;
            gpuParticles.forceOverLifetimeLUT = MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeEnabled = false;
            gpuParticles.velocityOverLifetimeSpeedModifierEnabled = false;
            gpuParticles.velocityOverLifetimeOrbitalEnabled = false;
            gpuParticles.velocityOverLifetimeLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultVelocityLUT();
            gpuParticles.velocityOverLifetimeOrbitalLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeOrbitalOffsetLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.inheritVelocityEnabled = false;
            gpuParticles.inheritVelocityLUT = CurveLUTBuilder.GetDefaultZeroLUT();
        }

        void ConfigureStartColorProfile(ParticleSystemGradientMode mode)
        {
            ConfigureEmissionPointBase(StartColorProfileDuration, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);

            var emission = shuriken.emission;
            emission.rateOverTime = 24f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.MinMaxGradient startColor;
            if (mode == ParticleSystemGradientMode.TwoGradients)
            {
                profileColorMinimumGradient = CreateGradient(
                    new Color(0.1f, 0.2f, 0f, 0.4f),
                    new Color(0.4f, 0.1f, 0.2f, 0.7f));
                profileColorMaximumGradient = CreateGradient(
                    new Color(0.6f, 0.7f, 0.8f, 0.8f),
                    new Color(1f, 0.9f, 0.6f, 1f));
                startColor = new ParticleSystem.MinMaxGradient(
                    profileColorMinimumGradient,
                    profileColorMaximumGradient);
            }
            else
            {
                profileColorMinimumGradient = CreateGradient(
                    Color.red,
                    Color.blue);
                profileColorMaximumGradient = profileColorMinimumGradient;
                startColor = new ParticleSystem.MinMaxGradient(
                    profileColorMaximumGradient);
                if (mode == ParticleSystemGradientMode.RandomColor)
                {
                    startColor.mode = ParticleSystemGradientMode.RandomColor;
                }
            }

            main.startColor = startColor;
            gpuParticles.SetStartColorRange(Color.white, Color.white, false);
            gpuParticles.startColorMode = mode;
            if (profileStartColorLUT != null)
            {
                Destroy(profileStartColorLUT);
            }
            profileStartColorLUT = GradientLUTBuilder.Build(
                startColor,
                assetName: "StartColor_Profile_LUT");
            gpuParticles.startColorLUT = profileStartColorLUT;
        }

        void ConfigureStartSpeedProfile(bool twoCurves)
        {
            ConfigureEmissionPointBase(StartSpeedProfileDuration, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);

            var emission = shuriken.emission;
            emission.rateOverTime = 24f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.MinMaxCurve startSpeed;
            if (twoCurves)
            {
                profileStartSpeedMinimumCurve = AnimationCurve.Linear(
                    0f, 1f,
                    1f, 3f);
                profileStartSpeedMaximumCurve = AnimationCurve.Linear(
                    0f, 5f,
                    1f, 9f);
                startSpeed = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartSpeedMinimumCurve,
                    profileStartSpeedMaximumCurve);
            }
            else
            {
                profileStartSpeedMaximumCurve = AnimationCurve.Linear(
                    0f, 1f,
                    1f, 5f);
                profileStartSpeedMinimumCurve = profileStartSpeedMaximumCurve;
                startSpeed = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartSpeedMaximumCurve);
            }

            main.startSpeed = startSpeed;
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.startSpeedMode = startSpeed.mode;
            if (profileStartSpeedLUT != null)
            {
                Destroy(profileStartSpeedLUT);
            }
            profileStartSpeedLUT = CurveLUTBuilder.BuildSigned(
                startSpeed,
                assetName: "StartSpeed_Profile_LUT");
            gpuParticles.startSpeedLUT = profileStartSpeedLUT;
        }

        void ConfigureStartLifetimeProfile(bool twoCurves)
        {
            ConfigureEmissionPointBase(StartLifetimeProfileDuration, true);

            var main = shuriken.main;
            main.startSpeed = 1f;
            gpuParticles.SetStartSpeedRange(1f, 1f);

            var emission = shuriken.emission;
            emission.rateOverTime = 24f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.MinMaxCurve startLifetime;
            if (twoCurves)
            {
                profileStartLifetimeMinimumCurve = AnimationCurve.Linear(
                    0f, 0.75f,
                    1f, 1.75f);
                profileStartLifetimeMaximumCurve = AnimationCurve.Linear(
                    0f, 1.75f,
                    1f, 2.75f);
                startLifetime = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartLifetimeMinimumCurve,
                    profileStartLifetimeMaximumCurve);
            }
            else
            {
                profileStartLifetimeMaximumCurve = AnimationCurve.Linear(
                    0f, 0.75f,
                    1f, 2.75f);
                profileStartLifetimeMinimumCurve =
                    profileStartLifetimeMaximumCurve;
                startLifetime = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartLifetimeMaximumCurve);
            }

            main.startLifetime = startLifetime;
            gpuParticles.SetStartLifetimeRange(1f, 1f);
            gpuParticles.startLifetimeMode = startLifetime.mode;
            if (profileStartLifetimeLUT != null)
            {
                Destroy(profileStartLifetimeLUT);
            }
            profileStartLifetimeLUT = CurveLUTBuilder.BuildHighPrecision(
                startLifetime,
                assetName: "StartLifetime_Profile_LUT");
            gpuParticles.startLifetimeLUT = profileStartLifetimeLUT;
        }

        void ConfigureStartRotationProfile(bool twoCurves)
        {
            ConfigureEmissionPointBase(StartRotationProfileDuration, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSize = 1.5f;
            Color translucentWhite = new Color(1f, 1f, 1f, 0.25f);
            main.startColor = translucentWhite;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSizeRange(1.5f, 1.5f);
            gpuParticles.SetStartColorRange(
                translucentWhite,
                translucentWhite,
                false);

            var emission = shuriken.emission;
            emission.rateOverTime = 24f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.MinMaxCurve startRotation;
            if (twoCurves)
            {
                profileStartRotationMinimumCurve = AnimationCurve.Linear(
                    0f, -1.2f,
                    1f, -0.2f);
                profileStartRotationMaximumCurve = AnimationCurve.Linear(
                    0f, 0.2f,
                    1f, 1.2f);
                startRotation = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartRotationMinimumCurve,
                    profileStartRotationMaximumCurve);
            }
            else
            {
                profileStartRotationMaximumCurve = AnimationCurve.Linear(
                    0f, -1f,
                    1f, 1f);
                profileStartRotationMinimumCurve =
                    profileStartRotationMaximumCurve;
                startRotation = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartRotationMaximumCurve);
            }

            main.startRotation = startRotation;
            gpuParticles.SetStartRotationRange(0f, 0f);
            gpuParticles.startRotationMode = startRotation.mode;
            if (profileStartRotationLUT != null)
            {
                Destroy(profileStartRotationLUT);
            }
            profileStartRotationLUT = CurveLUTBuilder.BuildSigned(
                startRotation,
                assetName: "StartRotation_Profile_LUT");
            gpuParticles.startRotationLUT = profileStartRotationLUT;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.pivot = new Vector3(0.35f, 0.15f, 0f);
            }
            gpuParticles.pivot = new Vector2(0.35f, 0.15f);
        }

        void ConfigureStartSizeProfile(bool twoCurves)
        {
            ConfigureEmissionPointBase(StartSizeProfileDuration, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 1f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(1f, 1f);

            var emission = shuriken.emission;
            emission.rateOverTime = 24f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.MinMaxCurve startSize;
            if (twoCurves)
            {
                profileStartSizeMinimumCurve = AnimationCurve.Linear(
                    0f, 0.25f,
                    1f, 0.75f);
                profileStartSizeMaximumCurve = AnimationCurve.Linear(
                    0f, 1.25f,
                    1f, 2.25f);
                startSize = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartSizeMinimumCurve,
                    profileStartSizeMaximumCurve);
            }
            else
            {
                profileStartSizeMaximumCurve = AnimationCurve.Linear(
                    0f, 0.25f,
                    1f, 1.25f);
                profileStartSizeMinimumCurve = profileStartSizeMaximumCurve;
                startSize = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileStartSizeMaximumCurve);
            }

            main.startSize = startSize;
            gpuParticles.SetStartSizeRange(0f, 0f);
            gpuParticles.startSizeMode = startSize.mode;
            if (profileStartSizeLUT != null)
            {
                Destroy(profileStartSizeLUT);
            }
            profileStartSizeLUT = CurveLUTBuilder.Build(
                startSize,
                assetName: "StartSize_Profile_LUT");
            gpuParticles.startSizeLUT = profileStartSizeLUT;
        }

        void ConfigureSizeSeparateAxesProfile()
        {
            ConfigureEmissionPointBase(3.2f, false);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = 2f;
            main.startSizeY = 0.75f;
            main.startSizeZ = 1.25f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.startSize3D = true;
            gpuParticles.SetStartSizeRange(2f, 2f);
            gpuParticles.SetStartSizeYRange(0.75f, 0.75f);
            gpuParticles.startSizeMode = ParticleSystemCurveMode.Constant;
            gpuParticles.startSizeYMode = ParticleSystemCurveMode.Constant;
            gpuParticles.startSizeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.startSizeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(bursts);
            gpuParticles.emissionLooping = false;

            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = true;
            sizeOverLifetime.x = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));
            sizeOverLifetime.y = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1.5f, 1f, 0.5f));
            sizeOverLifetime.z = 1f;

            var sizeBySpeed = shuriken.sizeBySpeed;
            sizeBySpeed.enabled = true;
            sizeBySpeed.separateAxes = true;
            sizeBySpeed.range = new Vector2(0f, 1f);
            sizeBySpeed.x = 1.25f;
            sizeBySpeed.y = 0.8f;
            sizeBySpeed.z = 1f;

            if (profileSizeLUT != null) Destroy(profileSizeLUT);
            if (profileSizeYLUT != null) Destroy(profileSizeYLUT);
            if (profileSizeBySpeedXLUT != null)
            {
                Destroy(profileSizeBySpeedXLUT);
            }
            if (profileSizeBySpeedYLUT != null)
            {
                Destroy(profileSizeBySpeedYLUT);
            }
            profileSizeLUT = CurveLUTBuilder.Build(sizeOverLifetime.x);
            profileSizeYLUT = CurveLUTBuilder.Build(sizeOverLifetime.y);
            profileSizeBySpeedXLUT = CurveLUTBuilder.Build(sizeBySpeed.x);
            profileSizeBySpeedYLUT = CurveLUTBuilder.Build(sizeBySpeed.y);

            gpuParticles.sizeOverLifetimeSeparateAxes = true;
            gpuParticles.sizeOverLifetimeLUT = profileSizeLUT;
            gpuParticles.sizeOverLifetimeYLUT = profileSizeYLUT;
            gpuParticles.sizeBySpeedEnabled = true;
            gpuParticles.sizeBySpeedSeparateAxes = true;
            gpuParticles.SetSizeBySpeedRange(sizeBySpeed.range);
            gpuParticles.sizeBySpeedLUT = profileSizeBySpeedXLUT;
            gpuParticles.sizeBySpeedYLUT = profileSizeBySpeedYLUT;
        }

        void ConfigureRendererScreenSizeClampProfile()
        {
            ConfigureEmissionPointBase(4.2f, false);

            var main = shuriken.main;
            main.startLifetime = 6f;
            main.startSpeed = 0f;
            main.startSize3D = false;
            main.startSize = 10f;
            main.startColor = (Color)RendererClampMarker;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            gpuParticles.SetStartLifetimeRange(6f, 6f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.startSize3D = false;
            gpuParticles.SetStartSizeRange(10f, 10f);
            gpuParticles.SetStartColorRange(
                RendererClampMarker,
                RendererClampMarker,
                false);
            gpuParticles.simulationSpace = SimulationSpace.Local;

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(bursts);
            gpuParticles.emissionLooping = false;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment = ParticleSystemRenderSpace.View;
                shurikenRenderer.minParticleSize = RendererClampMinimum;
                shurikenRenderer.maxParticleSize = RendererClampMaximum;
            }

            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.screenSpaceSizeClampEnabled = true;
            gpuParticles.minParticleSize = RendererClampMinimum;
            gpuParticles.maxParticleSize = RendererClampMaximum;

            // Keep the validation quad above the sample-scene ground while it
            // travels away from the camera.
            shurikenBasePositionWS += Vector3.up * 5f;
            gpuBasePositionWS += Vector3.up * 5f;
        }

        void ConfigureScalingModeProfile(ParticleSystemScalingMode mode)
        {
            ConfigureEmissionPointBase(2f, false);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 1f;
            main.startSize3D = false;
            main.startSize = 1f;
            main.startColor = (Color)ScalingModeMarker;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = mode;

            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(1f, 1f);
            gpuParticles.startSize3D = false;
            gpuParticles.SetStartSizeRange(1f, 1f);
            gpuParticles.SetStartColorRange(
                ScalingModeMarker,
                ScalingModeMarker,
                false);
            gpuParticles.simulationSpace = SimulationSpace.World;
            gpuParticles.scalingMode = mode;
            gpuParticles.scalingSource = shuriken.transform;

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(
                emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(bursts);
            gpuParticles.emissionLooping = false;

            var shape = shuriken.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0f;
            shape.arc = 0f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;
            shape.alignToDirection = false;

            gpuParticles.shapeType = ShapeTypeGPU.Circle;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Surface;
            gpuParticles.shapeCircleRadius = 1f;
            gpuParticles.shapeRadiusThickness = 0f;
            gpuParticles.shapeConeArcDeg = 0f;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment =
                    ParticleSystemRenderSpace.View;
                shurikenRenderer.minParticleSize = 0f;
                shurikenRenderer.maxParticleSize = 1f;
            }

            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.screenSpaceSizeClampEnabled = true;
            gpuParticles.minParticleSize = 0f;
            gpuParticles.maxParticleSize = 1f;

            Vector3 shurikenTargetPosition =
                shurikenBasePositionWS + Vector3.up * 5f;
            // Scaling validation renders each implementation independently at
            // exactly the same camera-space position, avoiding perspective bias.
            Vector3 gpuTargetPosition = shurikenTargetPosition;
            var hierarchyParent = new GameObject(
                $"ScalingMode_{mode}_Parent");
            hierarchyParent.transform.position = shurikenTargetPosition;
            hierarchyParent.transform.rotation = Quaternion.identity;
            hierarchyParent.transform.localScale = new Vector3(2f, 3f, 2f);

            shuriken.transform.SetParent(hierarchyParent.transform, false);
            shuriken.transform.localPosition = Vector3.zero;
            shuriken.transform.localRotation = Quaternion.identity;
            shuriken.transform.localScale = new Vector3(4f, 5f, 3f);

            // The converted GPU system is a child of the Shuriken object. Keep
            // its own scale neutral and use scalingSource for Local mode, while
            // placing both renderers at the same position for independent captures.
            gpuParticles.transform.localScale = Vector3.one;
            gpuParticles.transform.rotation = Quaternion.identity;
            gpuParticles.transform.position = gpuTargetPosition;

            shurikenBasePositionWS = shurikenTargetPosition;
            gpuBasePositionWS = gpuTargetPosition;
        }

        void ConfigureUnscaledTimeProfile()
        {
            ConfigureEmissionPointBase(3.4f, false);

            var main = shuriken.main;
            main.startLifetime = 120f;
            main.startSpeed = 0f;
            main.startSize = 1.5f;
            main.startColor = (Color)new Color32(31, 242, 242, 255);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            gpuParticles.SetStartLifetimeRange(120f, 120f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(1.5f, 1.5f);
            gpuParticles.SetStartColorRange(
                new Color32(31, 242, 242, 255),
                new Color32(31, 242, 242, 255),
                false);
            gpuParticles.simulationSpace = SimulationSpace.World;
            gpuParticles.useUnscaledTime = true;

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(bursts);
            gpuParticles.emissionLooping = false;
        }

        void ConfigureGravityModifierProfile(bool twoCurves)
        {
            ConfigureEmissionPointBase(
                GravityModifierProfileDuration,
                true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 24f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.MinMaxCurve gravityModifier;
            if (twoCurves)
            {
                profileGravityMinimumCurve = AnimationCurve.Linear(
                    0f, -1f,
                    1f, 0f);
                profileGravityMaximumCurve = AnimationCurve.Linear(
                    0f, 1f,
                    1f, 2f);
                gravityModifier = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileGravityMinimumCurve,
                    profileGravityMaximumCurve);
            }
            else
            {
                profileGravityMaximumCurve = AnimationCurve.Linear(
                    0f, 0f,
                    1f, 2f);
                profileGravityMinimumCurve = profileGravityMaximumCurve;
                gravityModifier = new ParticleSystem.MinMaxCurve(
                    1f,
                    profileGravityMaximumCurve);
            }

            main.gravityModifier = gravityModifier;
            gpuParticles.SetGravityModifierRange(0f, 0f);
            gpuParticles.gravityModifierMode = gravityModifier.mode;
            if (profileGravityModifierLUT != null)
            {
                Destroy(profileGravityModifierLUT);
            }
            profileGravityModifierLUT = CurveLUTBuilder.BuildSigned(
                gravityModifier,
                assetName: "GravityModifier_Profile_LUT");
            gpuParticles.gravityModifierLUT = profileGravityModifierLUT;
        }

        void ConfigureEmissionBurstProfile()
        {
            ConfigureEmissionPointBase(2f, true);

            var main = shuriken.main;
            main.startDelay = 0.1f;
            gpuParticles.SetEmissionStartDelayRange(0.1f, 0.1f);

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 5),
                new ParticleSystem.Burst(0.25f, 7, 7, 3, 0.5f),
                new ParticleSystem.Burst(1.75f, 4)
            });

            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionBursts(bursts);
        }

        void ConfigureEmissionRateCurveProfile()
        {
            ConfigureEmissionPointBase(2f, false);

            var emission = shuriken.emission;
            AnimationCurve rateCurve = AnimationCurve.Linear(0f, 10f, 1f, 30f);
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                1f, rateCurve, rateCurve);
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
        }

        void ConfigureEmissionRateDistanceProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 10f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
        }

        void ConfigureVelocityOverLifetimeProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());

            var velocity = shuriken.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(
                2f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            velocity.y = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f));
            velocity.z = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, -0.25f, 1f, -0.25f));
            velocity.orbitalX = 0f;
            velocity.orbitalY = 0f;
            velocity.orbitalZ = 0f;
            velocity.orbitalOffsetX = 0f;
            velocity.orbitalOffsetY = 0f;
            velocity.orbitalOffsetZ = 0f;
            velocity.radial = 0f;
            velocity.speedModifier = 1f;

            if (profileVelocityLUT != null) Destroy(profileVelocityLUT);
            profileVelocityLUT = MinMaxCurveVector3LUTBuilder.Build(
                velocity.x,
                velocity.y,
                velocity.z,
                velocity.speedModifier);
            gpuParticles.velocityOverLifetimeEnabled = true;
            gpuParticles.velocityOverLifetimeSpeedModifierEnabled = true;
            gpuParticles.velocityOverLifetimeOrbitalEnabled = false;
            gpuParticles.velocityOverLifetimeSpace = SimulationSpace.World;
            gpuParticles.velocityOverLifetimeLUT = profileVelocityLUT;
            gpuParticles.velocityOverLifetimeOrbitalLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeOrbitalOffsetLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
        }

        void ConfigureVelocityOrbitalRadialProfile()
        {
            ConfigureEmissionPointBase(4f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var velocity = shuriken.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0.35f;
            velocity.y = -0.15f;
            velocity.z = 0.1f;
            velocity.speedModifier = 1f;
            velocity.orbitalX = 0.2f;
            velocity.orbitalY = -0.1f;
            velocity.orbitalZ = 1f;
            velocity.orbitalOffsetX = -1.5f;
            velocity.orbitalOffsetY = 0.25f;
            velocity.orbitalOffsetZ = 0.15f;
            velocity.radial = 0.2f;

            if (profileVelocityLUT != null) Destroy(profileVelocityLUT);
            if (profileVelocityOrbitalLUT != null)
            {
                Destroy(profileVelocityOrbitalLUT);
            }
            if (profileVelocityOrbitalOffsetLUT != null)
            {
                Destroy(profileVelocityOrbitalOffsetLUT);
            }
            profileVelocityLUT = MinMaxCurveVector3LUTBuilder.Build(
                velocity.x,
                velocity.y,
                velocity.z,
                velocity.speedModifier);
            profileVelocityOrbitalLUT = MinMaxCurveVector3LUTBuilder.Build(
                velocity.orbitalX,
                velocity.orbitalY,
                velocity.orbitalZ,
                velocity.radial);
            profileVelocityOrbitalOffsetLUT =
                MinMaxCurveVector3LUTBuilder.Build(
                    velocity.orbitalOffsetX,
                    velocity.orbitalOffsetY,
                    velocity.orbitalOffsetZ);

            gpuParticles.velocityOverLifetimeEnabled = true;
            gpuParticles.velocityOverLifetimeSpeedModifierEnabled = true;
            gpuParticles.velocityOverLifetimeOrbitalEnabled = true;
            gpuParticles.velocityOverLifetimeSpace = SimulationSpace.World;
            gpuParticles.velocityOverLifetimeLUT = profileVelocityLUT;
            gpuParticles.velocityOverLifetimeOrbitalLUT =
                profileVelocityOrbitalLUT;
            gpuParticles.velocityOverLifetimeOrbitalOffsetLUT =
                profileVelocityOrbitalOffsetLUT;
        }

        void ConfigureVelocitySpeedModifierProfile()
        {
            ConfigureEmissionPointBase(VelocitySpeedModifierLifetime, true);

            var main = shuriken.main;
            main.startLifetime = VelocitySpeedModifierLifetime;
            main.startSpeed = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.SetStartLifetimeRange(
                VelocitySpeedModifierLifetime,
                VelocitySpeedModifierLifetime);
            gpuParticles.SetStartSpeedRange(4f, 4f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());

            var velocity = shuriken.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 2f;
            velocity.y = 0f;
            velocity.z = 0f;
            velocity.orbitalX = 0f;
            velocity.orbitalY = 0f;
            velocity.orbitalZ = 0f;
            velocity.orbitalOffsetX = 0f;
            velocity.orbitalOffsetY = 0f;
            velocity.orbitalOffsetZ = 0f;
            velocity.radial = 0f;
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));

            if (profileVelocityLUT != null) Destroy(profileVelocityLUT);
            profileVelocityLUT = MinMaxCurveVector3LUTBuilder.Build(
                velocity.x,
                velocity.y,
                velocity.z,
                velocity.speedModifier);
            gpuParticles.velocityOverLifetimeEnabled = true;
            gpuParticles.velocityOverLifetimeSpeedModifierEnabled = true;
            gpuParticles.velocityOverLifetimeOrbitalEnabled = false;
            gpuParticles.velocityOverLifetimeSpace = SimulationSpace.World;
            gpuParticles.velocityOverLifetimeLUT = profileVelocityLUT;
            gpuParticles.velocityOverLifetimeOrbitalLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeOrbitalOffsetLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
        }

        void ConfigureLimitVelocityOverLifetimeProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 10f;
            main.startSize = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(10f, 10f);
            gpuParticles.SetStartSizeRange(2f, 2f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var limit = shuriken.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = false;
            limit.space = ParticleSystemSimulationSpace.Local;
            limit.limit = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 6f, 1f, 3f));
            limit.dampen = 0.35f;
            limit.drag = 0.02f;
            limit.multiplyDragByParticleSize = true;
            limit.multiplyDragByParticleVelocity = true;

            gpuParticles.limitVelocityOverLifetimeEnabled = true;
            gpuParticles.limitVelocityOverLifetimeSeparateAxes = false;
            gpuParticles.limitVelocityOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.limitVelocityOverLifetimeDampen = limit.dampen;
            gpuParticles.limitVelocityMultiplyDragBySize = true;
            gpuParticles.limitVelocityMultiplyDragByVelocity = true;
            if (profileLimitVelocityLUT != null)
            {
                Destroy(profileLimitVelocityLUT);
            }
            profileLimitVelocityLUT = LimitVelocityLUTBuilder.Build(
                limit,
                assetName: "LimitVelocityOverLifetime_Profile_LUT");
            gpuParticles.limitVelocityOverLifetimeLUT =
                profileLimitVelocityLUT;
        }

        void ConfigureLimitVelocityOverLifetimeAxesProfile()
        {
            ConfigureEmissionPointBase(4f, true);

            Quaternion emitterRotation = Quaternion.Euler(17f, 31f, -12f);
            shuriken.transform.rotation = emitterRotation;
            gpuParticles.transform.rotation = emitterRotation;

            var main = shuriken.main;
            main.startLifetime = 3f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.SetStartLifetimeRange(3f, 3f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.randomized = false;
            force.x = LimitVelocityAxesAcceleration.x;
            force.y = LimitVelocityAxesAcceleration.y;
            force.z = LimitVelocityAxesAcceleration.z;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(
                force.x, force.y, force.z);
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.forceOverLifetimeRandomized = false;
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;

            var limit = shuriken.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = true;
            limit.space = ParticleSystemSimulationSpace.Local;
            limit.limitX = 2f;
            limit.limitY = 1f;
            limit.limitZ = 1.5f;
            limit.dampen = 0.6f;
            limit.drag = 0f;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            gpuParticles.limitVelocityOverLifetimeEnabled = true;
            gpuParticles.limitVelocityOverLifetimeSeparateAxes = true;
            gpuParticles.limitVelocityOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.limitVelocityOverLifetimeDampen = limit.dampen;
            gpuParticles.limitVelocityMultiplyDragBySize = false;
            gpuParticles.limitVelocityMultiplyDragByVelocity = false;
            if (profileLimitVelocityLUT != null)
            {
                Destroy(profileLimitVelocityLUT);
            }
            profileLimitVelocityLUT = LimitVelocityLUTBuilder.Build(
                limit,
                assetName: "LimitVelocityOverLifetimeAxes_Profile_LUT");
            gpuParticles.limitVelocityOverLifetimeLUT =
                profileLimitVelocityLUT;
        }

        void ConfigureInheritVelocityProfile(
            ParticleSystemInheritVelocityMode mode)
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.emitterVelocityMode =
                ParticleSystemEmitterVelocityMode.Transform;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var inherit = shuriken.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = mode;
            inherit.curve = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

            if (profileInheritVelocityLUT != null)
            {
                Destroy(profileInheritVelocityLUT);
            }
            profileInheritVelocityLUT = CurveLUTBuilder.BuildSigned(
                inherit.curve,
                assetName: "InheritVelocity_Profile_LUT");
            gpuParticles.inheritVelocityEnabled = true;
            gpuParticles.inheritVelocityMode = mode;
            gpuParticles.inheritVelocityLUT = profileInheritVelocityLUT;
        }

        void ConfigureLifetimeByEmitterSpeedProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = 1.25f;
            main.startRotation = RotationProfileStartRotation;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.emitterVelocityMode =
                ParticleSystemEmitterVelocityMode.Transform;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(1.25f, 1.25f);
            gpuParticles.SetStartRotationRange(
                RotationProfileStartRotation,
                RotationProfileStartRotation);
            gpuParticles.simulationSpace = SimulationSpace.Local;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var lifetime = shuriken.lifetimeByEmitterSpeed;
            lifetime.enabled = true;
            lifetime.range = new Vector2(0f, 2f);
            lifetime.curve = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));

            if (profileLifetimeByEmitterSpeedLUT != null)
            {
                Destroy(profileLifetimeByEmitterSpeedLUT);
            }
            profileLifetimeByEmitterSpeedLUT = CurveLUTBuilder.Build(
                lifetime.curve,
                assetName: "LifetimeByEmitterSpeed_Profile_LUT");
            gpuParticles.lifetimeByEmitterSpeedEnabled = true;
            gpuParticles.SetLifetimeByEmitterSpeedRange(lifetime.range);
            gpuParticles.lifetimeByEmitterSpeedLUT =
                profileLifetimeByEmitterSpeedLUT;

            var rotation = shuriken.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 2f));
            gpuParticles.SetRotationOverLifetimeRange(0f, 0f);
            if (profileRotationLUT != null)
            {
                Destroy(profileRotationLUT);
            }
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(rotation.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.pivot = new Vector3(0.35f, 0.15f, 0f);
            }
            gpuParticles.pivot = new Vector2(0.35f, 0.15f);
        }

        void ConfigureRotationOverLifetimeProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSize = 1.25f;
            main.startRotation = RotationProfileStartRotation;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSizeRange(1.25f, 1.25f);
            gpuParticles.SetStartRotationRange(
                RotationProfileStartRotation,
                RotationProfileStartRotation);

            var emission = shuriken.emission;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var rotation = shuriken.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 2f));

            gpuParticles.SetRotationOverLifetimeRange(0f, 0f);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(rotation.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.pivot = new Vector3(0.35f, 0.15f, 0f);
            }
            gpuParticles.pivot = new Vector2(0.35f, 0.15f);
        }

        void ConfigureRotationBySpeedProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSize = 1.25f;
            main.startRotation = RotationProfileStartRotation;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSizeRange(1.25f, 1.25f);
            gpuParticles.SetStartRotationRange(
                RotationProfileStartRotation,
                RotationProfileStartRotation);

            var emission = shuriken.emission;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.randomized = false;
            force.x = RotationBySpeedAcceleration.x;
            force.y = RotationBySpeedAcceleration.y;
            force.z = RotationBySpeedAcceleration.z;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(
                force.x, force.y, force.z);
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.forceOverLifetimeRandomized = false;
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;

            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = false;
            rotationOverLifetime.z = 0.5f;
            gpuParticles.SetRotationOverLifetimeRange(0.5f, 0.5f);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(
                rotationOverLifetime.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;

            var rotationBySpeed = shuriken.rotationBySpeed;
            rotationBySpeed.enabled = true;
            rotationBySpeed.separateAxes = false;
            rotationBySpeed.range = new Vector2(0f, 4f);
            rotationBySpeed.z = new ParticleSystem.MinMaxCurve(
                4f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            gpuParticles.rotationBySpeedEnabled = true;
            gpuParticles.SetRotationBySpeedRange(rotationBySpeed.range);
            if (profileRotationBySpeedLUT != null)
            {
                Destroy(profileRotationBySpeedLUT);
            }
            profileRotationBySpeedLUT = CurveLUTBuilder.BuildSigned(
                rotationBySpeed.z,
                assetName: "RotationBySpeed_Profile_LUT");
            gpuParticles.rotationBySpeedLUT = profileRotationBySpeedLUT;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.pivot = new Vector3(0.35f, 0.15f, 0f);
            }
            gpuParticles.pivot = new Vector2(0.35f, 0.15f);
        }

        void ConfigureColorSizeOverLifetimeProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);

            var emission = shuriken.emission;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            profileColorMinimumGradient = CreateGradient(
                new Color(0.2f, 0.1f, 0.3f, 0.8f),
                new Color(0.4f, 0.6f, 0.2f, 0.2f));
            profileColorMaximumGradient = CreateGradient(
                new Color(0.9f, 0.4f, 0.8f, 1f),
                new Color(0.8f, 1f, 0.5f, 0.6f));
            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                profileColorMinimumGradient,
                profileColorMaximumGradient);

            profileSizeMinimumCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
            profileSizeMaximumCurve = AnimationCurve.Linear(0f, 1.5f, 1f, 2f);
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = false;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                profileSizeMinimumCurve,
                profileSizeMaximumCurve);

            if (profileColorLUT != null) Destroy(profileColorLUT);
            if (profileSizeLUT != null) Destroy(profileSizeLUT);
            profileColorLUT = GradientLUTBuilder.Build(colorOverLifetime.color);
            profileSizeLUT = CurveLUTBuilder.Build(sizeOverLifetime.size);
            gpuParticles.colorOverLifetimeMode = colorOverLifetime.color.mode;
            gpuParticles.colorOverLifetimeLUT = profileColorLUT;
            gpuParticles.sizeOverLifetimeLUT = profileSizeLUT;
        }

        static Gradient CreateGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(end.a, 1f)
                });
            return gradient;
        }

        void ConfigureColorSizeBySpeedProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.randomized = false;
            force.x = ValidationForce.x;
            force.y = ValidationForce.y;
            force.z = ValidationForce.z;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(
                force.x, force.y, force.z);
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.World;
            gpuParticles.forceOverLifetimeRandomized = false;
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;

            profileColorMinimumGradient = CreateGradient(
                new Color(0.2f, 0f, 0.3f, 0.8f),
                new Color(0.4f, 1f, 0.2f, 0.2f));
            profileColorMaximumGradient = CreateGradient(
                new Color(0.8f, 0f, 0.7f, 0.8f),
                new Color(1f, 1f, 0.6f, 0.2f));
            Vector2 speedRange = new Vector2(0f, ValidationForce.magnitude * 2f);
            var colorBySpeed = shuriken.colorBySpeed;
            colorBySpeed.enabled = true;
            colorBySpeed.range = speedRange;
            colorBySpeed.color = new ParticleSystem.MinMaxGradient(
                profileColorMinimumGradient,
                profileColorMaximumGradient);

            profileSizeMinimumCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
            profileSizeMaximumCurve = AnimationCurve.Linear(0f, 1.5f, 1f, 2f);
            var sizeBySpeed = shuriken.sizeBySpeed;
            sizeBySpeed.enabled = true;
            sizeBySpeed.separateAxes = false;
            sizeBySpeed.range = speedRange;
            sizeBySpeed.size = new ParticleSystem.MinMaxCurve(
                1f,
                profileSizeMinimumCurve,
                profileSizeMaximumCurve);

            if (profileColorLUT != null) Destroy(profileColorLUT);
            if (profileSizeLUT != null) Destroy(profileSizeLUT);
            profileColorLUT = GradientLUTBuilder.Build(
                colorBySpeed.color,
                assetName: "ColorBySpeed_Profile_LUT");
            profileSizeLUT = CurveLUTBuilder.Build(
                sizeBySpeed.size,
                assetName: "SizeBySpeed_Profile_LUT");
            gpuParticles.colorBySpeedEnabled = true;
            gpuParticles.colorBySpeedMode = colorBySpeed.color.mode;
            gpuParticles.SetColorBySpeedRange(speedRange);
            gpuParticles.colorBySpeedLUT = profileColorLUT;
            gpuParticles.sizeBySpeedEnabled = true;
            gpuParticles.SetSizeBySpeedRange(speedRange);
            gpuParticles.sizeBySpeedLUT = profileSizeLUT;
        }

        void ConfigureTextureSheetAnimationProfile(
            ParticleSystemAnimationTimeMode timeMode)
        {
            const float lifetime = 4f;
            bool singleRow = validationProfile ==
                ParticleABValidationProfile.TextureSheetSingleRowPoint;
            ConfigureEmissionPointBase(lifetime, false);

            var main = shuriken.main;
            main.maxParticles = 1;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 3f;
            main.startColor = Color.white;
            main.gravityModifier = 0f;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            var burst = new ParticleSystem.Burst(0f, 1)
            {
                probability = 1f
            };
            emission.SetBursts(new[] { burst });

            gpuParticles.maxParticles = 1;
            gpuParticles.emissionEnabled = true;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(new[] { burst });
            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(3f, 3f);
            gpuParticles.SetStartColorRange(Color.white, Color.white, false);
            gpuParticles.startColorLUT = GradientLUTBuilder.GetDefaultWhiteLUT();

            var force = shuriken.forceOverLifetime;
            bool animateBySpeed = timeMode == ParticleSystemAnimationTimeMode.Speed;
            force.enabled = animateBySpeed;
            force.space = ParticleSystemSimulationSpace.Local;
            force.randomized = false;
            force.x = animateBySpeed ? 0.5f : 0f;
            force.y = 0f;
            force.z = 0f;
            gpuParticles.forceOverLifetimeEnabled = animateBySpeed;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.forceOverLifetimeRandomized = false;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = animateBySpeed
                ? MinMaxCurveVector3LUTBuilder.Build(force.x, force.y, force.z)
                : null;
            gpuParticles.forceOverLifetimeLUT = profileForceLUT != null
                ? profileForceLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();

            var textureSheet = shuriken.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Grid;
            textureSheet.numTilesX = 4;
            textureSheet.numTilesY = 2;
            textureSheet.animation = singleRow
                ? ParticleSystemAnimationType.SingleRow
                : ParticleSystemAnimationType.WholeSheet;
            textureSheet.timeMode = timeMode;
            textureSheet.rowMode = ParticleSystemAnimationRowMode.Custom;
            textureSheet.rowIndex = singleRow ? 1 : 0;
            textureSheet.cycleCount = timeMode ==
                ParticleSystemAnimationTimeMode.Lifetime ? 2 : 1;
            textureSheet.speedRange = new Vector2(0f, 2f);
            textureSheet.fps = 2f;
            textureSheet.uvChannelMask = UVChannelFlags.UV0;
            textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            textureSheet.startFrame = new ParticleSystem.MinMaxCurve(
                singleRow ? 0.25f : 0.125f);

            if (profileTextureSheetFrameLUT != null)
            {
                Destroy(profileTextureSheetFrameLUT);
            }
            if (profileTextureSheetStartLUT != null)
            {
                Destroy(profileTextureSheetStartLUT);
            }
            profileTextureSheetFrameLUT = CurveLUTBuilder.BuildSigned(
                textureSheet.frameOverTime,
                assetName: "TextureSheetFrame_Profile_LUT");
            profileTextureSheetStartLUT = CurveLUTBuilder.BuildSigned(
                textureSheet.startFrame,
                resolution: 2,
                assetName: "TextureSheetStart_Profile_LUT");
            gpuParticles.textureSheetAnimationEnabled = true;
            gpuParticles.textureSheetMode = ParticleSystemAnimationMode.Grid;
            gpuParticles.textureSheetAnimation = textureSheet.animation;
            gpuParticles.textureSheetTimeMode = timeMode;
            gpuParticles.textureSheetRowMode =
                ParticleSystemAnimationRowMode.Custom;
            gpuParticles.textureSheetUVChannelMask = UVChannelFlags.UV0;
            gpuParticles.textureSheetTilesX = 4;
            gpuParticles.textureSheetTilesY = 2;
            gpuParticles.textureSheetRowIndex = textureSheet.rowIndex;
            gpuParticles.textureSheetCycleCount = textureSheet.cycleCount;
            gpuParticles.textureSheetFps = textureSheet.fps;
            gpuParticles.SetTextureSheetSpeedRange(textureSheet.speedRange);
            gpuParticles.textureSheetFrameOverTimeLUT =
                profileTextureSheetFrameLUT;
            gpuParticles.textureSheetStartFrameLUT =
                profileTextureSheetStartLUT;

            if (profileTextureSheetAtlas != null)
            {
                Destroy(profileTextureSheetAtlas);
            }
            profileTextureSheetAtlas = CreateTextureSheetAtlas();
            gpuParticles.baseMap = profileTextureSheetAtlas;

            if (profileTextureSheetMaterial != null)
            {
                Destroy(profileTextureSheetMaterial);
            }
            Material sourceMaterial = shurikenRenderer != null
                ? shurikenRenderer.sharedMaterial
                : null;
            if (sourceMaterial == null)
            {
                Shader shader = Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "URP particle shader was not found for Texture Sheet validation.");
                }
                profileTextureSheetMaterial = new Material(shader);
            }
            else
            {
                profileTextureSheetMaterial = new Material(sourceMaterial);
            }
            profileTextureSheetMaterial.name = "TextureSheetAB_Profile_Material";
            profileTextureSheetMaterial.hideFlags = HideFlags.HideAndDontSave;
            if (profileTextureSheetMaterial.HasProperty("_BaseMap"))
            {
                profileTextureSheetMaterial.SetTexture(
                    "_BaseMap", profileTextureSheetAtlas);
            }
            if (profileTextureSheetMaterial.HasProperty("_MainTex"))
            {
                profileTextureSheetMaterial.SetTexture(
                    "_MainTex", profileTextureSheetAtlas);
            }
            if (profileTextureSheetMaterial.HasProperty("_BaseColor"))
            {
                profileTextureSheetMaterial.SetColor("_BaseColor", Color.white);
            }
            if (profileTextureSheetMaterial.HasProperty("_Color"))
            {
                profileTextureSheetMaterial.SetColor("_Color", Color.white);
            }
            if (shurikenRenderer != null)
            {
                shurikenRenderer.sharedMaterial = profileTextureSheetMaterial;
                shurikenRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment = ParticleSystemRenderSpace.View;
            }
            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.pivot = Vector2.zero;
        }

        static Texture2D CreateTextureSheetAtlas()
        {
            const int tileSize = 16;
            const int columns = 4;
            const int rows = 2;
            int width = columns * tileSize;
            int height = rows * tileSize;
            var pixels = new Color32[width * height];
            for (int frame = 0; frame < TextureSheetPalette.Length; frame++)
            {
                int column = frame % columns;
                int rowFromTop = frame / columns;
                int rowFromBottom = rows - 1 - rowFromTop;
                int startX = column * tileSize;
                int startY = rowFromBottom * tileSize;
                for (int y = 0; y < tileSize; y++)
                {
                    for (int x = 0; x < tileSize; x++)
                    {
                        pixels[(startY + y) * width + startX + x] =
                            TextureSheetPalette[frame];
                    }
                }
            }

            var atlas = new Texture2D(
                width, height, TextureFormat.RGBA32, false, false)
            {
                name = "TextureSheetAB_Profile_Atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            atlas.SetPixels32(pixels);
            atlas.Apply(false, false);
            return atlas;
        }

        void ConfigureShapeProfile()
        {
            ConfigureEmissionPointBase(2f, true);

            var main = shuriken.main;
            main.startLifetime = 2f;
            main.startSpeed = 1f;
            main.startSize = 0.3f;
            gpuParticles.SetStartLifetimeRange(2f, 2f);
            gpuParticles.SetStartSpeedRange(1f, 1f);
            gpuParticles.SetStartSizeRange(0.3f, 0.3f);

            var emission = shuriken.emission;
            emission.rateOverTime = 60f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var shape = shuriken.shape;
            shape.enabled = true;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;
            shape.radius = 2f;
            shape.radiusThickness = 1f;
            shape.arc = 360f;
            shape.randomDirectionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;
            shape.randomPositionAmount = 0f;
            shape.alignToDirection = true;

            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.shapeRadiusThickness = 1f;
            gpuParticles.shapeConeArcDeg = 360f;
            gpuParticles.alignToDirection = true;

            switch (validationProfile)
            {
                case ParticleABValidationProfile.ShapeSpherePoint:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    gpuParticles.shapeType = ShapeTypeGPU.Sphere;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpuParticles.shapeSphereRadius = 2f;
                    break;

                case ParticleABValidationProfile.ShapeCirclePoint:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    gpuParticles.shapeType = ShapeTypeGPU.Circle;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpuParticles.shapeCircleRadius = 2f;
                    break;

                case ParticleABValidationProfile.ShapeDonutPoint:
                    shape.shapeType = ParticleSystemShapeType.Donut;
                    shape.donutRadius = 0.5f;
                    gpuParticles.shapeType = ShapeTypeGPU.Donut;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpuParticles.shapeDonutRadius = 2f;
                    gpuParticles.shapeDonutThickness = 0.5f;
                    break;

                case ParticleABValidationProfile.ShapeEdgePoint:
                    shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                    shape.scale = new Vector3(3f, 1f, 1f);
                    gpuParticles.shapeType = ShapeTypeGPU.Edge;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Edge;
                    gpuParticles.shapeEdgeLength = 4f;
                    gpuParticles.shapeLocalScale = shape.scale;
                    break;

                case ParticleABValidationProfile.ShapeRectanglePoint:
                    shape.shapeType = ParticleSystemShapeType.Rectangle;
                    shape.scale = new Vector3(4f, 2f, 1f);
                    gpuParticles.shapeType = ShapeTypeGPU.Rectangle;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpuParticles.shapeRectangleSize = Vector2.one;
                    gpuParticles.shapeLocalScale = shape.scale;
                    break;

                case ParticleABValidationProfile.ShapeBoxEdgePoint:
                    shape.shapeType = ParticleSystemShapeType.BoxEdge;
                    shape.scale = new Vector3(4f, 2f, 1f);
                    gpuParticles.shapeType = ShapeTypeGPU.Box;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Edge;
                    gpuParticles.shapeBoxSize = Vector3.one;
                    gpuParticles.shapeLocalScale = shape.scale;
                    break;
            }
        }

        bool IsShapeProfile()
        {
            switch (validationProfile)
            {
                case ParticleABValidationProfile.ShapeSpherePoint:
                case ParticleABValidationProfile.ShapeCirclePoint:
                case ParticleABValidationProfile.ShapeDonutPoint:
                case ParticleABValidationProfile.ShapeEdgePoint:
                case ParticleABValidationProfile.ShapeRectanglePoint:
                case ParticleABValidationProfile.ShapeBoxEdgePoint:
                    return true;
                default:
                    return false;
            }
        }

        void ResetTextureSheetAnimation()
        {
            var textureSheet = shuriken.textureSheetAnimation;
            textureSheet.enabled = false;
            gpuParticles.textureSheetAnimationEnabled = false;
            gpuParticles.textureSheetMode = ParticleSystemAnimationMode.Grid;
            gpuParticles.textureSheetAnimation =
                ParticleSystemAnimationType.WholeSheet;
            gpuParticles.textureSheetTimeMode =
                ParticleSystemAnimationTimeMode.Lifetime;
            gpuParticles.textureSheetUVChannelMask = UVChannelFlags.UV0;
            gpuParticles.textureSheetTilesX = 1;
            gpuParticles.textureSheetTilesY = 1;
            gpuParticles.textureSheetCycleCount = 1;
            gpuParticles.textureSheetFrameOverTimeLUT =
                CurveLUTBuilder.GetDefaultLinear01LUT();
            gpuParticles.textureSheetStartFrameLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
        }

        bool IsTextureSheetProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.TextureSheetLifetimePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetSpeedPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetFPSPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetSingleRowPoint;
        }

        void ResetBySpeedModules()
        {
            var lifetimeByEmitterSpeed = shuriken.lifetimeByEmitterSpeed;
            lifetimeByEmitterSpeed.enabled = false;
            gpuParticles.lifetimeByEmitterSpeedEnabled = false;
            gpuParticles.SetLifetimeByEmitterSpeedRange(new Vector2(0f, 1f));
            gpuParticles.lifetimeByEmitterSpeedLUT =
                CurveLUTBuilder.GetDefaultUnitLUT();

            var colorBySpeed = shuriken.colorBySpeed;
            colorBySpeed.enabled = false;
            var sizeBySpeed = shuriken.sizeBySpeed;
            sizeBySpeed.enabled = false;

            gpuParticles.colorBySpeedEnabled = false;
            gpuParticles.colorBySpeedMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.SetColorBySpeedRange(new Vector2(0f, 1f));
            gpuParticles.colorBySpeedLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeBySpeedEnabled = false;
            gpuParticles.sizeBySpeedSeparateAxes = false;
            gpuParticles.SetSizeBySpeedRange(new Vector2(0f, 1f));
            gpuParticles.sizeBySpeedLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.sizeBySpeedYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            var rotationBySpeed = shuriken.rotationBySpeed;
            rotationBySpeed.enabled = false;
            gpuParticles.rotationBySpeedEnabled = false;
            gpuParticles.SetRotationBySpeedRange(new Vector2(0f, 1f));
            gpuParticles.rotationBySpeedLUT = CurveLUTBuilder.GetDefaultZeroLUT();
        }

        void ConfigurePrewarmProfile()
        {
            ConfigureEmissionPointBase(2f, true);

            var main = shuriken.main;
            main.prewarm = true;
            main.startLifetime = 1.25f;
            main.startSize = 0.5f;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;

            gpuParticles.SetStartLifetimeRange(1.25f, 1.25f);
            gpuParticles.SetStartSizeRange(0.5f, 0.5f);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.prewarm = true;

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.randomized = false;
            force.x = ValidationForce.x;
            force.y = ValidationForce.y;
            force.z = ValidationForce.z;

            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(
                force.x,
                force.y,
                force.z);
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.forceOverLifetimeRandomized = false;
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;
        }

        void UpdatePrewarmLifecycle()
        {
            if (!captureActive ||
                validationProfile != ParticleABValidationProfile.PrewarmPoint ||
                shuriken == null)
            {
                return;
            }

            if (playbackFrame == PrewarmRestartStopFrame)
            {
                shuriken.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            else if (playbackFrame == PrewarmRestartPlayFrame)
            {
                shuriken.Play(true);
            }
        }

        void ConfigurePlaybackLifecycleProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.playOnAwake = false;
            main.startLifetime = 1.25f;
            main.startSize = 0.5f;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;

            gpuParticles.playOnAwake = false;
            gpuParticles.SetStartLifetimeRange(1.25f, 1.25f);
            gpuParticles.SetStartSizeRange(0.5f, 0.5f);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);

            shuriken.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            gpuParticles.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        bool IsPlaybackLifecycleProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.PlaybackLifecyclePoint;
        }

        void UpdatePlaybackLifecycle()
        {
            if (!captureActive || !IsPlaybackLifecycleProfile() ||
                shuriken == null || gpuParticles == null)
            {
                return;
            }

            switch (playbackFrame)
            {
                case PlaybackPlayFrame:
                    shuriken.Play(true);
                    break;

                case PlaybackPauseFrame:
                    shuriken.Pause(true);
                    break;

                case PlaybackResumeFrame:
                    shuriken.Play(true);
                    break;

                case PlaybackStopEmittingFrame:
                    shuriken.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmitting);
                    break;

                case PlaybackReplayFrame:
                    shuriken.Play(true);
                    break;

                case PlaybackClearFrame:
                    shuriken.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                    break;
            }
        }

        void ObservePlaybackState()
        {
            bool statesMatch =
                shuriken.isPlaying == gpuParticles.isPlaying &&
                shuriken.isPaused == gpuParticles.isPaused &&
                shuriken.isStopped == gpuParticles.isStopped &&
                shuriken.isEmitting == gpuParticles.isEmitting;
            if (!statesMatch)
            {
                playbackStateMismatchCount++;
            }

            if (playbackFrame < PlaybackPlayFrame)
            {
                if (shuriken.isStopped && gpuParticles.isStopped)
                {
                    playbackTransitionMask |= 1 << 0;
                }
            }
            else if (playbackFrame < PlaybackPauseFrame)
            {
                if (shuriken.isPlaying && gpuParticles.isPlaying &&
                    shuriken.isEmitting && gpuParticles.isEmitting)
                {
                    playbackTransitionMask |= 1 << 1;
                }
            }
            else if (playbackFrame < PlaybackResumeFrame)
            {
                if (shuriken.isPaused && gpuParticles.isPaused)
                {
                    playbackTransitionMask |= 1 << 2;
                }
            }
            else if (playbackFrame < PlaybackStopEmittingFrame)
            {
                if (shuriken.isPlaying && gpuParticles.isPlaying)
                {
                    playbackTransitionMask |= 1 << 3;
                }
            }
            else if (playbackFrame < PlaybackDrainExpectedFrame)
            {
                if (shuriken.isPlaying && gpuParticles.isPlaying &&
                    !shuriken.isStopped && !gpuParticles.isStopped &&
                    !shuriken.isEmitting && !gpuParticles.isEmitting)
                {
                    playbackTransitionMask |= 1 << 4;
                }
            }
            else if (playbackFrame < PlaybackReplayFrame)
            {
                if (shuriken.isStopped && gpuParticles.isStopped)
                {
                    playbackTransitionMask |= 1 << 5;
                }
            }
            else if (playbackFrame < PlaybackClearFrame)
            {
                if (shuriken.isPlaying && gpuParticles.isPlaying &&
                    shuriken.isEmitting && gpuParticles.isEmitting)
                {
                    playbackTransitionMask |= 1 << 6;
                }
            }
            else if (shuriken.isStopped && gpuParticles.isStopped)
            {
                playbackTransitionMask |= 1 << 7;
            }
        }

        void ConfigureEmissionPointBase(float duration, bool looping)
        {
            shuriken.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = shuriken.main;
            main.maxParticles = 1000;
            main.duration = duration;
            main.loop = looping;
            main.startDelay = 0f;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.startSize3D = false;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.startRotation3D = false;
            main.startRotation = 0f;
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;
            main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.emitterVelocityMode =
                ParticleSystemEmitterVelocityMode.Transform;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverDistance = 0f;
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);

            var shape = shuriken.shape;
            shape.enabled = false;
            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = false;
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = false;
            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = false;
            var force = shuriken.forceOverLifetime;
            force.enabled = false;
            var velocityOverLifetime = shuriken.velocityOverLifetime;
            velocityOverLifetime.enabled = false;
            var limitVelocity = shuriken.limitVelocityOverLifetime;
            limitVelocity.enabled = false;
            var inheritVelocity = shuriken.inheritVelocity;
            inheritVelocity.enabled = false;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.minParticleSize = 0f;
                shurikenRenderer.maxParticleSize = 0.5f;
            }

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.emissionDuration = main.duration;
            gpuParticles.emissionLooping = main.loop;
            gpuParticles.emissionRandomSeed = randomSeed == 0 ? 1u : randomSeed;
            gpuParticles.SetEmissionStartDelayRange(0f, 0f);
            gpuParticles.SetStartLifetimeRange(5f, 5f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(1f, 1f);
            gpuParticles.startSize3D = false;
            gpuParticles.SetStartSizeYRange(1f, 1f);
            gpuParticles.startSizeYMode = ParticleSystemCurveMode.Constant;
            gpuParticles.startSizeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.SetStartColorRange(Color.white, Color.white, false);
            gpuParticles.startColorLUT =
                GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.SetGravityModifierRange(0f, 0f);
            gpuParticles.SetStartRotationRange(0f, 0f);
            gpuParticles.SetRotationOverLifetimeRange(0f, 0f);
            gpuParticles.rotationOverLifetimeIntegralLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.useUnscaledTime = false;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.scalingMode = ParticleSystemScalingMode.Hierarchy;
            gpuParticles.scalingSource = shuriken.transform;
            gpuParticles.colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.colorOverLifetimeLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeOverLifetimeSeparateAxes = false;
            gpuParticles.sizeOverLifetimeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.sizeOverLifetimeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.shapeType = ShapeTypeGPU.Point;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.alignToDirection = false;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.forceOverLifetimeEnabled = false;
            gpuParticles.forceOverLifetimeLUT = MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeEnabled = false;
            gpuParticles.velocityOverLifetimeSpeedModifierEnabled = false;
            gpuParticles.velocityOverLifetimeOrbitalEnabled = false;
            gpuParticles.velocityOverLifetimeLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultVelocityLUT();
            gpuParticles.velocityOverLifetimeOrbitalLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeOrbitalOffsetLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.limitVelocityOverLifetimeEnabled = false;
            gpuParticles.limitVelocityOverLifetimeSeparateAxes = false;
            gpuParticles.limitVelocityOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.limitVelocityOverLifetimeDampen = 0f;
            gpuParticles.limitVelocityMultiplyDragBySize = false;
            gpuParticles.limitVelocityMultiplyDragByVelocity = false;
            gpuParticles.limitVelocityOverLifetimeLUT =
                LimitVelocityLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.inheritVelocityEnabled = false;
            gpuParticles.inheritVelocityMode =
                ParticleSystemInheritVelocityMode.Initial;
            gpuParticles.inheritVelocityLUT = CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.lifetimeByEmitterSpeedEnabled = false;
            gpuParticles.SetLifetimeByEmitterSpeedRange(new Vector2(0f, 1f));
            gpuParticles.lifetimeByEmitterSpeedLUT =
                CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.screenSpaceSizeClampEnabled = false;
            gpuParticles.minParticleSize = 0f;
            gpuParticles.maxParticleSize = 0.5f;
        }

        public void RestartPlayback()
        {
            Time.timeScale = IsUnscaledTimeProfile()
                ? UnscaledTimeScale
                : 1f;
            MoveValidationEmitters(0f);

            if (shuriken != null)
            {
                shuriken.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                shuriken.Clear(true);
                shuriken.useAutoRandomSeed = false;
                shuriken.randomSeed = randomSeed == 0 ? 1u : randomSeed;
                var main = shuriken.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                if (!IsPlaybackLifecycleProfile())
                {
                    shuriken.Play(true);
                }
            }

            if (gpuParticles != null)
            {
                gpuParticles.ResetSimulation();
                if (IsPlaybackLifecycleProfile())
                {
                    gpuParticles.Stop(
                        false,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                else
                {
                    gpuParticles.Play(false);
                }
            }

            if (captureActive)
            {
                playbackFrame = 0;
                captureIndex = 1;
                nextCaptureFrame = CaptureFrameForIndex(captureIndex);
            }
        }

        void MoveValidationEmitters(float elapsed)
        {
            elapsed = Mathf.Max(0f, elapsed);
            if (IsRendererScreenSizeClampProfile())
            {
                float travel = RendererClampTravel * Mathf.Clamp01(
                    elapsed / Mathf.Max(0.1f, captureDuration));
                Vector3 travelOffset = Vector3.forward * travel;
                if (shuriken != null)
                {
                    shuriken.transform.position =
                        shurikenBasePositionWS + travelOffset;
                }
                if (gpuParticles != null)
                {
                    gpuParticles.transform.position =
                        gpuBasePositionWS + travelOffset;
                }
                return;
            }

            float position;
            if (validationProfile ==
                    ParticleABValidationProfile.InheritVelocityInitialPoint ||
                validationProfile ==
                    ParticleABValidationProfile.InheritVelocityCurrentPoint ||
                validationProfile ==
                    ParticleABValidationProfile.LifetimeByEmitterSpeedPoint)
            {
                // Move at +2 units/s, reverse at -1 unit/s, then stop. Initial
                // particles retain their birth velocity; Current particles follow
                // both changes, making the two modes independently observable.
                position = elapsed <= 1f
                    ? 2f * elapsed
                    : elapsed <= 2f
                        ? 3f - elapsed
                        : 1f;
            }
            else
            {
                position = elapsed;
            }

            Vector3 offset = Vector3.right * position;
            if (shuriken != null)
            {
                shuriken.transform.position = shurikenBasePositionWS + offset;
            }
            if (gpuParticles != null)
            {
                gpuParticles.transform.position = gpuBasePositionWS + offset;
            }
        }

        bool UsesMovingEmitterProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.EmissionRateDistancePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.InheritVelocityInitialPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.InheritVelocityCurrentPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.LifetimeByEmitterSpeedPoint ||
                   IsRendererScreenSizeClampProfile();
        }

        bool IsInheritVelocityProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.InheritVelocityInitialPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.InheritVelocityCurrentPoint;
        }

        bool IsLifetimeByEmitterSpeedProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.LifetimeByEmitterSpeedPoint;
        }

        bool IsStartColorProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.StartColorGradientPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.StartColorTwoGradientsPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.StartColorRandomColorPoint;
        }

        bool IsStartLifetimeProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.StartLifetimeCurvePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.StartLifetimeTwoCurvesPoint;
        }

        bool IsStartRotationProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.StartRotationCurvePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.StartRotationTwoCurvesPoint;
        }

        bool IsStartSpeedProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.StartSpeedCurvePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.StartSpeedTwoCurvesPoint;
        }

        bool IsStartSizeProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.StartSizeCurvePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.StartSizeTwoCurvesPoint;
        }

        bool IsGravityModifierProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.GravityModifierCurvePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.GravityModifierTwoCurvesPoint;
        }

        bool IsRendererScreenSizeClampProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.RendererScreenSizeClampPoint;
        }

        bool IsUnscaledTimeProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.UnscaledTimePoint;
        }

        bool IsScalingModeProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.ScalingHierarchyPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.ScalingLocalPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.ScalingShapePoint;
        }

        ParticleSystemScalingMode ScalingModeForProfile()
        {
            switch (validationProfile)
            {
                case ParticleABValidationProfile.ScalingLocalPoint:
                    return ParticleSystemScalingMode.Local;
                case ParticleABValidationProfile.ScalingShapePoint:
                    return ParticleSystemScalingMode.Shape;
                default:
                    return ParticleSystemScalingMode.Hierarchy;
            }
        }

        public void SetDisplayMode(ParticleABDisplayMode mode)
        {
            displayMode = mode;
            ApplyDisplayMode();
        }

        public void BeginCapture()
        {
            if (captureActive) return;

            string root = Path.IsPathRooted(outputFolder)
                ? outputFolder
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
            string sessionName = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            sessionFolder = Path.Combine(root, sessionName);
            Directory.CreateDirectory(sessionFolder);

            metricsPath = Path.Combine(sessionFolder, "metrics.csv");
            File.WriteAllText(metricsPath,
                "capture,time,frame,shuriken_count,gpu_count,count_delta," +
                "shuriken_mean_x,shuriken_mean_y,shuriken_mean_z," +
                "gpu_mean_x,gpu_mean_y,gpu_mean_z," +
                "shuriken_mean_vel_x,shuriken_mean_vel_y,shuriken_mean_vel_z," +
                "gpu_mean_vel_x,gpu_mean_vel_y,gpu_mean_vel_z," +
                "shuriken_mean_speed,gpu_mean_speed," +
                "shuriken_mean_size,gpu_mean_size," +
                "shuriken_mean_age,gpu_mean_age," +
                "shuriken_mean_lifetime,gpu_mean_lifetime\n");

            EnsureCameraCaptureTarget();
            playbackFrame = 0;
            captureIndex = 1;
            nextCaptureFrame = CaptureFrameForIndex(captureIndex);
            finalCaptureFrame = Mathf.Max(1, Mathf.RoundToInt(captureDuration * fixedFrameRate));
            maximumCountDelta = 0;
            maximumMeanAgeError = 0f;
            maximumMeanLifetimeError = 0f;
            maximumMeanStartRotationError = 0f;
            maximumMeanSpeedError = 0f;
            maximumMeanSizeError = 0f;
            maximumMeanSizeYError = 0f;
            maximumMeanVelocityError = 0f;
            maximumMeanPositionError = 0f;
            maximumShurikenConeError = 0f;
            maximumGPUConeError = 0f;
            maximumForceKinematicsError = 0f;
            maximumVelocitySpeedModifierKinematicsError = 0f;
            maximumShurikenColorBoundsError = 0f;
            maximumGPUColorBoundsError = 0f;
            maximumShurikenStartLifetimeBoundsError = 0f;
            maximumGPUStartLifetimeBoundsError = 0f;
            maximumShurikenStartSpeedBoundsError = 0f;
            maximumGPUStartSpeedBoundsError = 0f;
            maximumShurikenStartSizeBoundsError = 0f;
            maximumGPUStartSizeBoundsError = 0f;
            maximumShurikenGravityIntegralBoundsError = 0f;
            maximumGPUGravityIntegralBoundsError = 0f;
            maximumShurikenSizeBoundsError = 0f;
            maximumGPUSizeBoundsError = 0f;
            maximumShurikenRotationError = 0f;
            maximumGPURotationError = 0f;
            maximumShurikenLimitVelocityError = 0f;
            maximumGPULimitVelocityError = 0f;
            maximumShurikenShapeDirectionError = 0f;
            maximumGPUShapeDirectionError = 0f;
            maximumShurikenShapeGeometryError = 0f;
            maximumGPUShapeGeometryError = 0f;
            maximumShurikenParticleCount = 0;
            maximumGPUParticleCount = 0;
            textureSheetComparableSamples = 0;
            textureSheetFrameMismatches = 0;
            textureSheetClassificationFailures = 0;
            maximumTextureSheetFrameDelta = 0;
            shurikenTextureSheetFrameMask = 0;
            gpuTextureSheetFrameMask = 0;
            maximumScreenSizePixelError = 0f;
            screenSizeClassificationFailures = 0;
            currentShurikenScreenSizePixels = -1;
            maximumScalingWidthPixelError = 0f;
            maximumScalingHeightPixelError = 0f;
            maximumScalingSpawnOffsetPixelError = 0f;
            scalingBoundsClassificationFailures = 0;
            hasCurrentShurikenScalingBounds = false;
            currentShurikenScalingBounds = default;
            currentShurikenScalingOffsetPixels = Vector2.zero;
            maximumShurikenMeanAge = 0f;
            maximumGPUMeanAge = 0f;
            playbackStateMismatchCount = 0;
            playbackEmptyViolationCount = 0;
            playbackTransitionMask = 0;
            playbackDrainObserved = false;
            playbackClearObserved = false;
            maximumShurikenStoppedParticleCount = 0;
            maximumGPUStoppedParticleCount = 0;
            prewarmFirstSnapshotObserved = false;
            prewarmFirstShurikenCount = 0;
            prewarmFirstGPUCount = 0;
            prewarmFirstShurikenMeanAge = 0f;
            prewarmFirstGPUMeanAge = 0f;
            prewarmRestartSnapshotObserved = false;
            prewarmRestartShurikenCount = 0;
            prewarmRestartGPUCount = 0;
            prewarmRestartShurikenMeanAge = 0f;
            prewarmRestartGPUMeanAge = 0f;
            shurikenLifetimeRange.Reset();
            gpuLifetimeRange.Reset();
            shurikenSpeedRange.Reset();
            gpuSpeedRange.Reset();
            shurikenSizeRange.Reset();
            gpuSizeRange.Reset();
            shurikenColorRedRange.Reset();
            gpuColorRedRange.Reset();
            shurikenStartRotationRange.Reset();
            gpuStartRotationRange.Reset();
            shurikenDistancePositionRange.Reset();
            gpuDistancePositionRange.Reset();
            shurikenShapeSpawnXRange.Reset();
            shurikenShapeSpawnYRange.Reset();
            shurikenShapeSpawnZRange.Reset();
            gpuShapeSpawnXRange.Reset();
            gpuShapeSpawnYRange.Reset();
            gpuShapeSpawnZRange.Reset();
            shurikenLifetimeColorBlendRange.Reset();
            gpuLifetimeColorBlendRange.Reset();
            shurikenLifetimeSizeBlendRange.Reset();
            gpuLifetimeSizeBlendRange.Reset();
            shurikenSpeedColorBlendRange.Reset();
            gpuSpeedColorBlendRange.Reset();
            shurikenSpeedSizeBlendRange.Reset();
            gpuSpeedSizeBlendRange.Reset();
            shurikenStartColorBlendRange.Reset();
            gpuStartColorBlendRange.Reset();
            shurikenStartLifetimeBlendRange.Reset();
            gpuStartLifetimeBlendRange.Reset();
            shurikenStartSpeedBlendRange.Reset();
            gpuStartSpeedBlendRange.Reset();
            shurikenStartSizeBlendRange.Reset();
            gpuStartSizeBlendRange.Reset();
            shurikenGravityBlendRange.Reset();
            gpuGravityBlendRange.Reset();
            shurikenStartRotationBlendRange.Reset();
            gpuStartRotationBlendRange.Reset();
            shurikenScreenSizePixelRange.Reset();
            gpuScreenSizePixelRange.Reset();
            shurikenScalingWidthPixelRange.Reset();
            gpuScalingWidthPixelRange.Reset();
            shurikenScalingHeightPixelRange.Reset();
            gpuScalingHeightPixelRange.Reset();
            shurikenScalingSpawnOffsetPixelRange.Reset();
            gpuScalingSpawnOffsetPixelRange.Reset();
            shurikenScalingBirthXRange.Reset();
            gpuScalingBirthXRange.Reset();
            shurikenPausedMeanAgeRange.Reset();
            gpuPausedMeanAgeRange.Reset();
            captureActive = true;

            Debug.Log($"Particle A/B RT capture started: {sessionFolder}", this);
        }

        int CaptureFrameForIndex(int index)
        {
            return Mathf.Max(1, Mathf.RoundToInt(index * fixedFrameRate / Mathf.Max(0.1f, captureFrequency)));
        }

        void CaptureSnapshot(float elapsed)
        {
            if (captureCamera == null || gpuParticles == null) return;

            EnsureCameraCaptureTarget();
            string prefix = $"{captureIndex:D4}-t{elapsed:F3}";
            ParticleABDisplayMode previousMode = displayMode;
            CaptureCameraImage(prefix + "-both.png", ParticleABDisplayMode.Both);
            int shurikenTextureSheetFrame = CaptureCameraImage(
                prefix + "-shuriken.png",
                ParticleABDisplayMode.ShurikenOnly);
            int gpuTextureSheetFrame = CaptureCameraImage(
                prefix + "-gpu.png",
                ParticleABDisplayMode.GPUOnly);
            SetDisplayMode(previousMode);
            ObserveTextureSheetFrames(
                shurikenTextureSheetFrame,
                gpuTextureSheetFrame);

            Texture2D posLife = ReadRenderTexture(gpuParticles.CurrentPositionLifetimeTexture, TextureFormat.RGBAFloat, true);
            Texture2D velSize = ReadRenderTexture(gpuParticles.CurrentVelocitySizeTexture, TextureFormat.RGBAFloat, true);
            Texture2D colors = ReadRenderTexture(gpuParticles.CurrentColorTexture, TextureFormat.RGBAFloat, true);
            Texture2D rotationPhases = ReadRenderTexture(
                gpuParticles.CurrentRotationPhaseTexture,
                TextureFormat.RGBAFloat,
                true);
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-poslife.exr"),
                posLife.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-velsize.exr"),
                velSize.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-color.exr"),
                colors.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-rotation.exr"),
                rotationPhases.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));

            AppendMetrics(
                elapsed,
                posLife.GetPixels(),
                velSize.GetPixels(),
                colors.GetPixels(),
                rotationPhases.GetPixels());
            WriteParticleState(
                prefix,
                posLife.GetPixels(),
                velSize.GetPixels(),
                rotationPhases.GetPixels());
            Destroy(posLife);
            Destroy(velSize);
            Destroy(colors);
            Destroy(rotationPhases);
        }

        int CaptureCameraImage(string fileName, ParticleABDisplayMode mode)
        {
            SetDisplayMode(mode);
            RenderTexture previousTarget = captureCamera.targetTexture;
            captureCamera.targetTexture = cameraCaptureRT;
            captureCamera.Render();
            captureCamera.targetTexture = previousTarget;

            Texture2D cameraImage = ReadRenderTexture(cameraCaptureRT, TextureFormat.RGBA32, false);
            File.WriteAllBytes(Path.Combine(sessionFolder, fileName), cameraImage.EncodeToPNG());
            int textureSheetFrame = IsTextureSheetProfile()
                ? ClassifyTextureSheetFrame(cameraImage)
                : -1;
            int screenSizePixels = IsRendererScreenSizeClampProfile() &&
                                   mode != ParticleABDisplayMode.Both
                ? ClassifyRendererScreenSize(cameraImage)
                : -1;
            ObserveRendererScreenSize(mode, screenSizePixels);
            MarkerPixelBounds scalingBounds = IsScalingModeProfile() &&
                                               mode != ParticleABDisplayMode.Both
                ? ClassifyMarkerBounds(cameraImage)
                : default;
            ObserveScalingModeBounds(mode, scalingBounds);
            Destroy(cameraImage);
            return textureSheetFrame;
        }

        static int ClassifyRendererScreenSize(Texture2D image)
        {
            MarkerPixelBounds bounds = ClassifyMarkerBounds(image);
            return bounds.Valid
                ? Mathf.RoundToInt(Mathf.Max(bounds.Width, bounds.Height))
                : -1;
        }

        static MarkerPixelBounds ClassifyMarkerBounds(Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            int minimumX = image.width;
            int minimumY = image.height;
            int maximumX = -1;
            int maximumY = -1;
            int markerPixels = 0;
            for (int y = 0; y < image.height; y++)
            {
                int row = y * image.width;
                for (int x = 0; x < image.width; x++)
                {
                    Color32 pixel = pixels[row + x];
                    if (pixel.r < 150 || pixel.b < 150 || pixel.g > 140 ||
                        pixel.r - pixel.g < 50 || pixel.b - pixel.g < 50)
                    {
                        continue;
                    }

                    markerPixels++;
                    minimumX = Mathf.Min(minimumX, x);
                    minimumY = Mathf.Min(minimumY, y);
                    maximumX = Mathf.Max(maximumX, x);
                    maximumY = Mathf.Max(maximumY, y);
                }
            }

            if (markerPixels < 32 || maximumX < minimumX || maximumY < minimumY)
            {
                return default;
            }
            return new MarkerPixelBounds
            {
                Valid = true,
                MinimumX = minimumX,
                MinimumY = minimumY,
                MaximumX = maximumX,
                MaximumY = maximumY
            };
        }

        void ObserveRendererScreenSize(
            ParticleABDisplayMode mode,
            int screenSizePixels)
        {
            if (!IsRendererScreenSizeClampProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                currentShurikenScreenSizePixels = screenSizePixels;
                if (screenSizePixels < 0)
                {
                    screenSizeClassificationFailures++;
                    return;
                }
                shurikenScreenSizePixelRange.Observe(screenSizePixels);
                return;
            }

            if (screenSizePixels < 0)
            {
                screenSizeClassificationFailures++;
            }
            else
            {
                gpuScreenSizePixelRange.Observe(screenSizePixels);
            }

            if (screenSizePixels < 0 ||
                currentShurikenScreenSizePixels < 0)
            {
                if (currentShurikenScreenSizePixels < 0)
                {
                    screenSizeClassificationFailures++;
                }
            }
            else
            {
                maximumScreenSizePixelError = Mathf.Max(
                    maximumScreenSizePixelError,
                    Mathf.Abs(
                        currentShurikenScreenSizePixels -
                        screenSizePixels));
            }
            currentShurikenScreenSizePixels = -1;
        }

        void ObserveScalingModeBounds(
            ParticleABDisplayMode mode,
            MarkerPixelBounds bounds)
        {
            if (!IsScalingModeProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            Transform emitter = mode == ParticleABDisplayMode.ShurikenOnly
                ? shuriken != null ? shuriken.transform : null
                : gpuParticles != null ? gpuParticles.transform : null;
            Vector2 spawnOffset = Vector2.zero;
            if (bounds.Valid && emitter != null)
            {
                Vector3 emitterViewport = captureCamera.WorldToViewportPoint(
                    emitter.position);
                spawnOffset = bounds.Center -
                              new Vector2(
                                  emitterViewport.x * captureWidth,
                                  emitterViewport.y * captureHeight);
            }

            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenScalingBounds = bounds.Valid;
                currentShurikenScalingBounds = bounds;
                currentShurikenScalingOffsetPixels = spawnOffset;
                if (!bounds.Valid)
                {
                    scalingBoundsClassificationFailures++;
                    return;
                }

                shurikenScalingWidthPixelRange.Observe(bounds.Width);
                shurikenScalingHeightPixelRange.Observe(bounds.Height);
                shurikenScalingSpawnOffsetPixelRange.Observe(
                    spawnOffset.magnitude);
                return;
            }

            if (!bounds.Valid)
            {
                scalingBoundsClassificationFailures++;
            }
            else
            {
                gpuScalingWidthPixelRange.Observe(bounds.Width);
                gpuScalingHeightPixelRange.Observe(bounds.Height);
                gpuScalingSpawnOffsetPixelRange.Observe(
                    spawnOffset.magnitude);
            }

            if (bounds.Valid && hasCurrentShurikenScalingBounds)
            {
                maximumScalingWidthPixelError = Mathf.Max(
                    maximumScalingWidthPixelError,
                    Mathf.Abs(
                        currentShurikenScalingBounds.Width - bounds.Width));
                maximumScalingHeightPixelError = Mathf.Max(
                    maximumScalingHeightPixelError,
                    Mathf.Abs(
                        currentShurikenScalingBounds.Height - bounds.Height));
                maximumScalingSpawnOffsetPixelError = Mathf.Max(
                    maximumScalingSpawnOffsetPixelError,
                    Vector2.Distance(
                        currentShurikenScalingOffsetPixels,
                        spawnOffset));
            }

            hasCurrentShurikenScalingBounds = false;
            currentShurikenScalingBounds = default;
            currentShurikenScalingOffsetPixels = Vector2.zero;
        }

        static int ClassifyTextureSheetFrame(Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            var counts = new int[TextureSheetPalette.Length];
            const int maximumDistanceSquared = 55 * 55 * 3;
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                Color32 pixel = pixels[pixelIndex];
                int closestFrame = -1;
                int closestDistance = int.MaxValue;
                for (int frame = 0; frame < TextureSheetPalette.Length; frame++)
                {
                    Color32 target = TextureSheetPalette[frame];
                    int red = pixel.r - target.r;
                    int green = pixel.g - target.g;
                    int blue = pixel.b - target.b;
                    int distance = red * red + green * green + blue * blue;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestFrame = frame;
                    }
                }

                if (closestDistance <= maximumDistanceSquared)
                {
                    counts[closestFrame]++;
                }
            }

            int dominantFrame = -1;
            int dominantCount = 0;
            for (int frame = 0; frame < counts.Length; frame++)
            {
                if (counts[frame] <= dominantCount) continue;
                dominantCount = counts[frame];
                dominantFrame = frame;
            }
            return dominantCount >= 32 ? dominantFrame : -1;
        }

        void ObserveTextureSheetFrames(int shurikenFrame, int gpuFrame)
        {
            if (!IsTextureSheetProfile()) return;
            if (shurikenFrame < 0 || gpuFrame < 0)
            {
                textureSheetClassificationFailures++;
                return;
            }

            textureSheetComparableSamples++;
            shurikenTextureSheetFrameMask |= 1 << shurikenFrame;
            gpuTextureSheetFrameMask |= 1 << gpuFrame;
            int frameDelta = Mathf.Abs(shurikenFrame - gpuFrame);
            frameDelta = Mathf.Min(frameDelta, TextureSheetPalette.Length - frameDelta);
            maximumTextureSheetFrameDelta = Mathf.Max(
                maximumTextureSheetFrameDelta, frameDelta);
            if (shurikenFrame != gpuFrame)
            {
                textureSheetFrameMismatches++;
            }
        }

        void WriteParticleState(
            string prefix,
            Color[] gpuPosLife,
            Color[] gpuVelSize,
            Color[] gpuModuleStates)
        {
            var state = new StringBuilder(4096);
            state.Append("system,index,pos_x,pos_y,pos_z,vel_x,vel_y,vel_z,age,lifetime\n");

            if (shuriken != null)
            {
                int required = Mathf.Max(1, shuriken.main.maxParticles);
                if (shurikenParticles == null || shurikenParticles.Length < required)
                {
                    shurikenParticles = new ParticleSystem.Particle[required];
                }

                int count = shuriken.GetParticles(shurikenParticles);
                for (int i = 0; i < count; i++)
                {
                    ParticleSystem.Particle particle = shurikenParticles[i];
                    AppendStateRow(state, "shuriken", i, particle.position, particle.totalVelocity,
                        particle.startLifetime - particle.remainingLifetime, particle.remainingLifetime);
                }
            }

            int pixelCount = Mathf.Min(
                Mathf.Min(gpuPosLife.Length, gpuVelSize.Length),
                gpuModuleStates.Length);
            for (int i = 0; i < pixelCount; i++)
            {
                Color positionLife = gpuPosLife[i];
                if (positionLife.a <= 0f) continue;

                Color velocitySize = gpuVelSize[i];
                Vector3 birthEmitterVelocityWS = ModuleBirthEmitterVelocity(
                    gpuModuleStates[i]);
                gpuParticles.ResolveParticleLifetimeState(
                    i,
                    positionLife.a,
                    birthEmitterVelocityWS,
                    out _,
                    out float particleAge,
                    out float remainingLifetime);
                AppendStateRow(state, "gpu", i,
                    new Vector3(positionLife.r, positionLife.g, positionLife.b),
                    new Vector3(velocitySize.r, velocitySize.g, velocitySize.b),
                    particleAge,
                    remainingLifetime);
            }

            File.WriteAllText(Path.Combine(sessionFolder, prefix + "-state.csv"), state.ToString());
        }

        static void AppendStateRow(StringBuilder state, string systemName, int index, Vector3 position,
            Vector3 velocity, float age, float remainingLifetime)
        {
            state.Append(systemName).Append(',');
            Append(state, index);
            Append(state, position.x);
            Append(state, position.y);
            Append(state, position.z);
            Append(state, velocity.x);
            Append(state, velocity.y);
            Append(state, velocity.z);
            Append(state, age);
            state.Append(remainingLifetime.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }

        void AppendMetrics(
            float elapsed,
            Color[] gpuPosLife,
            Color[] gpuVelSize,
            Color[] gpuColors,
            Color[] gpuRotationPhases)
        {
            int shurikenCount = 0;
            Vector3 shurikenPositionSum = Vector3.zero;
            Vector3 shurikenVelocitySum = Vector3.zero;
            float shurikenSpeedSum = 0f;
            float shurikenSizeSum = 0f;
            float shurikenSizeYSum = 0f;
            float shurikenAgeSum = 0f;
            float shurikenLifetimeSum = 0f;
            float shurikenStartRotationSum = 0f;

            if (shuriken != null)
            {
                int required = Mathf.Max(1, shuriken.main.maxParticles);
                if (shurikenParticles == null || shurikenParticles.Length < required)
                {
                    shurikenParticles = new ParticleSystem.Particle[required];
                }

                shurikenCount = shuriken.GetParticles(shurikenParticles);
                for (int i = 0; i < shurikenCount; i++)
                {
                    ParticleSystem.Particle particle = shurikenParticles[i];
                    Vector3 shurikenVelocity = particle.totalVelocity;
                    shurikenPositionSum += particle.position;
                    shurikenVelocitySum += shurikenVelocity;
                    shurikenSpeedSum += shurikenVelocity.magnitude;
                    Vector3 shurikenSize = particle.GetCurrentSize3D(shuriken);
                    shurikenSizeSum += shurikenSize.x;
                    shurikenSizeYSum += shurikenSize.y;
                    float age = particle.startLifetime - particle.remainingLifetime;
                    shurikenAgeSum += age;
                    shurikenLifetimeSum += particle.startLifetime;
                    if (IsShapeProfile())
                    {
                        ObserveShapeSample(
                            false,
                            particle.position - shurikenVelocity * age,
                            shurikenVelocity);
                    }
                    if (IsInheritVelocityProfile())
                    {
                        shurikenSpeedRange.Observe(shurikenVelocity.magnitude);
                    }
                    if (IsScalingModeProfile())
                    {
                        shurikenSpeedRange.Observe(
                            shurikenVelocity.magnitude);
                        Vector3 birthPosition =
                            particle.position - shurikenVelocity * age;
                        shurikenScalingBirthXRange.Observe(
                            birthPosition.x - shurikenBasePositionWS.x);
                    }
                    if (IsLifetimeByEmitterSpeedProfile())
                    {
                        shurikenLifetimeRange.Observe(particle.startLifetime);
                    }
                    if (validationProfile == ParticleABValidationProfile.RandomizedMainPoint)
                    {
                        Color startColor = particle.startColor;
                        shurikenLifetimeRange.Observe(particle.startLifetime);
                        shurikenSpeedRange.Observe(shurikenVelocity.magnitude);
                        shurikenSizeRange.Observe(particle.startSize);
                        shurikenColorRedRange.Observe(startColor.r);
                    }
                    if (IsStartColorProfile())
                    {
                        ObserveStartColorSample(
                            false,
                            particle.startColor,
                            elapsed,
                            age);
                    }
                    if (IsStartLifetimeProfile())
                    {
                        ObserveStartLifetimeSample(
                            false,
                            particle.startLifetime,
                            elapsed,
                            age);
                    }
                    if (IsStartSpeedProfile())
                    {
                        ObserveStartSpeedSample(
                            false,
                            shurikenVelocity.magnitude,
                            elapsed,
                            age);
                    }
                    if (IsStartSizeProfile())
                    {
                        ObserveStartSizeSample(
                            false,
                            particle.startSize,
                            elapsed,
                            age);
                    }
                    if (IsGravityModifierProfile())
                    {
                        ObserveGravityModifierSample(
                            false,
                            shurikenVelocity,
                            elapsed,
                            age);
                    }
                    if (IsStartRotationProfile())
                    {
                        float rotation = particle.rotation * Mathf.Deg2Rad;
                        shurikenStartRotationSum += rotation;
                        ObserveStartRotationSample(
                            false,
                            rotation,
                            elapsed,
                            age);
                    }
                    if (validationProfile == ParticleABValidationProfile.BaselineCone)
                    {
                        maximumShurikenConeError = Mathf.Max(maximumShurikenConeError,
                            ConeRelationError(particle.position, shurikenVelocity, age));
                    }
                    else if (validationProfile ==
                                 ParticleABValidationProfile.ForceOverLifetimePoint ||
                             validationProfile ==
                                 ParticleABValidationProfile.PrewarmPoint)
                    {
                        maximumForceKinematicsError = Mathf.Max(maximumForceKinematicsError,
                            (shurikenVelocity - ValidationForce * age).magnitude);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.VelocitySpeedModifierPoint)
                    {
                        Vector3 displacement =
                            particle.position - shurikenBasePositionWS;
                        maximumVelocitySpeedModifierKinematicsError = Mathf.Max(
                            maximumVelocitySpeedModifierKinematicsError,
                            Mathf.Max(
                                (shurikenVelocity -
                                 VelocitySpeedModifierRawVelocity).magnitude,
                                (displacement -
                                 VelocitySpeedModifierExpectedDisplacement(age)).magnitude));
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.LimitVelocityOverLifetimePoint)
                    {
                        float speed = shurikenVelocity.magnitude;
                        shurikenSpeedRange.Observe(speed);
                        maximumShurikenLimitVelocityError = Mathf.Max(
                            maximumShurikenLimitVelocityError,
                            Mathf.Abs(speed - LimitVelocityProfileExpectedSpeed(age)));
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint)
                    {
                        Vector3 expectedVelocity =
                            LimitVelocityAxesExpectedVelocity(
                                age, shuriken.transform);
                        maximumShurikenLimitVelocityError = Mathf.Max(
                            maximumShurikenLimitVelocityError,
                            (shurikenVelocity - expectedVelocity).magnitude);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.LifetimeByEmitterSpeedPoint)
                    {
                        float expectedRotation = RotationProfileExpectedRadians(
                            particle.startLifetime,
                            age);
                        float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                            expectedRotation * Mathf.Rad2Deg,
                            particle.rotation)) * Mathf.Deg2Rad;
                        maximumShurikenRotationError = Mathf.Max(
                            maximumShurikenRotationError,
                            rotationError);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.RotationOverLifetimeCurvePoint)
                    {
                        float expectedRotation = RotationProfileExpectedRadians(
                            particle.startLifetime, age);
                        float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                            expectedRotation * Mathf.Rad2Deg,
                            particle.rotation)) * Mathf.Deg2Rad;
                        maximumShurikenRotationError = Mathf.Max(
                            maximumShurikenRotationError, rotationError);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.RotationBySpeedCurvePoint)
                    {
                        float expectedRotation = RotationBySpeedProfileExpectedRadians(age);
                        float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                            expectedRotation * Mathf.Rad2Deg,
                            particle.rotation)) * Mathf.Deg2Rad;
                        maximumShurikenRotationError = Mathf.Max(
                            maximumShurikenRotationError, rotationError);
                        maximumForceKinematicsError = Mathf.Max(
                            maximumForceKinematicsError,
                            (shurikenVelocity - RotationBySpeedAcceleration * age).magnitude);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.EmissionRateDistancePoint)
                    {
                        shurikenDistancePositionRange.Observe(
                            particle.position.x - shurikenBasePositionWS.x);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint)
                    {
                        float normalizedAge = Mathf.Clamp01(age / particle.startLifetime);
                        Color minimumColor = profileColorMinimumGradient.Evaluate(normalizedAge);
                        Color maximumColor = profileColorMaximumGradient.Evaluate(normalizedAge);
                        Color currentColor = particle.GetCurrentColor(shuriken);
                        maximumShurikenColorBoundsError = Mathf.Max(
                            maximumShurikenColorBoundsError,
                            ColorBoundsError(currentColor, minimumColor, maximumColor));
                        ObserveBlendFactor(
                            currentColor.r,
                            minimumColor.r,
                            maximumColor.r,
                            ref shurikenLifetimeColorBlendRange);

                        float minimumSize = profileSizeMinimumCurve.Evaluate(normalizedAge);
                        float maximumSize = profileSizeMaximumCurve.Evaluate(normalizedAge);
                        float currentSize = particle.GetCurrentSize(shuriken);
                        maximumShurikenSizeBoundsError = Mathf.Max(
                            maximumShurikenSizeBoundsError,
                            RangeViolation(currentSize, minimumSize, maximumSize));
                        ObserveBlendFactor(
                            currentSize,
                            minimumSize,
                            maximumSize,
                            ref shurikenLifetimeSizeBlendRange);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint)
                    {
                        float speed = shurikenVelocity.magnitude;
                        float speedPosition = Mathf.InverseLerp(
                            gpuParticles.colorBySpeedRange.x,
                            gpuParticles.colorBySpeedRange.y,
                            speed);
                        Color minimumColor = profileColorMinimumGradient.Evaluate(speedPosition);
                        Color maximumColor = profileColorMaximumGradient.Evaluate(speedPosition);
                        Color currentColor = particle.GetCurrentColor(shuriken);
                        maximumShurikenColorBoundsError = Mathf.Max(
                            maximumShurikenColorBoundsError,
                            ColorBoundsError(currentColor, minimumColor, maximumColor));
                        ObserveBlendFactor(
                            currentColor.r,
                            minimumColor.r,
                            maximumColor.r,
                            ref shurikenSpeedColorBlendRange);

                        float minimumSize = profileSizeMinimumCurve.Evaluate(speedPosition);
                        float maximumSize = profileSizeMaximumCurve.Evaluate(speedPosition);
                        float currentSize = particle.GetCurrentSize(shuriken);
                        maximumShurikenSizeBoundsError = Mathf.Max(
                            maximumShurikenSizeBoundsError,
                            RangeViolation(currentSize, minimumSize, maximumSize));
                        ObserveBlendFactor(
                            currentSize,
                            minimumSize,
                            maximumSize,
                            ref shurikenSpeedSizeBlendRange);
                        maximumForceKinematicsError = Mathf.Max(
                            maximumForceKinematicsError,
                            (shurikenVelocity - ValidationForce * age).magnitude);
                    }
                }
            }

            int gpuCount = 0;
            Vector3 gpuPositionSum = Vector3.zero;
            Vector3 gpuVelocitySum = Vector3.zero;
            float gpuSpeedSum = 0f;
            float gpuSizeSum = 0f;
            float gpuSizeYSum = 0f;
            float gpuAgeSum = 0f;
            float gpuLifetimeSum = 0f;
            float gpuStartRotationSum = 0f;
            int pixelCount = Mathf.Min(
                Mathf.Min(
                    Mathf.Min(gpuPosLife.Length, gpuVelSize.Length),
                    gpuColors.Length),
                gpuRotationPhases.Length);
            for (int i = 0; i < pixelCount; i++)
            {
                Color positionLife = gpuPosLife[i];
                if (positionLife.a <= 0f) continue;

                Color velocitySize = gpuVelSize[i];
                gpuCount++;
                gpuPositionSum += new Vector3(positionLife.r, positionLife.g, positionLife.b);
                Vector3 gpuVelocity = new Vector3(velocitySize.r, velocitySize.g, velocitySize.b);
                gpuVelocitySum += gpuVelocity;
                gpuSpeedSum += gpuVelocity.magnitude;
                gpuSizeSum += velocitySize.a;
                Vector3 birthEmitterVelocityWS = ModuleBirthEmitterVelocity(
                    gpuRotationPhases[i]);
                Vector2 gpuBillboardSize = gpuParticles.ResolveParticleBillboardSize(
                    i,
                    positionLife.a,
                    velocitySize.a,
                    gpuVelocity.magnitude,
                    birthEmitterVelocityWS);
                gpuSizeYSum += gpuBillboardSize.y;
                gpuParticles.ResolveParticleLifetimeState(
                    i,
                    positionLife.a,
                    birthEmitterVelocityWS,
                    out float particleStartLifetime,
                    out float age,
                    out _);
                gpuAgeSum += age;
                gpuLifetimeSum += particleStartLifetime;
                if (IsShapeProfile())
                {
                    ObserveShapeSample(
                        true,
                        new Vector3(
                            positionLife.r,
                            positionLife.g,
                            positionLife.b) - gpuVelocity * age,
                        gpuVelocity);
                }
                if (IsInheritVelocityProfile())
                {
                    gpuSpeedRange.Observe(gpuVelocity.magnitude);
                }
                if (IsScalingModeProfile())
                {
                    gpuSpeedRange.Observe(gpuVelocity.magnitude);
                    Vector3 birthPosition = new Vector3(
                        positionLife.r,
                        positionLife.g,
                        positionLife.b) - gpuVelocity * age;
                    gpuScalingBirthXRange.Observe(
                        birthPosition.x - gpuBasePositionWS.x);
                }
                if (IsLifetimeByEmitterSpeedProfile())
                {
                    gpuLifetimeRange.Observe(particleStartLifetime);
                }
                if (validationProfile == ParticleABValidationProfile.RandomizedMainPoint)
                {
                    gpuLifetimeRange.Observe(particleStartLifetime);
                    gpuSpeedRange.Observe(gpuVelocity.magnitude);
                    gpuSizeRange.Observe(velocitySize.a);
                    gpuColorRedRange.Observe(gpuColors[i].r);
                }
                if (IsStartColorProfile())
                {
                    ObserveStartColorSample(
                        true,
                        gpuColors[i],
                        elapsed,
                        age);
                }
                if (IsStartLifetimeProfile())
                {
                    ObserveStartLifetimeSample(
                        true,
                        particleStartLifetime,
                        elapsed,
                        age);
                }
                if (IsStartSpeedProfile())
                {
                    ObserveStartSpeedSample(
                        true,
                        gpuVelocity.magnitude,
                        elapsed,
                        age);
                }
                if (IsStartSizeProfile())
                {
                    ObserveStartSizeSample(
                        true,
                        velocitySize.a,
                        elapsed,
                        age);
                }
                if (IsGravityModifierProfile())
                {
                    ObserveGravityModifierSample(
                        true,
                        gpuVelocity,
                        elapsed,
                        age);
                }
                if (IsStartRotationProfile())
                {
                    float rotation =
                        gpuParticles.ResolveParticleRotationRadians(
                            i,
                            positionLife.a,
                            gpuRotationPhases[i].r,
                            birthEmitterVelocityWS);
                    gpuStartRotationSum += rotation;
                    ObserveStartRotationSample(
                        true,
                        rotation,
                        elapsed,
                        age);
                }
                if (validationProfile == ParticleABValidationProfile.BaselineCone)
                {
                    maximumGPUConeError = Mathf.Max(maximumGPUConeError,
                        ConeRelationError(
                            new Vector3(positionLife.r, positionLife.g, positionLife.b),
                            gpuVelocity, age));
                }
                else if (validationProfile ==
                             ParticleABValidationProfile.ForceOverLifetimePoint ||
                         validationProfile ==
                             ParticleABValidationProfile.PrewarmPoint)
                {
                    maximumForceKinematicsError = Mathf.Max(maximumForceKinematicsError,
                        (gpuVelocity - ValidationForce * age).magnitude);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.VelocitySpeedModifierPoint)
                {
                    Vector3 displacement =
                        new Vector3(positionLife.r, positionLife.g, positionLife.b) -
                        gpuBasePositionWS;
                    maximumVelocitySpeedModifierKinematicsError = Mathf.Max(
                        maximumVelocitySpeedModifierKinematicsError,
                        Mathf.Max(
                            (gpuVelocity -
                             VelocitySpeedModifierRawVelocity).magnitude,
                            (displacement -
                             VelocitySpeedModifierExpectedDisplacement(age)).magnitude));
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.LimitVelocityOverLifetimePoint)
                {
                    float speed = gpuVelocity.magnitude;
                    gpuSpeedRange.Observe(speed);
                    maximumGPULimitVelocityError = Mathf.Max(
                        maximumGPULimitVelocityError,
                        Mathf.Abs(speed - LimitVelocityProfileExpectedSpeed(age)));
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint)
                {
                    Vector3 expectedVelocity =
                        LimitVelocityAxesExpectedVelocity(
                            age, gpuParticles.transform);
                    maximumGPULimitVelocityError = Mathf.Max(
                        maximumGPULimitVelocityError,
                        (gpuVelocity - expectedVelocity).magnitude);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.LifetimeByEmitterSpeedPoint)
                {
                    float expectedRotation = RotationProfileExpectedRadians(
                        particleStartLifetime,
                        age);
                    float actualRotation =
                        gpuParticles.ResolveParticleRotationRadians(
                            i,
                            positionLife.a,
                            gpuRotationPhases[i].r,
                            birthEmitterVelocityWS);
                    float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                        expectedRotation * Mathf.Rad2Deg,
                        actualRotation * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    maximumGPURotationError = Mathf.Max(
                        maximumGPURotationError,
                        rotationError);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.RotationOverLifetimeCurvePoint)
                {
                    float expectedRotation = RotationProfileExpectedRadians(
                        particleStartLifetime, age);
                    float actualRotation = gpuParticles.ResolveParticleRotationRadians(
                        i,
                        positionLife.a,
                        gpuRotationPhases[i].r,
                        birthEmitterVelocityWS);
                    float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                        expectedRotation * Mathf.Rad2Deg,
                        actualRotation * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    maximumGPURotationError = Mathf.Max(
                        maximumGPURotationError, rotationError);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.RotationBySpeedCurvePoint)
                {
                    float expectedRotation = RotationBySpeedProfileExpectedRadians(age);
                    float actualRotation = gpuParticles.ResolveParticleRotationRadians(
                        i,
                        positionLife.a,
                        gpuRotationPhases[i].r,
                        birthEmitterVelocityWS);
                    float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                        expectedRotation * Mathf.Rad2Deg,
                        actualRotation * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    maximumGPURotationError = Mathf.Max(
                        maximumGPURotationError, rotationError);
                    maximumForceKinematicsError = Mathf.Max(
                        maximumForceKinematicsError,
                        (gpuVelocity - RotationBySpeedAcceleration * age).magnitude);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.EmissionRateDistancePoint)
                {
                    gpuDistancePositionRange.Observe(
                        positionLife.r - gpuBasePositionWS.x);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint)
                {
                    float normalizedAge = particleStartLifetime > 0f
                        ? Mathf.Clamp01(age / particleStartLifetime)
                        : 0f;
                    Color minimumColor = profileColorMinimumGradient.Evaluate(normalizedAge);
                    Color maximumColor = profileColorMaximumGradient.Evaluate(normalizedAge);
                    Color currentColor = gpuColors[i];
                    maximumGPUColorBoundsError = Mathf.Max(
                        maximumGPUColorBoundsError,
                        ColorBoundsError(currentColor, minimumColor, maximumColor));
                    ObserveBlendFactor(
                        currentColor.r,
                        minimumColor.r,
                        maximumColor.r,
                        ref gpuLifetimeColorBlendRange);

                    float minimumSize = profileSizeMinimumCurve.Evaluate(normalizedAge);
                    float maximumSize = profileSizeMaximumCurve.Evaluate(normalizedAge);
                    maximumGPUSizeBoundsError = Mathf.Max(
                        maximumGPUSizeBoundsError,
                        RangeViolation(velocitySize.a, minimumSize, maximumSize));
                    ObserveBlendFactor(
                        velocitySize.a,
                        minimumSize,
                        maximumSize,
                        ref gpuLifetimeSizeBlendRange);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint)
                {
                    float speed = gpuVelocity.magnitude;
                    float speedPosition = Mathf.InverseLerp(
                        gpuParticles.colorBySpeedRange.x,
                        gpuParticles.colorBySpeedRange.y,
                        speed);
                    Color minimumColor = profileColorMinimumGradient.Evaluate(speedPosition);
                    Color maximumColor = profileColorMaximumGradient.Evaluate(speedPosition);
                    Color currentColor = gpuColors[i];
                    maximumGPUColorBoundsError = Mathf.Max(
                        maximumGPUColorBoundsError,
                        ColorBoundsError(currentColor, minimumColor, maximumColor));
                    ObserveBlendFactor(
                        currentColor.r,
                        minimumColor.r,
                        maximumColor.r,
                        ref gpuSpeedColorBlendRange);

                    float minimumSize = profileSizeMinimumCurve.Evaluate(speedPosition);
                    float maximumSize = profileSizeMaximumCurve.Evaluate(speedPosition);
                    maximumGPUSizeBoundsError = Mathf.Max(
                        maximumGPUSizeBoundsError,
                        RangeViolation(velocitySize.a, minimumSize, maximumSize));
                    ObserveBlendFactor(
                        velocitySize.a,
                        minimumSize,
                        maximumSize,
                        ref gpuSpeedSizeBlendRange);
                    maximumForceKinematicsError = Mathf.Max(
                        maximumForceKinematicsError,
                        (gpuVelocity - ValidationForce * age).magnitude);
                }
            }

            Vector3 shurikenMean = shurikenCount > 0 ? shurikenPositionSum / shurikenCount : Vector3.zero;
            Vector3 gpuMean = gpuCount > 0 ? gpuPositionSum / gpuCount : Vector3.zero;
            Vector3 shurikenMeanVelocity = shurikenCount > 0
                ? shurikenVelocitySum / shurikenCount
                : Vector3.zero;
            Vector3 gpuMeanVelocity = gpuCount > 0 ? gpuVelocitySum / gpuCount : Vector3.zero;
            float shurikenMeanSpeed = shurikenCount > 0 ? shurikenSpeedSum / shurikenCount : 0f;
            float gpuMeanSpeed = gpuCount > 0 ? gpuSpeedSum / gpuCount : 0f;
            float shurikenMeanSize = shurikenCount > 0 ? shurikenSizeSum / shurikenCount : 0f;
            float gpuMeanSize = gpuCount > 0 ? gpuSizeSum / gpuCount : 0f;
            float shurikenMeanSizeY = shurikenCount > 0
                ? shurikenSizeYSum / shurikenCount
                : 0f;
            float gpuMeanSizeY = gpuCount > 0 ? gpuSizeYSum / gpuCount : 0f;
            float shurikenMeanAge = shurikenCount > 0 ? shurikenAgeSum / shurikenCount : 0f;
            float gpuMeanAge = gpuCount > 0 ? gpuAgeSum / gpuCount : 0f;
            maximumShurikenMeanAge = Mathf.Max(
                maximumShurikenMeanAge,
                shurikenMeanAge);
            maximumGPUMeanAge = Mathf.Max(
                maximumGPUMeanAge,
                gpuMeanAge);
            ObservePlaybackMetrics(
                shurikenCount,
                gpuCount,
                shurikenMeanAge,
                gpuMeanAge);
            ObservePrewarmMetrics(
                shurikenCount,
                gpuCount,
                shurikenMeanAge,
                gpuMeanAge);
            ObservePrewarmRestartMetrics(
                shurikenCount,
                gpuCount,
                shurikenMeanAge,
                gpuMeanAge);
            float shurikenMeanLifetime = shurikenCount > 0
                ? shurikenLifetimeSum / shurikenCount
                : 0f;
            float gpuMeanLifetime = gpuCount > 0
                ? gpuLifetimeSum / gpuCount
                : 0f;
            float shurikenMeanStartRotation = shurikenCount > 0
                ? shurikenStartRotationSum / shurikenCount
                : 0f;
            float gpuMeanStartRotation = gpuCount > 0
                ? gpuStartRotationSum / gpuCount
                : 0f;

            maximumCountDelta = Mathf.Max(maximumCountDelta, Mathf.Abs(gpuCount - shurikenCount));
            maximumShurikenParticleCount = Mathf.Max(maximumShurikenParticleCount, shurikenCount);
            maximumGPUParticleCount = Mathf.Max(maximumGPUParticleCount, gpuCount);
            maximumMeanAgeError = Mathf.Max(maximumMeanAgeError, Mathf.Abs(gpuMeanAge - shurikenMeanAge));
            if (shurikenCount > 0 && gpuCount > 0)
            {
                maximumMeanLifetimeError = Mathf.Max(
                    maximumMeanLifetimeError,
                    Mathf.Abs(gpuMeanLifetime - shurikenMeanLifetime));
                if (IsStartRotationProfile())
                {
                    maximumMeanStartRotationError = Mathf.Max(
                        maximumMeanStartRotationError,
                        Mathf.Abs(Mathf.DeltaAngle(
                            shurikenMeanStartRotation * Mathf.Rad2Deg,
                            gpuMeanStartRotation * Mathf.Rad2Deg)) *
                        Mathf.Deg2Rad);
                }
            }
            maximumMeanSpeedError = Mathf.Max(maximumMeanSpeedError,
                Mathf.Abs(gpuMeanSpeed - shurikenMeanSpeed));
            maximumMeanSizeError = Mathf.Max(maximumMeanSizeError,
                Mathf.Abs(gpuMeanSize - shurikenMeanSize));
            if (shurikenCount > 0 && gpuCount > 0)
            {
                maximumMeanSizeYError = Mathf.Max(
                    maximumMeanSizeYError,
                    Mathf.Abs(gpuMeanSizeY - shurikenMeanSizeY));
            }
            maximumMeanVelocityError = Mathf.Max(maximumMeanVelocityError,
                (gpuMeanVelocity - shurikenMeanVelocity).magnitude);
            if ((validationProfile == ParticleABValidationProfile.EmissionRateDistancePoint ||
                 validationProfile == ParticleABValidationProfile.VelocityOverLifetimePoint ||
                 validationProfile ==
                     ParticleABValidationProfile.VelocityOrbitalRadialPoint ||
                 validationProfile ==
                     ParticleABValidationProfile.VelocitySpeedModifierPoint ||
                 validationProfile ==
                     ParticleABValidationProfile.LimitVelocityOverLifetimePoint ||
                 validationProfile ==
                     ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint ||
                 validationProfile ==
                     ParticleABValidationProfile.InheritVelocityInitialPoint ||
                  validationProfile ==
                      ParticleABValidationProfile.InheritVelocityCurrentPoint ||
                  validationProfile ==
                      ParticleABValidationProfile.UnscaledTimePoint) &&
                shurikenCount > 0 && gpuCount > 0)
            {
                Vector3 shurikenMeanDisplacement =
                    shurikenMean - shurikenBasePositionWS;
                Vector3 gpuMeanDisplacement = gpuMean - gpuBasePositionWS;
                maximumMeanPositionError = Mathf.Max(
                    maximumMeanPositionError,
                    (gpuMeanDisplacement - shurikenMeanDisplacement).magnitude);
            }

            var line = new StringBuilder(256);
            Append(line, captureIndex);
            Append(line, elapsed);
            Append(line, Time.frameCount);
            Append(line, shurikenCount);
            Append(line, gpuCount);
            Append(line, gpuCount - shurikenCount);
            Append(line, shurikenMean.x);
            Append(line, shurikenMean.y);
            Append(line, shurikenMean.z);
            Append(line, gpuMean.x);
            Append(line, gpuMean.y);
            Append(line, gpuMean.z);
            Append(line, shurikenMeanVelocity.x);
            Append(line, shurikenMeanVelocity.y);
            Append(line, shurikenMeanVelocity.z);
            Append(line, gpuMeanVelocity.x);
            Append(line, gpuMeanVelocity.y);
            Append(line, gpuMeanVelocity.z);
            Append(line, shurikenMeanSpeed);
            Append(line, gpuMeanSpeed);
            Append(line, shurikenMeanSize);
            Append(line, gpuMeanSize);
            Append(line, shurikenMeanAge);
            Append(line, gpuMeanAge);
            Append(line, shurikenMeanLifetime);
            line.Append(gpuMeanLifetime.ToString("R", CultureInfo.InvariantCulture));
            line.Append('\n');
            File.AppendAllText(metricsPath, line.ToString());
        }

        static float ColorBoundsError(Color value, Color minimum, Color maximum)
        {
            return Mathf.Max(
                Mathf.Max(
                    RangeViolation(value.r, minimum.r, maximum.r),
                    RangeViolation(value.g, minimum.g, maximum.g)),
                Mathf.Max(
                    RangeViolation(value.b, minimum.b, maximum.b),
                    RangeViolation(value.a, minimum.a, maximum.a)));
        }

        void ObserveStartColorSample(
            bool gpu,
            Color value,
            float elapsed,
            float age)
        {
            float error;
            float blend;
            if (validationProfile ==
                ParticleABValidationProfile.StartColorTwoGradientsPoint)
            {
                float birthTime = Mathf.Max(0f, elapsed - age);
                float systemTime = StartColorSystemTime(birthTime);
                Color minimum =
                    profileColorMinimumGradient.Evaluate(systemTime);
                Color maximum =
                    profileColorMaximumGradient.Evaluate(systemTime);
                error = ColorBoundsError(value, minimum, maximum);
                blend = Mathf.InverseLerp(minimum.r, maximum.r, value.r);
            }
            else if (validationProfile ==
                     ParticleABValidationProfile.StartColorRandomColorPoint)
            {
                error = GradientLineError(
                    value,
                    profileColorMaximumGradient,
                    out blend);
            }
            else
            {
                float birthTime = Mathf.Max(0f, elapsed - age);
                float systemTime = StartColorSystemTime(birthTime);
                Color expected =
                    profileColorMaximumGradient.Evaluate(systemTime);
                error = ColorBoundsError(value, expected, expected);
                GradientLineError(
                    value,
                    profileColorMaximumGradient,
                    out blend);
            }

            if (gpu)
            {
                maximumGPUColorBoundsError = Mathf.Max(
                    maximumGPUColorBoundsError,
                    error);
                gpuStartColorBlendRange.Observe(blend);
            }
            else
            {
                maximumShurikenColorBoundsError = Mathf.Max(
                    maximumShurikenColorBoundsError,
                    error);
                shurikenStartColorBlendRange.Observe(blend);
            }
        }

        void ObserveStartSpeedSample(
            bool gpu,
            float speed,
            float elapsed,
            float age)
        {
            float birthTime = Mathf.Max(0f, elapsed - age);
            float systemTime = Mathf.Repeat(
                birthTime,
                StartSpeedProfileDuration) / StartSpeedProfileDuration;
            float minimum = profileStartSpeedMinimumCurve.Evaluate(systemTime);
            float maximum = profileStartSpeedMaximumCurve.Evaluate(systemTime);
            const float loopBoundaryTolerance = 1f / 256f;
            if (systemTime <= loopBoundaryTolerance ||
                systemTime >= 1f - loopBoundaryTolerance)
            {
                minimum = Mathf.Min(
                    minimum,
                    Mathf.Min(
                        profileStartSpeedMinimumCurve.Evaluate(0f),
                        profileStartSpeedMinimumCurve.Evaluate(1f)));
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Max(
                        profileStartSpeedMaximumCurve.Evaluate(0f),
                        profileStartSpeedMaximumCurve.Evaluate(1f)));
            }
            float error = RangeViolation(speed, minimum, maximum);
            float blend = Mathf.InverseLerp(minimum, maximum, speed);

            if (gpu)
            {
                maximumGPUStartSpeedBoundsError = Mathf.Max(
                    maximumGPUStartSpeedBoundsError,
                    error);
                gpuSpeedRange.Observe(speed);
                if (validationProfile ==
                    ParticleABValidationProfile.StartSpeedTwoCurvesPoint)
                {
                    gpuStartSpeedBlendRange.Observe(blend);
                }
            }
            else
            {
                maximumShurikenStartSpeedBoundsError = Mathf.Max(
                    maximumShurikenStartSpeedBoundsError,
                    error);
                shurikenSpeedRange.Observe(speed);
                if (validationProfile ==
                    ParticleABValidationProfile.StartSpeedTwoCurvesPoint)
                {
                    shurikenStartSpeedBlendRange.Observe(blend);
                }
            }
        }

        void ObserveStartLifetimeSample(
            bool gpu,
            float lifetime,
            float elapsed,
            float age)
        {
            float birthTime = Mathf.Max(0f, elapsed - age);
            float frameDelta = 1f / Mathf.Max(1, fixedFrameRate);
            float sampledBirthTime = Mathf.Ceil(
                Mathf.Max(0f, birthTime - 1e-6f) / frameDelta) *
                frameDelta + frameDelta * StartLifetimeCurveTickPhase;
            float systemTime = Mathf.Repeat(
                sampledBirthTime,
                StartLifetimeProfileDuration) /
                StartLifetimeProfileDuration;
            float minimum =
                profileStartLifetimeMinimumCurve.Evaluate(systemTime);
            float maximum =
                profileStartLifetimeMaximumCurve.Evaluate(systemTime);
            const float loopBoundaryTolerance = 1f / 256f;
            if (systemTime <= loopBoundaryTolerance ||
                systemTime >= 1f - loopBoundaryTolerance)
            {
                minimum = Mathf.Min(
                    minimum,
                    Mathf.Min(
                        profileStartLifetimeMinimumCurve.Evaluate(0f),
                        profileStartLifetimeMinimumCurve.Evaluate(1f)));
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Max(
                        profileStartLifetimeMaximumCurve.Evaluate(0f),
                        profileStartLifetimeMaximumCurve.Evaluate(1f)));
            }
            float error = RangeViolation(lifetime, minimum, maximum);
            float blend = Mathf.InverseLerp(minimum, maximum, lifetime);

            if (gpu)
            {
                maximumGPUStartLifetimeBoundsError = Mathf.Max(
                    maximumGPUStartLifetimeBoundsError,
                    error);
                gpuLifetimeRange.Observe(lifetime);
                if (validationProfile ==
                    ParticleABValidationProfile.StartLifetimeTwoCurvesPoint)
                {
                    gpuStartLifetimeBlendRange.Observe(blend);
                }
            }
            else
            {
                maximumShurikenStartLifetimeBoundsError = Mathf.Max(
                    maximumShurikenStartLifetimeBoundsError,
                    error);
                shurikenLifetimeRange.Observe(lifetime);
                if (validationProfile ==
                    ParticleABValidationProfile.StartLifetimeTwoCurvesPoint)
                {
                    shurikenStartLifetimeBlendRange.Observe(blend);
                }
            }
        }

        void ObserveStartRotationSample(
            bool gpu,
            float rotation,
            float elapsed,
            float age)
        {
            float birthTime = Mathf.Max(0f, elapsed - age);
            float systemTime = Mathf.Repeat(
                birthTime,
                StartRotationProfileDuration) /
                StartRotationProfileDuration;
            float minimum =
                profileStartRotationMinimumCurve.Evaluate(systemTime);
            float maximum =
                profileStartRotationMaximumCurve.Evaluate(systemTime);
            const float loopBoundaryTolerance = 1f / 256f;
            if (systemTime <= loopBoundaryTolerance ||
                systemTime >= 1f - loopBoundaryTolerance)
            {
                minimum = Mathf.Min(
                    minimum,
                    Mathf.Min(
                        profileStartRotationMinimumCurve.Evaluate(0f),
                        profileStartRotationMinimumCurve.Evaluate(1f)));
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Max(
                        profileStartRotationMaximumCurve.Evaluate(0f),
                        profileStartRotationMaximumCurve.Evaluate(1f)));
            }
            float error = RangeViolation(rotation, minimum, maximum);
            float blend = Mathf.InverseLerp(minimum, maximum, rotation);

            if (gpu)
            {
                maximumGPURotationError = Mathf.Max(
                    maximumGPURotationError,
                    error);
                gpuStartRotationRange.Observe(rotation);
                if (validationProfile ==
                    ParticleABValidationProfile.StartRotationTwoCurvesPoint)
                {
                    gpuStartRotationBlendRange.Observe(blend);
                }
            }
            else
            {
                maximumShurikenRotationError = Mathf.Max(
                    maximumShurikenRotationError,
                    error);
                shurikenStartRotationRange.Observe(rotation);
                if (validationProfile ==
                    ParticleABValidationProfile.StartRotationTwoCurvesPoint)
                {
                    shurikenStartRotationBlendRange.Observe(blend);
                }
            }
        }

        void ObserveStartSizeSample(
            bool gpu,
            float size,
            float elapsed,
            float age)
        {
            float birthTime = Mathf.Max(0f, elapsed - age);
            float systemTime = Mathf.Repeat(
                birthTime,
                StartSizeProfileDuration) / StartSizeProfileDuration;
            float minimum = profileStartSizeMinimumCurve.Evaluate(systemTime);
            float maximum = profileStartSizeMaximumCurve.Evaluate(systemTime);
            const float loopBoundaryTolerance = 1f / 256f;
            if (systemTime <= loopBoundaryTolerance ||
                systemTime >= 1f - loopBoundaryTolerance)
            {
                minimum = Mathf.Min(
                    minimum,
                    Mathf.Min(
                        profileStartSizeMinimumCurve.Evaluate(0f),
                        profileStartSizeMinimumCurve.Evaluate(1f)));
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Max(
                        profileStartSizeMaximumCurve.Evaluate(0f),
                        profileStartSizeMaximumCurve.Evaluate(1f)));
            }
            float error = RangeViolation(size, minimum, maximum);
            float blend = Mathf.InverseLerp(minimum, maximum, size);

            if (gpu)
            {
                maximumGPUStartSizeBoundsError = Mathf.Max(
                    maximumGPUStartSizeBoundsError,
                    error);
                gpuSizeRange.Observe(size);
                if (validationProfile ==
                    ParticleABValidationProfile.StartSizeTwoCurvesPoint)
                {
                    gpuStartSizeBlendRange.Observe(blend);
                }
            }
            else
            {
                maximumShurikenStartSizeBoundsError = Mathf.Max(
                    maximumShurikenStartSizeBoundsError,
                    error);
                shurikenSizeRange.Observe(size);
                if (validationProfile ==
                    ParticleABValidationProfile.StartSizeTwoCurvesPoint)
                {
                    shurikenStartSizeBlendRange.Observe(blend);
                }
            }
        }

        void ObserveGravityModifierSample(
            bool gpu,
            Vector3 velocity,
            float elapsed,
            float age)
        {
            Vector3 gravity = Physics.gravity;
            if (gravity.sqrMagnitude <= 1e-6f)
            {
                return;
            }

            float birthTime = Mathf.Max(0f, elapsed - age);
            float minimumIntegral = IntegrateLoopingLinearCurve(
                profileGravityMinimumCurve,
                birthTime,
                elapsed,
                GravityModifierProfileDuration);
            float maximumIntegral = IntegrateLoopingLinearCurve(
                profileGravityMaximumCurve,
                birthTime,
                elapsed,
                GravityModifierProfileDuration);
            float observedIntegral = Vector3.Dot(velocity, gravity) /
                                     gravity.sqrMagnitude;
            float error = RangeViolation(
                observedIntegral,
                minimumIntegral,
                maximumIntegral);
            float blend = Mathf.InverseLerp(
                minimumIntegral,
                maximumIntegral,
                observedIntegral);

            if (gpu)
            {
                maximumGPUGravityIntegralBoundsError = Mathf.Max(
                    maximumGPUGravityIntegralBoundsError,
                    error);
                if (validationProfile ==
                    ParticleABValidationProfile.GravityModifierTwoCurvesPoint)
                {
                    gpuGravityBlendRange.Observe(blend);
                }
            }
            else
            {
                maximumShurikenGravityIntegralBoundsError = Mathf.Max(
                    maximumShurikenGravityIntegralBoundsError,
                    error);
                if (validationProfile ==
                    ParticleABValidationProfile.GravityModifierTwoCurvesPoint)
                {
                    shurikenGravityBlendRange.Observe(blend);
                }
            }
        }

        static float IntegrateLoopingLinearCurve(
            AnimationCurve curve,
            float startTime,
            float endTime,
            float duration)
        {
            duration = Mathf.Max(0.05f, duration);
            return LoopingLinearCurvePrimitive(curve, endTime, duration) -
                   LoopingLinearCurvePrimitive(curve, startTime, duration);
        }

        static float LoopingLinearCurvePrimitive(
            AnimationCurve curve,
            float time,
            float duration)
        {
            time = Mathf.Max(0f, time);
            float startValue = curve.Evaluate(0f);
            float endValue = curve.Evaluate(1f);
            int cycleCount = Mathf.FloorToInt(time / duration);
            float remainder = time - cycleCount * duration;
            float normalizedRemainder = remainder / duration;
            float cycleArea = 0.5f * (startValue + endValue) * duration;
            float remainderArea = duration *
                (startValue * normalizedRemainder +
                 0.5f * (endValue - startValue) *
                 normalizedRemainder * normalizedRemainder);
            return cycleCount * cycleArea + remainderArea;
        }

        float StartColorSystemTime(float birthTime)
        {
            float systemTime = Mathf.Repeat(
                birthTime,
                StartColorProfileDuration) / StartColorProfileDuration;
            const float loopBoundaryWindow = 1f / 256f;
            return systemTime >= 1f - loopBoundaryWindow
                ? 0f
                : systemTime;
        }

        static float GradientLineError(
            Color value,
            Gradient gradient,
            out float blend)
        {
            Color start = gradient.Evaluate(0f);
            Color end = gradient.Evaluate(1f);
            Vector4 delta = new Vector4(
                end.r - start.r,
                end.g - start.g,
                end.b - start.b,
                end.a - start.a);
            Vector4 offset = new Vector4(
                value.r - start.r,
                value.g - start.g,
                value.b - start.b,
                value.a - start.a);
            float denominator = Vector4.Dot(delta, delta);
            blend = denominator > 1e-8f
                ? Mathf.Clamp01(Vector4.Dot(offset, delta) / denominator)
                : 0f;
            Color expected = Color.Lerp(start, end, blend);
            return ColorBoundsError(value, expected, expected);
        }

        static float RangeViolation(float value, float minimum, float maximum)
        {
            float lower = Mathf.Min(minimum, maximum);
            float upper = Mathf.Max(minimum, maximum);
            if (value < lower) return lower - value;
            if (value > upper) return value - upper;
            return 0f;
        }

        static float RotationProfileExpectedRadians(float startLifetime, float age)
        {
            float normalizedAge = startLifetime > 1e-6f
                ? Mathf.Clamp01(age / startLifetime)
                : 0f;
            // The validation angular velocity curve is omega(u) = 2u radians/second.
            return RotationProfileStartRotation +
                   startLifetime * normalizedAge * normalizedAge;
        }

        static Vector3 VelocitySpeedModifierExpectedDisplacement(float age)
        {
            age = Mathf.Clamp(age, 0f, VelocitySpeedModifierLifetime);
            float integral = age * age /
                             (2f * VelocitySpeedModifierLifetime);
            return VelocitySpeedModifierRawVelocity * integral;
        }

        static float RotationBySpeedProfileExpectedRadians(float age)
        {
            age = Mathf.Max(0f, age);
            float rotationBySpeedPhase = age <= 2f
                ? age * age
                : 4f * age - 4f;
            return RotationProfileStartRotation + 0.5f * age +
                   rotationBySpeedPhase;
        }

        static float LimitVelocityProfileExpectedSpeed(float age)
        {
            const float frameDeltaTime = 1f / 60f;
            age = Mathf.Clamp(age, 0f, 4f);

            int fullSteps = Mathf.FloorToInt(age / frameDeltaTime + 0.0001f);
            float partialStep = age - fullSteps * frameDeltaTime;
            if (partialStep < -0.00001f)
            {
                fullSteps--;
                partialStep += frameDeltaTime;
            }
            if (partialStep < 0.00001f)
            {
                partialStep = 0f;
            }

            float speed = 10f;
            float simulatedAge = 0f;
            if (partialStep > 0f)
            {
                AdvanceLimitVelocityProfile(
                    ref speed, ref simulatedAge, partialStep);
            }
            for (int i = 0; i < fullSteps; i++)
            {
                AdvanceLimitVelocityProfile(
                    ref speed, ref simulatedAge, frameDeltaTime);
            }
            return speed;
        }

        static void AdvanceLimitVelocityProfile(
            ref float speed,
            ref float simulatedAge,
            float deltaTime)
        {
            simulatedAge += deltaTime;
            float normalizedAge = Mathf.Clamp01(simulatedAge / 4f);
            float speedLimit = Mathf.Lerp(6f, 3f, normalizedAge);
            if (speed > speedLimit)
            {
                float dampenFactor = Mathf.Pow(1f - 0.35f, deltaTime * 30f);
                speed = speedLimit + (speed - speedLimit) * dampenFactor;
            }

            // Size is 2, so Multiply by Size contributes pi * (2 / 2)^2.
            float drag = 0.02f * Mathf.PI * speed * speed;
            speed = Mathf.Max(0f, speed - drag * deltaTime);
        }

        static Vector3 LimitVelocityAxesExpectedVelocity(
            float age,
            Transform emitter)
        {
            const float frameDeltaTime = 1f / 60f;
            age = Mathf.Clamp(age, 0f, 3f);

            int fullSteps = Mathf.FloorToInt(age / frameDeltaTime + 0.0001f);
            float partialStep = age - fullSteps * frameDeltaTime;
            if (partialStep < -0.00001f)
            {
                fullSteps--;
                partialStep += frameDeltaTime;
            }
            if (partialStep < 0.00001f)
            {
                partialStep = 0f;
            }

            Vector3 accelerationInSimulationSpace = emitter
                .TransformDirection(LimitVelocityAxesAcceleration);
            Vector3 accelerationInLimitSpace = emitter
                .TransformDirection(accelerationInSimulationSpace);
            Vector3 velocityInLimitSpace = Vector3.zero;
            if (partialStep > 0f)
            {
                AdvanceLimitVelocityAxes(
                    ref velocityInLimitSpace,
                    accelerationInLimitSpace,
                    partialStep);
            }
            for (int i = 0; i < fullSteps; i++)
            {
                AdvanceLimitVelocityAxes(
                    ref velocityInLimitSpace,
                    accelerationInLimitSpace,
                    frameDeltaTime);
            }
            return emitter.InverseTransformDirection(velocityInLimitSpace);
        }

        static void AdvanceLimitVelocityAxes(
            ref Vector3 velocity,
            Vector3 acceleration,
            float deltaTime)
        {
            velocity += acceleration * deltaTime;
            float dampenFactor = Mathf.Pow(1f - 0.6f, deltaTime * 30f);
            velocity.x = DampenedExpectedAxis(
                velocity.x, 2f, dampenFactor);
            velocity.y = DampenedExpectedAxis(
                velocity.y, 1f, dampenFactor);
            velocity.z = DampenedExpectedAxis(
                velocity.z, 1.5f, dampenFactor);
        }

        static float DampenedExpectedAxis(
            float value,
            float limit,
            float dampenFactor)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= limit) return value;
            float dampenedMagnitude = limit +
                (magnitude - limit) * dampenFactor;
            return Mathf.Sign(value) * dampenedMagnitude;
        }

        static void ObserveBlendFactor(
            float value,
            float minimum,
            float maximum,
            ref ObservedRange range)
        {
            float denominator = maximum - minimum;
            if (Mathf.Abs(denominator) <= 1e-6f) return;
            range.Observe((value - minimum) / denominator);
        }

        void ObserveShapeSample(
            bool gpuSample,
            Vector3 spawnPosition,
            Vector3 velocity)
        {
            if (gpuSample)
            {
                gpuShapeSpawnXRange.Observe(spawnPosition.x);
                gpuShapeSpawnYRange.Observe(spawnPosition.y);
                gpuShapeSpawnZRange.Observe(spawnPosition.z);
            }
            else
            {
                shurikenShapeSpawnXRange.Observe(spawnPosition.x);
                shurikenShapeSpawnYRange.Observe(spawnPosition.y);
                shurikenShapeSpawnZRange.Observe(spawnPosition.z);
            }

            Vector3 expectedDirection = Vector3.zero;
            float geometryError = 0f;
            switch (validationProfile)
            {
                case ParticleABValidationProfile.ShapeSpherePoint:
                    geometryError = Mathf.Max(0f, spawnPosition.magnitude - 2f);
                    if (spawnPosition.sqrMagnitude > 1e-8f)
                    {
                        expectedDirection = spawnPosition.normalized;
                    }
                    break;

                case ParticleABValidationProfile.ShapeCirclePoint:
                {
                    Vector2 planar = new Vector2(
                        spawnPosition.x, spawnPosition.y);
                    geometryError = Mathf.Max(
                        Mathf.Abs(spawnPosition.z),
                        Mathf.Max(0f, planar.magnitude - 2f));
                    if (planar.sqrMagnitude > 1e-8f)
                    {
                        Vector2 direction = planar.normalized;
                        expectedDirection = new Vector3(
                            direction.x, direction.y, 0f);
                    }
                    break;
                }

                case ParticleABValidationProfile.ShapeDonutPoint:
                {
                    Vector2 planar = new Vector2(
                        spawnPosition.x, spawnPosition.y);
                    if (planar.sqrMagnitude > 1e-8f)
                    {
                        Vector2 ringCenter = planar.normalized * 2f;
                        Vector3 crossSection = new Vector3(
                            spawnPosition.x - ringCenter.x,
                            spawnPosition.y - ringCenter.y,
                            spawnPosition.z);
                        geometryError = Mathf.Max(
                            0f, crossSection.magnitude - 0.5f);
                        if (crossSection.sqrMagnitude > 1e-8f)
                        {
                            expectedDirection = crossSection.normalized;
                        }
                    }
                    break;
                }

                case ParticleABValidationProfile.ShapeEdgePoint:
                    expectedDirection = Vector3.up;
                    geometryError = Mathf.Max(
                        Mathf.Abs(spawnPosition.y),
                        Mathf.Abs(spawnPosition.z));
                    break;

                case ParticleABValidationProfile.ShapeRectanglePoint:
                    expectedDirection = Vector3.forward;
                    geometryError = Mathf.Abs(spawnPosition.z);
                    break;

                case ParticleABValidationProfile.ShapeBoxEdgePoint:
                {
                    expectedDirection = Vector3.forward;
                    float xBoundaryError = Mathf.Abs(
                        Mathf.Abs(spawnPosition.x) - 2f);
                    float yBoundaryError = Mathf.Abs(
                        Mathf.Abs(spawnPosition.y) - 1f);
                    float zBoundaryError = Mathf.Abs(
                        Mathf.Abs(spawnPosition.z) - 0.5f);
                    geometryError = xBoundaryError +
                                    yBoundaryError +
                                    zBoundaryError -
                                    Mathf.Min(
                                        xBoundaryError,
                                        Mathf.Min(
                                            yBoundaryError,
                                            zBoundaryError)) -
                                    Mathf.Max(
                                        xBoundaryError,
                                        Mathf.Max(
                                            yBoundaryError,
                                            zBoundaryError));
                    break;
                }
            }

            float directionError = expectedDirection.sqrMagnitude > 1e-8f
                ? (velocity - expectedDirection).magnitude
                : 0f;
            if (gpuSample)
            {
                maximumGPUShapeDirectionError = Mathf.Max(
                    maximumGPUShapeDirectionError, directionError);
                maximumGPUShapeGeometryError = Mathf.Max(
                    maximumGPUShapeGeometryError, geometryError);
            }
            else
            {
                maximumShurikenShapeDirectionError = Mathf.Max(
                    maximumShurikenShapeDirectionError, directionError);
                maximumShurikenShapeGeometryError = Mathf.Max(
                    maximumShurikenShapeGeometryError, geometryError);
            }
        }

        bool ShapeSpawnRangesPass()
        {
            Vector2 xRange;
            Vector2 yRange;
            Vector2 zRange;
            switch (validationProfile)
            {
                case ParticleABValidationProfile.ShapeSpherePoint:
                    xRange = new Vector2(-2f, 2f);
                    yRange = new Vector2(-2f, 2f);
                    zRange = new Vector2(-2f, 2f);
                    break;
                case ParticleABValidationProfile.ShapeCirclePoint:
                    xRange = new Vector2(-2f, 2f);
                    yRange = new Vector2(-2f, 2f);
                    zRange = Vector2.zero;
                    break;
                case ParticleABValidationProfile.ShapeDonutPoint:
                    xRange = new Vector2(-2.5f, 2.5f);
                    yRange = new Vector2(-2.5f, 2.5f);
                    zRange = new Vector2(-0.5f, 0.5f);
                    break;
                case ParticleABValidationProfile.ShapeEdgePoint:
                    xRange = new Vector2(-6f, 6f);
                    yRange = Vector2.zero;
                    zRange = Vector2.zero;
                    break;
                case ParticleABValidationProfile.ShapeRectanglePoint:
                    xRange = new Vector2(-2f, 2f);
                    yRange = new Vector2(-1f, 1f);
                    zRange = Vector2.zero;
                    break;
                case ParticleABValidationProfile.ShapeBoxEdgePoint:
                    xRange = new Vector2(-2f, 2f);
                    yRange = new Vector2(-1f, 1f);
                    zRange = new Vector2(-0.5f, 0.5f);
                    break;
                default:
                    return false;
            }

            return shurikenShapeSpawnXRange.Covers(xRange.x, xRange.y) &&
                   shurikenShapeSpawnYRange.Covers(yRange.x, yRange.y) &&
                   shurikenShapeSpawnZRange.Covers(zRange.x, zRange.y) &&
                   gpuShapeSpawnXRange.Covers(xRange.x, xRange.y) &&
                   gpuShapeSpawnYRange.Covers(yRange.x, yRange.y) &&
                   gpuShapeSpawnZRange.Covers(zRange.x, zRange.y);
        }

        float ConeRelationError(Vector3 position, Vector3 velocity, float age)
        {
            float radius = Mathf.Max(1e-6f, gpuParticles.shapeConeRadius);
            float tanAngle = Mathf.Tan(gpuParticles.shapeConeAngle * Mathf.Deg2Rad);
            if (tanAngle <= 1e-6f || Mathf.Abs(velocity.z) <= 1e-6f) return 0f;

            Vector3 spawnPosition = position - velocity * age;
            float positionRatio = new Vector2(spawnPosition.x, spawnPosition.y).magnitude / radius;
            float velocityRatio = new Vector2(velocity.x, velocity.y).magnitude /
                                  Mathf.Abs(velocity.z) / tanAngle;
            return Mathf.Abs(positionRatio - velocityRatio);
        }

        static void Append(StringBuilder line, int value)
        {
            line.Append(value.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
        }

        static void Append(StringBuilder line, float value)
        {
            line.Append(value.ToString("R", CultureInfo.InvariantCulture));
            line.Append(',');
        }

        static Texture2D ReadRenderTexture(RenderTexture source, TextureFormat format, bool linear)
        {
            if (source == null)
            {
                throw new InvalidOperationException("Particle A/B capture source RT is not initialised.");
            }

            var texture = new Texture2D(source.width, source.height, format, false, linear);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            return texture;
        }

        void EnsureCameraCaptureTarget()
        {
            if (cameraCaptureRT != null &&
                cameraCaptureRT.width == captureWidth &&
                cameraCaptureRT.height == captureHeight)
            {
                return;
            }

            ReleaseCameraCaptureTarget();
            cameraCaptureRT = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "ParticleAB_CameraCapture",
                antiAliasing = 1,
                useMipMap = false
            };
            cameraCaptureRT.Create();
        }

        void ReleaseCameraCaptureTarget()
        {
            if (cameraCaptureRT == null) return;
            cameraCaptureRT.Release();
            Destroy(cameraCaptureRT);
            cameraCaptureRT = null;
        }

        void ObservePlaybackMetrics(
            int shurikenCount,
            int gpuCount,
            float shurikenMeanAge,
            float gpuMeanAge)
        {
            if (!IsPlaybackLifecycleProfile()) return;

            if (playbackFrame < PlaybackPlayFrame)
            {
                if (shurikenCount != 0 || gpuCount != 0)
                {
                    playbackEmptyViolationCount++;
                }
                return;
            }

            if (playbackFrame >= PlaybackPauseFrame &&
                playbackFrame < PlaybackResumeFrame &&
                shurikenCount > 0 && gpuCount > 0)
            {
                shurikenPausedMeanAgeRange.Observe(shurikenMeanAge);
                gpuPausedMeanAgeRange.Observe(gpuMeanAge);
            }

            if (playbackFrame >= PlaybackStopEmittingFrame &&
                playbackFrame < PlaybackDrainExpectedFrame)
            {
                maximumShurikenStoppedParticleCount = Mathf.Max(
                    maximumShurikenStoppedParticleCount,
                    shurikenCount);
                maximumGPUStoppedParticleCount = Mathf.Max(
                    maximumGPUStoppedParticleCount,
                    gpuCount);
            }

            if (playbackFrame >= PlaybackDrainExpectedFrame &&
                playbackFrame < PlaybackReplayFrame &&
                shurikenCount == 0 && gpuCount == 0)
            {
                playbackDrainObserved = true;
            }

            if (playbackFrame >= PlaybackClearFrame &&
                shurikenCount == 0 && gpuCount == 0)
            {
                playbackClearObserved = true;
            }
        }

        static float ObservedSpread(ObservedRange range)
        {
            return range.HasSamples
                ? range.Maximum - range.Minimum
                : float.PositiveInfinity;
        }

        void ObservePrewarmMetrics(
            int shurikenCount,
            int gpuCount,
            float shurikenMeanAge,
            float gpuMeanAge)
        {
            if (validationProfile != ParticleABValidationProfile.PrewarmPoint ||
                prewarmFirstSnapshotObserved)
            {
                return;
            }

            prewarmFirstSnapshotObserved = true;
            prewarmFirstShurikenCount = shurikenCount;
            prewarmFirstGPUCount = gpuCount;
            prewarmFirstShurikenMeanAge = shurikenMeanAge;
            prewarmFirstGPUMeanAge = gpuMeanAge;
        }

        void ObservePrewarmRestartMetrics(
            int shurikenCount,
            int gpuCount,
            float shurikenMeanAge,
            float gpuMeanAge)
        {
            if (validationProfile != ParticleABValidationProfile.PrewarmPoint ||
                prewarmRestartSnapshotObserved ||
                playbackFrame < PrewarmRestartCaptureFrame)
            {
                return;
            }

            prewarmRestartSnapshotObserved = true;
            prewarmRestartShurikenCount = shurikenCount;
            prewarmRestartGPUCount = gpuCount;
            prewarmRestartShurikenMeanAge = shurikenMeanAge;
            prewarmRestartGPUMeanAge = gpuMeanAge;
        }

        void CompleteCapture()
        {
            captureActive = false;
            bool profileSpecificPassed;
            switch (validationProfile)
            {
                case ParticleABValidationProfile.BaselineCone:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenConeError <= 0.001f &&
                                            maximumGPUConeError <= 0.001f;
                    break;

                case ParticleABValidationProfile.ForceOverLifetimePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanVelocityError <= 0.001f &&
                                            maximumForceKinematicsError <= 0.005f;
                    break;

                case ParticleABValidationProfile.RandomizedMainPoint:
                    profileSpecificPassed =
                        shurikenLifetimeRange.Covers(3f, 5f) &&
                        gpuLifetimeRange.Covers(3f, 5f) &&
                        shurikenSpeedRange.Covers(1f, 3f) &&
                        gpuSpeedRange.Covers(1f, 3f) &&
                        shurikenSizeRange.Covers(0.5f, 1.5f) &&
                        gpuSizeRange.Covers(0.5f, 1.5f) &&
                        shurikenColorRedRange.Covers(0f, 1f) &&
                        gpuColorRedRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.StartColorGradientPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenColorBoundsError <= 0.015f &&
                                            maximumGPUColorBoundsError <= 0.006f &&
                                            shurikenStartColorBlendRange.Covers(0f, 1f) &&
                                            gpuStartColorBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.StartColorTwoGradientsPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenColorBoundsError <= 0.006f &&
                                            maximumGPUColorBoundsError <= 0.003f &&
                                            shurikenStartColorBlendRange.Covers(0f, 1f) &&
                                            gpuStartColorBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.StartColorRandomColorPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenColorBoundsError <= 0.008f &&
                                            maximumGPUColorBoundsError <= 0.003f &&
                                            shurikenStartColorBlendRange.Covers(0f, 1f) &&
                                            gpuStartColorBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.StartLifetimeCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanLifetimeError <= 0.025f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenStartLifetimeBoundsError <= 0.04f &&
                                            maximumGPUStartLifetimeBoundsError <= 0.02f &&
                                            shurikenLifetimeRange.Covers(0.75f, 2.75f) &&
                                            gpuLifetimeRange.Covers(0.75f, 2.75f);
                    break;

                case ParticleABValidationProfile.StartLifetimeTwoCurvesPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanLifetimeError <= 0.22f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenStartLifetimeBoundsError <= 0.04f &&
                                            maximumGPUStartLifetimeBoundsError <= 0.015f &&
                                            shurikenLifetimeRange.Covers(0.75f, 2.75f) &&
                                            gpuLifetimeRange.Covers(0.75f, 2.75f) &&
                                            shurikenStartLifetimeBlendRange.Covers(0f, 1f) &&
                                            gpuStartLifetimeBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.StartRotationCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanStartRotationError <= 0.025f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenRotationError <= 0.025f &&
                                            maximumGPURotationError <= 0.006f &&
                                            shurikenStartRotationRange.Covers(-1f, 1f) &&
                                            gpuStartRotationRange.Covers(-1f, 1f);
                    break;

                case ParticleABValidationProfile.StartRotationTwoCurvesPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanStartRotationError <= 0.2f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenRotationError <= 0.025f &&
                                            maximumGPURotationError <= 0.006f &&
                                            shurikenStartRotationRange.Covers(-1.2f, 1.2f) &&
                                            gpuStartRotationRange.Covers(-1.2f, 1.2f) &&
                                            shurikenStartRotationBlendRange.Covers(0f, 1f) &&
                                            gpuStartRotationBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.StartSpeedCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.03f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenStartSpeedBoundsError <= 0.02f &&
                                            maximumGPUStartSpeedBoundsError <= 0.01f &&
                                            shurikenSpeedRange.Covers(1f, 5f) &&
                                            gpuSpeedRange.Covers(1f, 5f);
                    break;

                case ParticleABValidationProfile.StartSpeedTwoCurvesPoint:
                    profileSpecificPassed = maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenStartSpeedBoundsError <= 0.02f &&
                                            maximumGPUStartSpeedBoundsError <= 0.01f &&
                                            shurikenSpeedRange.Covers(1f, 9f) &&
                                            gpuSpeedRange.Covers(1f, 9f) &&
                                            shurikenStartSpeedBlendRange.Covers(0f, 1f) &&
                                            gpuStartSpeedBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.StartSizeCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanSizeError <= 0.02f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenStartSizeBoundsError <= 0.015f &&
                                            maximumGPUStartSizeBoundsError <= 0.015f &&
                                            shurikenSizeRange.Covers(0.25f, 1.25f) &&
                                            gpuSizeRange.Covers(0.25f, 1.25f);
                    break;

                case ParticleABValidationProfile.StartSizeTwoCurvesPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenStartSizeBoundsError <= 0.02f &&
                                            maximumGPUStartSizeBoundsError <= 0.02f &&
                                            shurikenSizeRange.Covers(0.25f, 2.25f) &&
                                            gpuSizeRange.Covers(0.25f, 2.25f) &&
                                            shurikenStartSizeBlendRange.Covers(0f, 1f) &&
                                            gpuStartSizeBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.SizeSeparateAxesPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanSizeError <= 0.02f &&
                                            maximumMeanSizeYError <= 0.03f &&
                                            maximumShurikenParticleCount == 1 &&
                                            maximumGPUParticleCount == 1;
                    break;

                case ParticleABValidationProfile.RendererScreenSizeClampPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount == 1 &&
                                            maximumGPUParticleCount == 1 &&
                                            screenSizeClassificationFailures == 0 &&
                                            maximumScreenSizePixelError <=
                                                RendererClampPairTolerancePixels &&
                                            RendererScreenSizeRangePasses(
                                                shurikenScreenSizePixelRange) &&
                                            RendererScreenSizeRangePasses(
                                                gpuScreenSizePixelRange);
                    break;

                case ParticleABValidationProfile.ScalingHierarchyPoint:
                case ParticleABValidationProfile.ScalingLocalPoint:
                case ParticleABValidationProfile.ScalingShapePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanSizeError <= 0.001f &&
                                            maximumMeanSizeYError <= 0.001f &&
                                            maximumMeanPositionError <= 0.03f &&
                                            maximumShurikenParticleCount == 1 &&
                                            maximumGPUParticleCount == 1 &&
                                            scalingBoundsClassificationFailures == 0 &&
                                            maximumScalingWidthPixelError <=
                                                ScalingPairTolerancePixels &&
                                            maximumScalingHeightPixelError <=
                                                ScalingPairTolerancePixels &&
                                            maximumScalingSpawnOffsetPixelError <=
                                                ScalingPairTolerancePixels &&
                                            ScalingModeSemanticsPass();
                    break;

                case ParticleABValidationProfile.PlaybackLifecyclePoint:
                    profileSpecificPassed =
                        playbackInitialStopped &&
                        playbackStateMismatchCount == 0 &&
                        playbackEmptyViolationCount == 0 &&
                        playbackTransitionMask == 0xFF &&
                        shurikenPausedMeanAgeRange.HasSamples &&
                        gpuPausedMeanAgeRange.HasSamples &&
                        ObservedSpread(shurikenPausedMeanAgeRange) <= 0.02f &&
                        ObservedSpread(gpuPausedMeanAgeRange) <= 0.02f &&
                        maximumShurikenStoppedParticleCount > 0 &&
                        maximumGPUStoppedParticleCount > 0 &&
                        playbackDrainObserved &&
                        playbackClearObserved;
                    break;

                case ParticleABValidationProfile.PrewarmPoint:
                    profileSpecificPassed =
                        prewarmFirstSnapshotObserved &&
                        prewarmFirstShurikenCount == 15 &&
                        prewarmFirstGPUCount == 15 &&
                        prewarmFirstShurikenMeanAge >= 0.55f &&
                        prewarmFirstGPUMeanAge >= 0.55f &&
                        Mathf.Abs(
                            prewarmFirstGPUMeanAge -
                            prewarmFirstShurikenMeanAge) <= 0.001f &&
                        prewarmRestartSnapshotObserved &&
                        prewarmRestartShurikenCount == 15 &&
                        prewarmRestartGPUCount == 15 &&
                        prewarmRestartShurikenMeanAge >= 0.55f &&
                        prewarmRestartGPUMeanAge >= 0.55f &&
                        Mathf.Abs(
                            prewarmRestartGPUMeanAge -
                            prewarmRestartShurikenMeanAge) <= 0.001f &&
                        maximumMeanSpeedError <= 0.001f &&
                        maximumMeanVelocityError <= 0.001f &&
                        maximumForceKinematicsError <= 0.005f;
                    break;

                case ParticleABValidationProfile.UnscaledTimePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanAgeError <= 0.001f &&
                                            maximumMeanPositionError <= 0.01f &&
                                            maximumShurikenParticleCount == 1 &&
                                            maximumGPUParticleCount == 1 &&
                                            maximumShurikenMeanAge >= 3f &&
                                            maximumGPUMeanAge >= 3f;
                    break;

                case ParticleABValidationProfile.GravityModifierCurvePoint:
                    profileSpecificPassed = maximumMeanVelocityError <= 0.05f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenGravityIntegralBoundsError <= 0.04f &&
                                            maximumGPUGravityIntegralBoundsError <= 0.04f;
                    break;

                case ParticleABValidationProfile.GravityModifierTwoCurvesPoint:
                    profileSpecificPassed = maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenGravityIntegralBoundsError <= 0.04f &&
                                            maximumGPUGravityIntegralBoundsError <= 0.04f &&
                                            shurikenGravityBlendRange.Covers(0f, 1f) &&
                                            gpuGravityBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.EmissionBurstPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount == 42 &&
                                            maximumGPUParticleCount == 42;
                    break;

                case ParticleABValidationProfile.EmissionRateCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount == 39 &&
                                            maximumGPUParticleCount == 39;
                    break;

                case ParticleABValidationProfile.EmissionRateDistancePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanPositionError <= 0.03f &&
                                            maximumShurikenParticleCount >= 15 &&
                                            maximumGPUParticleCount >= 15 &&
                                            shurikenDistancePositionRange.Covers(
                                                0f, captureDuration + 0.1f) &&
                                            gpuDistancePositionRange.Covers(
                                                0f, captureDuration + 0.1f);
                    break;

                case ParticleABValidationProfile.VelocityOverLifetimePoint:
                    profileSpecificPassed = maximumMeanVelocityError <= 0.02f &&
                                            maximumMeanPositionError <= 0.03f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0;
                    break;

                case ParticleABValidationProfile.VelocityOrbitalRadialPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.05f &&
                                            maximumMeanVelocityError <= 0.06f &&
                                            maximumMeanPositionError <= 0.04f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0;
                    break;

                case ParticleABValidationProfile.VelocitySpeedModifierPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.02f &&
                                            maximumMeanVelocityError <= 0.02f &&
                                            maximumMeanPositionError <= 0.04f &&
                                            maximumVelocitySpeedModifierKinematicsError <= 0.06f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0;
                    break;

                case ParticleABValidationProfile.LimitVelocityOverLifetimePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.03f &&
                                            maximumMeanVelocityError <= 0.03f &&
                                            maximumMeanPositionError <= 0.05f &&
                                            maximumShurikenLimitVelocityError <= 0.04f &&
                                            maximumGPULimitVelocityError <= 0.04f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            shurikenSpeedRange.Covers(2f, 10f) &&
                                            gpuSpeedRange.Covers(2f, 10f);
                    break;

                case ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.02f &&
                                            maximumMeanVelocityError <= 0.02f &&
                                            maximumMeanPositionError <= 0.04f &&
                                            maximumShurikenLimitVelocityError <= 0.02f &&
                                            maximumGPULimitVelocityError <= 0.02f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0;
                    break;

                case ParticleABValidationProfile.InheritVelocityInitialPoint:
                case ParticleABValidationProfile.InheritVelocityCurrentPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.04f &&
                                            maximumMeanVelocityError <= 0.05f &&
                                            maximumMeanPositionError <= 0.06f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            shurikenSpeedRange.Covers(0f, 2f) &&
                                            gpuSpeedRange.Covers(0f, 2f);
                    break;

                case ParticleABValidationProfile.LifetimeByEmitterSpeedPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            shurikenLifetimeRange.Covers(2f, 6f) &&
                                            gpuLifetimeRange.Covers(2f, 6f) &&
                                            maximumShurikenRotationError <= 0.05f &&
                                            maximumGPURotationError <= 0.01f;
                    break;

                case ParticleABValidationProfile.ShapeSpherePoint:
                case ParticleABValidationProfile.ShapeCirclePoint:
                case ParticleABValidationProfile.ShapeDonutPoint:
                case ParticleABValidationProfile.ShapeEdgePoint:
                case ParticleABValidationProfile.ShapeRectanglePoint:
                case ParticleABValidationProfile.ShapeBoxEdgePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenShapeDirectionError <= 0.002f &&
                                            maximumGPUShapeDirectionError <= 0.002f &&
                                            maximumShurikenShapeGeometryError <= 0.002f &&
                                            maximumGPUShapeGeometryError <= 0.002f &&
                                            ShapeSpawnRangesPass();
                    break;

                case ParticleABValidationProfile.TextureSheetLifetimePoint:
                case ParticleABValidationProfile.TextureSheetSpeedPoint:
                case ParticleABValidationProfile.TextureSheetFPSPoint:
                case ParticleABValidationProfile.TextureSheetSingleRowPoint:
                {
                    int allowedFrameMismatches = Mathf.Max(
                        1, textureSheetComparableSamples / 20);
                    int expectedFrameMask = validationProfile ==
                            ParticleABValidationProfile.TextureSheetSingleRowPoint
                        ? 0xF0
                        : 0xFF;
                    profileSpecificPassed = maximumMeanSpeedError <= 0.01f &&
                                            maximumMeanPositionError <= 0.04f &&
                                            maximumShurikenParticleCount == 1 &&
                                            maximumGPUParticleCount == 1 &&
                                            textureSheetComparableSamples >= 20 &&
                                            textureSheetClassificationFailures == 0 &&
                                            textureSheetFrameMismatches <=
                                                allowedFrameMismatches &&
                                            maximumTextureSheetFrameDelta <= 1 &&
                                            shurikenTextureSheetFrameMask ==
                                                expectedFrameMask &&
                                            gpuTextureSheetFrameMask ==
                                                expectedFrameMask;
                    break;
                }

                case ParticleABValidationProfile.RotationOverLifetimeCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenRotationError <= 0.05f &&
                                            maximumGPURotationError <= 0.01f;
                    break;

                case ParticleABValidationProfile.RotationBySpeedCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanVelocityError <= 0.001f &&
                                            maximumForceKinematicsError <= 0.005f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenRotationError <= 0.08f &&
                                            maximumGPURotationError <= 0.02f;
                    break;

                case ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenColorBoundsError <= 0.015f &&
                                            maximumGPUColorBoundsError <= 0.015f &&
                                            maximumShurikenSizeBoundsError <= 0.01f &&
                                            maximumGPUSizeBoundsError <= 0.01f &&
                                            shurikenLifetimeColorBlendRange.Covers(0f, 1f) &&
                                            gpuLifetimeColorBlendRange.Covers(0f, 1f) &&
                                            shurikenLifetimeSizeBlendRange.Covers(0f, 1f) &&
                                            gpuLifetimeSizeBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanVelocityError <= 0.001f &&
                                            maximumForceKinematicsError <= 0.005f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenColorBoundsError <= 0.005f &&
                                            maximumGPUColorBoundsError <= 0.001f &&
                                            maximumShurikenSizeBoundsError <= 0.01f &&
                                            maximumGPUSizeBoundsError <= 0.01f &&
                                            shurikenSpeedColorBlendRange.Covers(0f, 1f) &&
                                            gpuSpeedColorBlendRange.Covers(0f, 1f) &&
                                            shurikenSpeedSizeBlendRange.Covers(0f, 1f) &&
                                            gpuSpeedSizeBlendRange.Covers(0f, 1f);
                    break;

                default:
                    profileSpecificPassed = false;
                    break;
            }

            int allowedCountDelta = validationProfile ==
                    ParticleABValidationProfile.StartLifetimeCurvePoint
                ? 1
                : validationProfile ==
                    ParticleABValidationProfile.StartLifetimeTwoCurvesPoint
                    ? 6
                    : validationProfile ==
                        ParticleABValidationProfile.PlaybackLifecyclePoint
                        ? 1
                    : 0;
            float allowedMeanAgeError = validationProfile ==
                    ParticleABValidationProfile.StartLifetimeCurvePoint
                ? 0.025f
                : validationProfile ==
                    ParticleABValidationProfile.StartLifetimeTwoCurvesPoint
                    ? 0.16f
                    : validationProfile ==
                        ParticleABValidationProfile.PlaybackLifecyclePoint
                        // Shuriken exposes a zero-remaining-lifetime entry for
                        // one StopEmitting drain frame; it is not rendered, but
                        // its inclusion shifts the raw mean age for that sample.
                        ? 0.05f
                    : 0.001f;
            bool passed = maximumCountDelta <= allowedCountDelta &&
                          maximumMeanAgeError <= allowedMeanAgeError &&
                          profileSpecificPassed;
            string result = passed ? "PASS" : "FAIL";
            Debug.Log(
                $"PARTICLE_AB_CAPTURE_RESULT:{result}; " +
                $"profile={validationProfile}; " +
                $"maxCountDelta={maximumCountDelta}; " +
                $"maxMeanAgeError={maximumMeanAgeError:R}; " +
                $"maxShurikenMeanAge={maximumShurikenMeanAge:R}; " +
                $"maxGPUMeanAge={maximumGPUMeanAge:R}; " +
                $"playbackInitialStopped={playbackInitialStopped}; " +
                $"playbackStateMismatchCount={playbackStateMismatchCount}; " +
                $"playbackEmptyViolationCount={playbackEmptyViolationCount}; " +
                $"playbackTransitionMask=0x{playbackTransitionMask:X2}; " +
                $"playbackPausedShurikenAgeSpread=" +
                $"{ObservedSpread(shurikenPausedMeanAgeRange):R}; " +
                $"playbackPausedGPUAgeSpread=" +
                $"{ObservedSpread(gpuPausedMeanAgeRange):R}; " +
                $"playbackStoppedShurikenCount=" +
                $"{maximumShurikenStoppedParticleCount}; " +
                $"playbackStoppedGPUCount=" +
                $"{maximumGPUStoppedParticleCount}; " +
                $"playbackDrainObserved={playbackDrainObserved}; " +
                $"playbackClearObserved={playbackClearObserved}; " +
                $"prewarmFirstShurikenCount={prewarmFirstShurikenCount}; " +
                $"prewarmFirstGPUCount={prewarmFirstGPUCount}; " +
                $"prewarmFirstShurikenMeanAge=" +
                $"{prewarmFirstShurikenMeanAge:R}; " +
                $"prewarmFirstGPUMeanAge={prewarmFirstGPUMeanAge:R}; " +
                $"prewarmRestartShurikenCount=" +
                $"{prewarmRestartShurikenCount}; " +
                $"prewarmRestartGPUCount={prewarmRestartGPUCount}; " +
                $"prewarmRestartShurikenMeanAge=" +
                $"{prewarmRestartShurikenMeanAge:R}; " +
                $"prewarmRestartGPUMeanAge=" +
                $"{prewarmRestartGPUMeanAge:R}; " +
                $"maxMeanLifetimeError={maximumMeanLifetimeError:R}; " +
                $"maxMeanStartRotationError=" +
                $"{maximumMeanStartRotationError:R}; " +
                $"maxMeanSpeedError={maximumMeanSpeedError:R}; " +
                $"maxMeanSizeError={maximumMeanSizeError:R}; " +
                $"maxMeanSizeYError={maximumMeanSizeYError:R}; " +
                $"maxMeanVelocityError={maximumMeanVelocityError:R}; " +
                $"maxMeanPositionError={maximumMeanPositionError:R}; " +
                $"maxShurikenConeError={maximumShurikenConeError:R}; " +
                $"maxGPUConeError={maximumGPUConeError:R}; " +
                $"maxForceKinematicsError={maximumForceKinematicsError:R}; " +
                $"maxVelocitySpeedModifierKinematicsError=" +
                $"{maximumVelocitySpeedModifierKinematicsError:R}; " +
                $"maxShurikenColorBoundsError={maximumShurikenColorBoundsError:R}; " +
                $"maxGPUColorBoundsError={maximumGPUColorBoundsError:R}; " +
                $"maxShurikenStartLifetimeBoundsError=" +
                $"{maximumShurikenStartLifetimeBoundsError:R}; " +
                $"maxGPUStartLifetimeBoundsError=" +
                $"{maximumGPUStartLifetimeBoundsError:R}; " +
                $"maxShurikenStartSpeedBoundsError=" +
                $"{maximumShurikenStartSpeedBoundsError:R}; " +
                $"maxGPUStartSpeedBoundsError={maximumGPUStartSpeedBoundsError:R}; " +
                $"maxShurikenStartSizeBoundsError=" +
                $"{maximumShurikenStartSizeBoundsError:R}; " +
                $"maxGPUStartSizeBoundsError={maximumGPUStartSizeBoundsError:R}; " +
                $"maxShurikenGravityIntegralBoundsError=" +
                $"{maximumShurikenGravityIntegralBoundsError:R}; " +
                $"maxGPUGravityIntegralBoundsError=" +
                $"{maximumGPUGravityIntegralBoundsError:R}; " +
                $"maxShurikenSizeBoundsError={maximumShurikenSizeBoundsError:R}; " +
                $"maxGPUSizeBoundsError={maximumGPUSizeBoundsError:R}; " +
                $"maxShurikenRotationError={maximumShurikenRotationError:R}; " +
                $"maxGPURotationError={maximumGPURotationError:R}; " +
                $"maxShurikenLimitVelocityError={maximumShurikenLimitVelocityError:R}; " +
                $"maxGPULimitVelocityError={maximumGPULimitVelocityError:R}; " +
                $"maxShurikenShapeDirectionError={maximumShurikenShapeDirectionError:R}; " +
                $"maxGPUShapeDirectionError={maximumGPUShapeDirectionError:R}; " +
                $"maxShurikenShapeGeometryError={maximumShurikenShapeGeometryError:R}; " +
                $"maxGPUShapeGeometryError={maximumGPUShapeGeometryError:R}; " +
                $"maxShurikenCount={maximumShurikenParticleCount}; " +
                $"maxGPUCount={maximumGPUParticleCount}; " +
                $"textureSheetComparableSamples={textureSheetComparableSamples}; " +
                $"textureSheetFrameMismatches={textureSheetFrameMismatches}; " +
                $"textureSheetClassificationFailures={textureSheetClassificationFailures}; " +
                $"maxTextureSheetFrameDelta={maximumTextureSheetFrameDelta}; " +
                $"shurikenTextureSheetFrameMask=0x{shurikenTextureSheetFrameMask:X2}; " +
                $"gpuTextureSheetFrameMask=0x{gpuTextureSheetFrameMask:X2}; " +
                $"maxScreenSizePixelError={maximumScreenSizePixelError:R}; " +
                $"screenSizeClassificationFailures=" +
                $"{screenSizeClassificationFailures}; " +
                $"shurikenScreenSizePixelRange=" +
                $"{FormatRange(shurikenScreenSizePixelRange)}; " +
                $"gpuScreenSizePixelRange=" +
                $"{FormatRange(gpuScreenSizePixelRange)}; " +
                $"maxScalingWidthPixelError=" +
                $"{maximumScalingWidthPixelError:R}; " +
                $"maxScalingHeightPixelError=" +
                $"{maximumScalingHeightPixelError:R}; " +
                $"maxScalingSpawnOffsetPixelError=" +
                $"{maximumScalingSpawnOffsetPixelError:R}; " +
                $"scalingBoundsClassificationFailures=" +
                $"{scalingBoundsClassificationFailures}; " +
                $"shurikenScalingWidthPixelRange=" +
                $"{FormatRange(shurikenScalingWidthPixelRange)}; " +
                $"gpuScalingWidthPixelRange=" +
                $"{FormatRange(gpuScalingWidthPixelRange)}; " +
                $"shurikenScalingHeightPixelRange=" +
                $"{FormatRange(shurikenScalingHeightPixelRange)}; " +
                $"gpuScalingHeightPixelRange=" +
                $"{FormatRange(gpuScalingHeightPixelRange)}; " +
                $"shurikenScalingSpawnOffsetPixelRange=" +
                $"{FormatRange(shurikenScalingSpawnOffsetPixelRange)}; " +
                $"gpuScalingSpawnOffsetPixelRange=" +
                $"{FormatRange(gpuScalingSpawnOffsetPixelRange)}; " +
                $"shurikenScalingBirthXRange=" +
                $"{FormatRange(shurikenScalingBirthXRange)}; " +
                $"gpuScalingBirthXRange=" +
                $"{FormatRange(gpuScalingBirthXRange)}; " +
                $"shurikenLifetimeRange={FormatRange(shurikenLifetimeRange)}; " +
                $"gpuLifetimeRange={FormatRange(gpuLifetimeRange)}; " +
                $"shurikenSpeedRange={FormatRange(shurikenSpeedRange)}; " +
                $"gpuSpeedRange={FormatRange(gpuSpeedRange)}; " +
                $"shurikenSizeRange={FormatRange(shurikenSizeRange)}; " +
                $"gpuSizeRange={FormatRange(gpuSizeRange)}; " +
                $"shurikenColorRedRange={FormatRange(shurikenColorRedRange)}; " +
                $"gpuColorRedRange={FormatRange(gpuColorRedRange)}; " +
                $"shurikenStartRotationRange=" +
                $"{FormatRange(shurikenStartRotationRange)}; " +
                $"gpuStartRotationRange=" +
                $"{FormatRange(gpuStartRotationRange)}; " +
                $"shurikenDistancePositionRange={FormatRange(shurikenDistancePositionRange)}; " +
                $"gpuDistancePositionRange={FormatRange(gpuDistancePositionRange)}; " +
                $"shurikenShapeSpawnRanges=" +
                $"({FormatRange(shurikenShapeSpawnXRange)}," +
                $"{FormatRange(shurikenShapeSpawnYRange)}," +
                $"{FormatRange(shurikenShapeSpawnZRange)}); " +
                $"gpuShapeSpawnRanges=" +
                $"({FormatRange(gpuShapeSpawnXRange)}," +
                $"{FormatRange(gpuShapeSpawnYRange)}," +
                $"{FormatRange(gpuShapeSpawnZRange)}); " +
                $"shurikenLifetimeColorBlendRange={FormatRange(shurikenLifetimeColorBlendRange)}; " +
                $"gpuLifetimeColorBlendRange={FormatRange(gpuLifetimeColorBlendRange)}; " +
                $"shurikenLifetimeSizeBlendRange={FormatRange(shurikenLifetimeSizeBlendRange)}; " +
                $"gpuLifetimeSizeBlendRange={FormatRange(gpuLifetimeSizeBlendRange)}; " +
                $"shurikenSpeedColorBlendRange={FormatRange(shurikenSpeedColorBlendRange)}; " +
                $"gpuSpeedColorBlendRange={FormatRange(gpuSpeedColorBlendRange)}; " +
                $"shurikenSpeedSizeBlendRange={FormatRange(shurikenSpeedSizeBlendRange)}; " +
                $"gpuSpeedSizeBlendRange={FormatRange(gpuSpeedSizeBlendRange)}; " +
                $"shurikenStartColorBlendRange=" +
                $"{FormatRange(shurikenStartColorBlendRange)}; " +
                $"gpuStartColorBlendRange=" +
                $"{FormatRange(gpuStartColorBlendRange)}; " +
                $"shurikenStartLifetimeBlendRange=" +
                $"{FormatRange(shurikenStartLifetimeBlendRange)}; " +
                $"gpuStartLifetimeBlendRange=" +
                $"{FormatRange(gpuStartLifetimeBlendRange)}; " +
                $"shurikenStartSpeedBlendRange=" +
                $"{FormatRange(shurikenStartSpeedBlendRange)}; " +
                $"gpuStartSpeedBlendRange=" +
                $"{FormatRange(gpuStartSpeedBlendRange)}; " +
                $"shurikenStartSizeBlendRange=" +
                $"{FormatRange(shurikenStartSizeBlendRange)}; " +
                $"gpuStartSizeBlendRange=" +
                $"{FormatRange(gpuStartSizeBlendRange)}; " +
                $"shurikenGravityBlendRange=" +
                $"{FormatRange(shurikenGravityBlendRange)}; " +
                $"gpuGravityBlendRange=" +
                $"{FormatRange(gpuGravityBlendRange)}; " +
                $"shurikenStartRotationBlendRange=" +
                $"{FormatRange(shurikenStartRotationBlendRange)}; " +
                $"gpuStartRotationBlendRange=" +
                $"{FormatRange(gpuStartRotationBlendRange)}", this);
            Debug.Log($"PARTICLE_AB_CAPTURE_COMPLETE:{sessionFolder}", this);

#if UNITY_EDITOR
            if (exitEditorWhenCaptureCompletes && Application.isBatchMode)
            {
                UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
            }
#endif
        }

        static string FormatRange(ObservedRange range)
        {
            return range.HasSamples
                ? $"[{range.Minimum:R},{range.Maximum:R}]"
                : "[]";
        }

        bool RendererScreenSizeRangePasses(ObservedRange range)
        {
            float expectedMinimum = RendererClampMinimum * captureWidth;
            float expectedMaximum = RendererClampMaximum * captureWidth;
            return range.HasSamples &&
                   Mathf.Abs(range.Minimum - expectedMinimum) <=
                       RendererClampRangeTolerancePixels &&
                   Mathf.Abs(range.Maximum - expectedMaximum) <=
                       RendererClampRangeTolerancePixels &&
                   range.Maximum - range.Minimum >=
                       (expectedMaximum - expectedMinimum) * 0.9f;
        }

        bool ScalingModeSemanticsPass()
        {
            if (!shurikenScalingWidthPixelRange.HasSamples ||
                !gpuScalingWidthPixelRange.HasSamples ||
                !shurikenScalingHeightPixelRange.HasSamples ||
                !gpuScalingHeightPixelRange.HasSamples ||
                !shurikenScalingSpawnOffsetPixelRange.HasSamples ||
                !gpuScalingSpawnOffsetPixelRange.HasSamples ||
                !shurikenSpeedRange.HasSamples ||
                !gpuSpeedRange.HasSamples ||
                !shurikenScalingBirthXRange.HasSamples ||
                !gpuScalingBirthXRange.HasSamples)
            {
                return false;
            }

            float width = 0.5f *
                (shurikenScalingWidthPixelRange.Minimum +
                 shurikenScalingWidthPixelRange.Maximum);
            float height = 0.5f *
                (shurikenScalingHeightPixelRange.Minimum +
                 shurikenScalingHeightPixelRange.Maximum);
            if (width < 8f || height < 8f)
            {
                return false;
            }

            float aspect = height / width;
            float expectedSpeed;
            float expectedBirthX;
            switch (validationProfile)
            {
                case ParticleABValidationProfile.ScalingHierarchyPoint:
                    expectedSpeed = 8f;
                    expectedBirthX = 8f;
                    break;
                case ParticleABValidationProfile.ScalingLocalPoint:
                    expectedSpeed = 4f;
                    expectedBirthX = 4f;
                    break;
                case ParticleABValidationProfile.ScalingShapePoint:
                    expectedSpeed = 1f;
                    expectedBirthX = 8f;
                    break;
                default:
                    return false;
            }

            bool speedPasses =
                Mathf.Abs(shurikenSpeedRange.Minimum - expectedSpeed) <=
                    0.01f &&
                Mathf.Abs(shurikenSpeedRange.Maximum - expectedSpeed) <=
                    0.01f &&
                Mathf.Abs(gpuSpeedRange.Minimum - expectedSpeed) <= 0.01f &&
                Mathf.Abs(gpuSpeedRange.Maximum - expectedSpeed) <= 0.01f;
            if (!speedPasses)
            {
                return false;
            }

            bool birthPositionPasses =
                Mathf.Abs(
                    shurikenScalingBirthXRange.Minimum -
                    expectedBirthX) <= 0.02f &&
                Mathf.Abs(
                    shurikenScalingBirthXRange.Maximum -
                    expectedBirthX) <= 0.02f &&
                Mathf.Abs(
                    gpuScalingBirthXRange.Minimum - expectedBirthX) <=
                    0.02f &&
                Mathf.Abs(
                    gpuScalingBirthXRange.Maximum - expectedBirthX) <=
                    0.02f;
            if (!birthPositionPasses)
            {
                return false;
            }

            switch (validationProfile)
            {
                case ParticleABValidationProfile.ScalingHierarchyPoint:
                    return aspect >= 1.55f && aspect <= 2.2f;
                case ParticleABValidationProfile.ScalingLocalPoint:
                    return aspect >= 1.05f && aspect <= 1.5f;
                case ParticleABValidationProfile.ScalingShapePoint:
                    return aspect >= 0.75f && aspect <= 1.25f;
                default:
                    return false;
            }
        }

        static Vector3 ModuleBirthEmitterVelocity(Color moduleState)
        {
            return new Vector3(moduleState.g, moduleState.b, moduleState.a);
        }

        void ApplyDisplayMode()
        {
            if (shurikenRenderer != null)
            {
                shurikenRenderer.enabled = displayMode != ParticleABDisplayMode.GPUOnly;
            }

            if (gpuParticles != null)
            {
                gpuParticles.renderEnabled = displayMode != ParticleABDisplayMode.ShurikenOnly;
            }
        }

        void OnGUI()
        {
            const float width = 190f;
            GUILayout.BeginArea(new Rect(12f, 12f, width, 210f), GUI.skin.box);
            GUILayout.Label("Particle A/B Validation");
            GUILayout.Label($"Profile: {validationProfile}");
            GUILayout.Label("B: Both  S: Shuriken  G: GPU");
            GUILayout.Label("R: Restart  Space: Pause  C: Capture");
            GUILayout.Label($"Mode: {displayMode}");
            GUILayout.Label(captureActive ? $"Capture #{captureIndex}" : "Capture idle");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Both")) SetDisplayMode(ParticleABDisplayMode.Both);
            if (GUILayout.Button("CPU")) SetDisplayMode(ParticleABDisplayMode.ShurikenOnly);
            if (GUILayout.Button("GPU")) SetDisplayMode(ParticleABDisplayMode.GPUOnly);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Restart")) RestartPlayback();
            GUILayout.EndArea();
        }
    }
}
