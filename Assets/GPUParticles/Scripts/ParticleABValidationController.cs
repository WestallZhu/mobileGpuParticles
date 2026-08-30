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
        EmitterVelocityCustomPoint,
        EmitterVelocityRigidbodyPoint,
        CullingAutomaticLoopPoint,
        CullingAutomaticOneShotPoint,
        CullingPausePoint,
        CullingPauseAndCatchupPoint,
        CullingAlwaysSimulatePoint,
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
        ShapeRandomDirectionPoint,
        ShapeSphericalDirectionPoint,
        ShapeRandomPositionPoint,
        ShapeArcRandomSpreadPoint,
        ShapeArcLoopPoint,
        ShapeArcPingPongPoint,
        ShapeArcBurstSpreadPoint,
        NoiseCurlPositionPoint,
        NoiseSeparateAxesRemapPoint,
        NoiseRotationSizePoint,
        CollisionPlaneBouncePoint,
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
        RingBufferPausePoint,
        RingBufferLoopPoint,
        PrewarmPoint,
        FlipRotationPoint,
        GravitySource2DPoint,
        CustomSimulationSpacePoint,
        StopActionCallbackPoint,
        TextureSheetBlendLifetimePoint,
        TextureSheetBlendSpeedPoint,
        TextureSheetBlendFPSPoint,
        MaterialColorModesPoint,
        MaterialBlendModesPoint,
        MaterialAlphaClipPoint,
        MaterialSoftParticlesPoint,
        MaterialCameraFadingPoint,
        RendererTextureUVFlipPoint,
        StretchedBillboardPoint
    }

    [DisallowMultipleComponent]
    public sealed class ParticleStopActionObserver : MonoBehaviour
    {
        public int CallbackCount { get; private set; }
        public int FirstCallbackFrame { get; private set; } = -1;
        public int LastCallbackFrame { get; private set; } = -1;

        public void ResetObservation()
        {
            CallbackCount = 0;
            FirstCallbackFrame = -1;
            LastCallbackFrame = -1;
        }

        public void OnParticleSystemStopped()
        {
            if (CallbackCount == 0)
            {
                FirstCallbackFrame = Time.frameCount;
            }
            CallbackCount++;
            LastCallbackFrame = Time.frameCount;
        }
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
        float maximumShurikenShapeArcGridError;
        float maximumGPUShapeArcGridError;
        int shurikenShapeArcBinMask;
        int gpuShapeArcBinMask;
        float maximumShurikenNoiseKinematicsError;
        float maximumGPUNoiseKinematicsError;
        float maximumShurikenNoiseRotationError;
        float maximumGPUNoiseRotationError;
        float maximumGPUNoiseSizeError;
        float maximumNoiseSizePixelError;
        int noiseSizeClassificationFailures;
        bool hasCurrentShurikenNoiseSizeBounds;
        MarkerPixelBounds currentShurikenNoiseSizeBounds;
        int maximumShurikenParticleCount;
        int maximumGPUParticleCount;
        int textureSheetComparableSamples;
        int textureSheetFrameMismatches;
        int textureSheetClassificationFailures;
        int maximumTextureSheetFrameDelta;
        int shurikenTextureSheetFrameMask;
        int gpuTextureSheetFrameMask;
        int textureSheetBlendComparableSamples;
        int textureSheetBlendClassificationFailures;
        int shurikenTextureSheetBlendIntermediateSamples;
        int gpuTextureSheetBlendIntermediateSamples;
        float maximumTextureSheetBlendColorError;
        bool hasCurrentShurikenTextureSheetBlendColor;
        Color currentShurikenTextureSheetBlendColor;
        int textureUVFlipComparableSamples;
        int textureUVFlipClassificationFailures;
        int textureUVFlipSemanticFailures;
        float maximumTextureUVFlipColorError;
        float maximumTextureUVFlipExpectedColorError;
        bool hasCurrentShurikenTextureUVFlipColors;
        readonly Color[] currentShurikenTextureUVFlipColors = new Color[4];
        int stretchedBillboardComparableSamples;
        int stretchedBillboardClassificationFailures;
        int shurikenStretchedBillboardStateMask;
        int gpuStretchedBillboardStateMask;
        float maximumStretchedBillboardCentroidError;
        float maximumStretchedBillboardAspectError;
        bool hasCurrentShurikenStretchedBillboardSignature;
        StretchedBillboardSignature currentShurikenStretchedBillboardSignature;
        int currentShurikenStretchedBillboardState = -1;
        int activeStretchedBillboardState;
        readonly Vector2[] shurikenStretchedStateSignatureSums =
            new Vector2[3];
        readonly Vector2[] gpuStretchedStateSignatureSums =
            new Vector2[3];
        readonly int[] shurikenStretchedStateSignatureSamples = new int[3];
        readonly int[] gpuStretchedStateSignatureSamples = new int[3];
        int materialColorComparableSamples;
        int materialColorClassificationFailures;
        int shurikenMaterialColorModeMask;
        int gpuMaterialColorModeMask;
        float maximumMaterialColorError;
        readonly float[] maximumMaterialColorModeErrors = new float[6];
        readonly Vector3[] shurikenMaterialColorSums = new Vector3[6];
        readonly int[] shurikenMaterialColorSamples = new int[6];
        bool hasCurrentShurikenMaterialColor;
        Color currentShurikenMaterialColor;
        int currentShurikenMaterialColorMode = -1;
        int materialBlendComparableSamples;
        int materialBlendClassificationFailures;
        int shurikenMaterialBlendModeMask;
        int gpuMaterialBlendModeMask;
        float maximumMaterialBlendError;
        readonly float[] maximumMaterialBlendModeErrors = new float[4];
        readonly Vector3[] shurikenMaterialBlendSums = new Vector3[4];
        readonly int[] shurikenMaterialBlendSamples = new int[4];
        bool hasCurrentShurikenMaterialBlend;
        Color currentShurikenMaterialBlendColor;
        int currentShurikenMaterialBlendMode = -1;
        int activeMaterialBlendMode;
        int materialAlphaClipComparableSamples;
        int materialAlphaClipClassificationFailures;
        int shurikenMaterialAlphaClipStateMask;
        int gpuMaterialAlphaClipStateMask;
        float maximumMaterialAlphaClipWidthError;
        readonly float[] maximumMaterialAlphaClipStateErrors = new float[4];
        readonly float[] shurikenMaterialAlphaClipWidthSums = new float[4];
        readonly int[] shurikenMaterialAlphaClipSamples = new int[4];
        bool hasCurrentShurikenMaterialAlphaClipBounds;
        float currentShurikenMaterialAlphaClipWidth = -1f;
        int currentShurikenMaterialAlphaClipState = -1;
        int activeMaterialAlphaClipState;
        int materialSoftParticleComparableSamples;
        int materialSoftParticleClassificationFailures;
        int shurikenMaterialSoftParticleStateMask;
        int gpuMaterialSoftParticleStateMask;
        float maximumMaterialSoftParticleColorError;
        readonly float[] maximumMaterialSoftParticleStateErrors = new float[4];
        readonly Vector3[] shurikenMaterialSoftParticleColorSums =
            new Vector3[4];
        readonly int[] shurikenMaterialSoftParticleSamples = new int[4];
        bool hasCurrentShurikenMaterialSoftParticleColor;
        Color currentShurikenMaterialSoftParticleColor;
        int currentShurikenMaterialSoftParticleState = -1;
        int activeMaterialSoftParticleState;
        int materialCameraFadeComparableSamples;
        int materialCameraFadeClassificationFailures;
        int shurikenMaterialCameraFadeStateMask;
        int gpuMaterialCameraFadeStateMask;
        float maximumMaterialCameraFadeColorError;
        readonly float[] maximumMaterialCameraFadeStateErrors = new float[4];
        readonly Vector3[] shurikenMaterialCameraFadeColorSums =
            new Vector3[4];
        readonly int[] shurikenMaterialCameraFadeSamples = new int[4];
        bool hasCurrentShurikenMaterialCameraFadeColor;
        Color currentShurikenMaterialCameraFadeColor;
        int currentShurikenMaterialCameraFadeState = -1;
        int activeMaterialCameraFadeState;
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
        bool shurikenRingBufferReplacementObserved;
        bool gpuRingBufferReplacementObserved;
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
        bool cullingOffscreenObserved;
        bool cullingReturnObserved;
        int cullingReturnShurikenCount;
        int cullingReturnGPUCount;
        float cullingReturnShurikenMeanAge;
        float cullingReturnGPUMeanAge;
        ParticleStopActionObserver shurikenStopActionObserver;
        ParticleStopActionObserver gpuStopActionObserver;
        ParticleSystemSync stopActionSync;
        bool stopActionRestartPlayingObserved;
        GPUParticleSystem stopActionDisableProbe;
        GPUParticleSystem stopActionDestroyProbe;
        GameObject stopActionDisableTarget;
        GameObject stopActionDestroyTarget;
        bool stopActionDisableObserved;
        bool stopActionDestroyObserved;
        bool gravityOverrideActive;
        Vector3 savedPhysicsGravity;
        Vector2 savedPhysics2DGravity;
        GameObject shurikenCustomSpaceObject;
        GameObject gpuCustomSpaceObject;
        GameObject collisionPlaneObject;
        Material collisionPlaneMaterial;
        GameObject softParticleBackdropObject;
        Material softParticleBackdropMaterial;
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
        Texture2D profileMaterialColorTexture;
        Material profileMaterialColorMaterial;
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
        Texture2D profileShapeArcSpeedLUT;
        Texture2D profileNoiseStrengthLUT;
        Texture2D profileNoiseAmountsLUT;
        Texture2D profileNoiseRemapLUT;
        Texture2D profileCollisionParametersLUT;
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
        ObservedRange shurikenTextureSheetBlendRedRange;
        ObservedRange shurikenTextureSheetBlendGreenRange;
        ObservedRange gpuTextureSheetBlendRedRange;
        ObservedRange gpuTextureSheetBlendGreenRange;
        AnimationCurve profileSizeMinimumCurve;
        AnimationCurve profileSizeMaximumCurve;
        static readonly Vector3 ValidationForce = new Vector3(2f, -1f, 0.5f);
        static readonly Color MaterialColorTextureColor =
            new Color(0.55f, 0.9f, 0.35f, 1f);
        static readonly Color MaterialColorBaseColor =
            new Color(0.65f, 0.8f, 0.7f, 1f);
        static readonly Color MaterialColorParticleColor =
            new Color(0.8f, 0.18f, 0.62f, 1f);
        const float MaterialColorModeInterval = 0.5f;
        const int MaterialColorModeCount = 6;
        static readonly Color MaterialBlendTextureColor =
            new Color(0.28f, 0.74f, 0.9f, 0.62f);
        static readonly Color MaterialBlendBaseColor =
            new Color(0.82f, 0.68f, 0.42f, 0.78f);
        static readonly Color MaterialBlendParticleColor =
            new Color(0.72f, 0.28f, 0.86f, 0.76f);
        static readonly Color MaterialBlendBackgroundColor =
            new Color(0.16f, 0.32f, 0.5f, 1f);
        const float MaterialBlendModeInterval = 0.6f;
        const int MaterialBlendModeCount = 4;
        static readonly Color MaterialAlphaClipTextureColor =
            new Color(0.95f, 0.08f, 0.9f, 1f);
        static readonly Color MaterialAlphaClipBackgroundColor =
            new Color(0.08f, 0.18f, 0.28f, 1f);
        static readonly float[] MaterialAlphaClipCutoffs =
        {
            0f,
            0.25f,
            0.5f,
            0.75f
        };
        const float MaterialAlphaClipStateInterval = 0.6f;
        const int MaterialAlphaClipStateCount = 4;
        static readonly Color MaterialSoftParticleTextureColor =
            new Color(0.9f, 0.22f, 0.08f, 0.8f);
        static readonly Color MaterialSoftParticleBackgroundColor =
            new Color(0.08f, 0.15f, 0.24f, 1f);
        static readonly float[] MaterialSoftParticleDepthGaps =
        {
            0.2f,
            0.2f,
            0.6f,
            1.2f
        };
        const float MaterialSoftParticleStateInterval = 0.6f;
        const int MaterialSoftParticleStateCount = 4;
        const float MaterialSoftParticleCameraDepth = 12f;
        static readonly Color MaterialCameraFadeTextureColor =
            new Color(0.9f, 0.22f, 0.08f, 0.8f);
        static readonly Color MaterialCameraFadeBackgroundColor =
            new Color(0.08f, 0.15f, 0.24f, 1f);
        static readonly float[] MaterialCameraFadeDepths =
        {
            1.4f,
            1.4f,
            2f,
            3.2f
        };
        const float MaterialCameraFadeNearDistance = 1f;
        const float MaterialCameraFadeFarDistance = 3f;
        const float MaterialCameraFadeStateInterval = 0.6f;
        const int MaterialCameraFadeStateCount = 4;
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
        const float FlipRotationStartRadians = 0.5235988f;
        const float FlipRotationLifetimeRadiansPerSecond = 0.7853982f;
        const float FlipRotationBySpeedRadiansPerSecond = 0.5235988f;
        const float GravitySourceModifier = 1.25f;
        static readonly Vector3 GravitySourcePhysics3D =
            new Vector3(6f, 2f, 5f);
        static readonly Vector2 GravitySourcePhysics2D =
            new Vector2(-3f, -4f);
        static readonly Vector3 CustomSpaceBaseOffset =
            new Vector3(0.75f, 0.4f, 0.2f);
        static readonly Vector3 CustomSpaceBaseEuler =
            new Vector3(18f, -28f, 32f);
        static readonly Vector3 CustomSpaceBaseScale =
            new Vector3(1.2f, 0.8f, 1.4f);
        const float RendererClampMinimum = 0.04f;
        const float RendererClampMaximum = 0.12f;
        const float RendererClampTravel = 120f;
        const float RendererClampRangeTolerancePixels = 4f;
        const float RendererClampPairTolerancePixels = 2f;
        const float ScalingPairTolerancePixels = 5f;
        const float UnscaledTimeScale = 0f;
        const float CollisionPlaneHeight = -1.5f;
        const float CollisionParticleRadius = 0.2f;
        const int PlaybackPlayFrame = 31;
        const int PlaybackPauseFrame = 77;
        const int PlaybackResumeFrame = 107;
        const int PlaybackStopEmittingFrame = 151;
        const int PlaybackDrainExpectedFrame = 230;
        const int PlaybackReplayFrame = 241;
        const int PlaybackClearFrame = 260;
        const float RingBufferLifetime = 0.8f;
        const float RingBufferReplacementTime = 2f;
        const float RingBufferObservationStart = 0.9f;
        const float RingBufferObservationEnd = 1.9f;
        const float RingBufferStartSpeed = 0.75f;
        static readonly Vector2 RingBufferLoopRange =
            new Vector2(0.25f, 0.75f);
        const int PrewarmRestartStopFrame = 31;
        const int PrewarmRestartPlayFrame = 32;
        const int PrewarmRestartCaptureFrame = 36;
        const int StopActionRestartFrame = 75;
        const int StopActionExplicitProbeFrame = 20;
        const float CullingExitViewTime = 0.5f;
        const float CullingReturnTime = 1.5f;
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
        static readonly int[] TextureUVFlipExpectedPaletteIndices =
        {
            3, 2, 1, 0
        };
        const float StretchedBillboardStateInterval = 1.2f;
        Vector3 shurikenBasePositionWS;
        Vector3 gpuBasePositionWS;
        Vector3 captureCameraBasePositionWS;
        ObservedRange shurikenCustomWorldXRange;
        ObservedRange gpuCustomWorldXRange;
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
        ObservedRange shurikenShapeDirectionXRange;
        ObservedRange shurikenShapeDirectionYRange;
        ObservedRange shurikenShapeDirectionZRange;
        ObservedRange gpuShapeDirectionXRange;
        ObservedRange gpuShapeDirectionYRange;
        ObservedRange gpuShapeDirectionZRange;
        ObservedRange shurikenShapeArcAngleRange;
        ObservedRange gpuShapeArcAngleRange;
        ObservedRange shurikenNoiseXRange;
        ObservedRange shurikenNoiseYRange;
        ObservedRange shurikenNoiseZRange;
        ObservedRange gpuNoiseXRange;
        ObservedRange gpuNoiseYRange;
        ObservedRange gpuNoiseZRange;
        ObservedRange shurikenCollisionHeightRange;
        ObservedRange gpuCollisionHeightRange;
        ObservedRange shurikenCollisionVelocityYRange;
        ObservedRange gpuCollisionVelocityYRange;
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
        ObservedRange shurikenRingBufferAgeRange;
        ObservedRange gpuRingBufferAgeRange;
        ObservedRange shurikenRingBufferDisplacementRange;
        ObservedRange gpuRingBufferDisplacementRange;
        ObservedRange shurikenCulledCountRange;
        ObservedRange gpuCulledCountRange;
        ObservedRange shurikenCulledMeanAgeRange;
        ObservedRange gpuCulledMeanAgeRange;

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

        struct StretchedBillboardSignature
        {
            public bool Valid;
            public float AspectRatio;
            public Vector2[] ColorCentroids;
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
            captureCameraBasePositionWS = captureCamera != null
                ? captureCamera.transform.position
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
            if (profileMaterialColorTexture != null)
            {
                Destroy(profileMaterialColorTexture);
            }
            if (profileMaterialColorMaterial != null)
            {
                Destroy(profileMaterialColorMaterial);
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
            if (profileShapeArcSpeedLUT != null)
            {
                Destroy(profileShapeArcSpeedLUT);
            }
            if (profileNoiseStrengthLUT != null)
            {
                Destroy(profileNoiseStrengthLUT);
            }
            if (profileNoiseAmountsLUT != null)
            {
                Destroy(profileNoiseAmountsLUT);
            }
            if (profileNoiseRemapLUT != null)
            {
                Destroy(profileNoiseRemapLUT);
            }
            if (profileCollisionParametersLUT != null)
            {
                Destroy(profileCollisionParametersLUT);
            }
            if (collisionPlaneMaterial != null)
            {
                Destroy(collisionPlaneMaterial);
            }
            if (collisionPlaneObject != null)
            {
                Destroy(collisionPlaneObject);
            }
            if (softParticleBackdropMaterial != null)
            {
                Destroy(softParticleBackdropMaterial);
            }
            if (softParticleBackdropObject != null)
            {
                Destroy(softParticleBackdropObject);
            }
            RestoreGravityOverride();
            DestroyCustomSimulationSpaces();
            DestroyStopActionProbes();
            ReleaseCameraCaptureTarget();
        }

        void Update()
        {
            UpdatePlaybackLifecycle();
            UpdatePrewarmLifecycle();
            UpdateStopActionLifecycle();

            if (captureActive && IsMaterialColorProfile())
            {
                float nextSimulationTime =
                    (playbackFrame + 1f) / fixedFrameRate;
                UpdateMaterialColorMode(nextSimulationTime);
            }
            if (captureActive && IsMaterialBlendProfile())
            {
                float nextSimulationTime =
                    (playbackFrame + 1f) / fixedFrameRate;
                UpdateMaterialBlendMode(nextSimulationTime);
            }
            if (captureActive && IsMaterialAlphaClipProfile())
            {
                float nextSimulationTime =
                    (playbackFrame + 1f) / fixedFrameRate;
                UpdateMaterialAlphaClipState(nextSimulationTime);
            }
            if (captureActive && IsMaterialSoftParticlesProfile())
            {
                float nextSimulationTime =
                    (playbackFrame + 1f) / fixedFrameRate;
                UpdateMaterialSoftParticleState(nextSimulationTime);
            }
            if (captureActive && IsMaterialCameraFadingProfile())
            {
                float nextSimulationTime =
                    (playbackFrame + 1f) / fixedFrameRate;
                UpdateMaterialCameraFadingState(nextSimulationTime);
            }
            if (captureActive && IsStretchedBillboardProfile())
            {
                float nextSimulationTime =
                    (playbackFrame + 1f) / fixedFrameRate;
                UpdateStretchedBillboardState(nextSimulationTime);
            }

            if (captureActive && UsesMovingEmitterProfile())
            {
                float nextSimulationTime = (playbackFrame + 1f) / fixedFrameRate;
                MoveValidationEmitters(nextSimulationTime);
            }
            if (captureActive && IsCustomSimulationSpaceProfile())
            {
                float nextSimulationTime = (playbackFrame + 1f) / fixedFrameRate;
                MoveValidationCustomSpaces(nextSimulationTime);
            }
            if (captureActive && IsCullingProfile())
            {
                float nextSimulationTime =
                    (playbackFrame + 1f) / fixedFrameRate;
                MoveValidationCamera(nextSimulationTime);
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

            if (IsCullingProfile() && captureCamera != null)
            {
                // Shuriken updates culling visibility from actual camera
                // renders. Batch mode has no GameView render, so submit one
                // deterministic visibility render every validation frame.
                captureCamera.Render();
            }

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

            ObserveStopActionLifecycle();

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
            ResetMaterialParameters();

            if (IsPlaybackLifecycleProfile())
            {
                ConfigurePlaybackLifecycleProfile();
                return;
            }

            if (IsRingBufferProfile())
            {
                ConfigureRingBufferProfile();
                return;
            }

            if (IsStopActionProfile())
            {
                ConfigureStopActionProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.PrewarmPoint)
            {
                ConfigurePrewarmProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.FlipRotationPoint)
            {
                ConfigureFlipRotationProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.GravitySource2DPoint)
            {
                ConfigureGravitySource2DProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.CustomSimulationSpacePoint)
            {
                ConfigureCustomSimulationSpaceProfile();
                return;
            }

            if (IsCullingProfile())
            {
                ConfigureCullingProfile();
                return;
            }

            if (IsShapeProfile())
            {
                ConfigureShapeProfile();
                return;
            }

            if (IsNoiseProfile())
            {
                ConfigureNoiseProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.CollisionPlaneBouncePoint)
            {
                ConfigureCollisionPlaneProfile();
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

            if (validationProfile ==
                ParticleABValidationProfile.RendererTextureUVFlipPoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.Lifetime);
                ConfigureRendererTextureUVFlipProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.StretchedBillboardPoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.Lifetime);
                ConfigureRendererTextureUVFlipProfile();
                ConfigureStretchedBillboardProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.TextureSheetBlendLifetimePoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.Lifetime);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.TextureSheetBlendSpeedPoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.Speed);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.TextureSheetBlendFPSPoint)
            {
                ConfigureTextureSheetAnimationProfile(
                    ParticleSystemAnimationTimeMode.FPS);
                return;
            }

            if (IsMaterialColorProfile())
            {
                ConfigureMaterialColorProfile();
                return;
            }

            if (IsMaterialBlendProfile())
            {
                ConfigureMaterialBlendProfile();
                return;
            }

            if (IsMaterialAlphaClipProfile())
            {
                ConfigureMaterialAlphaClipProfile();
                return;
            }

            if (IsMaterialSoftParticlesProfile())
            {
                ConfigureMaterialSoftParticlesProfile();
                return;
            }

            if (IsMaterialCameraFadingProfile())
            {
                ConfigureMaterialCameraFadingProfile();
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
                ParticleABValidationProfile.EmitterVelocityCustomPoint)
            {
                ConfigureEmitterVelocityProfile(
                    ParticleSystemEmitterVelocityMode.Custom);
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.EmitterVelocityRigidbodyPoint)
            {
                ConfigureEmitterVelocityProfile(
                    ParticleSystemEmitterVelocityMode.Rigidbody);
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
            main.flipRotation = 0f;
            main.gravityModifier = 0f;
            main.gravitySource = ParticleSystemGravitySource.Physics3D;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.customSimulationSpace = null;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = shuriken.shape;
            shape.enabled = false;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.arcSpread = 0f;
            shape.arcSpeed = 1f;
            shape.randomDirectionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;
            shape.randomPositionAmount = 0f;

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
            var noise = shuriken.noise;
            noise.enabled = false;
            var collision = shuriken.collision;
            collision.enabled = false;
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
            gpuParticles.gravitySource = ParticleSystemGravitySource.Physics3D;
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.customSimulationSpace = null;
            gpuParticles.stopAction = ParticleSystemStopAction.None;
            gpuParticles.stopActionTarget = null;
            gpuParticles.colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.colorOverLifetimeLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeOverLifetimeSeparateAxes = false;
            gpuParticles.sizeOverLifetimeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.sizeOverLifetimeYLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.shapeType = ShapeTypeGPU.Point;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.alignToDirection = false;
            gpuParticles.shapeRandomDirectionAmount = 0f;
            gpuParticles.shapeSphericalDirectionAmount = 0f;
            gpuParticles.shapeRandomPositionAmount = 0f;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.shapeArcMode = ShapeArcModeGPU.Random;
            gpuParticles.shapeArcSpread = 0f;
            gpuParticles.shapeArcSpeedMode =
                ParticleSystemCurveMode.Constant;
            gpuParticles.shapeArcSpeedIntegralLUT =
                CurveLUTBuilder.GetDefaultLinear01LUT();
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

        void ConfigureEmitterVelocityProfile(
            ParticleSystemEmitterVelocityMode mode)
        {
            ConfigureEmissionPointBase(4f, true);

            Vector3 emitterVelocity = mode ==
                    ParticleSystemEmitterVelocityMode.Custom
                ? new Vector3(1.5f, 0.75f, 0f)
                : new Vector3(1.25f, 0.5f, 0f);

            var main = shuriken.main;
            main.startLifetime = 3f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.emitterVelocityMode = mode;
            if (mode == ParticleSystemEmitterVelocityMode.Custom)
            {
                main.emitterVelocity = emitterVelocity;
            }

            gpuParticles.SetStartLifetimeRange(3f, 3f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.simulationSpace = SimulationSpace.World;
            gpuParticles.emitterVelocityMode = mode;
            gpuParticles.customEmitterVelocity = emitterVelocity;
            gpuParticles.emitterVelocitySource = mode ==
                    ParticleSystemEmitterVelocityMode.Rigidbody
                ? shuriken
                : null;

            if (mode == ParticleSystemEmitterVelocityMode.Rigidbody)
            {
                Rigidbody body = shuriken.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = shuriken.gameObject.AddComponent<Rigidbody>();
                }
                body.useGravity = false;
                body.drag = 0f;
                body.angularDrag = 0f;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.velocity = emitterVelocity;
            }

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 8f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(
                emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(
                Array.Empty<ParticleSystem.Burst>());

            var inherit = shuriken.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = 1f;

            if (profileInheritVelocityLUT != null)
            {
                Destroy(profileInheritVelocityLUT);
            }
            profileInheritVelocityLUT = CurveLUTBuilder.BuildSigned(
                inherit.curve,
                assetName: "EmitterVelocity_Profile_LUT");
            gpuParticles.inheritVelocityEnabled = true;
            gpuParticles.inheritVelocityMode = inherit.mode;
            gpuParticles.inheritVelocityLUT = profileInheritVelocityLUT;
        }

        void ConfigureCullingProfile()
        {
            bool oneShot = validationProfile ==
                ParticleABValidationProfile.CullingAutomaticOneShotPoint;
            float duration = oneShot ? 0.6f : 4f;
            ConfigureEmissionPointBase(duration, !oneShot);

            ParticleSystemCullingMode mode = CullingModeForProfile();
            var main = shuriken.main;
            main.cullingMode = mode;
            main.startLifetime = oneShot ? 0.6f : 4f;
            main.playOnAwake = true;
            main.prewarm = false;
            gpuParticles.cullingMode = mode;
            gpuParticles.SetStartLifetimeRange(
                main.startLifetime.constant,
                main.startLifetime.constant);
            gpuParticles.playOnAwake = true;
            gpuParticles.prewarm = false;

            var emission = shuriken.emission;
            emission.rateOverTime = oneShot ? 20f : 12f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(
                emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(
                Array.Empty<ParticleSystem.Burst>());

            Bounds bounds = new Bounds(
                Vector3.zero,
                new Vector3(4f, 4f, 4f));
            if (shurikenRenderer != null)
            {
                shurikenRenderer.localBounds = bounds;
            }
            gpuParticles.localCullingBounds = bounds;
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

        void ConfigureNoiseProfile()
        {
            bool curlProfile = validationProfile ==
                ParticleABValidationProfile.NoiseCurlPositionPoint;
            bool axesProfile = validationProfile ==
                ParticleABValidationProfile.NoiseSeparateAxesRemapPoint;
            ConfigureEmissionPointBase(4f, curlProfile);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = curlProfile ? 0.3f : 1.5f;
            main.startRotation = 0f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(
                main.startSize.constant,
                main.startSize.constant);
            gpuParticles.SetStartRotationRange(0f, 0f);

            var emission = shuriken.emission;
            emission.rateOverDistance = 0f;
            ParticleSystem.Burst[] bursts;
            if (curlProfile)
            {
                emission.rateOverTime = 12f;
                bursts = Array.Empty<ParticleSystem.Burst>();
            }
            else
            {
                emission.rateOverTime = 0f;
                bursts = new[] { new ParticleSystem.Burst(0f, 1) };
            }
            emission.SetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(bursts);

            var noise = shuriken.noise;
            noise.enabled = true;
            noise.separateAxes = axesProfile;
            noise.frequency = curlProfile ? 0.45f : 0.5f;
            noise.damping = curlProfile;
            noise.quality = curlProfile
                ? ParticleSystemNoiseQuality.High
                : ParticleSystemNoiseQuality.Medium;
            noise.octaveCount = curlProfile ? 2 : 1;
            noise.octaveMultiplier = 0.5f;
            noise.octaveScale = 2f;

            ParticleSystem.MinMaxCurve strengthX;
            ParticleSystem.MinMaxCurve strengthY;
            ParticleSystem.MinMaxCurve strengthZ;
            if (curlProfile)
            {
                var strength = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.25f, 1f, 0.65f));
                noise.strength = strength;
                strengthX = strength;
                strengthY = strength;
                strengthZ = strength;
            }
            else if (axesProfile)
            {
                strengthX = new ParticleSystem.MinMaxCurve(1f);
                strengthY = new ParticleSystem.MinMaxCurve(0f);
                strengthZ = new ParticleSystem.MinMaxCurve(0f);
                noise.strengthX = strengthX;
                noise.strengthY = strengthY;
                noise.strengthZ = strengthZ;
            }
            else
            {
                var strength = new ParticleSystem.MinMaxCurve(1f);
                noise.strength = strength;
                strengthX = strength;
                strengthY = strength;
                strengthZ = strength;
            }

            ParticleSystem.MinMaxCurve positionAmount =
                new ParticleSystem.MinMaxCurve(
                    validationProfile ==
                    ParticleABValidationProfile.NoiseRotationSizePoint
                        ? 0f
                        : 1f);
            ParticleSystem.MinMaxCurve rotationAmount =
                new ParticleSystem.MinMaxCurve(
                    validationProfile ==
                    ParticleABValidationProfile.NoiseRotationSizePoint
                        ? 90f
                        : 0f);
            ParticleSystem.MinMaxCurve sizeAmount =
                new ParticleSystem.MinMaxCurve(
                    validationProfile ==
                    ParticleABValidationProfile.NoiseRotationSizePoint
                        ? 0.5f
                        : 0f);
            ParticleSystem.MinMaxCurve scrollSpeed =
                new ParticleSystem.MinMaxCurve(curlProfile ? 0.2f : 0f);
            noise.positionAmount = positionAmount;
            noise.rotationAmount = rotationAmount;
            noise.sizeAmount = sizeAmount;
            noise.scrollSpeed = scrollSpeed;

            bool remapEnabled = !curlProfile;
            noise.remapEnabled = remapEnabled;
            ParticleSystem.MinMaxCurve remap = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Constant(0f, 1f, 1f));
            if (remapEnabled)
            {
                if (noise.separateAxes)
                {
                    noise.remapX = remap;
                    noise.remapY = remap;
                    noise.remapZ = remap;
                }
                else
                {
                    noise.remap = remap;
                }
            }

            gpuParticles.noiseEnabled = true;
            gpuParticles.noiseSeparateAxes = noise.separateAxes;
            gpuParticles.noiseFrequency = noise.frequency;
            gpuParticles.noiseDamping = noise.damping;
            gpuParticles.noiseQuality = noise.quality;
            gpuParticles.noiseOctaveCount = noise.octaveCount;
            gpuParticles.noiseOctaveMultiplier = noise.octaveMultiplier;
            gpuParticles.noiseOctaveScale = noise.octaveScale;
            gpuParticles.noiseRemapEnabled = remapEnabled;

            if (profileNoiseStrengthLUT != null)
            {
                Destroy(profileNoiseStrengthLUT);
            }
            if (profileNoiseAmountsLUT != null)
            {
                Destroy(profileNoiseAmountsLUT);
            }
            if (profileNoiseRemapLUT != null)
            {
                Destroy(profileNoiseRemapLUT);
            }
            profileNoiseStrengthLUT = MinMaxCurveVector3LUTBuilder.Build(
                strengthX,
                strengthY,
                strengthZ,
                assetName: "NoiseStrength_Profile_LUT");
            profileNoiseAmountsLUT = MinMaxCurveVector3LUTBuilder.Build(
                positionAmount,
                rotationAmount,
                sizeAmount,
                scrollSpeed,
                assetName: "NoiseAmounts_Profile_LUT");
            profileNoiseRemapLUT = remapEnabled
                ? MinMaxCurveVector3LUTBuilder.Build(
                    remap,
                    remap,
                    remap,
                    assetName: "NoiseRemap_Profile_LUT")
                : null;
            gpuParticles.noiseStrengthLUT = profileNoiseStrengthLUT;
            gpuParticles.noiseAmountsLUT = profileNoiseAmountsLUT;
            gpuParticles.noiseRemapLUT = profileNoiseRemapLUT != null
                ? profileNoiseRemapLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultSignedIdentityLUT();
        }

        bool IsNoiseProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.NoiseCurlPositionPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.NoiseSeparateAxesRemapPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.NoiseRotationSizePoint;
        }

        void ConfigureCollisionPlaneProfile()
        {
            const float lifetime = 3.5f;
            const float startSpeed = 3f;
            const float startSize = CollisionParticleRadius * 2f;
            const float dampen = 0.2f;
            const float bounce = 0.6f;
            const float lifetimeLoss = 0.25f;

            ConfigureEmissionPointBase(4f, true);
            Transform plane = EnsureCollisionPlane();
            Quaternion downwardRotation = Quaternion.Euler(90f, 0f, 0f);
            shuriken.transform.rotation = downwardRotation;
            gpuParticles.transform.rotation = downwardRotation;

            ParticleSystem.MainModule main = shuriken.main;
            main.maxParticles = 64;
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = shuriken.emission;
            emission.rateOverTime = 5f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.ShapeModule shape = shuriken.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 0f;
            shape.radiusThickness = 1f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;

            ParticleSystem.CollisionModule collision = shuriken.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.dampen = dampen;
            collision.bounce = bounce;
            collision.lifetimeLoss = lifetimeLoss;
            collision.minKillSpeed = 0.1f;
            collision.maxKillSpeed = 10f;
            collision.radiusScale = 1f;
            collision.sendCollisionMessages = false;
            collision.SetPlane(0, plane);

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(startSpeed, startSpeed);
            gpuParticles.SetStartSizeRange(startSize, startSize);
            gpuParticles.simulationSpace = SimulationSpace.World;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(
                System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.shapeType = ShapeTypeGPU.Cone;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.shapeConeAngle = 0f;
            gpuParticles.shapeConeRadius = 0f;
            gpuParticles.shapeRadiusThickness = 1f;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;

            gpuParticles.collisionEnabled = true;
            gpuParticles.collisionType = ParticleSystemCollisionType.Planes;
            gpuParticles.collisionPlanes = new[] { plane };
            gpuParticles.collisionMinKillSpeed = collision.minKillSpeed;
            gpuParticles.collisionMaxKillSpeed = collision.maxKillSpeed;
            gpuParticles.collisionRadiusScale = collision.radiusScale;
            if (profileCollisionParametersLUT != null)
            {
                Destroy(profileCollisionParametersLUT);
            }
            profileCollisionParametersLUT =
                MinMaxCurveVector3LUTBuilder.Build(
                    collision.dampen,
                    collision.bounce,
                    collision.lifetimeLoss,
                    assetName: "CollisionParameters_Profile_LUT");
            gpuParticles.collisionParametersLUT =
                profileCollisionParametersLUT;
        }

        Transform EnsureCollisionPlane()
        {
            if (collisionPlaneObject == null)
            {
                collisionPlaneObject = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                collisionPlaneObject.name = "ParticleAB_CollisionPlane";
                Collider collider = collisionPlaneObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Shader shader = Shader.Find(
                    "Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    collisionPlaneMaterial = new Material(shader)
                    {
                        name = "ParticleAB_CollisionPlane_Material",
                        hideFlags = HideFlags.DontSave
                    };
                    collisionPlaneMaterial.SetColor(
                        "_BaseColor",
                        new Color(0.16f, 0.2f, 0.24f, 1f));
                    collisionPlaneObject.GetComponent<MeshRenderer>()
                        .sharedMaterial = collisionPlaneMaterial;
                }
            }

            collisionPlaneObject.transform.position =
                new Vector3(0f, CollisionPlaneHeight, 0.5f);
            collisionPlaneObject.transform.rotation = Quaternion.identity;
            collisionPlaneObject.transform.localScale =
                new Vector3(12f, 0.05f, 0.1f);
            return collisionPlaneObject.transform;
        }

        void ConfigureTextureSheetAnimationProfile(
            ParticleSystemAnimationTimeMode timeMode)
        {
            const float lifetime = 4f;
            bool singleRow = validationProfile ==
                ParticleABValidationProfile.TextureSheetSingleRowPoint;
            bool frameBlending = IsTextureSheetBlendProfile();
            int tilesX = frameBlending ? 2 : 4;
            int tilesY = frameBlending ? 1 : 2;
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
            textureSheet.numTilesX = tilesX;
            textureSheet.numTilesY = tilesY;
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
            textureSheet.uvChannelMask = frameBlending
                ? UVChannelFlags.UV0 | UVChannelFlags.UV1
                : UVChannelFlags.UV0;
            textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            textureSheet.startFrame = new ParticleSystem.MinMaxCurve(
                frameBlending ? 0f : (singleRow ? 0.25f : 0.125f));

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
            gpuParticles.textureSheetUVChannelMask =
                textureSheet.uvChannelMask;
            gpuParticles.textureSheetTilesX = tilesX;
            gpuParticles.textureSheetTilesY = tilesY;
            gpuParticles.textureSheetRowIndex = textureSheet.rowIndex;
            gpuParticles.textureSheetCycleCount = textureSheet.cycleCount;
            gpuParticles.textureSheetFps = textureSheet.fps;
            gpuParticles.SetTextureSheetSpeedRange(textureSheet.speedRange);
            gpuParticles.textureSheetFrameOverTimeLUT =
                profileTextureSheetFrameLUT;
            gpuParticles.textureSheetStartFrameLUT =
                profileTextureSheetStartLUT;
            gpuParticles.textureSheetFrameBlending = frameBlending;

            if (profileTextureSheetAtlas != null)
            {
                Destroy(profileTextureSheetAtlas);
            }
            profileTextureSheetAtlas = frameBlending
                ? CreateTextureSheetBlendAtlas()
                : CreateTextureSheetAtlas();
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
            if (profileTextureSheetMaterial.HasProperty(
                    "_FlipbookBlending"))
            {
                profileTextureSheetMaterial.SetFloat(
                    "_FlipbookBlending", frameBlending ? 1f : 0f);
            }
            if (frameBlending)
            {
                profileTextureSheetMaterial.EnableKeyword(
                    "_FLIPBOOKBLENDING_ON");
            }
            else
            {
                profileTextureSheetMaterial.DisableKeyword(
                    "_FLIPBOOKBLENDING_ON");
            }
            if (shurikenRenderer != null)
            {
                shurikenRenderer.sharedMaterial = profileTextureSheetMaterial;
                shurikenRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment = ParticleSystemRenderSpace.View;
                if (frameBlending)
                {
                    shurikenRenderer.SetActiveVertexStreams(
                        new System.Collections.Generic.List<
                            ParticleSystemVertexStream>
                        {
                            ParticleSystemVertexStream.Position,
                            ParticleSystemVertexStream.Color,
                            ParticleSystemVertexStream.UV,
                            ParticleSystemVertexStream.UV2,
                            ParticleSystemVertexStream.AnimBlend
                        });
                }
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

        static Texture2D CreateTextureSheetBlendAtlas()
        {
            const int tileSize = 32;
            const int columns = 2;
            int width = columns * tileSize;
            var pixels = new Color32[width * tileSize];
            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = x < tileSize
                        ? TextureSheetPalette[0]
                        : TextureSheetPalette[1];
                }
            }

            var atlas = new Texture2D(
                width, tileSize, TextureFormat.RGBA32, false, false)
            {
                name = "TextureSheetBlendAB_Profile_Atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            atlas.SetPixels32(pixels);
            atlas.Apply(false, false);
            return atlas;
        }

        void ConfigureRendererTextureUVFlipProfile()
        {
            ParticleSystem.TextureSheetAnimationModule textureSheet =
                shuriken.textureSheetAnimation;
            textureSheet.numTilesX = 1;
            textureSheet.numTilesY = 1;
            textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
            textureSheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            textureSheet.cycleCount = 1;
            textureSheet.frameOverTime = 0f;
            textureSheet.startFrame = 0f;
            textureSheet.flipU = 0f;
            textureSheet.flipV = 0f;

            gpuParticles.textureSheetTilesX = 1;
            gpuParticles.textureSheetTilesY = 1;
            gpuParticles.textureSheetAnimation =
                ParticleSystemAnimationType.WholeSheet;
            gpuParticles.textureSheetTimeMode =
                ParticleSystemAnimationTimeMode.Lifetime;
            gpuParticles.textureSheetCycleCount = 1;
            gpuParticles.textureSheetFrameOverTimeLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.textureSheetStartFrameLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
            if (shurikenRenderer != null)
            {
                shurikenRenderer.flip = new Vector3(1f, 0f, 0f);
            }
            else
            {
                textureSheet.flipU = 1f;
            }
            textureSheet.flipV = 1f;
            gpuParticles.rendererFlip = new Vector3(1f, 1f, 0f);

            if (profileTextureSheetAtlas != null)
            {
                Destroy(profileTextureSheetAtlas);
            }
            profileTextureSheetAtlas = CreateTextureUVFlipAtlas();
            gpuParticles.baseMap = profileTextureSheetAtlas;
            if (profileTextureSheetMaterial != null)
            {
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
            }
        }

        static Texture2D CreateTextureUVFlipAtlas()
        {
            const int size = 64;
            const int half = size / 2;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int paletteIndex;
                    if (y < half)
                    {
                        paletteIndex = x < half ? 0 : 1;
                    }
                    else
                    {
                        paletteIndex = x < half ? 2 : 3;
                    }
                    pixels[y * size + x] =
                        TextureSheetPalette[paletteIndex];
                }
            }

            var atlas = new Texture2D(
                size, size, TextureFormat.RGBA32, false, false)
            {
                name = "RendererTextureUVFlipAB_Profile_Atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            atlas.SetPixels32(pixels);
            atlas.Apply(false, false);
            return atlas;
        }

        void ConfigureStretchedBillboardProfile()
        {
            const float lifetime = 4f;
            const float startSpeed = 1.75f;
            const float startSize = 2f;
            const float startRotation = 37f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(2f, 1f, 0f).normalized;

            ParticleSystem.MainModule main = shuriken.main;
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.startRotation = startRotation;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Quaternion emitterRotation = Quaternion.LookRotation(
                direction, Vector3.up);
            shuriken.transform.rotation = emitterRotation;
            gpuParticles.transform.rotation = emitterRotation;

            ParticleSystem.TextureSheetAnimationModule textureSheet =
                shuriken.textureSheetAnimation;
            textureSheet.enabled = false;
            textureSheet.flipU = 0f;
            textureSheet.flipV = 0f;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.flip = Vector3.zero;
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Stretch;
                shurikenRenderer.lengthScale = 1.5f;
                shurikenRenderer.velocityScale = 0.4f;
                shurikenRenderer.cameraVelocityScale = 0f;
                shurikenRenderer.freeformStretching = false;
                shurikenRenderer.rotateWithStretchDirection = false;
                shurikenRenderer.pivot = Vector3.zero;
            }

            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(startSpeed, startSpeed);
            gpuParticles.SetStartSizeRange(startSize, startSize);
            gpuParticles.SetStartRotationRange(
                startRotation, startRotation);
            gpuParticles.simulationSpace = SimulationSpace.World;
            gpuParticles.initialDirectionWS = direction;
            gpuParticles.textureSheetAnimationEnabled = false;
            gpuParticles.rendererFlip = Vector3.zero;
            gpuParticles.renderMode = GPURenderMode.StretchedBillboard;
            gpuParticles.stretchedLengthScale = 1.5f;
            gpuParticles.stretchedVelocityScale = 0.4f;
            gpuParticles.stretchedCameraVelocityScale = 0f;
            gpuParticles.freeformStretching = false;
            gpuParticles.rotateWithStretchDirection = false;
            gpuParticles.pivot = Vector2.zero;
            activeStretchedBillboardState = 0;
        }

        void UpdateStretchedBillboardState(float elapsed)
        {
            int state = Mathf.Clamp(
                Mathf.FloorToInt(
                    Mathf.Max(0f, elapsed) /
                    StretchedBillboardStateInterval),
                0,
                2);
            bool freeformStretching = state != 0;
            bool rotateWithStretchDirection = state == 2;
            if (shurikenRenderer != null)
            {
                shurikenRenderer.freeformStretching = freeformStretching;
                shurikenRenderer.rotateWithStretchDirection =
                    rotateWithStretchDirection;
            }
            gpuParticles.freeformStretching = freeformStretching;
            gpuParticles.rotateWithStretchDirection =
                rotateWithStretchDirection;
            activeStretchedBillboardState = state;
        }

        void ConfigureMaterialColorProfile()
        {
            const float lifetime = 4f;
            ConfigureEmissionPointBase(lifetime, false);

            var main = shuriken.main;
            main.maxParticles = 1;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 3f;
            main.startColor = MaterialColorParticleColor;
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
            gpuParticles.SetEmissionRateOverTime(0f);
            gpuParticles.SetEmissionRateOverDistance(0f);
            gpuParticles.SetEmissionBursts(new[] { burst });
            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(3f, 3f);
            gpuParticles.SetStartColorRange(
                MaterialColorParticleColor,
                MaterialColorParticleColor,
                false);
            gpuParticles.startColorLUT =
                GradientLUTBuilder.GetDefaultWhiteLUT();

            if (profileMaterialColorTexture != null)
            {
                Destroy(profileMaterialColorTexture);
            }
            profileMaterialColorTexture = CreateMaterialColorTexture();

            if (profileMaterialColorMaterial != null)
            {
                Destroy(profileMaterialColorMaterial);
            }
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP particle shader was not found for material color validation.");
            }
            profileMaterialColorMaterial = new Material(shader)
            {
                name = "MaterialColorAB_Profile_Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            profileMaterialColorMaterial.SetTexture(
                "_BaseMap", profileMaterialColorTexture);
            profileMaterialColorMaterial.SetColor(
                "_BaseColor", MaterialColorBaseColor);
            ConfigureTransparentAlphaMaterial(profileMaterialColorMaterial);
            SetParticleMaterialColorMode(
                profileMaterialColorMaterial,
                GPUParticleColorMode.Multiply);

            if (shurikenRenderer != null)
            {
                shurikenRenderer.sharedMaterial =
                    profileMaterialColorMaterial;
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment =
                    ParticleSystemRenderSpace.View;
            }

            gpuParticles.baseMap = profileMaterialColorTexture;
            gpuParticles.materialBaseColor = MaterialColorBaseColor;
            gpuParticles.materialColorMode =
                GPUParticleColorMode.Multiply;
            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.pivot = Vector2.zero;
        }

        static Texture2D CreateMaterialColorTexture()
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            Color32 color = MaterialColorTextureColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false, false)
            {
                name = "MaterialColorAB_Profile_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static void ConfigureTransparentAlphaMaterial(Material material)
        {
            if (material == null) return;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_BlendOp", (float)BlendOp.Add);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat(
                "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
        }

        void UpdateMaterialColorMode(float elapsed)
        {
            int modeIndex = Mathf.Clamp(
                Mathf.FloorToInt(
                    Mathf.Max(0f, elapsed) / MaterialColorModeInterval),
                0,
                MaterialColorModeCount - 1);
            var mode = (GPUParticleColorMode)modeIndex;
            SetParticleMaterialColorMode(
                profileMaterialColorMaterial,
                mode);
            gpuParticles.materialColorMode = mode;
        }

        static void SetParticleMaterialColorMode(
            Material material,
            GPUParticleColorMode mode)
        {
            if (material == null) return;
            material.SetFloat("_ColorMode", (float)mode);
            material.DisableKeyword("_COLOROVERLAY_ON");
            material.DisableKeyword("_COLORCOLOR_ON");
            material.DisableKeyword("_COLORADDSUBDIFF_ON");

            switch (mode)
            {
                case GPUParticleColorMode.Additive:
                    material.EnableKeyword("_COLORADDSUBDIFF_ON");
                    material.SetVector(
                        "_BaseColorAddSubDiff",
                        new Vector4(1f, 0f, 0f, 0f));
                    break;
                case GPUParticleColorMode.Subtractive:
                    material.EnableKeyword("_COLORADDSUBDIFF_ON");
                    material.SetVector(
                        "_BaseColorAddSubDiff",
                        new Vector4(-1f, 0f, 0f, 0f));
                    break;
                case GPUParticleColorMode.Overlay:
                    material.EnableKeyword("_COLOROVERLAY_ON");
                    break;
                case GPUParticleColorMode.Color:
                    material.EnableKeyword("_COLORCOLOR_ON");
                    break;
                case GPUParticleColorMode.Difference:
                    material.EnableKeyword("_COLORADDSUBDIFF_ON");
                    material.SetVector(
                        "_BaseColorAddSubDiff",
                        new Vector4(-1f, 1f, 0f, 0f));
                    break;
            }
        }

        void ConfigureMaterialBlendProfile()
        {
            const float lifetime = 4f;
            ConfigureEmissionPointBase(lifetime, false);

            var main = shuriken.main;
            main.maxParticles = 1;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 3f;
            main.startColor = MaterialBlendParticleColor;
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
            gpuParticles.SetEmissionRateOverTime(0f);
            gpuParticles.SetEmissionRateOverDistance(0f);
            gpuParticles.SetEmissionBursts(new[] { burst });
            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(3f, 3f);
            gpuParticles.SetStartColorRange(
                MaterialBlendParticleColor,
                MaterialBlendParticleColor,
                false);
            gpuParticles.startColorLUT =
                GradientLUTBuilder.GetDefaultWhiteLUT();

            if (profileMaterialColorTexture != null)
            {
                Destroy(profileMaterialColorTexture);
            }
            profileMaterialColorTexture = CreateMaterialBlendTexture();

            if (profileMaterialColorMaterial != null)
            {
                Destroy(profileMaterialColorMaterial);
            }
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP particle shader was not found for material blend validation.");
            }
            profileMaterialColorMaterial = new Material(shader)
            {
                name = "MaterialBlendAB_Profile_Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            profileMaterialColorMaterial.SetTexture(
                "_BaseMap", profileMaterialColorTexture);
            profileMaterialColorMaterial.SetColor(
                "_BaseColor", MaterialBlendBaseColor);
            SetParticleMaterialColorMode(
                profileMaterialColorMaterial,
                GPUParticleColorMode.Multiply);
            SetParticleMaterialBlendMode(profileMaterialColorMaterial, 0);

            if (shurikenRenderer != null)
            {
                shurikenRenderer.sharedMaterial =
                    profileMaterialColorMaterial;
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment =
                    ParticleSystemRenderSpace.View;
            }

            gpuParticles.baseMap = profileMaterialColorTexture;
            gpuParticles.materialBaseColor = MaterialBlendBaseColor;
            gpuParticles.materialColorMode =
                GPUParticleColorMode.Multiply;
            ApplyGPUMaterialBlendMode(0);
            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.pivot = Vector2.zero;

            if (captureCamera != null)
            {
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor =
                    MaterialBlendBackgroundColor;
            }
        }

        static Texture2D CreateMaterialBlendTexture()
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            Color32 color = MaterialBlendTextureColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false, false)
            {
                name = "MaterialBlendAB_Profile_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        void UpdateMaterialBlendMode(float elapsed)
        {
            int mode = Mathf.Clamp(
                Mathf.FloorToInt(
                    Mathf.Max(0f, elapsed) / MaterialBlendModeInterval),
                0,
                MaterialBlendModeCount - 1);
            SetParticleMaterialBlendMode(
                profileMaterialColorMaterial,
                mode);
            ApplyGPUMaterialBlendMode(mode);
        }

        static void SetParticleMaterialBlendMode(
            Material material,
            int mode)
        {
            if (material == null) return;
            mode = Mathf.Clamp(mode, 0, MaterialBlendModeCount - 1);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", mode);
            material.SetFloat("_BlendOp", (float)BlendOp.Add);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");

            switch (mode)
            {
                case 1: // Premultiply
                    material.SetFloat(
                        "_SrcBlend", (float)BlendMode.One);
                    material.SetFloat(
                        "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat(
                        "_SrcBlendAlpha", (float)BlendMode.One);
                    material.SetFloat(
                        "_DstBlendAlpha",
                        (float)BlendMode.OneMinusSrcAlpha);
                    break;
                case 2: // Additive
                    material.SetFloat(
                        "_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat(
                        "_DstBlend", (float)BlendMode.One);
                    material.SetFloat(
                        "_SrcBlendAlpha", (float)BlendMode.One);
                    material.SetFloat(
                        "_DstBlendAlpha", (float)BlendMode.One);
                    break;
                case 3: // Multiply
                    material.SetFloat(
                        "_SrcBlend", (float)BlendMode.DstColor);
                    material.SetFloat(
                        "_DstBlend", (float)BlendMode.Zero);
                    material.SetFloat(
                        "_SrcBlendAlpha", (float)BlendMode.Zero);
                    material.SetFloat(
                        "_DstBlendAlpha", (float)BlendMode.One);
                    material.EnableKeyword("_ALPHAMODULATE_ON");
                    break;
                default: // Alpha
                    material.SetFloat(
                        "_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat(
                        "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat(
                        "_SrcBlendAlpha", (float)BlendMode.One);
                    material.SetFloat(
                        "_DstBlendAlpha",
                        (float)BlendMode.OneMinusSrcAlpha);
                    break;
            }
        }

        void ApplyGPUMaterialBlendMode(int mode)
        {
            activeMaterialBlendMode = Mathf.Clamp(
                mode, 0, MaterialBlendModeCount - 1);
            gpuParticles.materialBlendOperation = BlendOp.Add;
            gpuParticles.materialAlphaPremultiply = false;
            gpuParticles.materialAlphaModulate =
                activeMaterialBlendMode == 3;
            gpuParticles.materialZWrite = false;

            switch (activeMaterialBlendMode)
            {
                case 1: // Premultiply
                    gpuParticles.materialSourceBlend = BlendMode.One;
                    gpuParticles.materialDestinationBlend =
                        BlendMode.OneMinusSrcAlpha;
                    gpuParticles.materialSourceBlendAlpha = BlendMode.One;
                    gpuParticles.materialDestinationBlendAlpha =
                        BlendMode.OneMinusSrcAlpha;
                    break;
                case 2: // Additive
                    gpuParticles.materialSourceBlend = BlendMode.SrcAlpha;
                    gpuParticles.materialDestinationBlend = BlendMode.One;
                    gpuParticles.materialSourceBlendAlpha = BlendMode.One;
                    gpuParticles.materialDestinationBlendAlpha =
                        BlendMode.One;
                    break;
                case 3: // Multiply
                    gpuParticles.materialSourceBlend = BlendMode.DstColor;
                    gpuParticles.materialDestinationBlend = BlendMode.Zero;
                    gpuParticles.materialSourceBlendAlpha = BlendMode.Zero;
                    gpuParticles.materialDestinationBlendAlpha =
                        BlendMode.One;
                    break;
                default: // Alpha
                    gpuParticles.materialSourceBlend = BlendMode.SrcAlpha;
                    gpuParticles.materialDestinationBlend =
                        BlendMode.OneMinusSrcAlpha;
                    gpuParticles.materialSourceBlendAlpha = BlendMode.One;
                    gpuParticles.materialDestinationBlendAlpha =
                        BlendMode.OneMinusSrcAlpha;
                    break;
            }
        }

        void ConfigureMaterialAlphaClipProfile()
        {
            const float lifetime = 4f;
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
            gpuParticles.SetEmissionRateOverTime(0f);
            gpuParticles.SetEmissionRateOverDistance(0f);
            gpuParticles.SetEmissionBursts(new[] { burst });
            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(3f, 3f);
            gpuParticles.SetStartColorRange(
                Color.white, Color.white, false);
            gpuParticles.startColorLUT =
                GradientLUTBuilder.GetDefaultWhiteLUT();

            if (profileMaterialColorTexture != null)
            {
                Destroy(profileMaterialColorTexture);
            }
            profileMaterialColorTexture = CreateMaterialAlphaClipTexture();

            if (profileMaterialColorMaterial != null)
            {
                Destroy(profileMaterialColorMaterial);
            }
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP particle shader was not found for alpha clip validation.");
            }
            profileMaterialColorMaterial = new Material(shader)
            {
                name = "MaterialAlphaClipAB_Profile_Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            profileMaterialColorMaterial.SetTexture(
                "_BaseMap", profileMaterialColorTexture);
            profileMaterialColorMaterial.SetColor(
                "_BaseColor", Color.white);
            SetParticleMaterialColorMode(
                profileMaterialColorMaterial,
                GPUParticleColorMode.Multiply);
            ConfigureOpaqueMaterial(profileMaterialColorMaterial);
            SetParticleMaterialAlphaClipState(
                profileMaterialColorMaterial, 0);

            if (shurikenRenderer != null)
            {
                shurikenRenderer.sharedMaterial =
                    profileMaterialColorMaterial;
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment =
                    ParticleSystemRenderSpace.View;
            }

            gpuParticles.baseMap = profileMaterialColorTexture;
            gpuParticles.materialBaseColor = Color.white;
            gpuParticles.materialColorMode =
                GPUParticleColorMode.Multiply;
            gpuParticles.materialBlendOperation = BlendOp.Add;
            gpuParticles.materialSourceBlend = BlendMode.One;
            gpuParticles.materialDestinationBlend = BlendMode.Zero;
            gpuParticles.materialSourceBlendAlpha = BlendMode.One;
            gpuParticles.materialDestinationBlendAlpha = BlendMode.Zero;
            gpuParticles.materialAlphaPremultiply = false;
            gpuParticles.materialAlphaModulate = false;
            gpuParticles.materialZWrite = true;
            ApplyGPUMaterialAlphaClipState(0);
            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.pivot = Vector2.zero;

            if (captureCamera != null)
            {
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor =
                    MaterialAlphaClipBackgroundColor;
            }
        }

        static Texture2D CreateMaterialAlphaClipTexture()
        {
            const int width = 64;
            const int height = 16;
            var pixels = new Color32[width * height];
            Color32 color = MaterialAlphaClipTextureColor;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    color.a = (byte)Mathf.RoundToInt(
                        255f * x / (width - 1f));
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(
                width, height, TextureFormat.RGBA32, false, false)
            {
                name = "MaterialAlphaClipAB_Profile_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static void ConfigureOpaqueMaterial(Material material)
        {
            if (material == null) return;
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_BlendOp", (float)BlendOp.Add);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
        }

        void UpdateMaterialAlphaClipState(float elapsed)
        {
            int state = Mathf.Clamp(
                Mathf.FloorToInt(
                    Mathf.Max(0f, elapsed) /
                    MaterialAlphaClipStateInterval),
                0,
                MaterialAlphaClipStateCount - 1);
            SetParticleMaterialAlphaClipState(
                profileMaterialColorMaterial,
                state);
            ApplyGPUMaterialAlphaClipState(state);
        }

        static void SetParticleMaterialAlphaClipState(
            Material material,
            int state)
        {
            if (material == null) return;
            state = Mathf.Clamp(
                state, 0, MaterialAlphaClipStateCount - 1);
            bool enabled = state > 0;
            material.SetFloat("_AlphaClip", enabled ? 1f : 0f);
            material.SetFloat(
                "_Cutoff", MaterialAlphaClipCutoffs[state]);
            if (material.HasProperty("_AlphaToMask"))
            {
                material.SetFloat("_AlphaToMask", enabled ? 1f : 0f);
            }
            if (enabled)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag(
                    "RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
            }
        }

        void ApplyGPUMaterialAlphaClipState(int state)
        {
            activeMaterialAlphaClipState = Mathf.Clamp(
                state, 0, MaterialAlphaClipStateCount - 1);
            gpuParticles.materialAlphaClip =
                activeMaterialAlphaClipState > 0;
            gpuParticles.materialAlphaCutoff =
                MaterialAlphaClipCutoffs[activeMaterialAlphaClipState];
        }

        void ConfigureMaterialSoftParticlesProfile()
        {
            const float lifetime = 4f;
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
            gpuParticles.SetEmissionRateOverTime(0f);
            gpuParticles.SetEmissionRateOverDistance(0f);
            gpuParticles.SetEmissionBursts(new[] { burst });
            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(3f, 3f);
            gpuParticles.SetStartColorRange(
                Color.white, Color.white, false);
            gpuParticles.startColorLUT =
                GradientLUTBuilder.GetDefaultWhiteLUT();

            if (captureCamera != null)
            {
                Vector3 shurikenPosition =
                    captureCamera.transform.TransformPoint(
                        new Vector3(
                            -2.2f,
                            -1.5f,
                            MaterialSoftParticleCameraDepth));
                Vector3 gpuPosition =
                    captureCamera.transform.TransformPoint(
                        new Vector3(
                            2.2f,
                            -1.5f,
                            MaterialSoftParticleCameraDepth));
                shuriken.transform.position = shurikenPosition;
                gpuParticles.transform.position = gpuPosition;
                shurikenBasePositionWS = shurikenPosition;
                gpuBasePositionWS = gpuPosition;
            }

            if (profileMaterialColorTexture != null)
            {
                Destroy(profileMaterialColorTexture);
            }
            profileMaterialColorTexture = CreateMaterialSoftParticleTexture();

            if (profileMaterialColorMaterial != null)
            {
                Destroy(profileMaterialColorMaterial);
            }
            Shader particleShader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
            {
                throw new InvalidOperationException(
                    "URP particle shader was not found for soft particle validation.");
            }
            profileMaterialColorMaterial = new Material(particleShader)
            {
                name = "MaterialSoftParticlesAB_Profile_Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            profileMaterialColorMaterial.SetTexture(
                "_BaseMap", profileMaterialColorTexture);
            profileMaterialColorMaterial.SetColor(
                "_BaseColor", Color.white);
            ConfigureTransparentAlphaMaterial(profileMaterialColorMaterial);
            SetParticleMaterialColorMode(
                profileMaterialColorMaterial,
                GPUParticleColorMode.Multiply);
            SetParticleMaterialSoftParticleState(
                profileMaterialColorMaterial, 0);

            if (shurikenRenderer != null)
            {
                shurikenRenderer.sharedMaterial =
                    profileMaterialColorMaterial;
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment =
                    ParticleSystemRenderSpace.View;
            }

            gpuParticles.baseMap = profileMaterialColorTexture;
            gpuParticles.materialBaseColor = Color.white;
            gpuParticles.materialColorMode =
                GPUParticleColorMode.Multiply;
            gpuParticles.materialBlendOperation = BlendOp.Add;
            gpuParticles.materialSourceBlend = BlendMode.SrcAlpha;
            gpuParticles.materialDestinationBlend =
                BlendMode.OneMinusSrcAlpha;
            gpuParticles.materialSourceBlendAlpha = BlendMode.One;
            gpuParticles.materialDestinationBlendAlpha =
                BlendMode.OneMinusSrcAlpha;
            gpuParticles.materialAlphaPremultiply = false;
            gpuParticles.materialAlphaModulate = false;
            gpuParticles.materialZWrite = false;
            gpuParticles.materialAlphaClip = false;
            ApplyGPUMaterialSoftParticleState(0);
            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.pivot = Vector2.zero;

            CreateSoftParticleBackdrop();
            UpdateSoftParticleBackdrop(0);
            if (captureCamera != null)
            {
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor =
                    MaterialSoftParticleBackgroundColor;
            }
        }

        static Texture2D CreateMaterialSoftParticleTexture()
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            Color32 color = MaterialSoftParticleTextureColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false, false)
            {
                name = "MaterialSoftParticlesAB_Profile_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        void CreateSoftParticleBackdrop()
        {
            if (softParticleBackdropObject == null)
            {
                softParticleBackdropObject = GameObject.CreatePrimitive(
                    PrimitiveType.Quad);
                softParticleBackdropObject.name =
                    "ParticleAB_SoftParticleBackdrop";
                Collider collider =
                    softParticleBackdropObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            if (softParticleBackdropMaterial == null)
            {
                Shader shader = Shader.Find(
                    "Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "URP unlit shader was not found for the soft particle backdrop.");
                }
                softParticleBackdropMaterial = new Material(shader)
                {
                    name = "SoftParticlesAB_Backdrop_Material",
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Geometry
                };
                softParticleBackdropMaterial.SetColor(
                    "_BaseColor", MaterialSoftParticleBackgroundColor);
                softParticleBackdropMaterial.SetTexture(
                    "_BaseMap", Texture2D.whiteTexture);
                ConfigureOpaqueMaterial(softParticleBackdropMaterial);
                if (softParticleBackdropMaterial.HasProperty("_Cull"))
                {
                    softParticleBackdropMaterial.SetFloat(
                        "_Cull", (float)CullMode.Off);
                }
            }

            MeshRenderer renderer =
                softParticleBackdropObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = softParticleBackdropMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            softParticleBackdropObject.transform.localScale =
                new Vector3(12f, 6f, 1f);
        }

        void UpdateMaterialSoftParticleState(float elapsed)
        {
            int state = Mathf.Clamp(
                Mathf.FloorToInt(
                    Mathf.Max(0f, elapsed) /
                    MaterialSoftParticleStateInterval),
                0,
                MaterialSoftParticleStateCount - 1);
            SetParticleMaterialSoftParticleState(
                profileMaterialColorMaterial,
                state);
            ApplyGPUMaterialSoftParticleState(state);
            UpdateSoftParticleBackdrop(state);
        }

        static void SetParticleMaterialSoftParticleState(
            Material material,
            int state)
        {
            if (material == null) return;
            state = Mathf.Clamp(
                state, 0, MaterialSoftParticleStateCount - 1);
            bool enabled = state > 0;
            material.SetFloat(
                "_SoftParticlesEnabled", enabled ? 1f : 0f);
            material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            material.SetFloat("_SoftParticlesFarFadeDistance", 1f);
            material.SetVector(
                "_SoftParticleFadeParams",
                enabled
                    ? new Vector4(0f, 1f, 0f, 0f)
                    : Vector4.zero);
            if (enabled)
            {
                material.EnableKeyword("_SOFTPARTICLES_ON");
            }
            else
            {
                material.DisableKeyword("_SOFTPARTICLES_ON");
            }
        }

        void ApplyGPUMaterialSoftParticleState(int state)
        {
            activeMaterialSoftParticleState = Mathf.Clamp(
                state, 0, MaterialSoftParticleStateCount - 1);
            gpuParticles.materialSoftParticles =
                activeMaterialSoftParticleState > 0;
            gpuParticles.materialSoftParticleFadeParams =
                gpuParticles.materialSoftParticles
                    ? new Vector2(0f, 1f)
                    : Vector2.zero;
        }

        void UpdateSoftParticleBackdrop(int state)
        {
            if (softParticleBackdropObject == null ||
                captureCamera == null)
            {
                return;
            }

            state = Mathf.Clamp(
                state, 0, MaterialSoftParticleStateCount - 1);
            Transform cameraTransform = captureCamera.transform;
            softParticleBackdropObject.transform.SetPositionAndRotation(
                cameraTransform.TransformPoint(
                    new Vector3(
                        0f,
                        -1.5f,
                        MaterialSoftParticleCameraDepth +
                        MaterialSoftParticleDepthGaps[state])),
                cameraTransform.rotation);
        }

        void ConfigureMaterialCameraFadingProfile()
        {
            const float lifetime = 4f;
            const float particleSize = 0.4f;
            ConfigureEmissionPointBase(lifetime, false);

            var main = shuriken.main;
            main.maxParticles = 1;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = particleSize;
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
            gpuParticles.SetEmissionRateOverTime(0f);
            gpuParticles.SetEmissionRateOverDistance(0f);
            gpuParticles.SetEmissionBursts(new[] { burst });
            gpuParticles.SetStartLifetimeRange(lifetime, lifetime);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(particleSize, particleSize);
            gpuParticles.SetStartColorRange(
                Color.white, Color.white, false);
            gpuParticles.startColorLUT =
                GradientLUTBuilder.GetDefaultWhiteLUT();

            if (profileMaterialColorTexture != null)
            {
                Destroy(profileMaterialColorTexture);
            }
            profileMaterialColorTexture = CreateMaterialCameraFadeTexture();

            if (profileMaterialColorMaterial != null)
            {
                Destroy(profileMaterialColorMaterial);
            }
            Shader particleShader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
            {
                throw new InvalidOperationException(
                    "URP particle shader was not found for camera fade validation.");
            }
            profileMaterialColorMaterial = new Material(particleShader)
            {
                name = "MaterialCameraFadeAB_Profile_Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            profileMaterialColorMaterial.SetTexture(
                "_BaseMap", profileMaterialColorTexture);
            profileMaterialColorMaterial.SetColor(
                "_BaseColor", Color.white);
            ConfigureTransparentAlphaMaterial(profileMaterialColorMaterial);
            SetParticleMaterialColorMode(
                profileMaterialColorMaterial,
                GPUParticleColorMode.Multiply);
            SetParticleMaterialSoftParticleState(
                profileMaterialColorMaterial, 0);
            SetParticleMaterialCameraFadingState(
                profileMaterialColorMaterial, 0);

            if (shurikenRenderer != null)
            {
                shurikenRenderer.sharedMaterial =
                    profileMaterialColorMaterial;
                shurikenRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                shurikenRenderer.alignment =
                    ParticleSystemRenderSpace.View;
                shurikenRenderer.minParticleSize = 0f;
                shurikenRenderer.maxParticleSize = 1f;
            }

            gpuParticles.baseMap = profileMaterialColorTexture;
            gpuParticles.materialBaseColor = Color.white;
            gpuParticles.materialColorMode =
                GPUParticleColorMode.Multiply;
            gpuParticles.materialBlendOperation = BlendOp.Add;
            gpuParticles.materialSourceBlend = BlendMode.SrcAlpha;
            gpuParticles.materialDestinationBlend =
                BlendMode.OneMinusSrcAlpha;
            gpuParticles.materialSourceBlendAlpha = BlendMode.One;
            gpuParticles.materialDestinationBlendAlpha =
                BlendMode.OneMinusSrcAlpha;
            gpuParticles.materialAlphaPremultiply = false;
            gpuParticles.materialAlphaModulate = false;
            gpuParticles.materialZWrite = false;
            gpuParticles.materialAlphaClip = false;
            gpuParticles.materialSoftParticles = false;
            gpuParticles.materialSoftParticleFadeParams = Vector2.zero;
            ApplyGPUMaterialCameraFadingState(0);
            gpuParticles.renderMode = GPURenderMode.Billboard;
            gpuParticles.renderAlignment = GPUAlignment.View;
            gpuParticles.pivot = Vector2.zero;

            UpdateMaterialCameraFadePositions(0);
            if (captureCamera != null)
            {
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor =
                    MaterialCameraFadeBackgroundColor;
            }
        }

        static Texture2D CreateMaterialCameraFadeTexture()
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            Color32 color = MaterialCameraFadeTextureColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false, false)
            {
                name = "MaterialCameraFadeAB_Profile_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        void UpdateMaterialCameraFadingState(float elapsed)
        {
            int state = Mathf.Clamp(
                Mathf.FloorToInt(
                    Mathf.Max(0f, elapsed) /
                    MaterialCameraFadeStateInterval),
                0,
                MaterialCameraFadeStateCount - 1);
            SetParticleMaterialCameraFadingState(
                profileMaterialColorMaterial,
                state);
            ApplyGPUMaterialCameraFadingState(state);
            UpdateMaterialCameraFadePositions(state);
        }

        static void SetParticleMaterialCameraFadingState(
            Material material,
            int state)
        {
            if (material == null) return;
            state = Mathf.Clamp(
                state, 0, MaterialCameraFadeStateCount - 1);
            bool enabled = state > 0;
            float inverseFadeDistance = 1f /
                (MaterialCameraFadeFarDistance -
                 MaterialCameraFadeNearDistance);
            material.SetFloat(
                "_CameraFadingEnabled", enabled ? 1f : 0f);
            material.SetFloat(
                "_CameraNearFadeDistance",
                MaterialCameraFadeNearDistance);
            material.SetFloat(
                "_CameraFarFadeDistance",
                MaterialCameraFadeFarDistance);
            material.SetVector(
                "_CameraFadeParams",
                enabled
                    ? new Vector4(
                        MaterialCameraFadeNearDistance,
                        inverseFadeDistance,
                        0f,
                        0f)
                    : new Vector4(
                        0f,
                        float.PositiveInfinity,
                        0f,
                        0f));
            if (enabled)
            {
                material.EnableKeyword("_FADING_ON");
            }
            else
            {
                material.DisableKeyword("_FADING_ON");
            }
        }

        void ApplyGPUMaterialCameraFadingState(int state)
        {
            activeMaterialCameraFadeState = Mathf.Clamp(
                state, 0, MaterialCameraFadeStateCount - 1);
            gpuParticles.materialCameraFading =
                activeMaterialCameraFadeState > 0;
            gpuParticles.materialCameraFadeParams =
                gpuParticles.materialCameraFading
                    ? new Vector2(
                        MaterialCameraFadeNearDistance,
                        1f / (MaterialCameraFadeFarDistance -
                              MaterialCameraFadeNearDistance))
                    : Vector2.zero;
        }

        void UpdateMaterialCameraFadePositions(int state)
        {
            if (captureCamera == null) return;

            state = Mathf.Clamp(
                state, 0, MaterialCameraFadeStateCount - 1);
            float depth = MaterialCameraFadeDepths[state];
            float horizontalOffset = depth * 0.28f;
            float verticalOffset = -depth * 0.12f;
            float screenSizeScale = depth / MaterialCameraFadeDepths[0];
            Transform cameraTransform = captureCamera.transform;
            Vector3 shurikenPosition = cameraTransform.TransformPoint(
                new Vector3(
                    -horizontalOffset,
                    verticalOffset,
                    depth));
            Vector3 gpuPosition = cameraTransform.TransformPoint(
                new Vector3(
                    horizontalOffset,
                    verticalOffset,
                    depth));
            shuriken.transform.position = shurikenPosition;
            gpuParticles.transform.position = gpuPosition;
            shuriken.transform.localScale =
                Vector3.one * screenSizeScale;
            gpuParticles.transform.localScale =
                Vector3.one * screenSizeScale;
            shurikenBasePositionWS = shurikenPosition;
            gpuBasePositionWS = gpuPosition;
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
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.arcSpread = 0f;
            shape.arcSpeed = 1f;

            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.shapeRadiusThickness = 1f;
            gpuParticles.shapeConeArcDeg = 360f;
            gpuParticles.alignToDirection = true;
            gpuParticles.shapeRandomDirectionAmount = 0f;
            gpuParticles.shapeSphericalDirectionAmount = 0f;
            gpuParticles.shapeRandomPositionAmount = 0f;
            gpuParticles.shapeArcMode = ShapeArcModeGPU.Random;
            gpuParticles.shapeArcSpread = 0f;
            gpuParticles.shapeArcSpeedMode =
                ParticleSystemCurveMode.Constant;
            gpuParticles.shapeArcSpeedIntegralLUT =
                CurveLUTBuilder.GetDefaultLinear01LUT();

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

                case ParticleABValidationProfile.ShapeRandomDirectionPoint:
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = Vector3.one * 2f;
                    shape.randomDirectionAmount = 1f;
                    shape.alignToDirection = false;
                    gpuParticles.shapeType = ShapeTypeGPU.Box;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpuParticles.shapeBoxSize = Vector3.one;
                    gpuParticles.shapeLocalScale = shape.scale;
                    gpuParticles.shapeRandomDirectionAmount = 1f;
                    gpuParticles.alignToDirection = false;
                    break;

                case ParticleABValidationProfile.ShapeSphericalDirectionPoint:
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(2f, 4f, 6f);
                    shape.sphericalDirectionAmount = 1f;
                    shape.alignToDirection = false;
                    gpuParticles.shapeType = ShapeTypeGPU.Box;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpuParticles.shapeBoxSize = Vector3.one;
                    gpuParticles.shapeLocalScale = shape.scale;
                    gpuParticles.shapeSphericalDirectionAmount = 1f;
                    gpuParticles.alignToDirection = false;
                    break;

                case ParticleABValidationProfile.ShapeRandomPositionPoint:
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(1f, 2f, 4f);
                    shape.randomPositionAmount = 1f;
                    shape.alignToDirection = false;
                    gpuParticles.shapeType = ShapeTypeGPU.Box;
                    gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Volume;
                    gpuParticles.shapeBoxSize = Vector3.one;
                    gpuParticles.shapeLocalScale = shape.scale;
                    gpuParticles.shapeRandomPositionAmount = 1f;
                    gpuParticles.alignToDirection = false;
                    break;

                case ParticleABValidationProfile.ShapeArcRandomSpreadPoint:
                    ConfigureShapeArcProfile(
                        ParticleSystemShapeMultiModeValue.Random,
                        ShapeArcModeGPU.Random,
                        0.25f,
                        new ParticleSystem.MinMaxCurve(1f),
                        false);
                    break;

                case ParticleABValidationProfile.ShapeArcLoopPoint:
                    ConfigureShapeArcProfile(
                        ParticleSystemShapeMultiModeValue.Loop,
                        ShapeArcModeGPU.Loop,
                        0f,
                        new ParticleSystem.MinMaxCurve(
                            1f,
                            AnimationCurve.Linear(0f, 0.1f, 1f, 0.6f)),
                        false);
                    break;

                case ParticleABValidationProfile.ShapeArcPingPongPoint:
                    ConfigureShapeArcProfile(
                        ParticleSystemShapeMultiModeValue.PingPong,
                        ShapeArcModeGPU.PingPong,
                        0.25f,
                        new ParticleSystem.MinMaxCurve(0.25f),
                        false);
                    break;

                case ParticleABValidationProfile.ShapeArcBurstSpreadPoint:
                    ConfigureShapeArcProfile(
                        ParticleSystemShapeMultiModeValue.BurstSpread,
                        ShapeArcModeGPU.BurstSpread,
                        0.25f,
                        new ParticleSystem.MinMaxCurve(1f),
                        true);
                    break;
            }
        }

        void ConfigureShapeArcProfile(
            ParticleSystemShapeMultiModeValue shurikenMode,
            ShapeArcModeGPU gpuMode,
            float spread,
            ParticleSystem.MinMaxCurve speed,
            bool burstSpread)
        {
            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = 0.3f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(0.3f, 0.3f);

            var emission = shuriken.emission;
            emission.rateOverTime = burstSpread ? 0f : 16f;
            emission.rateOverDistance = 0f;
            ParticleSystem.Burst[] bursts = burstSpread
                ? new[]
                {
                    new ParticleSystem.Burst(0.25f, 6, 4, 0.5f)
                }
                : Array.Empty<ParticleSystem.Burst>();
            emission.SetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(bursts);

            var shape = shuriken.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2f;
            shape.radiusThickness = 0f;
            shape.arc = 180f;
            shape.arcMode = shurikenMode;
            shape.arcSpread = spread;
            shape.arcSpeed = speed;
            shape.alignToDirection = false;

            gpuParticles.shapeType = ShapeTypeGPU.Circle;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Surface;
            gpuParticles.shapeCircleRadius = 2f;
            gpuParticles.shapeRadiusThickness = 0f;
            gpuParticles.shapeConeArcDeg = 180f;
            gpuParticles.shapeArcMode = gpuMode;
            gpuParticles.shapeArcSpread = spread;
            gpuParticles.shapeArcSpeedMode = speed.mode;
            gpuParticles.alignToDirection = false;

            if (profileShapeArcSpeedLUT != null)
            {
                Destroy(profileShapeArcSpeedLUT);
            }
            profileShapeArcSpeedLUT = CurveLUTBuilder.BuildIntegral(speed);
            gpuParticles.shapeArcSpeedIntegralLUT =
                profileShapeArcSpeedLUT;
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
                case ParticleABValidationProfile.ShapeRandomDirectionPoint:
                case ParticleABValidationProfile.ShapeSphericalDirectionPoint:
                case ParticleABValidationProfile.ShapeRandomPositionPoint:
                case ParticleABValidationProfile.ShapeArcRandomSpreadPoint:
                case ParticleABValidationProfile.ShapeArcLoopPoint:
                case ParticleABValidationProfile.ShapeArcPingPongPoint:
                case ParticleABValidationProfile.ShapeArcBurstSpreadPoint:
                    return true;
                default:
                    return false;
            }
        }

        bool IsShapeArcProfile()
        {
            switch (validationProfile)
            {
                case ParticleABValidationProfile.ShapeArcRandomSpreadPoint:
                case ParticleABValidationProfile.ShapeArcLoopPoint:
                case ParticleABValidationProfile.ShapeArcPingPongPoint:
                case ParticleABValidationProfile.ShapeArcBurstSpreadPoint:
                    return true;
                default:
                    return false;
            }
        }

        void ResetTextureSheetAnimation()
        {
            var textureSheet = shuriken.textureSheetAnimation;
            textureSheet.enabled = false;
            textureSheet.flipU = 0f;
            textureSheet.flipV = 0f;
            if (shurikenRenderer != null)
            {
                shurikenRenderer.flip = Vector3.zero;
            }
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
            gpuParticles.textureSheetFrameBlending = false;
            gpuParticles.rendererFlip = Vector3.zero;
            gpuParticles.textureSheetFrameOverTimeLUT =
                CurveLUTBuilder.GetDefaultLinear01LUT();
            gpuParticles.textureSheetStartFrameLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
        }

        void ResetMaterialParameters()
        {
            gpuParticles.materialBaseColor = Color.white;
            gpuParticles.materialColorMode =
                GPUParticleColorMode.Multiply;
            gpuParticles.materialBlendOperation = BlendOp.Add;
            gpuParticles.materialSourceBlend = BlendMode.SrcAlpha;
            gpuParticles.materialDestinationBlend =
                BlendMode.OneMinusSrcAlpha;
            gpuParticles.materialSourceBlendAlpha = BlendMode.One;
            gpuParticles.materialDestinationBlendAlpha =
                BlendMode.OneMinusSrcAlpha;
            gpuParticles.materialAlphaPremultiply = false;
            gpuParticles.materialAlphaModulate = false;
            gpuParticles.materialZWrite = false;
            gpuParticles.materialAlphaClip = false;
            gpuParticles.materialAlphaCutoff = 0.5f;
            gpuParticles.materialSoftParticles = false;
            gpuParticles.materialSoftParticleFadeParams = Vector2.zero;
            gpuParticles.materialCameraFading = false;
            gpuParticles.materialCameraFadeParams = Vector2.zero;
        }

        bool IsMaterialColorProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.MaterialColorModesPoint;
        }

        bool IsRendererTextureUVFlipProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.RendererTextureUVFlipPoint;
        }

        bool IsStretchedBillboardProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.StretchedBillboardPoint;
        }

        bool IsMaterialBlendProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.MaterialBlendModesPoint;
        }

        bool IsMaterialAlphaClipProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.MaterialAlphaClipPoint;
        }

        bool IsMaterialSoftParticlesProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.MaterialSoftParticlesPoint;
        }

        bool IsMaterialCameraFadingProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.MaterialCameraFadingPoint;
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
                       ParticleABValidationProfile.TextureSheetSingleRowPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetBlendLifetimePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetBlendSpeedPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetBlendFPSPoint;
        }

        bool IsDiscreteTextureSheetProfile()
        {
            return IsTextureSheetProfile() &&
                   !IsTextureSheetBlendProfile();
        }

        bool IsTextureSheetBlendProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.TextureSheetBlendLifetimePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetBlendSpeedPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.TextureSheetBlendFPSPoint;
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

        void ConfigureGravitySource2DProfile()
        {
            ApplyGravitySourceOverride();
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 3f;
            main.startSpeed = 0f;
            main.startSize = 0.6f;
            main.gravityModifier = GravitySourceModifier;
            main.gravitySource = ParticleSystemGravitySource.Physics2D;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 20f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetStartLifetimeRange(3f, 3f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(0.6f, 0.6f);
            gpuParticles.SetGravityModifierRange(
                GravitySourceModifier,
                GravitySourceModifier);
            gpuParticles.gravitySource = ParticleSystemGravitySource.Physics2D;
            gpuParticles.simulationSpace = SimulationSpace.World;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());
        }

        void ConfigureCustomSimulationSpaceProfile()
        {
            ConfigureEmissionPointBase(4f, true);
            DestroyCustomSimulationSpaces();

            shuriken.transform.rotation = Quaternion.identity;
            shuriken.transform.localScale = Vector3.one;
            gpuParticles.transform.rotation = Quaternion.identity;
            gpuParticles.transform.localScale = Vector3.one;
            gpuParticles.transform.position = shurikenBasePositionWS;
            gpuBasePositionWS = shurikenBasePositionWS;

            shurikenCustomSpaceObject = new GameObject(
                "Shuriken Custom Simulation Space");
            gpuCustomSpaceObject = new GameObject(
                "GPU Custom Simulation Space");
            MoveValidationCustomSpaces(0f);

            var main = shuriken.main;
            main.startLifetime = 3.5f;
            main.startSpeed = 1.5f;
            main.startSize = 0.5f;
            main.gravityModifier = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace =
                shurikenCustomSpaceObject.transform;

            var emission = shuriken.emission;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetStartLifetimeRange(3.5f, 3.5f);
            gpuParticles.SetStartSpeedRange(1.5f, 1.5f);
            gpuParticles.SetStartSizeRange(0.5f, 0.5f);
            gpuParticles.SetGravityModifierRange(0.12f, 0.12f);
            gpuParticles.simulationSpace = SimulationSpace.Custom;
            gpuParticles.customSimulationSpace =
                gpuCustomSpaceObject.transform;
            gpuParticles.initialDirectionWS = Vector3.forward;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());
        }

        void DestroyCustomSimulationSpaces()
        {
            if (shurikenCustomSpaceObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(shurikenCustomSpaceObject);
                }
                else
                {
                    DestroyImmediate(shurikenCustomSpaceObject);
                }
                shurikenCustomSpaceObject = null;
            }

            if (gpuCustomSpaceObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(gpuCustomSpaceObject);
                }
                else
                {
                    DestroyImmediate(gpuCustomSpaceObject);
                }
                gpuCustomSpaceObject = null;
            }
        }

        void ApplyGravitySourceOverride()
        {
            if (!gravityOverrideActive)
            {
                savedPhysicsGravity = Physics.gravity;
                savedPhysics2DGravity = Physics2D.gravity;
                gravityOverrideActive = true;
            }

            Physics.gravity = GravitySourcePhysics3D;
            Physics2D.gravity = GravitySourcePhysics2D;
        }

        void RestoreGravityOverride()
        {
            if (!gravityOverrideActive) return;

            Physics.gravity = savedPhysicsGravity;
            Physics2D.gravity = savedPhysics2DGravity;
            gravityOverrideActive = false;
        }

        void ConfigureFlipRotationProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 3f;
            main.startSpeed = 2f;
            main.startSize = 0.8f;
            main.startRotation = FlipRotationStartRadians;
            main.flipRotation = 0.5f;

            var emission = shuriken.emission;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetStartLifetimeRange(3f, 3f);
            gpuParticles.SetStartSpeedRange(2f, 2f);
            gpuParticles.SetStartSizeRange(0.8f, 0.8f);
            gpuParticles.SetStartRotationRange(
                FlipRotationStartRadians,
                FlipRotationStartRadians);
            gpuParticles.flipRotation = 0.5f;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = false;
            rotationOverLifetime.z = FlipRotationLifetimeRadiansPerSecond;
            gpuParticles.SetRotationOverLifetimeRange(
                FlipRotationLifetimeRadiansPerSecond,
                FlipRotationLifetimeRadiansPerSecond);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(
                rotationOverLifetime.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;

            var rotationBySpeed = shuriken.rotationBySpeed;
            rotationBySpeed.enabled = true;
            rotationBySpeed.separateAxes = false;
            rotationBySpeed.range = new Vector2(0f, 4f);
            rotationBySpeed.z = FlipRotationBySpeedRadiansPerSecond;
            gpuParticles.rotationBySpeedEnabled = true;
            gpuParticles.SetRotationBySpeedRange(rotationBySpeed.range);
            if (profileRotationBySpeedLUT != null)
            {
                Destroy(profileRotationBySpeedLUT);
            }
            profileRotationBySpeedLUT = CurveLUTBuilder.BuildSigned(
                rotationBySpeed.z,
                assetName: "FlipRotationBySpeed_Profile_LUT");
            gpuParticles.rotationBySpeedLUT = profileRotationBySpeedLUT;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.pivot = new Vector3(0.35f, 0.15f, 0f);
            }
            gpuParticles.pivot = new Vector2(0.35f, 0.15f);
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

        void ConfigureStopActionProfile()
        {
            ConfigureEmissionPointBase(0.5f, false);

            stopActionSync = gpuParticles.GetComponent<ParticleSystemSync>();
            if (stopActionSync != null)
            {
                // This profile runs the systems independently so each callback
                // can be attributed to its own implementation.
                stopActionSync.enabled = false;
            }

            var main = shuriken.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 0.6f;
            main.stopAction = ParticleSystemStopAction.Callback;

            var emission = shuriken.emission;
            emission.rateOverTime = 20f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetStartLifetimeRange(0.4f, 0.4f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(0.6f, 0.6f);
            gpuParticles.stopAction = ParticleSystemStopAction.Callback;
            gpuParticles.stopActionTarget = null;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            shurikenStopActionObserver =
                shuriken.GetComponent<ParticleStopActionObserver>();
            if (shurikenStopActionObserver == null)
            {
                shurikenStopActionObserver =
                    shuriken.gameObject.AddComponent<
                        ParticleStopActionObserver>();
            }

            gpuStopActionObserver =
                gpuParticles.GetComponent<ParticleStopActionObserver>();
            if (gpuStopActionObserver == null)
            {
                gpuStopActionObserver =
                    gpuParticles.gameObject.AddComponent<
                        ParticleStopActionObserver>();
            }
            ResetStopActionObservers();

            stopActionDisableProbe = CreateStopActionProbe(
                "Stop Action Disable Probe",
                ParticleSystemStopAction.Disable,
                out stopActionDisableTarget);
            stopActionDestroyProbe = CreateStopActionProbe(
                "Stop Action Destroy Probe",
                ParticleSystemStopAction.Destroy,
                out stopActionDestroyTarget);
        }

        static GPUParticleSystem CreateStopActionProbe(
            string name,
            ParticleSystemStopAction action,
            out GameObject target)
        {
            var owner = new GameObject(name + " System");
            target = new GameObject(name + " Target");
            var probe = owner.AddComponent<GPUParticleSystem>();
            probe.maxParticles = 1;
            probe.renderEnabled = false;
            probe.emissionEnabled = false;
            probe.emissionLooping = false;
            probe.emissionDuration = 0.05f;
            probe.SetStartLifetimeRange(0.05f, 0.05f);
            probe.playOnAwake = false;
            probe.stopAction = action;
            probe.stopActionTarget = target;
            probe.ResetSimulation();
            probe.InitializePlaybackFromSettings();
            return probe;
        }

        void DestroyStopActionProbes()
        {
            DestroyStopActionProbeObject(
                stopActionDisableProbe != null
                    ? stopActionDisableProbe.gameObject
                    : null);
            DestroyStopActionProbeObject(
                stopActionDestroyProbe != null
                    ? stopActionDestroyProbe.gameObject
                    : null);
            DestroyStopActionProbeObject(stopActionDisableTarget);
            DestroyStopActionProbeObject(stopActionDestroyTarget);
            stopActionDisableProbe = null;
            stopActionDestroyProbe = null;
            stopActionDisableTarget = null;
            stopActionDestroyTarget = null;
        }

        static void DestroyStopActionProbeObject(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        bool IsStopActionProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.StopActionCallbackPoint;
        }

        void UpdateStopActionLifecycle()
        {
            if (!captureActive || !IsStopActionProfile() ||
                shuriken == null || gpuParticles == null)
            {
                return;
            }

            if (playbackFrame == StopActionRestartFrame)
            {
                shuriken.Play(true);
                gpuParticles.Play(false);
            }
            else if (playbackFrame == StopActionExplicitProbeFrame)
            {
                TriggerStopActionProbe(stopActionDisableProbe);
                TriggerStopActionProbe(stopActionDestroyProbe);
            }
        }

        static void TriggerStopActionProbe(GPUParticleSystem probe)
        {
            if (probe == null) return;
            probe.Play(false);
            probe.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void ObserveStopActionLifecycle()
        {
            if (!captureActive || !IsStopActionProfile() ||
                shuriken == null || gpuParticles == null)
            {
                return;
            }

            if (playbackFrame >= StopActionRestartFrame &&
                shuriken.isPlaying && gpuParticles.isPlaying)
            {
                stopActionRestartPlayingObserved = true;
            }
            if (stopActionDisableTarget != null &&
                !stopActionDisableTarget.activeSelf)
            {
                stopActionDisableObserved = true;
            }
            if (playbackFrame > StopActionExplicitProbeFrame &&
                stopActionDestroyTarget == null)
            {
                stopActionDestroyObserved = true;
            }
        }

        void ResetStopActionObservers()
        {
            if (shurikenStopActionObserver != null)
            {
                shurikenStopActionObserver.ResetObservation();
            }
            if (gpuStopActionObserver != null)
            {
                gpuStopActionObserver.ResetObservation();
            }
            stopActionRestartPlayingObserved = false;
            stopActionDisableObserved = false;
            stopActionDestroyObserved = false;
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

        bool IsRingBufferProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.RingBufferPausePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.RingBufferLoopPoint;
        }

        void ConfigureRingBufferProfile()
        {
            ConfigureEmissionPointBase(2.4f, false);

            ParticleSystemRingBufferMode mode = validationProfile ==
                    ParticleABValidationProfile.RingBufferLoopPoint
                ? ParticleSystemRingBufferMode.LoopUntilReplaced
                : ParticleSystemRingBufferMode.PauseUntilReplaced;

            var main = shuriken.main;
            main.maxParticles = 1;
            main.startLifetime = RingBufferLifetime;
            main.startSpeed = RingBufferStartSpeed;
            main.startSize = 1f;
            main.ringBufferMode = mode;
            main.ringBufferLoopRange = RingBufferLoopRange;

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 1),
                new ParticleSystem.Burst(RingBufferReplacementTime, 1)
            });

            var color = shuriken.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        new Color(0.95f, 0.1f, 0.85f),
                        0f),
                    new GradientColorKey(
                        new Color(0.05f, 0.9f, 0.95f),
                        1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            color.color = gradient;

            var size = shuriken.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

            var rotation = shuriken.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0.2f, 1f, 1.2f));

            gpuParticles.maxParticles = 1;
            gpuParticles.SetStartLifetimeRange(
                RingBufferLifetime,
                RingBufferLifetime);
            gpuParticles.SetStartSpeedRange(
                RingBufferStartSpeed,
                RingBufferStartSpeed);
            gpuParticles.ringBufferMode = mode;
            gpuParticles.SetRingBufferLoopRange(RingBufferLoopRange);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpuParticles.SetEmissionBursts(bursts);

            if (profileColorLUT != null)
            {
                Destroy(profileColorLUT);
            }
            profileColorLUT = GradientLUTBuilder.Build(
                color.color,
                assetName: "RingBufferColor_Profile_LUT");
            gpuParticles.colorOverLifetimeMode = color.color.mode;
            gpuParticles.colorOverLifetimeLUT = profileColorLUT;

            if (profileSizeLUT != null)
            {
                Destroy(profileSizeLUT);
            }
            profileSizeLUT = CurveLUTBuilder.Build(
                size.size,
                assetName: "RingBufferSize_Profile_LUT");
            gpuParticles.sizeOverLifetimeLUT = profileSizeLUT;

            if (profileRotationLUT != null)
            {
                Destroy(profileRotationLUT);
            }
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(
                rotation.z,
                assetName: "RingBufferRotation_Profile_LUT");
            gpuParticles.SetRotationOverLifetimeRange(1.2f, 1.2f);
            gpuParticles.rotationOverLifetimeIntegralLUT =
                profileRotationLUT;
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
            main.flipRotation = 0f;
            main.gravityModifier = 0f;
            main.gravitySource = ParticleSystemGravitySource.Physics3D;
            main.simulationSpeed = 1f;
            main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.customSimulationSpace = null;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
            main.ringBufferLoopRange = new Vector2(0f, 1f);
            main.emitterVelocityMode =
                ParticleSystemEmitterVelocityMode.Transform;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverDistance = 0f;
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);

            var shape = shuriken.shape;
            shape.enabled = false;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.arcSpread = 0f;
            shape.arcSpeed = 1f;
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

            var noise = shuriken.noise;
            noise.enabled = false;

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
            gpuParticles.gravitySource = ParticleSystemGravitySource.Physics3D;
            gpuParticles.SetStartRotationRange(0f, 0f);
            gpuParticles.flipRotation = 0f;
            gpuParticles.SetRotationOverLifetimeRange(0f, 0f);
            gpuParticles.rotationOverLifetimeIntegralLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.useUnscaledTime = false;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.customSimulationSpace = null;
            gpuParticles.emitterVelocityMode =
                ParticleSystemEmitterVelocityMode.Transform;
            gpuParticles.customEmitterVelocity = Vector3.zero;
            gpuParticles.emitterVelocitySource = null;
            gpuParticles.cullingMode =
                ParticleSystemCullingMode.AlwaysSimulate;
            gpuParticles.scalingMode = ParticleSystemScalingMode.Hierarchy;
            gpuParticles.stopAction = ParticleSystemStopAction.None;
            gpuParticles.stopActionTarget = null;
            gpuParticles.ringBufferMode =
                ParticleSystemRingBufferMode.Disabled;
            gpuParticles.SetRingBufferLoopRange(new Vector2(0f, 1f));
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
            gpuParticles.shapeArcMode = ShapeArcModeGPU.Random;
            gpuParticles.shapeArcSpread = 0f;
            gpuParticles.shapeArcSpeedMode =
                ParticleSystemCurveMode.Constant;
            gpuParticles.shapeArcSpeedIntegralLUT =
                CurveLUTBuilder.GetDefaultLinear01LUT();
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
            gpuParticles.noiseEnabled = false;
            gpuParticles.noiseSeparateAxes = false;
            gpuParticles.noiseFrequency = 0.5f;
            gpuParticles.noiseDamping = true;
            gpuParticles.noiseQuality = ParticleSystemNoiseQuality.High;
            gpuParticles.noiseOctaveCount = 1;
            gpuParticles.noiseOctaveMultiplier = 0.5f;
            gpuParticles.noiseOctaveScale = 2f;
            gpuParticles.noiseStrengthLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultUnitVectorLUT();
            gpuParticles.noiseAmountsLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultNoiseAmountsLUT();
            gpuParticles.noiseRemapEnabled = false;
            gpuParticles.noiseRemapLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultSignedIdentityLUT();
            gpuParticles.collisionEnabled = false;
            gpuParticles.collisionType = ParticleSystemCollisionType.Planes;
            gpuParticles.collisionPlanes = System.Array.Empty<Transform>();
            gpuParticles.collisionParametersLUT =
                MinMaxCurveVector3LUTBuilder
                    .GetDefaultCollisionParametersLUT();
            gpuParticles.collisionMinKillSpeed = 0f;
            gpuParticles.collisionMaxKillSpeed = 10000f;
            gpuParticles.collisionRadiusScale = 1f;
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
            MoveValidationCustomSpaces(0f);
            MoveValidationCamera(0f);

            bool suspendStopAction = IsStopActionProfile();
            ParticleSystemStopAction savedShurikenStopAction =
                ParticleSystemStopAction.None;
            ParticleSystemStopAction savedGPUStopAction =
                ParticleSystemStopAction.None;
            GameObject savedGPUStopActionTarget = null;
            if (suspendStopAction && shuriken != null)
            {
                var main = shuriken.main;
                savedShurikenStopAction = main.stopAction;
                main.stopAction = ParticleSystemStopAction.None;
            }
            if (suspendStopAction && gpuParticles != null)
            {
                savedGPUStopAction = gpuParticles.stopAction;
                savedGPUStopActionTarget = gpuParticles.stopActionTarget;
                gpuParticles.stopAction = ParticleSystemStopAction.None;
            }

            if (shuriken != null)
            {
                shuriken.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                shuriken.Clear(true);
                shuriken.useAutoRandomSeed = false;
                shuriken.randomSeed = randomSeed == 0 ? 1u : randomSeed;
                var main = shuriken.main;
                main.cullingMode = IsCullingProfile()
                    ? CullingModeForProfile()
                    : ParticleSystemCullingMode.AlwaysSimulate;
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

            if (suspendStopAction)
            {
                if (shuriken != null)
                {
                    var main = shuriken.main;
                    main.stopAction = savedShurikenStopAction;
                }
                if (gpuParticles != null)
                {
                    gpuParticles.stopAction = savedGPUStopAction;
                    gpuParticles.stopActionTarget = savedGPUStopActionTarget;
                }
                ResetStopActionObservers();
            }

            if (captureActive)
            {
                playbackFrame = 0;
                captureIndex = 1;
                nextCaptureFrame = CaptureFrameForIndex(captureIndex);
            }
        }

        void MoveValidationCamera(float elapsed)
        {
            if (captureCamera == null) return;

            Vector3 position = captureCameraBasePositionWS;
            if (IsCullingProfile())
            {
                bool alwaysOffscreen = validationProfile ==
                        ParticleABValidationProfile.CullingAlwaysSimulatePoint ||
                    validationProfile ==
                        ParticleABValidationProfile.CullingAutomaticOneShotPoint;
                bool timedOffscreen = elapsed >= CullingExitViewTime &&
                                      elapsed < CullingReturnTime;
                if (alwaysOffscreen || timedOffscreen)
                {
                    position += Vector3.right * 100f;
                }
            }

            captureCamera.transform.position = position;
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

        void MoveValidationCustomSpaces(float elapsed)
        {
            if (!IsCustomSimulationSpaceProfile()) return;
            if (shurikenCustomSpaceObject == null ||
                gpuCustomSpaceObject == null)
            {
                return;
            }

            elapsed = Mathf.Max(0f, elapsed);
            float phase = elapsed * 1.35f;
            Vector3 animatedOffset = CustomSpaceBaseOffset + new Vector3(
                Mathf.Sin(phase) * 1.4f,
                Mathf.Cos(phase * 0.8f) * 0.65f,
                Mathf.Sin(phase * 0.55f) * 0.35f);
            Quaternion animatedRotation = Quaternion.Euler(
                CustomSpaceBaseEuler + new Vector3(
                    Mathf.Sin(phase * 0.7f) * 18f,
                    elapsed * 42f,
                    Mathf.Cos(phase) * 24f));
            Vector3 animatedScale = CustomSpaceBaseScale + new Vector3(
                Mathf.Sin(phase * 0.9f) * 0.18f,
                Mathf.Cos(phase * 1.1f) * 0.12f,
                Mathf.Sin(phase * 0.6f) * 0.2f);

            ApplyCustomSpaceTransform(
                shurikenCustomSpaceObject.transform,
                shurikenBasePositionWS + animatedOffset,
                animatedRotation,
                animatedScale);
            ApplyCustomSpaceTransform(
                gpuCustomSpaceObject.transform,
                gpuBasePositionWS + animatedOffset,
                animatedRotation,
                animatedScale);
        }

        static void ApplyCustomSpaceTransform(
            Transform target,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            target.SetPositionAndRotation(position, rotation);
            target.localScale = scale;
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

        bool IsCustomSimulationSpaceProfile()
        {
            return validationProfile ==
                ParticleABValidationProfile.CustomSimulationSpacePoint;
        }

        bool IsCullingProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.CullingAutomaticLoopPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.CullingAutomaticOneShotPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.CullingPausePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.CullingPauseAndCatchupPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.CullingAlwaysSimulatePoint;
        }

        ParticleSystemCullingMode CullingModeForProfile()
        {
            switch (validationProfile)
            {
                case ParticleABValidationProfile.CullingPausePoint:
                    return ParticleSystemCullingMode.Pause;
                case ParticleABValidationProfile.CullingPauseAndCatchupPoint:
                    return ParticleSystemCullingMode.PauseAndCatchup;
                case ParticleABValidationProfile.CullingAlwaysSimulatePoint:
                    return ParticleSystemCullingMode.AlwaysSimulate;
                default:
                    return ParticleSystemCullingMode.Automatic;
            }
        }

        bool IsInheritVelocityProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.InheritVelocityInitialPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.InheritVelocityCurrentPoint ||
                   IsEmitterVelocityProfile();
        }

        bool IsEmitterVelocityProfile()
        {
            return validationProfile ==
                       ParticleABValidationProfile.EmitterVelocityCustomPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.EmitterVelocityRigidbodyPoint;
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
            maximumShurikenShapeArcGridError = 0f;
            maximumGPUShapeArcGridError = 0f;
            maximumShurikenNoiseKinematicsError = 0f;
            maximumGPUNoiseKinematicsError = 0f;
            maximumShurikenNoiseRotationError = 0f;
            maximumGPUNoiseRotationError = 0f;
            maximumGPUNoiseSizeError = 0f;
            maximumNoiseSizePixelError = 0f;
            noiseSizeClassificationFailures = 0;
            hasCurrentShurikenNoiseSizeBounds = false;
            currentShurikenNoiseSizeBounds = default;
            shurikenShapeArcBinMask = 0;
            gpuShapeArcBinMask = 0;
            maximumShurikenParticleCount = 0;
            maximumGPUParticleCount = 0;
            textureSheetComparableSamples = 0;
            textureSheetFrameMismatches = 0;
            textureSheetClassificationFailures = 0;
            maximumTextureSheetFrameDelta = 0;
            shurikenTextureSheetFrameMask = 0;
            gpuTextureSheetFrameMask = 0;
            textureSheetBlendComparableSamples = 0;
            textureSheetBlendClassificationFailures = 0;
            shurikenTextureSheetBlendIntermediateSamples = 0;
            gpuTextureSheetBlendIntermediateSamples = 0;
            maximumTextureSheetBlendColorError = 0f;
            hasCurrentShurikenTextureSheetBlendColor = false;
            currentShurikenTextureSheetBlendColor = Color.clear;
            textureUVFlipComparableSamples = 0;
            textureUVFlipClassificationFailures = 0;
            textureUVFlipSemanticFailures = 0;
            maximumTextureUVFlipColorError = 0f;
            maximumTextureUVFlipExpectedColorError = 0f;
            hasCurrentShurikenTextureUVFlipColors = false;
            Array.Clear(
                currentShurikenTextureUVFlipColors,
                0,
                currentShurikenTextureUVFlipColors.Length);
            stretchedBillboardComparableSamples = 0;
            stretchedBillboardClassificationFailures = 0;
            shurikenStretchedBillboardStateMask = 0;
            gpuStretchedBillboardStateMask = 0;
            maximumStretchedBillboardCentroidError = 0f;
            maximumStretchedBillboardAspectError = 0f;
            hasCurrentShurikenStretchedBillboardSignature = false;
            currentShurikenStretchedBillboardSignature = default;
            currentShurikenStretchedBillboardState = -1;
            Array.Clear(
                shurikenStretchedStateSignatureSums,
                0,
                shurikenStretchedStateSignatureSums.Length);
            Array.Clear(
                gpuStretchedStateSignatureSums,
                0,
                gpuStretchedStateSignatureSums.Length);
            Array.Clear(
                shurikenStretchedStateSignatureSamples,
                0,
                shurikenStretchedStateSignatureSamples.Length);
            Array.Clear(
                gpuStretchedStateSignatureSamples,
                0,
                gpuStretchedStateSignatureSamples.Length);
            materialColorComparableSamples = 0;
            materialColorClassificationFailures = 0;
            shurikenMaterialColorModeMask = 0;
            gpuMaterialColorModeMask = 0;
            maximumMaterialColorError = 0f;
            Array.Clear(
                maximumMaterialColorModeErrors,
                0,
                maximumMaterialColorModeErrors.Length);
            Array.Clear(
                shurikenMaterialColorSums,
                0,
                shurikenMaterialColorSums.Length);
            Array.Clear(
                shurikenMaterialColorSamples,
                0,
                shurikenMaterialColorSamples.Length);
            hasCurrentShurikenMaterialColor = false;
            currentShurikenMaterialColor = Color.clear;
            currentShurikenMaterialColorMode = -1;
            materialBlendComparableSamples = 0;
            materialBlendClassificationFailures = 0;
            shurikenMaterialBlendModeMask = 0;
            gpuMaterialBlendModeMask = 0;
            maximumMaterialBlendError = 0f;
            Array.Clear(
                maximumMaterialBlendModeErrors,
                0,
                maximumMaterialBlendModeErrors.Length);
            Array.Clear(
                shurikenMaterialBlendSums,
                0,
                shurikenMaterialBlendSums.Length);
            Array.Clear(
                shurikenMaterialBlendSamples,
                0,
                shurikenMaterialBlendSamples.Length);
            hasCurrentShurikenMaterialBlend = false;
            currentShurikenMaterialBlendColor = Color.clear;
            currentShurikenMaterialBlendMode = -1;
            activeMaterialBlendMode = 0;
            materialAlphaClipComparableSamples = 0;
            materialAlphaClipClassificationFailures = 0;
            shurikenMaterialAlphaClipStateMask = 0;
            gpuMaterialAlphaClipStateMask = 0;
            maximumMaterialAlphaClipWidthError = 0f;
            Array.Clear(
                maximumMaterialAlphaClipStateErrors,
                0,
                maximumMaterialAlphaClipStateErrors.Length);
            Array.Clear(
                shurikenMaterialAlphaClipWidthSums,
                0,
                shurikenMaterialAlphaClipWidthSums.Length);
            Array.Clear(
                shurikenMaterialAlphaClipSamples,
                0,
                shurikenMaterialAlphaClipSamples.Length);
            hasCurrentShurikenMaterialAlphaClipBounds = false;
            currentShurikenMaterialAlphaClipWidth = -1f;
            currentShurikenMaterialAlphaClipState = -1;
            activeMaterialAlphaClipState = 0;
            materialSoftParticleComparableSamples = 0;
            materialSoftParticleClassificationFailures = 0;
            shurikenMaterialSoftParticleStateMask = 0;
            gpuMaterialSoftParticleStateMask = 0;
            maximumMaterialSoftParticleColorError = 0f;
            Array.Clear(
                maximumMaterialSoftParticleStateErrors,
                0,
                maximumMaterialSoftParticleStateErrors.Length);
            Array.Clear(
                shurikenMaterialSoftParticleColorSums,
                0,
                shurikenMaterialSoftParticleColorSums.Length);
            Array.Clear(
                shurikenMaterialSoftParticleSamples,
                0,
                shurikenMaterialSoftParticleSamples.Length);
            hasCurrentShurikenMaterialSoftParticleColor = false;
            currentShurikenMaterialSoftParticleColor = Color.clear;
            currentShurikenMaterialSoftParticleState = -1;
            activeMaterialSoftParticleState = 0;
            materialCameraFadeComparableSamples = 0;
            materialCameraFadeClassificationFailures = 0;
            shurikenMaterialCameraFadeStateMask = 0;
            gpuMaterialCameraFadeStateMask = 0;
            maximumMaterialCameraFadeColorError = 0f;
            Array.Clear(
                maximumMaterialCameraFadeStateErrors,
                0,
                maximumMaterialCameraFadeStateErrors.Length);
            Array.Clear(
                shurikenMaterialCameraFadeColorSums,
                0,
                shurikenMaterialCameraFadeColorSums.Length);
            Array.Clear(
                shurikenMaterialCameraFadeSamples,
                0,
                shurikenMaterialCameraFadeSamples.Length);
            hasCurrentShurikenMaterialCameraFadeColor = false;
            currentShurikenMaterialCameraFadeColor = Color.clear;
            currentShurikenMaterialCameraFadeState = -1;
            activeMaterialCameraFadeState = 0;
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
            shurikenRingBufferReplacementObserved = false;
            gpuRingBufferReplacementObserved = false;
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
            cullingOffscreenObserved = false;
            cullingReturnObserved = false;
            cullingReturnShurikenCount = 0;
            cullingReturnGPUCount = 0;
            cullingReturnShurikenMeanAge = 0f;
            cullingReturnGPUMeanAge = 0f;
            if (IsStopActionProfile())
            {
                ResetStopActionObservers();
            }
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
            shurikenShapeDirectionXRange.Reset();
            shurikenShapeDirectionYRange.Reset();
            shurikenShapeDirectionZRange.Reset();
            gpuShapeDirectionXRange.Reset();
            gpuShapeDirectionYRange.Reset();
            gpuShapeDirectionZRange.Reset();
            shurikenShapeArcAngleRange.Reset();
            gpuShapeArcAngleRange.Reset();
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
            shurikenRingBufferAgeRange.Reset();
            gpuRingBufferAgeRange.Reset();
            shurikenRingBufferDisplacementRange.Reset();
            gpuRingBufferDisplacementRange.Reset();
            shurikenCulledCountRange.Reset();
            gpuCulledCountRange.Reset();
            shurikenCulledMeanAgeRange.Reset();
            gpuCulledMeanAgeRange.Reset();
            shurikenNoiseXRange.Reset();
            shurikenNoiseYRange.Reset();
            shurikenNoiseZRange.Reset();
            gpuNoiseXRange.Reset();
            gpuNoiseYRange.Reset();
            gpuNoiseZRange.Reset();
            shurikenCollisionHeightRange.Reset();
            gpuCollisionHeightRange.Reset();
            shurikenCollisionVelocityYRange.Reset();
            gpuCollisionVelocityYRange.Reset();
            shurikenTextureSheetBlendRedRange.Reset();
            shurikenTextureSheetBlendGreenRange.Reset();
            gpuTextureSheetBlendRedRange.Reset();
            gpuTextureSheetBlendGreenRange.Reset();
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
            int textureSheetFrame = IsDiscreteTextureSheetProfile()
                ? ClassifyTextureSheetFrame(cameraImage)
                : -1;
            if (IsTextureSheetBlendProfile() &&
                mode != ParticleABDisplayMode.Both)
            {
                bool colorValid = TryMeasureTextureSheetBlendColor(
                    cameraImage, out Color blendColor);
                ObserveTextureSheetBlendColor(
                    mode, colorValid, blendColor);
            }
            if (IsRendererTextureUVFlipProfile() &&
                mode != ParticleABDisplayMode.Both)
            {
                bool colorsValid = TryMeasureTextureUVFlipColors(
                    cameraImage, mode, out Color[] quadrantColors);
                ObserveTextureUVFlipColors(
                    mode, colorsValid, quadrantColors);
            }
            if (IsStretchedBillboardProfile() &&
                mode != ParticleABDisplayMode.Both)
            {
                bool signatureValid = TryMeasureStretchedBillboardSignature(
                    cameraImage,
                    mode,
                    out StretchedBillboardSignature signature);
                ObserveStretchedBillboardSignature(
                    mode,
                    signatureValid,
                    signature,
                    activeStretchedBillboardState);
            }
            if (IsMaterialColorProfile() &&
                mode != ParticleABDisplayMode.Both)
            {
                bool colorValid = TryMeasureMaterialColor(
                    cameraImage, mode, out Color materialColor);
                ObserveMaterialColor(
                    mode,
                    colorValid,
                    materialColor,
                    Mathf.Clamp(
                        (int)gpuParticles.materialColorMode,
                        0,
                        MaterialColorModeCount - 1));
            }
            if (IsMaterialBlendProfile() &&
                mode != ParticleABDisplayMode.Both)
            {
                bool colorValid = TryMeasureMaterialColor(
                    cameraImage, mode, out Color materialColor);
                ObserveMaterialBlend(
                    mode,
                    colorValid,
                    materialColor,
                    activeMaterialBlendMode);
            }
            MarkerPixelBounds alphaClipBounds =
                IsMaterialAlphaClipProfile() &&
                mode != ParticleABDisplayMode.Both
                    ? ClassifyMarkerBounds(cameraImage)
                    : default;
            ObserveMaterialAlphaClipBounds(
                mode,
                alphaClipBounds,
                activeMaterialAlphaClipState);
            if (IsMaterialSoftParticlesProfile() &&
                mode != ParticleABDisplayMode.Both)
            {
                bool colorValid = TryMeasureMaterialColor(
                    cameraImage,
                    mode,
                    out Color materialColor);
                ObserveMaterialSoftParticleColor(
                    mode,
                    colorValid,
                    materialColor,
                    activeMaterialSoftParticleState);
            }
            if (IsMaterialCameraFadingProfile() &&
                mode != ParticleABDisplayMode.Both)
            {
                bool colorValid = TryMeasureMaterialColor(
                    cameraImage,
                    mode,
                    out Color materialColor);
                ObserveMaterialCameraFadeColor(
                    mode,
                    colorValid,
                    materialColor,
                    activeMaterialCameraFadeState);
            }
            int screenSizePixels = IsRendererScreenSizeClampProfile() &&
                                   mode != ParticleABDisplayMode.Both
                ? ClassifyRendererScreenSize(cameraImage)
                : -1;
            ObserveRendererScreenSize(mode, screenSizePixels);
            MarkerPixelBounds noiseSizeBounds = validationProfile ==
                                                    ParticleABValidationProfile.NoiseRotationSizePoint &&
                                                mode != ParticleABDisplayMode.Both
                ? ClassifyWhiteParticleBounds(cameraImage)
                : default;
            ObserveNoiseSizeBounds(mode, noiseSizeBounds);
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

        static MarkerPixelBounds ClassifyWhiteParticleBounds(Texture2D image)
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
                    if (pixel.r < 180 || pixel.g < 180 || pixel.b < 180)
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

        void ObserveNoiseSizeBounds(
            ParticleABDisplayMode mode,
            MarkerPixelBounds bounds)
        {
            if (validationProfile !=
                    ParticleABValidationProfile.NoiseRotationSizePoint ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenNoiseSizeBounds = bounds.Valid;
                currentShurikenNoiseSizeBounds = bounds;
                if (!bounds.Valid)
                {
                    noiseSizeClassificationFailures++;
                }
                return;
            }

            if (!bounds.Valid || !hasCurrentShurikenNoiseSizeBounds)
            {
                noiseSizeClassificationFailures++;
            }
            else
            {
                maximumNoiseSizePixelError = Mathf.Max(
                    maximumNoiseSizePixelError,
                    Mathf.Max(
                        Mathf.Abs(
                            currentShurikenNoiseSizeBounds.Width - bounds.Width),
                        Mathf.Abs(
                            currentShurikenNoiseSizeBounds.Height - bounds.Height)));
            }
            hasCurrentShurikenNoiseSizeBounds = false;
            currentShurikenNoiseSizeBounds = default;
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

        static bool TryMeasureTextureSheetBlendColor(
            Texture2D image,
            out Color color)
        {
            Color32[] pixels = image.GetPixels32();
            Vector3 sum = Vector3.zero;
            int sampleCount = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.b >= 70 ||
                    pixel.r + pixel.g <= 180 ||
                    Mathf.Max(pixel.r, pixel.g) <= 130)
                {
                    continue;
                }

                sum += new Vector3(pixel.r, pixel.g, pixel.b);
                sampleCount++;
            }

            if (sampleCount < 32)
            {
                color = Color.clear;
                return false;
            }

            Vector3 average = sum / (255f * sampleCount);
            color = new Color(average.x, average.y, average.z, 1f);
            return true;
        }

        void ObserveTextureSheetBlendColor(
            ParticleABDisplayMode mode,
            bool valid,
            Color color)
        {
            if (!IsTextureSheetBlendProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenTextureSheetBlendColor = valid;
                currentShurikenTextureSheetBlendColor = color;
                if (!valid)
                {
                    textureSheetBlendClassificationFailures++;
                    return;
                }

                shurikenTextureSheetBlendRedRange.Observe(color.r);
                shurikenTextureSheetBlendGreenRange.Observe(color.g);
                if (color.r >= 0.3f && color.g >= 0.3f)
                {
                    shurikenTextureSheetBlendIntermediateSamples++;
                }
                return;
            }

            if (!valid)
            {
                textureSheetBlendClassificationFailures++;
            }
            else
            {
                gpuTextureSheetBlendRedRange.Observe(color.r);
                gpuTextureSheetBlendGreenRange.Observe(color.g);
                if (color.r >= 0.3f && color.g >= 0.3f)
                {
                    gpuTextureSheetBlendIntermediateSamples++;
                }
            }

            if (valid && hasCurrentShurikenTextureSheetBlendColor)
            {
                textureSheetBlendComparableSamples++;
                maximumTextureSheetBlendColorError = Mathf.Max(
                    maximumTextureSheetBlendColorError,
                    Mathf.Max(
                        Mathf.Abs(
                            currentShurikenTextureSheetBlendColor.r - color.r),
                        Mathf.Max(
                            Mathf.Abs(
                                currentShurikenTextureSheetBlendColor.g - color.g),
                            Mathf.Abs(
                                currentShurikenTextureSheetBlendColor.b - color.b))));
            }
            else if (!hasCurrentShurikenTextureSheetBlendColor)
            {
                textureSheetBlendClassificationFailures++;
            }

            hasCurrentShurikenTextureSheetBlendColor = false;
            currentShurikenTextureSheetBlendColor = Color.clear;
        }

        bool TryMeasureMaterialColor(
            Texture2D image,
            ParticleABDisplayMode mode,
            out Color color)
        {
            Transform emitter = mode == ParticleABDisplayMode.ShurikenOnly
                ? (shuriken != null ? shuriken.transform : null)
                : (gpuParticles != null ? gpuParticles.transform : null);
            if (image == null || captureCamera == null || emitter == null)
            {
                color = Color.clear;
                return false;
            }

            Vector3 viewport = captureCamera.WorldToViewportPoint(
                emitter.position);
            if (viewport.z <= 0f ||
                viewport.x <= 0f || viewport.x >= 1f ||
                viewport.y <= 0f || viewport.y >= 1f)
            {
                color = Color.clear;
                return false;
            }

            int centerX = Mathf.RoundToInt(
                viewport.x * (image.width - 1));
            int centerY = Mathf.RoundToInt(
                viewport.y * (image.height - 1));
            const int sampleRadius = 8;
            int minimumX = Mathf.Max(0, centerX - sampleRadius);
            int maximumX = Mathf.Min(
                image.width - 1, centerX + sampleRadius);
            int minimumY = Mathf.Max(0, centerY - sampleRadius);
            int maximumY = Mathf.Min(
                image.height - 1, centerY + sampleRadius);
            Color32[] pixels = image.GetPixels32();
            Vector3 sum = Vector3.zero;
            int sampleCount = 0;
            for (int y = minimumY; y <= maximumY; y++)
            {
                int row = y * image.width;
                for (int x = minimumX; x <= maximumX; x++)
                {
                    Color32 pixel = pixels[row + x];
                    sum += new Vector3(pixel.r, pixel.g, pixel.b);
                    sampleCount++;
                }
            }

            if (sampleCount < 32)
            {
                color = Color.clear;
                return false;
            }

            Vector3 average = sum / (255f * sampleCount);
            color = new Color(average.x, average.y, average.z, 1f);
            return true;
        }

        bool TryMeasureTextureUVFlipColors(
            Texture2D image,
            ParticleABDisplayMode mode,
            out Color[] colors)
        {
            colors = new Color[4];
            Transform emitter = mode == ParticleABDisplayMode.ShurikenOnly
                ? (shuriken != null ? shuriken.transform : null)
                : (gpuParticles != null ? gpuParticles.transform : null);
            if (image == null || captureCamera == null || emitter == null ||
                mode == ParticleABDisplayMode.Both)
            {
                return false;
            }

            Vector3 viewport = captureCamera.WorldToViewportPoint(
                emitter.position);
            if (viewport.z <= 0f)
            {
                return false;
            }
            int searchCenterX = Mathf.RoundToInt(
                viewport.x * (image.width - 1));
            int searchCenterY = Mathf.RoundToInt(
                viewport.y * (image.height - 1));
            int searchRadius = Mathf.Max(
                64, Mathf.Min(image.width, image.height) / 5);
            int searchMinimumX = Mathf.Max(0, searchCenterX - searchRadius);
            int searchMaximumX = Mathf.Min(
                image.width - 1, searchCenterX + searchRadius);
            int searchMinimumY = Mathf.Max(0, searchCenterY - searchRadius);
            int searchMaximumY = Mathf.Min(
                image.height - 1, searchCenterY + searchRadius);
            Color32[] pixels = image.GetPixels32();
            int minimumX = image.width;
            int maximumX = -1;
            int minimumY = image.height;
            int maximumY = -1;
            const int maximumDistanceSquared = 55 * 55 * 3;
            for (int y = searchMinimumY; y <= searchMaximumY; y++)
            {
                int row = y * image.width;
                for (int x = searchMinimumX; x <= searchMaximumX; x++)
                {
                    Color32 pixel = pixels[row + x];
                    int closestDistance = int.MaxValue;
                    for (int paletteIndex = 0; paletteIndex < 4; paletteIndex++)
                    {
                        Color32 target = TextureSheetPalette[paletteIndex];
                        int red = pixel.r - target.r;
                        int green = pixel.g - target.g;
                        int blue = pixel.b - target.b;
                        closestDistance = Mathf.Min(
                            closestDistance,
                            red * red + green * green + blue * blue);
                    }
                    if (closestDistance > maximumDistanceSquared)
                    {
                        continue;
                    }

                    minimumX = Mathf.Min(minimumX, x);
                    maximumX = Mathf.Max(maximumX, x);
                    minimumY = Mathf.Min(minimumY, y);
                    maximumY = Mathf.Max(maximumY, y);
                }
            }

            int particleWidth = maximumX - minimumX + 1;
            int particleHeight = maximumY - minimumY + 1;
            if (particleWidth < 8 || particleHeight < 8)
            {
                return false;
            }

            int sampleRadius = Mathf.Clamp(
                Mathf.Min(particleWidth, particleHeight) / 16,
                1,
                5);
            int[] centerXs =
            {
                Mathf.RoundToInt(Mathf.Lerp(minimumX, maximumX, 0.25f)),
                Mathf.RoundToInt(Mathf.Lerp(minimumX, maximumX, 0.75f))
            };
            int[] centerYs =
            {
                Mathf.RoundToInt(Mathf.Lerp(minimumY, maximumY, 0.25f)),
                Mathf.RoundToInt(Mathf.Lerp(minimumY, maximumY, 0.75f))
            };
            for (int quadrant = 0; quadrant < colors.Length; quadrant++)
            {
                int centerX = centerXs[quadrant & 1];
                int centerY = centerYs[quadrant >> 1];
                int sampleMinimumX = Mathf.Max(0, centerX - sampleRadius);
                int sampleMaximumX = Mathf.Min(
                    image.width - 1, centerX + sampleRadius);
                int sampleMinimumY = Mathf.Max(0, centerY - sampleRadius);
                int sampleMaximumY = Mathf.Min(
                    image.height - 1, centerY + sampleRadius);
                Vector3 sum = Vector3.zero;
                int sampleCount = 0;
                for (int y = sampleMinimumY; y <= sampleMaximumY; y++)
                {
                    int row = y * image.width;
                    for (int x = sampleMinimumX; x <= sampleMaximumX; x++)
                    {
                        Color32 pixel = pixels[row + x];
                        sum += new Vector3(pixel.r, pixel.g, pixel.b);
                        sampleCount++;
                    }
                }
                if (sampleCount == 0)
                {
                    return false;
                }

                Vector3 average = sum / (255f * sampleCount);
                colors[quadrant] = new Color(
                    average.x, average.y, average.z, 1f);
            }
            return true;
        }

        void ObserveTextureUVFlipColors(
            ParticleABDisplayMode mode,
            bool valid,
            Color[] colors)
        {
            if (!IsRendererTextureUVFlipProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            if (!valid || colors == null || colors.Length != 4)
            {
                textureUVFlipClassificationFailures++;
                if (mode == ParticleABDisplayMode.ShurikenOnly)
                {
                    hasCurrentShurikenTextureUVFlipColors = false;
                }
                return;
            }

            for (int quadrant = 0; quadrant < colors.Length; quadrant++)
            {
                Color expected = (Color)TextureSheetPalette[
                    TextureUVFlipExpectedPaletteIndices[quadrant]];
                if (ClosestTextureUVFlipPaletteIndex(colors[quadrant]) !=
                    TextureUVFlipExpectedPaletteIndices[quadrant])
                {
                    textureUVFlipSemanticFailures++;
                }
                maximumTextureUVFlipExpectedColorError = Mathf.Max(
                    maximumTextureUVFlipExpectedColorError,
                    ColorChannelError(colors[quadrant], expected));
            }

            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenTextureUVFlipColors = true;
                Array.Copy(
                    colors,
                    currentShurikenTextureUVFlipColors,
                    colors.Length);
                return;
            }

            if (!hasCurrentShurikenTextureUVFlipColors)
            {
                textureUVFlipClassificationFailures++;
                return;
            }

            textureUVFlipComparableSamples++;
            for (int quadrant = 0; quadrant < colors.Length; quadrant++)
            {
                maximumTextureUVFlipColorError = Mathf.Max(
                    maximumTextureUVFlipColorError,
                    ColorChannelError(
                        colors[quadrant],
                        currentShurikenTextureUVFlipColors[quadrant]));
            }
            hasCurrentShurikenTextureUVFlipColors = false;
        }

        static int ClosestTextureUVFlipPaletteIndex(Color color)
        {
            int closestIndex = -1;
            float closestError = float.PositiveInfinity;
            for (int paletteIndex = 0; paletteIndex < 4; paletteIndex++)
            {
                Color target = (Color)TextureSheetPalette[paletteIndex];
                float error = ColorChannelError(color, target);
                if (error < closestError)
                {
                    closestError = error;
                    closestIndex = paletteIndex;
                }
            }
            return closestIndex;
        }

        static float ColorChannelError(Color left, Color right)
        {
            return Mathf.Max(
                Mathf.Abs(left.r - right.r),
                Mathf.Max(
                    Mathf.Abs(left.g - right.g),
                    Mathf.Abs(left.b - right.b)));
        }

        bool TryMeasureStretchedBillboardSignature(
            Texture2D image,
            ParticleABDisplayMode mode,
            out StretchedBillboardSignature signature)
        {
            signature = default;
            Transform emitter = mode == ParticleABDisplayMode.ShurikenOnly
                ? (shuriken != null ? shuriken.transform : null)
                : (gpuParticles != null ? gpuParticles.transform : null);
            if (image == null || captureCamera == null || emitter == null ||
                mode == ParticleABDisplayMode.Both)
            {
                return false;
            }

            Vector3 viewport = captureCamera.WorldToViewportPoint(
                emitter.position);
            if (viewport.z <= 0f)
            {
                return false;
            }
            int searchCenterX = Mathf.RoundToInt(
                viewport.x * (image.width - 1));
            int searchCenterY = Mathf.RoundToInt(
                viewport.y * (image.height - 1));
            int searchRadius = Mathf.Max(
                96, Mathf.Min(image.width, image.height) / 3);
            int searchMinimumX = Mathf.Max(0, searchCenterX - searchRadius);
            int searchMaximumX = Mathf.Min(
                image.width - 1, searchCenterX + searchRadius);
            int searchMinimumY = Mathf.Max(0, searchCenterY - searchRadius);
            int searchMaximumY = Mathf.Min(
                image.height - 1, searchCenterY + searchRadius);

            Color32[] pixels = image.GetPixels32();
            int minimumX = image.width;
            int maximumX = -1;
            int minimumY = image.height;
            int maximumY = -1;
            var colorSums = new Vector2[4];
            var colorCounts = new int[4];
            const int maximumDistanceSquared = 55 * 55 * 3;
            for (int y = searchMinimumY; y <= searchMaximumY; y++)
            {
                int row = y * image.width;
                for (int x = searchMinimumX; x <= searchMaximumX; x++)
                {
                    Color32 pixel = pixels[row + x];
                    int closestIndex = -1;
                    int closestDistance = int.MaxValue;
                    for (int paletteIndex = 0; paletteIndex < 4; paletteIndex++)
                    {
                        Color32 target = TextureSheetPalette[paletteIndex];
                        int red = pixel.r - target.r;
                        int green = pixel.g - target.g;
                        int blue = pixel.b - target.b;
                        int distance = red * red + green * green + blue * blue;
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestIndex = paletteIndex;
                        }
                    }
                    if (closestIndex < 0 ||
                        closestDistance > maximumDistanceSquared)
                    {
                        continue;
                    }

                    minimumX = Mathf.Min(minimumX, x);
                    maximumX = Mathf.Max(maximumX, x);
                    minimumY = Mathf.Min(minimumY, y);
                    maximumY = Mathf.Max(maximumY, y);
                    colorSums[closestIndex] += new Vector2(x, y);
                    colorCounts[closestIndex]++;
                }
            }

            int width = maximumX - minimumX + 1;
            int height = maximumY - minimumY + 1;
            if (width < 8 || height < 8)
            {
                return false;
            }

            var normalizedCentroids = new Vector2[4];
            for (int paletteIndex = 0;
                 paletteIndex < normalizedCentroids.Length;
                 paletteIndex++)
            {
                if (colorCounts[paletteIndex] < 16)
                {
                    return false;
                }
                Vector2 centroid =
                    colorSums[paletteIndex] / colorCounts[paletteIndex];
                normalizedCentroids[paletteIndex] = new Vector2(
                    (centroid.x - minimumX) / Mathf.Max(1f, width - 1f),
                    (centroid.y - minimumY) / Mathf.Max(1f, height - 1f));
            }

            signature.Valid = true;
            signature.AspectRatio = width / Mathf.Max(1f, (float)height);
            signature.ColorCentroids = normalizedCentroids;
            return true;
        }

        void ObserveStretchedBillboardSignature(
            ParticleABDisplayMode mode,
            bool valid,
            StretchedBillboardSignature signature,
            int state)
        {
            if (!IsStretchedBillboardProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            state = Mathf.Clamp(
                state,
                0,
                shurikenStretchedStateSignatureSums.Length - 1);
            if (!valid || !signature.Valid ||
                signature.ColorCentroids == null ||
                signature.ColorCentroids.Length != 4)
            {
                stretchedBillboardClassificationFailures++;
                if (mode == ParticleABDisplayMode.ShurikenOnly)
                {
                    hasCurrentShurikenStretchedBillboardSignature = false;
                }
                return;
            }

            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                shurikenStretchedBillboardStateMask |= 1 << state;
                shurikenStretchedStateSignatureSums[state] +=
                    signature.ColorCentroids[0];
                shurikenStretchedStateSignatureSamples[state]++;
                currentShurikenStretchedBillboardSignature = signature;
                currentShurikenStretchedBillboardState = state;
                hasCurrentShurikenStretchedBillboardSignature = true;
                return;
            }

            gpuStretchedBillboardStateMask |= 1 << state;
            gpuStretchedStateSignatureSums[state] +=
                signature.ColorCentroids[0];
            gpuStretchedStateSignatureSamples[state]++;
            if (!hasCurrentShurikenStretchedBillboardSignature ||
                currentShurikenStretchedBillboardState != state)
            {
                stretchedBillboardClassificationFailures++;
                hasCurrentShurikenStretchedBillboardSignature = false;
                return;
            }

            stretchedBillboardComparableSamples++;
            maximumStretchedBillboardAspectError = Mathf.Max(
                maximumStretchedBillboardAspectError,
                Mathf.Abs(
                    signature.AspectRatio -
                    currentShurikenStretchedBillboardSignature.AspectRatio));
            for (int paletteIndex = 0; paletteIndex < 4; paletteIndex++)
            {
                Vector2 difference =
                    signature.ColorCentroids[paletteIndex] -
                    currentShurikenStretchedBillboardSignature
                        .ColorCentroids[paletteIndex];
                maximumStretchedBillboardCentroidError = Mathf.Max(
                    maximumStretchedBillboardCentroidError,
                    Mathf.Max(
                        Mathf.Abs(difference.x),
                        Mathf.Abs(difference.y)));
            }
            hasCurrentShurikenStretchedBillboardSignature = false;
        }

        static float StretchedStateSeparation(
            Vector2[] sums,
            int[] samples)
        {
            if (sums == null || sums.Length < 2 ||
                samples == null || samples.Length != sums.Length)
            {
                return 0f;
            }

            float minimumSeparation = float.PositiveInfinity;
            for (int firstIndex = 0;
                 firstIndex < sums.Length - 1;
                 firstIndex++)
            {
                if (samples[firstIndex] <= 0)
                {
                    return 0f;
                }
                Vector2 first = sums[firstIndex] / samples[firstIndex];
                for (int secondIndex = firstIndex + 1;
                     secondIndex < sums.Length;
                     secondIndex++)
                {
                    if (samples[secondIndex] <= 0)
                    {
                        return 0f;
                    }
                    Vector2 second =
                        sums[secondIndex] / samples[secondIndex];
                    minimumSeparation = Mathf.Min(
                        minimumSeparation,
                        Vector2.Distance(first, second));
                }
            }
            return float.IsPositiveInfinity(minimumSeparation)
                ? 0f
                : minimumSeparation;
        }

        void ObserveMaterialColor(
            ParticleABDisplayMode mode,
            bool valid,
            Color color,
            int colorMode)
        {
            if (!IsMaterialColorProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            int modeBit = 1 << colorMode;
            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenMaterialColor = valid;
                currentShurikenMaterialColor = color;
                currentShurikenMaterialColorMode = colorMode;
                if (!valid)
                {
                    materialColorClassificationFailures++;
                    return;
                }

                shurikenMaterialColorModeMask |= modeBit;
                shurikenMaterialColorSums[colorMode] +=
                    new Vector3(color.r, color.g, color.b);
                shurikenMaterialColorSamples[colorMode]++;
                return;
            }

            if (!valid)
            {
                materialColorClassificationFailures++;
            }
            else
            {
                gpuMaterialColorModeMask |= modeBit;
            }

            if (valid &&
                hasCurrentShurikenMaterialColor &&
                currentShurikenMaterialColorMode == colorMode)
            {
                materialColorComparableSamples++;
                float error = Mathf.Max(
                    Mathf.Abs(
                        currentShurikenMaterialColor.r - color.r),
                    Mathf.Max(
                        Mathf.Abs(
                            currentShurikenMaterialColor.g - color.g),
                        Mathf.Abs(
                            currentShurikenMaterialColor.b - color.b)));
                maximumMaterialColorError = Mathf.Max(
                    maximumMaterialColorError, error);
                maximumMaterialColorModeErrors[colorMode] = Mathf.Max(
                    maximumMaterialColorModeErrors[colorMode], error);
            }
            else
            {
                materialColorClassificationFailures++;
            }

            hasCurrentShurikenMaterialColor = false;
            currentShurikenMaterialColor = Color.clear;
            currentShurikenMaterialColorMode = -1;
        }

        void ObserveMaterialBlend(
            ParticleABDisplayMode mode,
            bool valid,
            Color color,
            int blendMode)
        {
            if (!IsMaterialBlendProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            blendMode = Mathf.Clamp(
                blendMode, 0, MaterialBlendModeCount - 1);
            int modeBit = 1 << blendMode;
            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenMaterialBlend = valid;
                currentShurikenMaterialBlendColor = color;
                currentShurikenMaterialBlendMode = blendMode;
                if (!valid)
                {
                    materialBlendClassificationFailures++;
                    return;
                }

                shurikenMaterialBlendModeMask |= modeBit;
                shurikenMaterialBlendSums[blendMode] +=
                    new Vector3(color.r, color.g, color.b);
                shurikenMaterialBlendSamples[blendMode]++;
                return;
            }

            if (!valid)
            {
                materialBlendClassificationFailures++;
            }
            else
            {
                gpuMaterialBlendModeMask |= modeBit;
            }

            if (valid &&
                hasCurrentShurikenMaterialBlend &&
                currentShurikenMaterialBlendMode == blendMode)
            {
                materialBlendComparableSamples++;
                float error = Mathf.Max(
                    Mathf.Abs(
                        currentShurikenMaterialBlendColor.r - color.r),
                    Mathf.Max(
                        Mathf.Abs(
                            currentShurikenMaterialBlendColor.g - color.g),
                        Mathf.Abs(
                            currentShurikenMaterialBlendColor.b - color.b)));
                maximumMaterialBlendError = Mathf.Max(
                    maximumMaterialBlendError, error);
                maximumMaterialBlendModeErrors[blendMode] = Mathf.Max(
                    maximumMaterialBlendModeErrors[blendMode], error);
            }
            else
            {
                materialBlendClassificationFailures++;
            }

            hasCurrentShurikenMaterialBlend = false;
            currentShurikenMaterialBlendColor = Color.clear;
            currentShurikenMaterialBlendMode = -1;
        }

        void ObserveMaterialAlphaClipBounds(
            ParticleABDisplayMode mode,
            MarkerPixelBounds bounds,
            int state)
        {
            if (!IsMaterialAlphaClipProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            state = Mathf.Clamp(
                state, 0, MaterialAlphaClipStateCount - 1);
            int stateBit = 1 << state;
            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenMaterialAlphaClipBounds = bounds.Valid;
                currentShurikenMaterialAlphaClipWidth = bounds.Width;
                currentShurikenMaterialAlphaClipState = state;
                if (!bounds.Valid)
                {
                    materialAlphaClipClassificationFailures++;
                    return;
                }

                shurikenMaterialAlphaClipStateMask |= stateBit;
                shurikenMaterialAlphaClipWidthSums[state] +=
                    bounds.Width;
                shurikenMaterialAlphaClipSamples[state]++;
                return;
            }

            if (!bounds.Valid)
            {
                materialAlphaClipClassificationFailures++;
            }
            else
            {
                gpuMaterialAlphaClipStateMask |= stateBit;
            }

            if (bounds.Valid &&
                hasCurrentShurikenMaterialAlphaClipBounds &&
                currentShurikenMaterialAlphaClipState == state)
            {
                materialAlphaClipComparableSamples++;
                float error = Mathf.Abs(
                    currentShurikenMaterialAlphaClipWidth -
                    bounds.Width);
                maximumMaterialAlphaClipWidthError = Mathf.Max(
                    maximumMaterialAlphaClipWidthError, error);
                maximumMaterialAlphaClipStateErrors[state] = Mathf.Max(
                    maximumMaterialAlphaClipStateErrors[state], error);
            }
            else
            {
                materialAlphaClipClassificationFailures++;
            }

            hasCurrentShurikenMaterialAlphaClipBounds = false;
            currentShurikenMaterialAlphaClipWidth = -1f;
            currentShurikenMaterialAlphaClipState = -1;
        }

        void ObserveMaterialSoftParticleColor(
            ParticleABDisplayMode mode,
            bool valid,
            Color color,
            int state)
        {
            if (!IsMaterialSoftParticlesProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            state = Mathf.Clamp(
                state, 0, MaterialSoftParticleStateCount - 1);
            int stateBit = 1 << state;
            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenMaterialSoftParticleColor = valid;
                currentShurikenMaterialSoftParticleColor = color;
                currentShurikenMaterialSoftParticleState = state;
                if (!valid)
                {
                    materialSoftParticleClassificationFailures++;
                    return;
                }

                shurikenMaterialSoftParticleStateMask |= stateBit;
                shurikenMaterialSoftParticleColorSums[state] +=
                    new Vector3(color.r, color.g, color.b);
                shurikenMaterialSoftParticleSamples[state]++;
                return;
            }

            if (!valid)
            {
                materialSoftParticleClassificationFailures++;
            }
            else
            {
                gpuMaterialSoftParticleStateMask |= stateBit;
            }

            if (valid &&
                hasCurrentShurikenMaterialSoftParticleColor &&
                currentShurikenMaterialSoftParticleState == state)
            {
                materialSoftParticleComparableSamples++;
                float error = Mathf.Max(
                    Mathf.Abs(
                        currentShurikenMaterialSoftParticleColor.r -
                        color.r),
                    Mathf.Max(
                        Mathf.Abs(
                            currentShurikenMaterialSoftParticleColor.g -
                            color.g),
                        Mathf.Abs(
                            currentShurikenMaterialSoftParticleColor.b -
                            color.b)));
                maximumMaterialSoftParticleColorError = Mathf.Max(
                    maximumMaterialSoftParticleColorError, error);
                maximumMaterialSoftParticleStateErrors[state] = Mathf.Max(
                    maximumMaterialSoftParticleStateErrors[state], error);
            }
            else
            {
                materialSoftParticleClassificationFailures++;
            }

            hasCurrentShurikenMaterialSoftParticleColor = false;
            currentShurikenMaterialSoftParticleColor = Color.clear;
            currentShurikenMaterialSoftParticleState = -1;
        }

        void ObserveMaterialCameraFadeColor(
            ParticleABDisplayMode mode,
            bool valid,
            Color color,
            int state)
        {
            if (!IsMaterialCameraFadingProfile() ||
                mode == ParticleABDisplayMode.Both)
            {
                return;
            }

            state = Mathf.Clamp(
                state, 0, MaterialCameraFadeStateCount - 1);
            int stateBit = 1 << state;
            if (mode == ParticleABDisplayMode.ShurikenOnly)
            {
                hasCurrentShurikenMaterialCameraFadeColor = valid;
                currentShurikenMaterialCameraFadeColor = color;
                currentShurikenMaterialCameraFadeState = state;
                if (!valid)
                {
                    materialCameraFadeClassificationFailures++;
                    return;
                }

                shurikenMaterialCameraFadeStateMask |= stateBit;
                shurikenMaterialCameraFadeColorSums[state] +=
                    new Vector3(color.r, color.g, color.b);
                shurikenMaterialCameraFadeSamples[state]++;
                return;
            }

            if (!valid)
            {
                materialCameraFadeClassificationFailures++;
            }
            else
            {
                gpuMaterialCameraFadeStateMask |= stateBit;
            }

            if (valid &&
                hasCurrentShurikenMaterialCameraFadeColor &&
                currentShurikenMaterialCameraFadeState == state)
            {
                materialCameraFadeComparableSamples++;
                float error = Mathf.Max(
                    Mathf.Abs(
                        currentShurikenMaterialCameraFadeColor.r -
                        color.r),
                    Mathf.Max(
                        Mathf.Abs(
                            currentShurikenMaterialCameraFadeColor.g -
                            color.g),
                        Mathf.Abs(
                            currentShurikenMaterialCameraFadeColor.b -
                            color.b)));
                maximumMaterialCameraFadeColorError = Mathf.Max(
                    maximumMaterialCameraFadeColorError, error);
                maximumMaterialCameraFadeStateErrors[state] = Mathf.Max(
                    maximumMaterialCameraFadeStateErrors[state], error);
            }
            else
            {
                materialCameraFadeClassificationFailures++;
            }

            hasCurrentShurikenMaterialCameraFadeColor = false;
            currentShurikenMaterialCameraFadeColor = Color.clear;
            currentShurikenMaterialCameraFadeState = -1;
        }

        void ObserveTextureSheetFrames(int shurikenFrame, int gpuFrame)
        {
            if (!IsDiscreteTextureSheetProfile()) return;
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
                    Vector3 shurikenPosition = particle.position;
                    Vector3 shurikenVelocity = particle.totalVelocity;
                    if (IsCustomSimulationSpaceProfile() &&
                        shuriken.main.customSimulationSpace != null)
                    {
                        Transform customSpace =
                            shuriken.main.customSimulationSpace;
                        shurikenPosition = customSpace.TransformPoint(
                            shurikenPosition);
                        shurikenVelocity = customSpace.TransformVector(
                            shurikenVelocity);
                        shurikenCustomWorldXRange.Observe(
                            shurikenPosition.x - shurikenBasePositionWS.x);
                    }
                    shurikenPositionSum += shurikenPosition;
                    shurikenVelocitySum += shurikenVelocity;
                    shurikenSpeedSum += shurikenVelocity.magnitude;
                    Vector3 shurikenSize = particle.GetCurrentSize3D(shuriken);
                    shurikenSizeSum += shurikenSize.x;
                    shurikenSizeYSum += shurikenSize.y;
                    float age = particle.startLifetime - particle.remainingLifetime;
                    shurikenAgeSum += age;
                    shurikenLifetimeSum += particle.startLifetime;
                    if (validationProfile ==
                        ParticleABValidationProfile.CollisionPlaneBouncePoint)
                    {
                        shurikenCollisionHeightRange.Observe(
                            shurikenPosition.y);
                        shurikenCollisionVelocityYRange.Observe(
                            shurikenVelocity.y);
                    }
                    if (IsNoiseProfile())
                    {
                        ObserveNoisePosition(false, shurikenPosition, age);
                        if (validationProfile ==
                            ParticleABValidationProfile.NoiseRotationSizePoint)
                        {
                            float expectedRotationDegrees = age * 45f;
                            float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                                expectedRotationDegrees,
                                particle.rotation)) * Mathf.Deg2Rad;
                            maximumShurikenNoiseRotationError = Mathf.Max(
                                maximumShurikenNoiseRotationError,
                                rotationError);
                        }
                    }
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
                    if (IsStartRotationProfile() || IsRingBufferProfile())
                    {
                        float rotation = particle.rotation * Mathf.Deg2Rad;
                        shurikenStartRotationSum += rotation;
                        if (IsStartRotationProfile())
                        {
                            ObserveStartRotationSample(
                                false,
                                rotation,
                                elapsed,
                                age);
                        }
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
                             ParticleABValidationProfile.GravitySource2DPoint)
                    {
                        maximumForceKinematicsError = Mathf.Max(
                            maximumForceKinematicsError,
                            (shurikenVelocity -
                             GravitySourceExpectedVelocity(age)).magnitude);
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
                             ParticleABValidationProfile.FlipRotationPoint)
                    {
                        float signedRotation = Mathf.DeltaAngle(
                            0f,
                            particle.rotation) * Mathf.Deg2Rad;
                        shurikenStartRotationRange.Observe(signedRotation);
                        maximumShurikenRotationError = Mathf.Max(
                            maximumShurikenRotationError,
                            Mathf.Abs(
                                Mathf.Abs(signedRotation) -
                                FlipRotationExpectedMagnitudeRadians(age)));
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
                Vector3 gpuPosition = new Vector3(
                    positionLife.r,
                    positionLife.g,
                    positionLife.b);
                Vector3 gpuVelocity = new Vector3(velocitySize.r, velocitySize.g, velocitySize.b);
                if (IsCustomSimulationSpaceProfile() &&
                    gpuParticles.customSimulationSpace != null)
                {
                    Transform customSpace =
                        gpuParticles.customSimulationSpace;
                    gpuPosition = customSpace.TransformPoint(gpuPosition);
                    gpuVelocity = customSpace.TransformVector(gpuVelocity);
                    gpuCustomWorldXRange.Observe(
                        gpuPosition.x - gpuBasePositionWS.x);
                }
                gpuPositionSum += gpuPosition;
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
                if (validationProfile ==
                    ParticleABValidationProfile.CollisionPlaneBouncePoint)
                {
                    gpuCollisionHeightRange.Observe(gpuPosition.y);
                    gpuCollisionVelocityYRange.Observe(gpuVelocity.y);
                }
                if (IsNoiseProfile())
                {
                    ObserveNoisePosition(true, gpuPosition, age);
                    if (validationProfile ==
                        ParticleABValidationProfile.NoiseRotationSizePoint)
                    {
                        float actualRotation =
                            gpuParticles.ResolveParticleRotationRadians(
                                i,
                                positionLife.a,
                                gpuRotationPhases[i].r,
                                birthEmitterVelocityWS);
                        float expectedRotation = age * 45f * Mathf.Deg2Rad;
                        float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                            expectedRotation * Mathf.Rad2Deg,
                            actualRotation * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                        maximumGPUNoiseRotationError = Mathf.Max(
                            maximumGPUNoiseRotationError,
                            rotationError);
                        maximumGPUNoiseSizeError = Mathf.Max(
                            maximumGPUNoiseSizeError,
                            Mathf.Abs(velocitySize.a - 1.875f));
                    }
                }
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
                if (IsStartRotationProfile() || IsRingBufferProfile())
                {
                    float rotation =
                        gpuParticles.ResolveParticleRotationRadians(
                            i,
                            positionLife.a,
                            gpuRotationPhases[i].r,
                            birthEmitterVelocityWS);
                    gpuStartRotationSum += rotation;
                    if (IsStartRotationProfile())
                    {
                        ObserveStartRotationSample(
                            true,
                            rotation,
                            elapsed,
                            age);
                    }
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
                         ParticleABValidationProfile.GravitySource2DPoint)
                {
                    maximumForceKinematicsError = Mathf.Max(
                        maximumForceKinematicsError,
                        (gpuVelocity -
                         GravitySourceExpectedVelocity(age)).magnitude);
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
                         ParticleABValidationProfile.FlipRotationPoint)
                {
                    float actualRotation =
                        gpuParticles.ResolveParticleRotationRadians(
                            i,
                            positionLife.a,
                            gpuRotationPhases[i].r,
                            birthEmitterVelocityWS);
                    float signedRotation = Mathf.DeltaAngle(
                        0f,
                        actualRotation * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    gpuStartRotationRange.Observe(signedRotation);
                    maximumGPURotationError = Mathf.Max(
                        maximumGPURotationError,
                        Mathf.Abs(
                            Mathf.Abs(signedRotation) -
                            FlipRotationExpectedMagnitudeRadians(age)));
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
            ObserveCullingMetrics(
                elapsed,
                shurikenCount,
                gpuCount,
                shurikenMeanAge,
                gpuMeanAge);
            ObserveRingBufferMetrics(
                elapsed,
                shurikenCount,
                gpuCount,
                shurikenMean,
                gpuMean,
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
                if (IsStartRotationProfile() || IsRingBufferProfile())
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
                      ParticleABValidationProfile.CollisionPlaneBouncePoint ||
                  validationProfile ==
                      ParticleABValidationProfile.InheritVelocityInitialPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.InheritVelocityCurrentPoint ||
                   validationProfile ==
                       ParticleABValidationProfile.UnscaledTimePoint ||
                   validationProfile ==
                       ParticleABValidationProfile.CustomSimulationSpacePoint) &&
                shurikenCount > 0 && gpuCount > 0)
            {
                Vector3 shurikenMeanDisplacement =
                    shurikenMean - shurikenBasePositionWS;
                Vector3 gpuMeanDisplacement = gpuMean - gpuBasePositionWS;
                maximumMeanPositionError = Mathf.Max(
                    maximumMeanPositionError,
                    (gpuMeanDisplacement - shurikenMeanDisplacement).magnitude);
            }

            if (IsRingBufferProfile() &&
                shurikenCount > 0 && gpuCount > 0)
            {
                maximumMeanPositionError = Mathf.Max(
                    maximumMeanPositionError,
                    (gpuMean - shurikenMean).magnitude);
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

        void ObserveRingBufferMetrics(
            float elapsed,
            int shurikenCount,
            int gpuCount,
            Vector3 shurikenMeanPosition,
            Vector3 gpuMeanPosition,
            float shurikenMeanAge,
            float gpuMeanAge)
        {
            if (!IsRingBufferProfile()) return;

            if (elapsed >= RingBufferObservationStart &&
                elapsed <= RingBufferObservationEnd)
            {
                if (shurikenCount == 1)
                {
                    shurikenRingBufferAgeRange.Observe(shurikenMeanAge);
                    shurikenRingBufferDisplacementRange.Observe(
                        shurikenMeanPosition.magnitude);
                }
                if (gpuCount == 1)
                {
                    gpuRingBufferAgeRange.Observe(gpuMeanAge);
                    gpuRingBufferDisplacementRange.Observe(
                        gpuMeanPosition.magnitude);
                }
            }

            if (elapsed >= RingBufferReplacementTime + 0.02f &&
                elapsed <= RingBufferReplacementTime + 0.3f)
            {
                shurikenRingBufferReplacementObserved |=
                    shurikenCount == 1 &&
                    shurikenMeanAge < 0.35f;
                gpuRingBufferReplacementObserved |=
                    gpuCount == 1 &&
                    gpuMeanAge < 0.35f;
            }
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

        static float FlipRotationExpectedMagnitudeRadians(float age)
        {
            age = Mathf.Max(0f, age);
            return FlipRotationStartRadians +
                   (FlipRotationLifetimeRadiansPerSecond +
                    FlipRotationBySpeedRadiansPerSecond) * age;
        }

        static Vector3 GravitySourceExpectedVelocity(float age)
        {
            age = Mathf.Max(0f, age);
            return new Vector3(
                GravitySourcePhysics2D.x,
                GravitySourcePhysics2D.y,
                0f) * GravitySourceModifier * age;
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

            Vector3 normalizedVelocity = velocity.sqrMagnitude > 1e-8f
                ? velocity.normalized
                : Vector3.zero;
            if (gpuSample)
            {
                gpuShapeDirectionXRange.Observe(normalizedVelocity.x);
                gpuShapeDirectionYRange.Observe(normalizedVelocity.y);
                gpuShapeDirectionZRange.Observe(normalizedVelocity.z);
            }
            else
            {
                shurikenShapeDirectionXRange.Observe(normalizedVelocity.x);
                shurikenShapeDirectionYRange.Observe(normalizedVelocity.y);
                shurikenShapeDirectionZRange.Observe(normalizedVelocity.z);
            }

            if (IsShapeArcProfile())
            {
                ObserveShapeArcSample(gpuSample, spawnPosition);
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

                case ParticleABValidationProfile.ShapeRandomDirectionPoint:
                    geometryError = BoxBoundsError(
                        spawnPosition,
                        Vector3.one);
                    break;

                case ParticleABValidationProfile.ShapeSphericalDirectionPoint:
                    geometryError = BoxBoundsError(
                        spawnPosition,
                        new Vector3(1f, 2f, 3f));
                    if (spawnPosition.sqrMagnitude > 1e-8f)
                    {
                        expectedDirection = spawnPosition.normalized;
                    }
                    break;

                case ParticleABValidationProfile.ShapeRandomPositionPoint:
                    expectedDirection = Vector3.forward;
                    geometryError = BoxBoundsError(
                        spawnPosition,
                        new Vector3(1.5f, 3f, 6f));
                    break;

                case ParticleABValidationProfile.ShapeArcRandomSpreadPoint:
                case ParticleABValidationProfile.ShapeArcLoopPoint:
                case ParticleABValidationProfile.ShapeArcPingPongPoint:
                case ParticleABValidationProfile.ShapeArcBurstSpreadPoint:
                {
                    Vector2 planar = new Vector2(
                        spawnPosition.x,
                        spawnPosition.y);
                    geometryError = Mathf.Max(
                        Mathf.Abs(planar.magnitude - 2f),
                        Mathf.Max(
                            Mathf.Abs(spawnPosition.z),
                            Mathf.Max(0f, -spawnPosition.y)));
                    break;
                }
            }

            float directionError = expectedDirection.sqrMagnitude > 1e-8f
                ? (normalizedVelocity - expectedDirection).magnitude
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

        void ObserveShapeArcSample(bool gpuSample, Vector3 spawnPosition)
        {
            float angle = Mathf.Atan2(
                spawnPosition.y,
                spawnPosition.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }
            if (angle > 359.9f && Mathf.Abs(spawnPosition.y) <= 0.005f)
            {
                angle = 0f;
            }

            if (gpuSample)
            {
                gpuShapeArcAngleRange.Observe(angle);
            }
            else
            {
                shurikenShapeArcAngleRange.Observe(angle);
            }

            if (validationProfile ==
                ParticleABValidationProfile.ShapeArcLoopPoint)
            {
                return;
            }

            const float step = 45f;
            int bin = Mathf.Clamp(Mathf.RoundToInt(angle / step), 0, 4);
            float gridError = Mathf.Abs(angle - bin * step);
            if (gpuSample)
            {
                maximumGPUShapeArcGridError = Mathf.Max(
                    maximumGPUShapeArcGridError,
                    gridError);
                if (gridError <= 1f)
                {
                    gpuShapeArcBinMask |= 1 << bin;
                }
            }
            else
            {
                maximumShurikenShapeArcGridError = Mathf.Max(
                    maximumShurikenShapeArcGridError,
                    gridError);
                if (gridError <= 1f)
                {
                    shurikenShapeArcBinMask |= 1 << bin;
                }
            }
        }

        static float BoxBoundsError(
            Vector3 position,
            Vector3 halfExtents)
        {
            Vector3 excess = new Vector3(
                Mathf.Abs(position.x) - halfExtents.x,
                Mathf.Abs(position.y) - halfExtents.y,
                Mathf.Abs(position.z) - halfExtents.z);
            return Mathf.Max(
                0f,
                Mathf.Max(excess.x, Mathf.Max(excess.y, excess.z)));
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
                case ParticleABValidationProfile.ShapeRandomDirectionPoint:
                    xRange = new Vector2(-1f, 1f);
                    yRange = new Vector2(-1f, 1f);
                    zRange = new Vector2(-1f, 1f);
                    break;
                case ParticleABValidationProfile.ShapeSphericalDirectionPoint:
                    xRange = new Vector2(-1f, 1f);
                    yRange = new Vector2(-2f, 2f);
                    zRange = new Vector2(-3f, 3f);
                    break;
                case ParticleABValidationProfile.ShapeRandomPositionPoint:
                    xRange = new Vector2(-1.5f, 1.5f);
                    yRange = new Vector2(-3f, 3f);
                    zRange = new Vector2(-6f, 6f);
                    break;
                case ParticleABValidationProfile.ShapeArcRandomSpreadPoint:
                case ParticleABValidationProfile.ShapeArcLoopPoint:
                case ParticleABValidationProfile.ShapeArcPingPongPoint:
                case ParticleABValidationProfile.ShapeArcBurstSpreadPoint:
                    xRange = new Vector2(-2f, 2f);
                    yRange = new Vector2(0f, 2f);
                    zRange = Vector2.zero;
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

        bool ShapeDirectionRangesPass()
        {
            const float minimum = -1f;
            const float maximum = 1f;
            return shurikenShapeDirectionXRange.Covers(minimum, maximum) &&
                   shurikenShapeDirectionYRange.Covers(minimum, maximum) &&
                   shurikenShapeDirectionZRange.Covers(minimum, maximum) &&
                   gpuShapeDirectionXRange.Covers(minimum, maximum) &&
                   gpuShapeDirectionYRange.Covers(minimum, maximum) &&
                   gpuShapeDirectionZRange.Covers(minimum, maximum);
        }

        bool ShapeArcAngleRangesPass()
        {
            return shurikenShapeArcAngleRange.Covers(0f, 180f) &&
                   gpuShapeArcAngleRange.Covers(0f, 180f);
        }

        bool ShapeArcGridPass(int expectedMask)
        {
            return maximumShurikenShapeArcGridError <= 0.25f &&
                   maximumGPUShapeArcGridError <= 0.25f &&
                   (shurikenShapeArcBinMask & expectedMask) == expectedMask &&
                   (gpuShapeArcBinMask & expectedMask) == expectedMask;
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

        void ObserveCullingMetrics(
            float elapsed,
            int shurikenCount,
            int gpuCount,
            float shurikenMeanAge,
            float gpuMeanAge)
        {
            if (!IsCullingProfile()) return;

            bool alwaysOffscreen = validationProfile ==
                    ParticleABValidationProfile.CullingAlwaysSimulatePoint ||
                validationProfile ==
                    ParticleABValidationProfile.CullingAutomaticOneShotPoint;
            float observationStart = alwaysOffscreen
                ? 0.25f
                : CullingExitViewTime + 0.2f;
            bool observeOffscreen = elapsed >= observationStart &&
                                    elapsed <= CullingReturnTime - 0.1f;
            if (observeOffscreen)
            {
                bool shurikenVisible = shurikenRenderer != null &&
                                       shurikenRenderer.isVisible;
                cullingOffscreenObserved |= !shurikenVisible &&
                                            !gpuParticles.isVisible;
                shurikenCulledCountRange.Observe(shurikenCount);
                gpuCulledCountRange.Observe(gpuCount);
                shurikenCulledMeanAgeRange.Observe(shurikenMeanAge);
                gpuCulledMeanAgeRange.Observe(gpuMeanAge);
            }

            if (alwaysOffscreen || cullingReturnObserved ||
                elapsed < CullingReturnTime + 0.2f)
            {
                return;
            }

            cullingReturnObserved = true;
            cullingReturnShurikenCount = shurikenCount;
            cullingReturnGPUCount = gpuCount;
            cullingReturnShurikenMeanAge = shurikenMeanAge;
            cullingReturnGPUMeanAge = gpuMeanAge;
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

        bool RingBufferPauseSemanticsPass()
        {
            return RingBufferPauseAgeRangePasses(
                       shurikenRingBufferAgeRange) &&
                   RingBufferPauseAgeRangePasses(
                       gpuRingBufferAgeRange) &&
                   RingBufferMotionContinues();
        }

        static bool RingBufferPauseAgeRangePasses(ObservedRange range)
        {
            return range.HasSamples &&
                   range.Minimum >= RingBufferLifetime - 0.03f &&
                   range.Maximum <= RingBufferLifetime + 0.01f &&
                   ObservedSpread(range) <= 0.03f;
        }

        bool RingBufferLoopSemanticsPass()
        {
            float expectedMinimum =
                RingBufferLoopRange.x * RingBufferLifetime;
            float expectedMaximum =
                RingBufferLoopRange.y * RingBufferLifetime;
            return RingBufferLoopAgeRangePasses(
                       shurikenRingBufferAgeRange,
                       expectedMinimum,
                       expectedMaximum) &&
                   RingBufferLoopAgeRangePasses(
                       gpuRingBufferAgeRange,
                       expectedMinimum,
                       expectedMaximum) &&
                   RingBufferMotionContinues();
        }

        static bool RingBufferLoopAgeRangePasses(
            ObservedRange range,
            float expectedMinimum,
            float expectedMaximum)
        {
            return range.HasSamples &&
                   range.Minimum >= expectedMinimum - 0.03f &&
                   range.Maximum <= expectedMaximum + 0.03f &&
                   ObservedSpread(range) >=
                       (expectedMaximum - expectedMinimum) * 0.7f;
        }

        bool RingBufferMotionContinues()
        {
            const float minimumDisplacementSpread = 0.6f;
            return shurikenRingBufferDisplacementRange.HasSamples &&
                   gpuRingBufferDisplacementRange.HasSamples &&
                   ObservedSpread(
                       shurikenRingBufferDisplacementRange) >=
                       minimumDisplacementSpread &&
                   ObservedSpread(
                       gpuRingBufferDisplacementRange) >=
                       minimumDisplacementSpread;
        }

        bool RingBufferReplacementPasses()
        {
            return shurikenRingBufferReplacementObserved &&
                   gpuRingBufferReplacementObserved;
        }

        void ObserveNoisePosition(bool gpuSample, Vector3 position, float age)
        {
            if (gpuSample)
            {
                gpuNoiseXRange.Observe(position.x);
                gpuNoiseYRange.Observe(position.y);
                gpuNoiseZRange.Observe(position.z);
            }
            else
            {
                shurikenNoiseXRange.Observe(position.x);
                shurikenNoiseYRange.Observe(position.y);
                shurikenNoiseZRange.Observe(position.z);
            }

            if (validationProfile !=
                ParticleABValidationProfile.NoiseSeparateAxesRemapPoint)
            {
                return;
            }

            Vector3 expectedPosition = Vector3.right * age;
            float error = (position - expectedPosition).magnitude;
            if (gpuSample)
            {
                maximumGPUNoiseKinematicsError = Mathf.Max(
                    maximumGPUNoiseKinematicsError,
                    error);
            }
            else
            {
                maximumShurikenNoiseKinematicsError = Mathf.Max(
                    maximumShurikenNoiseKinematicsError,
                    error);
            }
        }

        static bool NoiseRangePass(ObservedRange range, float minimumSpread)
        {
            return range.HasSamples &&
                   !float.IsNaN(range.Minimum) &&
                   !float.IsNaN(range.Maximum) &&
                   Mathf.Abs(range.Minimum) <= 8f &&
                   Mathf.Abs(range.Maximum) <= 8f &&
                   ObservedSpread(range) >= minimumSpread;
        }

        bool NoiseCurlRangesPass()
        {
            const float minimumSpread = 0.02f;
            return NoiseRangePass(shurikenNoiseXRange, minimumSpread) &&
                   NoiseRangePass(shurikenNoiseYRange, minimumSpread) &&
                   NoiseRangePass(shurikenNoiseZRange, minimumSpread) &&
                   NoiseRangePass(gpuNoiseXRange, minimumSpread) &&
                   NoiseRangePass(gpuNoiseYRange, minimumSpread) &&
                   NoiseRangePass(gpuNoiseZRange, minimumSpread);
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
            if (captureCamera != null)
            {
                captureCamera.transform.position =
                    captureCameraBasePositionWS;
            }
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

                case ParticleABValidationProfile.RingBufferPausePoint:
                    profileSpecificPassed =
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        maximumMeanLifetimeError <= 0.001f &&
                        maximumMeanSpeedError <= 0.001f &&
                        maximumMeanVelocityError <= 0.001f &&
                        maximumMeanPositionError <= 0.035f &&
                        maximumMeanStartRotationError <= 0.025f &&
                        maximumMeanSizeError <= 0.06f &&
                        RingBufferPauseSemanticsPass() &&
                        RingBufferReplacementPasses();
                    break;

                case ParticleABValidationProfile.RingBufferLoopPoint:
                    profileSpecificPassed =
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        maximumMeanLifetimeError <= 0.001f &&
                        maximumMeanSpeedError <= 0.001f &&
                        maximumMeanVelocityError <= 0.001f &&
                        maximumMeanPositionError <= 0.035f &&
                        maximumMeanStartRotationError <= 0.025f &&
                        maximumMeanSizeError <= 0.06f &&
                        RingBufferLoopSemanticsPass() &&
                        RingBufferReplacementPasses();
                    break;

                case ParticleABValidationProfile.StopActionCallbackPoint:
                    profileSpecificPassed =
                        shurikenStopActionObserver != null &&
                        gpuStopActionObserver != null &&
                        shurikenStopActionObserver.CallbackCount == 2 &&
                        gpuStopActionObserver.CallbackCount == 2 &&
                        Mathf.Abs(
                            shurikenStopActionObserver.FirstCallbackFrame -
                            gpuStopActionObserver.FirstCallbackFrame) <= 3 &&
                        Mathf.Abs(
                            shurikenStopActionObserver.LastCallbackFrame -
                            gpuStopActionObserver.LastCallbackFrame) <= 3 &&
                        stopActionRestartPlayingObserved &&
                        stopActionDisableObserved &&
                        stopActionDestroyObserved &&
                        shuriken.isStopped &&
                        gpuParticles.isStopped &&
                        maximumShurikenParticleCount > 0 &&
                        maximumGPUParticleCount > 0;
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

                case ParticleABValidationProfile.FlipRotationPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.001f &&
                        maximumMeanVelocityError <= 0.001f &&
                        maximumShurikenParticleCount > 0 &&
                        maximumGPUParticleCount > 0 &&
                        maximumShurikenRotationError <= 0.05f &&
                        maximumGPURotationError <= 0.02f &&
                        shurikenStartRotationRange.HasSamples &&
                        shurikenStartRotationRange.Minimum <= -0.5f &&
                        shurikenStartRotationRange.Maximum >= 0.5f &&
                        gpuStartRotationRange.HasSamples &&
                        gpuStartRotationRange.Minimum <= -0.5f &&
                        gpuStartRotationRange.Maximum >= 0.5f;
                    break;

                case ParticleABValidationProfile.GravitySource2DPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.001f &&
                        maximumMeanVelocityError <= 0.001f &&
                        maximumForceKinematicsError <= 0.005f &&
                        maximumShurikenParticleCount > 0 &&
                        maximumGPUParticleCount > 0;
                    break;

                case ParticleABValidationProfile.CustomSimulationSpacePoint:
                    profileSpecificPassed =
                        maximumCountDelta == 0 &&
                        maximumMeanAgeError <= 0.002f &&
                        maximumMeanSpeedError <= 0.001f &&
                        maximumMeanVelocityError <= 0.001f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount > 0 &&
                        maximumGPUParticleCount > 0 &&
                        ObservedSpread(shurikenCustomWorldXRange) >= 1f &&
                        ObservedSpread(gpuCustomWorldXRange) >= 1f;
                    break;

                case ParticleABValidationProfile.CullingAutomaticLoopPoint:
                case ParticleABValidationProfile.CullingPausePoint:
                    profileSpecificPassed =
                        cullingOffscreenObserved &&
                        cullingReturnObserved &&
                        ObservedSpread(shurikenCulledCountRange) <= 0.1f &&
                        ObservedSpread(gpuCulledCountRange) <= 0.1f &&
                        ObservedSpread(shurikenCulledMeanAgeRange) <= 0.03f &&
                        ObservedSpread(gpuCulledMeanAgeRange) <= 0.03f &&
                        cullingReturnShurikenCount >
                            shurikenCulledCountRange.Maximum &&
                        cullingReturnGPUCount >
                            gpuCulledCountRange.Maximum;
                    break;

                case ParticleABValidationProfile.CullingPauseAndCatchupPoint:
                    profileSpecificPassed =
                        cullingOffscreenObserved &&
                        cullingReturnObserved &&
                        ObservedSpread(shurikenCulledCountRange) <= 0.1f &&
                        ObservedSpread(gpuCulledCountRange) <= 0.1f &&
                        ObservedSpread(shurikenCulledMeanAgeRange) <= 0.03f &&
                        ObservedSpread(gpuCulledMeanAgeRange) <= 0.03f &&
                        cullingReturnShurikenCount >=
                            shurikenCulledCountRange.Maximum + 8f &&
                        cullingReturnGPUCount >=
                            gpuCulledCountRange.Maximum + 8f &&
                        cullingReturnShurikenMeanAge >=
                            shurikenCulledMeanAgeRange.Maximum + 0.25f &&
                        cullingReturnGPUMeanAge >=
                            gpuCulledMeanAgeRange.Maximum + 0.25f;
                    break;

                case ParticleABValidationProfile.CullingAlwaysSimulatePoint:
                    profileSpecificPassed =
                        cullingOffscreenObserved &&
                        maximumShurikenParticleCount > 0 &&
                        maximumGPUParticleCount > 0 &&
                        ObservedSpread(shurikenCulledCountRange) >= 8f &&
                        ObservedSpread(gpuCulledCountRange) >= 8f;
                    break;

                case ParticleABValidationProfile.CullingAutomaticOneShotPoint:
                    profileSpecificPassed =
                        cullingOffscreenObserved &&
                        maximumShurikenParticleCount > 0 &&
                        maximumGPUParticleCount > 0 &&
                        shuriken.isStopped &&
                        gpuParticles.isStopped;
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

                case ParticleABValidationProfile.EmitterVelocityCustomPoint:
                case ParticleABValidationProfile.EmitterVelocityRigidbodyPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.04f &&
                                            maximumMeanVelocityError <= 0.05f &&
                                            maximumMeanPositionError <= 0.06f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            shurikenSpeedRange.HasSamples &&
                                            gpuSpeedRange.HasSamples;
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

                case ParticleABValidationProfile.NoiseCurlPositionPoint:
                    profileSpecificPassed = maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            NoiseCurlRangesPass();
                    break;

                case ParticleABValidationProfile.NoiseSeparateAxesRemapPoint:
                    profileSpecificPassed = maximumShurikenParticleCount == 1 &&
                                            maximumGPUParticleCount == 1 &&
                                            maximumShurikenNoiseKinematicsError <= 0.03f &&
                                            maximumGPUNoiseKinematicsError <= 0.03f;
                    break;

                case ParticleABValidationProfile.NoiseRotationSizePoint:
                    profileSpecificPassed = maximumShurikenParticleCount == 1 &&
                                            maximumGPUParticleCount == 1 &&
                                            maximumShurikenNoiseRotationError <= 0.03f &&
                                            maximumGPUNoiseRotationError <= 0.03f &&
                                            maximumGPUNoiseSizeError <= 0.03f &&
                                            noiseSizeClassificationFailures == 0 &&
                                            maximumNoiseSizePixelError <= 3f;
                    break;

                case ParticleABValidationProfile.CollisionPlaneBouncePoint:
                {
                    float minimumCollisionHeight =
                        CollisionPlaneHeight + CollisionParticleRadius - 0.04f;
                    profileSpecificPassed =
                        maximumShurikenParticleCount > 0 &&
                        maximumGPUParticleCount > 0 &&
                        maximumMeanSpeedError <= 0.12f &&
                        maximumMeanVelocityError <= 0.15f &&
                        maximumMeanPositionError <= 0.12f &&
                        shurikenCollisionHeightRange.HasSamples &&
                        gpuCollisionHeightRange.HasSamples &&
                        shurikenCollisionHeightRange.Minimum >=
                            minimumCollisionHeight &&
                        gpuCollisionHeightRange.Minimum >=
                            minimumCollisionHeight &&
                        shurikenCollisionVelocityYRange.Minimum <= -2.8f &&
                        gpuCollisionVelocityYRange.Minimum <= -2.8f &&
                        shurikenCollisionVelocityYRange.Maximum >= 1.3f &&
                        gpuCollisionVelocityYRange.Maximum >= 1.3f;
                    break;
                }

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

                case ParticleABValidationProfile.ShapeRandomDirectionPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenShapeGeometryError <= 0.002f &&
                                            maximumGPUShapeGeometryError <= 0.002f &&
                                            ShapeSpawnRangesPass() &&
                                            ShapeDirectionRangesPass();
                    break;

                case ParticleABValidationProfile.ShapeSphericalDirectionPoint:
                case ParticleABValidationProfile.ShapeRandomPositionPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenShapeDirectionError <= 0.003f &&
                                            maximumGPUShapeDirectionError <= 0.003f &&
                                            maximumShurikenShapeGeometryError <= 0.002f &&
                                            maximumGPUShapeGeometryError <= 0.002f &&
                                            ShapeSpawnRangesPass();
                    break;

                case ParticleABValidationProfile.ShapeArcRandomSpreadPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenShapeGeometryError <= 0.003f &&
                                            maximumGPUShapeGeometryError <= 0.003f &&
                                            ShapeSpawnRangesPass() &&
                                            ShapeArcAngleRangesPass() &&
                                            ShapeArcGridPass(0x0F);
                    break;

                case ParticleABValidationProfile.ShapeArcLoopPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanPositionError <= 0.18f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenShapeGeometryError <= 0.003f &&
                                            maximumGPUShapeGeometryError <= 0.003f &&
                                            ShapeSpawnRangesPass() &&
                                            ShapeArcAngleRangesPass();
                    break;

                case ParticleABValidationProfile.ShapeArcPingPongPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanPositionError <= 0.12f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenShapeGeometryError <= 0.003f &&
                                            maximumGPUShapeGeometryError <= 0.003f &&
                                            ShapeSpawnRangesPass() &&
                                            ShapeArcAngleRangesPass() &&
                                            ShapeArcGridPass(0x0F);
                    break;

                case ParticleABValidationProfile.ShapeArcBurstSpreadPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanPositionError <= 0.03f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenShapeGeometryError <= 0.003f &&
                                            maximumGPUShapeGeometryError <= 0.003f &&
                                            ShapeSpawnRangesPass() &&
                                            ShapeArcAngleRangesPass() &&
                                            ShapeArcGridPass(0x1F);
                    break;

                case ParticleABValidationProfile.MaterialColorModesPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        materialColorComparableSamples >= 50 &&
                        materialColorClassificationFailures == 0 &&
                        maximumMaterialColorError <= 0.08f &&
                        shurikenMaterialColorModeMask == 0x3F &&
                        gpuMaterialColorModeMask == 0x3F &&
                        MaterialColorModesHaveSamples(5) &&
                        MaterialColorModesAreDistinct(0.15f);
                    break;

                case ParticleABValidationProfile.MaterialBlendModesPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        materialBlendComparableSamples >= 40 &&
                        materialBlendClassificationFailures == 0 &&
                        maximumMaterialBlendError <= 0.08f &&
                        shurikenMaterialBlendModeMask == 0x0F &&
                        gpuMaterialBlendModeMask == 0x0F &&
                        MaterialBlendModesHaveSamples(5) &&
                        MaterialBlendModesAreDistinct(0.04f);
                    break;

                case ParticleABValidationProfile.MaterialAlphaClipPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        materialAlphaClipComparableSamples >= 40 &&
                        materialAlphaClipClassificationFailures == 0 &&
                        maximumMaterialAlphaClipWidthError <= 2f &&
                        shurikenMaterialAlphaClipStateMask == 0x0F &&
                        gpuMaterialAlphaClipStateMask == 0x0F &&
                        MaterialAlphaClipStatesHaveSamples(5) &&
                        MaterialAlphaClipSemanticsPass();
                    break;

                case ParticleABValidationProfile.MaterialSoftParticlesPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        materialSoftParticleComparableSamples >= 40 &&
                        materialSoftParticleClassificationFailures == 0 &&
                        maximumMaterialSoftParticleColorError <= 0.08f &&
                        shurikenMaterialSoftParticleStateMask == 0x0F &&
                        gpuMaterialSoftParticleStateMask == 0x0F &&
                        MaterialSoftParticleStatesHaveSamples(5) &&
                        MaterialSoftParticleSemanticsPass();
                    break;

                case ParticleABValidationProfile.MaterialCameraFadingPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        materialCameraFadeComparableSamples >= 40 &&
                        materialCameraFadeClassificationFailures == 0 &&
                        maximumMaterialCameraFadeColorError <= 0.08f &&
                        shurikenMaterialCameraFadeStateMask == 0x0F &&
                        gpuMaterialCameraFadeStateMask == 0x0F &&
                        MaterialCameraFadeStatesHaveSamples(5) &&
                        MaterialCameraFadeSemanticsPass();
                    break;

                case ParticleABValidationProfile.TextureSheetBlendLifetimePoint:
                case ParticleABValidationProfile.TextureSheetBlendSpeedPoint:
                case ParticleABValidationProfile.TextureSheetBlendFPSPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        textureSheetBlendComparableSamples >= 40 &&
                        textureSheetBlendClassificationFailures == 0 &&
                        maximumTextureSheetBlendColorError <= 0.08f &&
                        shurikenTextureSheetBlendIntermediateSamples >= 20 &&
                        gpuTextureSheetBlendIntermediateSamples >= 20 &&
                        shurikenTextureSheetBlendRedRange.HasSamples &&
                        shurikenTextureSheetBlendRedRange.Minimum <= 0.25f &&
                        shurikenTextureSheetBlendRedRange.Maximum >= 0.75f &&
                        shurikenTextureSheetBlendGreenRange.HasSamples &&
                        shurikenTextureSheetBlendGreenRange.Minimum <= 0.25f &&
                        shurikenTextureSheetBlendGreenRange.Maximum >= 0.75f &&
                        gpuTextureSheetBlendRedRange.HasSamples &&
                        gpuTextureSheetBlendRedRange.Minimum <= 0.25f &&
                        gpuTextureSheetBlendRedRange.Maximum >= 0.75f &&
                        gpuTextureSheetBlendGreenRange.HasSamples &&
                        gpuTextureSheetBlendGreenRange.Minimum <= 0.25f &&
                        gpuTextureSheetBlendGreenRange.Maximum >= 0.75f;
                    break;

                case ParticleABValidationProfile.RendererTextureUVFlipPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        textureUVFlipComparableSamples >= 20 &&
                        textureUVFlipClassificationFailures == 0 &&
                        textureUVFlipSemanticFailures == 0 &&
                        maximumTextureUVFlipColorError <= 0.08f &&
                        maximumTextureUVFlipExpectedColorError <= 0.25f;
                    break;

                case ParticleABValidationProfile.StretchedBillboardPoint:
                    profileSpecificPassed =
                        maximumMeanSpeedError <= 0.01f &&
                        maximumMeanPositionError <= 0.04f &&
                        maximumShurikenParticleCount == 1 &&
                        maximumGPUParticleCount == 1 &&
                        stretchedBillboardComparableSamples >= 60 &&
                        stretchedBillboardClassificationFailures == 0 &&
                        shurikenStretchedBillboardStateMask == 0x07 &&
                        gpuStretchedBillboardStateMask == 0x07 &&
                        maximumStretchedBillboardCentroidError <= 0.08f &&
                        maximumStretchedBillboardAspectError <= 0.15f &&
                        StretchedStateSeparation(
                            shurikenStretchedStateSignatureSums,
                            shurikenStretchedStateSignatureSamples) >= 0.08f &&
                        StretchedStateSeparation(
                            gpuStretchedStateSignatureSums,
                            gpuStretchedStateSignatureSamples) >= 0.08f;
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
                    : validationProfile ==
                        ParticleABValidationProfile.StopActionCallbackPoint
                        ? 1
                    : validationProfile ==
                        ParticleABValidationProfile.CollisionPlaneBouncePoint
                        ? 1
                    : IsEmitterVelocityProfile()
                        ? 1
                    : IsCullingProfile()
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
                    : validationProfile ==
                        ParticleABValidationProfile.StopActionCallbackPoint
                        ? 0.03f
                    : validationProfile ==
                        ParticleABValidationProfile.CollisionPlaneBouncePoint
                        ? 0.03f
                    : IsRingBufferProfile()
                        ? 0.03f
                    : IsEmitterVelocityProfile()
                        ? 0.03f
                    : IsCullingProfile()
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
                $"maxShurikenNoiseKinematicsError=" +
                $"{maximumShurikenNoiseKinematicsError:R}; " +
                $"maxGPUNoiseKinematicsError=" +
                $"{maximumGPUNoiseKinematicsError:R}; " +
                $"maxShurikenNoiseRotationError=" +
                $"{maximumShurikenNoiseRotationError:R}; " +
                $"maxGPUNoiseRotationError=" +
                $"{maximumGPUNoiseRotationError:R}; " +
                $"maxGPUNoiseSizeError={maximumGPUNoiseSizeError:R}; " +
                $"maxNoiseSizePixelError={maximumNoiseSizePixelError:R}; " +
                $"noiseSizeClassificationFailures=" +
                $"{noiseSizeClassificationFailures}; " +
                $"shurikenNoiseRanges=" +
                $"({FormatRange(shurikenNoiseXRange)}," +
                $"{FormatRange(shurikenNoiseYRange)}," +
                $"{FormatRange(shurikenNoiseZRange)}); " +
                $"gpuNoiseRanges=" +
                $"({FormatRange(gpuNoiseXRange)}," +
                $"{FormatRange(gpuNoiseYRange)}," +
                $"{FormatRange(gpuNoiseZRange)}); " +
                $"shurikenCollisionHeightRange=" +
                $"{FormatRange(shurikenCollisionHeightRange)}; " +
                $"gpuCollisionHeightRange=" +
                $"{FormatRange(gpuCollisionHeightRange)}; " +
                $"shurikenCollisionVelocityYRange=" +
                $"{FormatRange(shurikenCollisionVelocityYRange)}; " +
                $"gpuCollisionVelocityYRange=" +
                $"{FormatRange(gpuCollisionVelocityYRange)}; " +
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
                $"shurikenRingBufferAgeRange=" +
                $"{FormatRange(shurikenRingBufferAgeRange)}; " +
                $"gpuRingBufferAgeRange=" +
                $"{FormatRange(gpuRingBufferAgeRange)}; " +
                $"shurikenRingBufferDisplacementRange=" +
                $"{FormatRange(shurikenRingBufferDisplacementRange)}; " +
                $"gpuRingBufferDisplacementRange=" +
                $"{FormatRange(gpuRingBufferDisplacementRange)}; " +
                $"ringBufferReplacementObserved=" +
                $"({shurikenRingBufferReplacementObserved}," +
                $"{gpuRingBufferReplacementObserved}); " +
                $"stopActionShurikenCallbacks=" +
                $"{(shurikenStopActionObserver != null ? shurikenStopActionObserver.CallbackCount : 0)}; " +
                $"stopActionGPUCallbacks=" +
                $"{(gpuStopActionObserver != null ? gpuStopActionObserver.CallbackCount : 0)}; " +
                $"stopActionShurikenFirstFrame=" +
                $"{(shurikenStopActionObserver != null ? shurikenStopActionObserver.FirstCallbackFrame : -1)}; " +
                $"stopActionGPUFirstFrame=" +
                $"{(gpuStopActionObserver != null ? gpuStopActionObserver.FirstCallbackFrame : -1)}; " +
                $"stopActionShurikenLastFrame=" +
                $"{(shurikenStopActionObserver != null ? shurikenStopActionObserver.LastCallbackFrame : -1)}; " +
                $"stopActionGPULastFrame=" +
                $"{(gpuStopActionObserver != null ? gpuStopActionObserver.LastCallbackFrame : -1)}; " +
                $"stopActionRestartPlayingObserved=" +
                $"{stopActionRestartPlayingObserved}; " +
                $"stopActionDisableObserved={stopActionDisableObserved}; " +
                $"stopActionDestroyObserved={stopActionDestroyObserved}; " +
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
                $"cullingOffscreenObserved={cullingOffscreenObserved}; " +
                $"cullingReturnObserved={cullingReturnObserved}; " +
                $"cullingShurikenCountRange=" +
                $"{FormatRange(shurikenCulledCountRange)}; " +
                $"cullingGPUCountRange=" +
                $"{FormatRange(gpuCulledCountRange)}; " +
                $"cullingShurikenAgeRange=" +
                $"{FormatRange(shurikenCulledMeanAgeRange)}; " +
                $"cullingGPUAgeRange=" +
                $"{FormatRange(gpuCulledMeanAgeRange)}; " +
                $"cullingReturnCounts=" +
                $"({cullingReturnShurikenCount}," +
                $"{cullingReturnGPUCount}); " +
                $"cullingReturnMeanAges=" +
                $"({cullingReturnShurikenMeanAge:R}," +
                $"{cullingReturnGPUMeanAge:R}); " +
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
                $"maxShurikenShapeArcGridError=" +
                $"{maximumShurikenShapeArcGridError:R}; " +
                $"maxGPUShapeArcGridError={maximumGPUShapeArcGridError:R}; " +
                $"shurikenShapeArcBinMask=0x{shurikenShapeArcBinMask:X2}; " +
                $"gpuShapeArcBinMask=0x{gpuShapeArcBinMask:X2}; " +
                $"shurikenShapeArcAngleRange=" +
                $"{FormatRange(shurikenShapeArcAngleRange)}; " +
                $"gpuShapeArcAngleRange={FormatRange(gpuShapeArcAngleRange)}; " +
                $"maxShurikenCount={maximumShurikenParticleCount}; " +
                $"maxGPUCount={maximumGPUParticleCount}; " +
                $"textureSheetComparableSamples={textureSheetComparableSamples}; " +
                $"textureSheetFrameMismatches={textureSheetFrameMismatches}; " +
                $"textureSheetClassificationFailures={textureSheetClassificationFailures}; " +
                $"maxTextureSheetFrameDelta={maximumTextureSheetFrameDelta}; " +
                $"shurikenTextureSheetFrameMask=0x{shurikenTextureSheetFrameMask:X2}; " +
                $"gpuTextureSheetFrameMask=0x{gpuTextureSheetFrameMask:X2}; " +
                $"textureSheetBlendComparableSamples=" +
                $"{textureSheetBlendComparableSamples}; " +
                $"textureSheetBlendClassificationFailures=" +
                $"{textureSheetBlendClassificationFailures}; " +
                $"maxTextureSheetBlendColorError=" +
                $"{maximumTextureSheetBlendColorError:R}; " +
                $"shurikenTextureSheetBlendIntermediateSamples=" +
                $"{shurikenTextureSheetBlendIntermediateSamples}; " +
                $"gpuTextureSheetBlendIntermediateSamples=" +
                $"{gpuTextureSheetBlendIntermediateSamples}; " +
                $"shurikenTextureSheetBlendRanges=" +
                $"({FormatRange(shurikenTextureSheetBlendRedRange)}," +
                $"{FormatRange(shurikenTextureSheetBlendGreenRange)}); " +
                $"gpuTextureSheetBlendRanges=" +
                $"({FormatRange(gpuTextureSheetBlendRedRange)}," +
                $"{FormatRange(gpuTextureSheetBlendGreenRange)}); " +
                $"textureUVFlipComparableSamples=" +
                $"{textureUVFlipComparableSamples}; " +
                $"textureUVFlipClassificationFailures=" +
                $"{textureUVFlipClassificationFailures}; " +
                $"textureUVFlipSemanticFailures=" +
                $"{textureUVFlipSemanticFailures}; " +
                $"maxTextureUVFlipColorError=" +
                $"{maximumTextureUVFlipColorError:R}; " +
                $"maxTextureUVFlipExpectedColorError=" +
                $"{maximumTextureUVFlipExpectedColorError:R}; " +
                $"stretchedBillboardComparableSamples=" +
                $"{stretchedBillboardComparableSamples}; " +
                $"stretchedBillboardClassificationFailures=" +
                $"{stretchedBillboardClassificationFailures}; " +
                $"shurikenStretchedBillboardStateMask=" +
                $"0x{shurikenStretchedBillboardStateMask:X2}; " +
                $"gpuStretchedBillboardStateMask=" +
                $"0x{gpuStretchedBillboardStateMask:X2}; " +
                $"maxStretchedBillboardCentroidError=" +
                $"{maximumStretchedBillboardCentroidError:R}; " +
                $"maxStretchedBillboardAspectError=" +
                $"{maximumStretchedBillboardAspectError:R}; " +
                $"shurikenStretchedBillboardStateSeparation=" +
                $"{StretchedStateSeparation(shurikenStretchedStateSignatureSums, shurikenStretchedStateSignatureSamples):R}; " +
                $"gpuStretchedBillboardStateSeparation=" +
                $"{StretchedStateSeparation(gpuStretchedStateSignatureSums, gpuStretchedStateSignatureSamples):R}; " +
                $"materialColorComparableSamples=" +
                $"{materialColorComparableSamples}; " +
                $"materialColorClassificationFailures=" +
                $"{materialColorClassificationFailures}; " +
                $"maxMaterialColorError={maximumMaterialColorError:R}; " +
                $"shurikenMaterialColorModeMask=" +
                $"0x{shurikenMaterialColorModeMask:X2}; " +
                $"gpuMaterialColorModeMask=" +
                $"0x{gpuMaterialColorModeMask:X2}; " +
                $"materialColorModeErrors=" +
                $"{FormatMaterialColorModeErrors()}; " +
                $"shurikenMaterialColorModeMeans=" +
                $"{FormatMaterialColorModeMeans()}; " +
                $"materialBlendComparableSamples=" +
                $"{materialBlendComparableSamples}; " +
                $"materialBlendClassificationFailures=" +
                $"{materialBlendClassificationFailures}; " +
                $"maxMaterialBlendError={maximumMaterialBlendError:R}; " +
                $"shurikenMaterialBlendModeMask=" +
                $"0x{shurikenMaterialBlendModeMask:X2}; " +
                $"gpuMaterialBlendModeMask=" +
                $"0x{gpuMaterialBlendModeMask:X2}; " +
                $"materialBlendModeErrors=" +
                $"{FormatMaterialBlendModeErrors()}; " +
                $"shurikenMaterialBlendModeMeans=" +
                $"{FormatMaterialBlendModeMeans()}; " +
                $"materialAlphaClipComparableSamples=" +
                $"{materialAlphaClipComparableSamples}; " +
                $"materialAlphaClipClassificationFailures=" +
                $"{materialAlphaClipClassificationFailures}; " +
                $"maxMaterialAlphaClipWidthError=" +
                $"{maximumMaterialAlphaClipWidthError:R}; " +
                $"shurikenMaterialAlphaClipStateMask=" +
                $"0x{shurikenMaterialAlphaClipStateMask:X2}; " +
                $"gpuMaterialAlphaClipStateMask=" +
                $"0x{gpuMaterialAlphaClipStateMask:X2}; " +
                $"materialAlphaClipStateErrors=" +
                $"{FormatMaterialAlphaClipStateErrors()}; " +
                $"shurikenMaterialAlphaClipWidths=" +
                $"{FormatMaterialAlphaClipWidths()}; " +
                $"materialSoftParticleComparableSamples=" +
                $"{materialSoftParticleComparableSamples}; " +
                $"materialSoftParticleClassificationFailures=" +
                $"{materialSoftParticleClassificationFailures}; " +
                $"maxMaterialSoftParticleColorError=" +
                $"{maximumMaterialSoftParticleColorError:R}; " +
                $"shurikenMaterialSoftParticleStateMask=" +
                $"0x{shurikenMaterialSoftParticleStateMask:X2}; " +
                $"gpuMaterialSoftParticleStateMask=" +
                $"0x{gpuMaterialSoftParticleStateMask:X2}; " +
                $"materialSoftParticleStateErrors=" +
                $"{FormatMaterialSoftParticleStateErrors()}; " +
                $"shurikenMaterialSoftParticleStateMeans=" +
                $"{FormatMaterialSoftParticleStateMeans()}; " +
                $"materialCameraFadeComparableSamples=" +
                $"{materialCameraFadeComparableSamples}; " +
                $"materialCameraFadeClassificationFailures=" +
                $"{materialCameraFadeClassificationFailures}; " +
                $"maxMaterialCameraFadeColorError=" +
                $"{maximumMaterialCameraFadeColorError:R}; " +
                $"shurikenMaterialCameraFadeStateMask=" +
                $"0x{shurikenMaterialCameraFadeStateMask:X2}; " +
                $"gpuMaterialCameraFadeStateMask=" +
                $"0x{gpuMaterialCameraFadeStateMask:X2}; " +
                $"materialCameraFadeStateErrors=" +
                $"{FormatMaterialCameraFadeStateErrors()}; " +
                $"shurikenMaterialCameraFadeStateMeans=" +
                $"{FormatMaterialCameraFadeStateMeans()}; " +
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
                $"shurikenShapeDirectionRanges=" +
                $"({FormatRange(shurikenShapeDirectionXRange)}," +
                $"{FormatRange(shurikenShapeDirectionYRange)}," +
                $"{FormatRange(shurikenShapeDirectionZRange)}); " +
                $"gpuShapeDirectionRanges=" +
                $"({FormatRange(gpuShapeDirectionXRange)}," +
                $"{FormatRange(gpuShapeDirectionYRange)}," +
                $"{FormatRange(gpuShapeDirectionZRange)}); " +
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
                $"{FormatRange(gpuStartRotationBlendRange)}; " +
                $"shurikenCustomWorldXRange=" +
                $"{FormatRange(shurikenCustomWorldXRange)}; " +
                $"gpuCustomWorldXRange=" +
                $"{FormatRange(gpuCustomWorldXRange)}", this);
            Debug.Log($"PARTICLE_AB_CAPTURE_COMPLETE:{sessionFolder}", this);
            RestoreGravityOverride();

#if UNITY_EDITOR
            if (exitEditorWhenCaptureCompletes && Application.isBatchMode)
            {
                UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
            }
#endif
        }

        bool MaterialColorModesHaveSamples(int minimumSamples)
        {
            for (int i = 0; i < MaterialColorModeCount; i++)
            {
                if (shurikenMaterialColorSamples[i] < minimumSamples)
                {
                    return false;
                }
            }
            return true;
        }

        bool MaterialColorModesAreDistinct(float minimumDistance)
        {
            if (!MaterialColorModesHaveSamples(1)) return false;

            float maximumDistance = 0f;
            for (int first = 0; first < MaterialColorModeCount; first++)
            {
                Vector3 firstMean = shurikenMaterialColorSums[first] /
                    shurikenMaterialColorSamples[first];
                for (int second = first + 1;
                    second < MaterialColorModeCount;
                    second++)
                {
                    Vector3 secondMean = shurikenMaterialColorSums[second] /
                        shurikenMaterialColorSamples[second];
                    maximumDistance = Mathf.Max(
                        maximumDistance,
                        Vector3.Distance(firstMean, secondMean));
                }
            }
            return maximumDistance >= minimumDistance;
        }

        string FormatMaterialColorModeErrors()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialColorModeCount; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(
                    maximumMaterialColorModeErrors[i].ToString(
                        "R", CultureInfo.InvariantCulture));
            }
            return text.Append(']').ToString();
        }

        string FormatMaterialColorModeMeans()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialColorModeCount; i++)
            {
                if (i > 0) text.Append(',');
                if (shurikenMaterialColorSamples[i] <= 0)
                {
                    text.Append("[]");
                    continue;
                }

                Vector3 mean = shurikenMaterialColorSums[i] /
                    shurikenMaterialColorSamples[i];
                text.Append('(')
                    .Append(mean.x.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.y.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.z.ToString("R", CultureInfo.InvariantCulture))
                    .Append(')');
            }
            return text.Append(']').ToString();
        }

        bool MaterialBlendModesHaveSamples(int minimumSamples)
        {
            for (int i = 0; i < MaterialBlendModeCount; i++)
            {
                if (shurikenMaterialBlendSamples[i] < minimumSamples)
                {
                    return false;
                }
            }
            return true;
        }

        bool MaterialBlendModesAreDistinct(float minimumDistance)
        {
            if (!MaterialBlendModesHaveSamples(1)) return false;

            float closestDistance = float.PositiveInfinity;
            for (int first = 0; first < MaterialBlendModeCount; first++)
            {
                Vector3 firstMean = shurikenMaterialBlendSums[first] /
                    shurikenMaterialBlendSamples[first];
                for (int second = first + 1;
                    second < MaterialBlendModeCount;
                    second++)
                {
                    Vector3 secondMean =
                        shurikenMaterialBlendSums[second] /
                        shurikenMaterialBlendSamples[second];
                    closestDistance = Mathf.Min(
                        closestDistance,
                        Vector3.Distance(firstMean, secondMean));
                }
            }
            return closestDistance >= minimumDistance;
        }

        string FormatMaterialBlendModeErrors()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialBlendModeCount; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(
                    maximumMaterialBlendModeErrors[i].ToString(
                        "R", CultureInfo.InvariantCulture));
            }
            return text.Append(']').ToString();
        }

        string FormatMaterialBlendModeMeans()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialBlendModeCount; i++)
            {
                if (i > 0) text.Append(',');
                if (shurikenMaterialBlendSamples[i] <= 0)
                {
                    text.Append("[]");
                    continue;
                }

                Vector3 mean = shurikenMaterialBlendSums[i] /
                    shurikenMaterialBlendSamples[i];
                text.Append('(')
                    .Append(mean.x.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.y.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.z.ToString("R", CultureInfo.InvariantCulture))
                    .Append(')');
            }
            return text.Append(']').ToString();
        }

        bool MaterialAlphaClipStatesHaveSamples(int minimumSamples)
        {
            for (int i = 0; i < MaterialAlphaClipStateCount; i++)
            {
                if (shurikenMaterialAlphaClipSamples[i] < minimumSamples)
                {
                    return false;
                }
            }
            return true;
        }

        bool MaterialAlphaClipSemanticsPass()
        {
            if (!MaterialAlphaClipStatesHaveSamples(1)) return false;

            float fullWidth = MaterialAlphaClipMeanWidth(0);
            float quarterCutoffWidth = MaterialAlphaClipMeanWidth(1);
            float halfCutoffWidth = MaterialAlphaClipMeanWidth(2);
            float threeQuarterCutoffWidth =
                MaterialAlphaClipMeanWidth(3);
            return fullWidth >= 48f &&
                   threeQuarterCutoffWidth >= 8f &&
                   fullWidth - quarterCutoffWidth >= 8f &&
                   quarterCutoffWidth - halfCutoffWidth >= 8f &&
                   halfCutoffWidth - threeQuarterCutoffWidth >= 8f &&
                   quarterCutoffWidth / fullWidth >= 0.65f &&
                   quarterCutoffWidth / fullWidth <= 0.85f &&
                   halfCutoffWidth / fullWidth >= 0.4f &&
                   halfCutoffWidth / fullWidth <= 0.6f &&
                   threeQuarterCutoffWidth / fullWidth >= 0.15f &&
                   threeQuarterCutoffWidth / fullWidth <= 0.35f;
        }

        float MaterialAlphaClipMeanWidth(int state)
        {
            return shurikenMaterialAlphaClipSamples[state] > 0
                ? shurikenMaterialAlphaClipWidthSums[state] /
                  shurikenMaterialAlphaClipSamples[state]
                : -1f;
        }

        string FormatMaterialAlphaClipStateErrors()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialAlphaClipStateCount; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(
                    maximumMaterialAlphaClipStateErrors[i].ToString(
                        "R", CultureInfo.InvariantCulture));
            }
            return text.Append(']').ToString();
        }

        string FormatMaterialAlphaClipWidths()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialAlphaClipStateCount; i++)
            {
                if (i > 0) text.Append(',');
                if (shurikenMaterialAlphaClipSamples[i] <= 0)
                {
                    text.Append("[]");
                    continue;
                }
                text.Append(
                    MaterialAlphaClipMeanWidth(i).ToString(
                        "R", CultureInfo.InvariantCulture));
            }
            return text.Append(']').ToString();
        }

        bool MaterialSoftParticleStatesHaveSamples(int minimumSamples)
        {
            for (int i = 0; i < MaterialSoftParticleStateCount; i++)
            {
                if (shurikenMaterialSoftParticleSamples[i] < minimumSamples)
                {
                    return false;
                }
            }
            return true;
        }

        bool MaterialSoftParticleSemanticsPass()
        {
            if (!MaterialSoftParticleStatesHaveSamples(1)) return false;

            float fullRed = MaterialSoftParticleMeanColor(0).x;
            float nearRed = MaterialSoftParticleMeanColor(1).x;
            float middleRed = MaterialSoftParticleMeanColor(2).x;
            float farRed = MaterialSoftParticleMeanColor(3).x;
            return fullRed - nearRed >= 0.2f &&
                   middleRed - nearRed >= 0.12f &&
                   fullRed - middleRed >= 0.08f &&
                   Mathf.Abs(fullRed - farRed) <= 0.04f;
        }

        Vector3 MaterialSoftParticleMeanColor(int state)
        {
            return shurikenMaterialSoftParticleSamples[state] > 0
                ? shurikenMaterialSoftParticleColorSums[state] /
                  shurikenMaterialSoftParticleSamples[state]
                : Vector3.zero;
        }

        string FormatMaterialSoftParticleStateErrors()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialSoftParticleStateCount; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(
                    maximumMaterialSoftParticleStateErrors[i].ToString(
                        "R", CultureInfo.InvariantCulture));
            }
            return text.Append(']').ToString();
        }

        string FormatMaterialSoftParticleStateMeans()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialSoftParticleStateCount; i++)
            {
                if (i > 0) text.Append(',');
                if (shurikenMaterialSoftParticleSamples[i] <= 0)
                {
                    text.Append("[]");
                    continue;
                }

                Vector3 mean = MaterialSoftParticleMeanColor(i);
                text.Append('(')
                    .Append(mean.x.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.y.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.z.ToString("R", CultureInfo.InvariantCulture))
                    .Append(')');
            }
            return text.Append(']').ToString();
        }

        bool MaterialCameraFadeStatesHaveSamples(int minimumSamples)
        {
            for (int i = 0; i < MaterialCameraFadeStateCount; i++)
            {
                if (shurikenMaterialCameraFadeSamples[i] < minimumSamples)
                {
                    return false;
                }
            }
            return true;
        }

        bool MaterialCameraFadeSemanticsPass()
        {
            if (!MaterialCameraFadeStatesHaveSamples(1)) return false;

            float fullRed = MaterialCameraFadeMeanColor(0).x;
            float nearRed = MaterialCameraFadeMeanColor(1).x;
            float middleRed = MaterialCameraFadeMeanColor(2).x;
            float farRed = MaterialCameraFadeMeanColor(3).x;
            return fullRed - nearRed >= 0.2f &&
                   middleRed - nearRed >= 0.1f &&
                   fullRed - middleRed >= 0.08f &&
                   Mathf.Abs(fullRed - farRed) <= 0.04f;
        }

        Vector3 MaterialCameraFadeMeanColor(int state)
        {
            return shurikenMaterialCameraFadeSamples[state] > 0
                ? shurikenMaterialCameraFadeColorSums[state] /
                  shurikenMaterialCameraFadeSamples[state]
                : Vector3.zero;
        }

        string FormatMaterialCameraFadeStateErrors()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialCameraFadeStateCount; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(
                    maximumMaterialCameraFadeStateErrors[i].ToString(
                        "R", CultureInfo.InvariantCulture));
            }
            return text.Append(']').ToString();
        }

        string FormatMaterialCameraFadeStateMeans()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < MaterialCameraFadeStateCount; i++)
            {
                if (i > 0) text.Append(',');
                if (shurikenMaterialCameraFadeSamples[i] <= 0)
                {
                    text.Append("[]");
                    continue;
                }

                Vector3 mean = MaterialCameraFadeMeanColor(i);
                text.Append('(')
                    .Append(mean.x.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.y.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(mean.z.ToString("R", CultureInfo.InvariantCulture))
                    .Append(')');
            }
            return text.Append(']').ToString();
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
