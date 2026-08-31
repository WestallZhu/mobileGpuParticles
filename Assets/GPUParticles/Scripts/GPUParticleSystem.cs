using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace GPUParticles
{
    public enum SimulationSpace { Local = 0, World = 1, Custom = 2 }

    public enum ShapeTypeGPU { Sphere = 0, Hemisphere = 1, Cone = 2, Donut = 3, Box = 4, Circle = 5, Edge = 6, Rectangle = 7, Point = 8 }
    public enum ShapeEmitFromGPU
    {
        Volume = 0,
        Surface = 1,
        Base = 2,
        Edge = 3
    }
    public enum ShapeArcModeGPU
    {
        Random = 0,
        Loop = 1,
        PingPong = 2,
        BurstSpread = 3
    }

    public enum GPURenderMode
    {
        Billboard = 0,
        HorizontalBillboard = 1,
        VerticalBillboard = 2,
        StretchedBillboard = 3,
        Mesh = 4
    }
    public enum GPUAlignment  { View = 0, Facing = 1, World = 2, Local = 3, Velocity = 4 }
    public enum GPUParticleColorMode
    {
        Multiply = 0,
        Additive = 1,
        Subtractive = 2,
        Overlay = 3,
        Color = 4,
        Difference = 5
    }

    [ExecuteAlways]
    public class GPUParticleSystem : MonoBehaviour
    {
        // --------- Capacity & Emission ---------
        [Header("Capacity")]
        [Min(1)] public int maxParticles = 65536;

        [Header("Emission")]
        public bool emissionEnabled = true;
        [Min(0)] public float emissionRateOverTime = 2000f;
        public ParticleSystemCurveMode emissionRateOverTimeMode = ParticleSystemCurveMode.Constant;
        [Min(0)] public float emissionRateOverTimeMin = 2000f;
        [Min(0)] public float emissionRateOverTimeCurveMultiplier = 1f;
        public AnimationCurve emissionRateOverTimeCurveMin = AnimationCurve.Constant(0f, 1f, 2000f);
        public AnimationCurve emissionRateOverTimeCurveMax = AnimationCurve.Constant(0f, 1f, 2000f);
        [Min(0)] public float emissionRateOverDistance;
        public ParticleSystemCurveMode emissionRateOverDistanceMode = ParticleSystemCurveMode.Constant;
        [Min(0)] public float emissionRateOverDistanceMin;
        [Min(0)] public float emissionRateOverDistanceCurveMultiplier = 1f;
        public AnimationCurve emissionRateOverDistanceCurveMin = AnimationCurve.Constant(0f, 1f, 0f);
        public AnimationCurve emissionRateOverDistanceCurveMax = AnimationCurve.Constant(0f, 1f, 0f);
        [Min(0.05f)] public float emissionDuration = 5f;
        public bool emissionLooping = true;
        public bool randomizeEmissionStartDelay;
        [Min(0)] public float emissionStartDelayMin;
        [Min(0)] public float emissionStartDelay;
        public uint emissionRandomSeed = 1u;
        public GPUEmissionBurst[] emissionBursts = System.Array.Empty<GPUEmissionBurst>();

        // --------- Main ---------
        [Header("Main (Shuriken Mapping)")]
        [Min(0.001f)] public float startLifetime = 2.0f;
        public ParticleSystemCurveMode startLifetimeMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startLifetimeLUT;
        public float startSpeed = 5.0f;
        public ParticleSystemCurveMode startSpeedMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startSpeedLUT;
        [Min(0.0f)] public float startSize = 0.1f;
        public ParticleSystemCurveMode startSizeMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startSizeLUT;
        public bool startSize3D;
        [Min(0.0f)] public float startSizeY = 0.1f;
        public ParticleSystemCurveMode startSizeYMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startSizeYLUT;
        [Min(0.0f)] public float startSizeZ = 0.1f;
        public ParticleSystemCurveMode startSizeZMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startSizeZLUT;
        public Color startColor = Color.white;
        public ParticleSystemGradientMode startColorMode =
            ParticleSystemGradientMode.Color;
        public Texture2D startColorLUT;
        public float gravityModifier = 0.0f;
        public ParticleSystemCurveMode gravityModifierMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D gravityModifierLUT;
        public ParticleSystemGravitySource gravitySource =
            ParticleSystemGravitySource.Physics3D;
        [Min(0.0f)] public float simulationSpeed = 1.0f;
        public bool useUnscaledTime;
        public bool playOnAwake = true;
        public bool prewarm;
        public ParticleSystemStopAction stopAction =
            ParticleSystemStopAction.None;
        public ParticleSystemRingBufferMode ringBufferMode =
            ParticleSystemRingBufferMode.Disabled;
        public Vector2 ringBufferLoopRange = new Vector2(0f, 1f);
        [Tooltip("Optional GameObject that receives Main Stop Action. " +
                 "Leave empty to use this GPU particle GameObject.")]
        public GameObject stopActionTarget;
        public ParticleSystemScalingMode scalingMode =
            ParticleSystemScalingMode.Hierarchy;
        [Tooltip("Optional source Transform used for Shuriken Local scaling when the GPU system is created as a child.")]
        public Transform scalingSource;
        public float startRotation = 0.0f;
        public ParticleSystemCurveMode startRotationMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startRotationLUT;
        public bool startRotation3D;
        public float startRotationX;
        public ParticleSystemCurveMode startRotationXMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startRotationXLUT;
        public float startRotationY;
        public ParticleSystemCurveMode startRotationYMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startRotationYLUT;
        [Range(0f, 1f)] public float flipRotation;
        public float rotationOverLifetime = 0.0f;
        public SimulationSpace simulationSpace = SimulationSpace.Local;
        public Transform customSimulationSpace;
        public ParticleSystemEmitterVelocityMode emitterVelocityMode =
            ParticleSystemEmitterVelocityMode.Transform;
        [Tooltip("Custom emitter velocity in world space.")]
        public Vector3 customEmitterVelocity;
        [Tooltip("Optional Shuriken source used to resolve Rigidbody " +
                 "emitter velocity after child conversion.")]
        public ParticleSystem emitterVelocitySource;
        public ParticleSystemCullingMode cullingMode =
            ParticleSystemCullingMode.Automatic;
        [Tooltip("Particle renderer bounds in this GameObject's local space.")]
        public Bounds localCullingBounds =
            new Bounds(Vector3.zero, Vector3.one * 10f);

        [Header("Main Random Between Two Constants")]
        public bool randomizeStartLifetime;
        [Min(0.001f)] public float startLifetimeMin = 2.0f;
        public bool randomizeStartSpeed;
        public float startSpeedMin = 5.0f;
        public bool randomizeStartSize;
        [Min(0.0f)] public float startSizeMin = 0.1f;
        public bool randomizeStartSizeY;
        [Min(0.0f)] public float startSizeYMin = 0.1f;
        public bool randomizeStartSizeZ;
        [Min(0.0f)] public float startSizeZMin = 0.1f;
        public bool randomizeStartColor;
        public Color startColorMin = Color.white;
        public bool randomizeGravityModifier;
        public float gravityModifierMin;
        public bool randomizeStartRotation;
        public float startRotationMin;
        public bool randomizeStartRotationX;
        public float startRotationXMin;
        public bool randomizeStartRotationY;
        public float startRotationYMin;
        public bool randomizeRotationOverLifetime;
        public float rotationOverLifetimeMin;

        [Header("Over Lifetime (LUTs)")]
        public Texture2D rotationOverLifetimeIntegralLUT;
        public bool rotationOverLifetimeSeparateAxes;
        public Texture2D rotationOverLifetimeXIntegralLUT;
        public Texture2D rotationOverLifetimeYIntegralLUT;
        public Texture2D colorOverLifetimeLUT;
        public ParticleSystemGradientMode colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
        public Texture2D sizeOverLifetimeLUT;
        public bool sizeOverLifetimeSeparateAxes;
        public Texture2D sizeOverLifetimeYLUT;
        public Texture2D sizeOverLifetimeZLUT;

        [Header("By Speed (LUTs)")]
        public bool colorBySpeedEnabled;
        public Texture2D colorBySpeedLUT;
        public ParticleSystemGradientMode colorBySpeedMode = ParticleSystemGradientMode.Gradient;
        public Vector2 colorBySpeedRange = new Vector2(0f, 1f);
        public bool sizeBySpeedEnabled;
        public Texture2D sizeBySpeedLUT;
        public bool sizeBySpeedSeparateAxes;
        public Texture2D sizeBySpeedYLUT;
        public Texture2D sizeBySpeedZLUT;
        public Vector2 sizeBySpeedRange = new Vector2(0f, 1f);
        public bool rotationBySpeedEnabled;
        public bool rotationBySpeedSeparateAxes;
        public Texture2D rotationBySpeedLUT;
        public Texture2D rotationBySpeedXLUT;
        public Texture2D rotationBySpeedYLUT;
        public Vector2 rotationBySpeedRange = new Vector2(0f, 1f);

        [Header("Force Over Lifetime")]
        public bool forceOverLifetimeEnabled;
        public Texture2D forceOverLifetimeLUT;
        public SimulationSpace forceOverLifetimeSpace = SimulationSpace.Local;
        public bool forceOverLifetimeRandomized;

        [Header("Velocity Over Lifetime")]
        public bool velocityOverLifetimeEnabled;
        public Texture2D velocityOverLifetimeLUT;
        public SimulationSpace velocityOverLifetimeSpace = SimulationSpace.Local;
        public bool velocityOverLifetimeSpeedModifierEnabled;
        public bool velocityOverLifetimeOrbitalEnabled;
        public Texture2D velocityOverLifetimeOrbitalLUT;
        public Texture2D velocityOverLifetimeOrbitalOffsetLUT;

        [Header("Limit Velocity Over Lifetime")]
        public bool limitVelocityOverLifetimeEnabled;
        public Texture2D limitVelocityOverLifetimeLUT;
        public bool limitVelocityOverLifetimeSeparateAxes;
        public SimulationSpace limitVelocityOverLifetimeSpace = SimulationSpace.Local;
        [Range(0f, 1f)] public float limitVelocityOverLifetimeDampen;
        public bool limitVelocityMultiplyDragBySize;
        public bool limitVelocityMultiplyDragByVelocity;

        [Header("Inherit Velocity")]
        public bool inheritVelocityEnabled;
        public Texture2D inheritVelocityLUT;
        public ParticleSystemInheritVelocityMode inheritVelocityMode =
            ParticleSystemInheritVelocityMode.Initial;

        [Header("Lifetime By Emitter Speed")]
        public bool lifetimeByEmitterSpeedEnabled;
        public Texture2D lifetimeByEmitterSpeedLUT;
        public Vector2 lifetimeByEmitterSpeedRange = new Vector2(0f, 1f);

        [Header("Noise")]
        public bool noiseEnabled;
        public bool noiseSeparateAxes;
        public Texture2D noiseStrengthLUT;
        [Min(0.0001f)] public float noiseFrequency = 0.5f;
        public bool noiseDamping = true;
        public ParticleSystemNoiseQuality noiseQuality =
            ParticleSystemNoiseQuality.High;
        [Range(1, 4)] public int noiseOctaveCount = 1;
        [Min(0f)] public float noiseOctaveMultiplier = 0.5f;
        [Min(1f)] public float noiseOctaveScale = 2f;
        [Tooltip("RGBA: Position Amount, Rotation Amount (degrees/second), " +
                 "Size Amount, and Scroll Speed over particle lifetime.")]
        public Texture2D noiseAmountsLUT;
        public bool noiseRemapEnabled;
        public Texture2D noiseRemapLUT;

        [Header("Collision (Planes)")]
        public bool collisionEnabled;
        public ParticleSystemCollisionType collisionType =
            ParticleSystemCollisionType.Planes;
        [Tooltip("Shuriken collision planes. The first six valid transforms are used.")]
        public Transform[] collisionPlanes = System.Array.Empty<Transform>();
        [Tooltip("RGB: Dampen, Bounce, and Lifetime Loss over particle lifetime.")]
        public Texture2D collisionParametersLUT;
        [Min(0f)] public float collisionMinKillSpeed;
        [Min(0f)] public float collisionMaxKillSpeed = 10000f;
        [Min(0f)] public float collisionRadiusScale = 1f;

        [Header("Texture Sheet Animation (Grid / UV0)")]
        public bool textureSheetAnimationEnabled;
        public ParticleSystemAnimationMode textureSheetMode =
            ParticleSystemAnimationMode.Grid;
        public ParticleSystemAnimationType textureSheetAnimation =
            ParticleSystemAnimationType.WholeSheet;
        public ParticleSystemAnimationTimeMode textureSheetTimeMode =
            ParticleSystemAnimationTimeMode.Lifetime;
        public ParticleSystemAnimationRowMode textureSheetRowMode =
            ParticleSystemAnimationRowMode.Custom;
        public UVChannelFlags textureSheetUVChannelMask = UVChannelFlags.UV0;
        [Min(1)] public int textureSheetTilesX = 1;
        [Min(1)] public int textureSheetTilesY = 1;
        [Min(0)] public int textureSheetRowIndex;
        [Min(1)] public int textureSheetCycleCount = 1;
        [Min(0f)] public float textureSheetFps = 30f;
        public Vector2 textureSheetSpeedRange = new Vector2(0f, 1f);
        [Tooltip("Blend the current Grid frame with the next frame, matching " +
                 "URP particle material Flipbook Blending.")]
        public bool textureSheetFrameBlending;
        public Texture2D textureSheetFrameOverTimeLUT;
        public Texture2D textureSheetStartFrameLUT;

        [Header("Rendering")]
        public Texture2D baseMap;
        public Color materialBaseColor = Color.white;
        public GPUParticleColorMode materialColorMode =
            GPUParticleColorMode.Multiply;
        public BlendOp materialBlendOperation = BlendOp.Add;
        public BlendMode materialSourceBlend = BlendMode.SrcAlpha;
        public BlendMode materialDestinationBlend =
            BlendMode.OneMinusSrcAlpha;
        public BlendMode materialSourceBlendAlpha = BlendMode.One;
        public BlendMode materialDestinationBlendAlpha =
            BlendMode.OneMinusSrcAlpha;
        public bool materialAlphaPremultiply;
        public bool materialAlphaModulate;
        public bool materialZWrite;
        public bool materialAlphaClip;
        [Range(0f, 1f)] public float materialAlphaCutoff = 0.5f;
        public bool materialSoftParticles;
        public Vector2 materialSoftParticleFadeParams;
        public bool materialCameraFading;
        public Vector2 materialCameraFadeParams;
        [Range(0,1)] public float minAlphaCull = 0.001f;
        public bool renderEnabled = true;

        [Header("Emitter Direction (fallback)")]
        public Vector3 initialDirectionWS = Vector3.forward;

        // --------- Shape ---------
        [Header("Shape (Point/Sphere/Hemisphere/Cone/Donut/Box/Circle/Edge/Rectangle)")]
        public ShapeTypeGPU shapeType = ShapeTypeGPU.Cone;
        public ShapeEmitFromGPU shapeEmitFrom = ShapeEmitFromGPU.Volume;
        public bool alignToDirection = false; // default false (match Shuriken)
        [Range(0f, 1f)] public float shapeRandomDirectionAmount;
        [Range(0f, 1f)] public float shapeSphericalDirectionAmount;
        [Min(0f)] public float shapeRandomPositionAmount;

        // Sphere/Hemisphere
        public float shapeSphereRadius = 0.5f;

        // Cone
        [Range(0,90)] public float shapeConeAngle = 25f;
        public float shapeConeRadius = 0.25f;
        public float shapeConeLength = 1.0f;
        [Range(0,1)] public float shapeRadiusThickness = 1.0f;
        [Range(0,360)] public float shapeConeArcDeg = 360f;
        public ShapeArcModeGPU shapeArcMode = ShapeArcModeGPU.Random;
        [Range(0f, 1f)] public float shapeArcSpread;
        public ParticleSystemCurveMode shapeArcSpeedMode =
            ParticleSystemCurveMode.Constant;
        [Tooltip("Integral of Shape Arc Speed over normalized system time.")]
        public Texture2D shapeArcSpeedIntegralLUT;

        // Donut
        public float shapeDonutRadius = 1.0f; // 主圆环半径
        public float shapeDonutThickness = 0.2f; // 环的厚度

        // Box
        public Vector3 shapeBoxSize = new Vector3(1,1,1);

        // Circle
        public float shapeCircleRadius = 0.5f;

        // Edge
        public float shapeEdgeLength = 1.0f;

        // Rectangle
        public Vector2 shapeRectangleSize = new Vector2(1, 1);

        // Shape TRS (relative to emitter local)
        public Vector3 shapeLocalPosition = Vector3.zero;
        public Vector3 shapeLocalRotationEuler = Vector3.zero;
        public Vector3 shapeLocalScale = Vector3.one;

        // --------- Renderer (Shuriken Renderer Module mapping) ---------
        [Header("Renderer (Shuriken Mapping)")]
        public GPURenderMode renderMode = GPURenderMode.Billboard;
        [Tooltip("Mesh used when Render Mode is Mesh. The first Shuriken " +
                 "renderer mesh is mapped.")]
        public Mesh renderMesh;
        public GPUAlignment  renderAlignment = GPUAlignment.View; // ignored in Stretched per Unity
        public bool allowRoll = true;
        [Range(0,1)] public float normalDirection = 1.0f; // billboard only
        public Vector2 pivot = Vector2.zero;
        public float pivotDepth;
        [Tooltip("Per-axis probability of flipping each particle. Shuriken " +
                 "Texture Sheet flipU/flipV alias Renderer Flip X/Y. Billboard " +
                 "rendering consumes X/Y as horizontal/vertical UV flips; Z " +
                 "is retained for mapping but has no billboard effect.")]
        public Vector3 rendererFlip;
        public bool screenSpaceSizeClampEnabled;
        [Range(0f, 1f)] public float minParticleSize;
        [Range(0f, 1f)] public float maxParticleSize = 0.5f;

        [Header("Stretched Billboard")]
        public float stretchedLengthScale = 1.0f;   // 1 = neutral
        public float stretchedVelocityScale = 0.0f;
        public float stretchedCameraVelocityScale = 0.0f;
        public bool  freeformStretching = false;
        public bool  rotateWithStretchDirection = true;

        // --------- Runtime ---------
        public static readonly List<GPUParticleSystem> Active = new List<GPUParticleSystem>();

        Material simulateMaterial;
        Material renderMaterial;
        MaterialPropertyBlock simulateProperties;

        RenderTexture[] posLife = new RenderTexture[2];
        RenderTexture[] velSize = new RenderTexture[2];
        RenderTexture[] colorRT = new RenderTexture[2];
        RenderTexture[] rotationPhaseRT = new RenderTexture[2];
        RenderTexture[] rotationXYPhaseRT = new RenderTexture[2];
        bool rotationXYPhaseActive;

        int ping;
        int gridSize;
        int capacity;

        // emission cursor
        int emitCursor = 0;
        float emitCarry = 0f;
        float distanceEmitCarry;
        float emissionTime;
        float latestParticleBirthTime;
        bool particleBirthObserved;
        bool emissionStartDelayCacheValid;
        float cachedEmissionStartDelayMinimum;
        float cachedEmissionStartDelayMaximum;
        uint cachedEmissionStartDelaySeed;
        float cachedResolvedEmissionStartDelay;
        Vector3 previousEmitterPositionWS;
        bool previousEmitterPositionValid;
        Vector3 previousEmitterVelocityWS;
        uint simulationTick;
        float lastSimulationDeltaTime = 1f / 60f;
        int lastSimulatedFrame = -1;
        readonly Plane[] cullingPlanes = new Plane[6];
        readonly Vector4[] collisionPlaneEquations = new Vector4[6];
        int visibilityFrame = -1;
        bool visibleThisFrame;
        float lastCullingClock;
        bool cullingClockValid;
        bool wasCulled;
        ParticleSystemCullingMode trackedCullingMode;
        PlaybackState playbackState = PlaybackState.Stopped;
        PlaybackState resumeState = PlaybackState.Playing;
        float stoppingElapsed;
        float stoppingDuration;
        bool prewarmPending;
        bool stopActionPending;
        bool stopActionInvoked;
        const float PrewarmStep = 1f / 60f;
        const float CullingCatchupStep = 1f / 60f;

        enum PlaybackState
        {
            Stopped,
            Playing,
            Paused,
            Stopping
        }

        public bool isPlaying =>
            playbackState == PlaybackState.Playing ||
            playbackState == PlaybackState.Stopping;
        public bool isPaused => playbackState == PlaybackState.Paused;
        public bool isStopped => playbackState == PlaybackState.Stopped;
        public bool isEmitting => IsActivelyEmitting();
        public bool isVisible => visibleThisFrame;
        public float time => emissionTime;
        // Unity 2022.3 Shuriken samples Main curves just after the emission
        // tick boundary. This measured phase keeps automatic births aligned.
        const float StartLifetimeCurveTickPhase = 0.2f;
        const int MaxBurstGroupsPerStep = 8;
        const int MaxBurstCyclesPerLoop = 4096;
        readonly int[] stepBurstCounts = new int[MaxBurstGroupsPerStep];
        readonly float[] stepBurstAges = new float[MaxBurstGroupsPerStep];
        int stepBurstGroupCount;

        // camera velocity cache
        Vector3 prevCamPos;
        bool prevCamPosValid = false;

        // ---- Shader property IDs ----
        static readonly int _CurPosLife = Shader.PropertyToID("_CurPosLife");
        static readonly int _CurVelSize = Shader.PropertyToID("_CurVelSize");
        static readonly int _CurColor   = Shader.PropertyToID("_CurColor");
        static readonly int _CurRotationPhase = Shader.PropertyToID("_CurRotationPhase");
        static readonly int _CurRotationXYPhase =
            Shader.PropertyToID("_CurRotationXYPhase");
        static readonly int _NextPosLife = Shader.PropertyToID("_NextPosLife");
        static readonly int _NextVelSize = Shader.PropertyToID("_NextVelSize");
        static readonly int _GridSize   = Shader.PropertyToID("_GridSize");
        static readonly int _MaxParticles = Shader.PropertyToID("_MaxParticles");
        static readonly int _DeltaTime  = Shader.PropertyToID("_DeltaTime");
        static readonly int _StartLifetime = Shader.PropertyToID("_StartLifetime");
        static readonly int _StartLifetimeMin = Shader.PropertyToID("_StartLifetimeMin");
        static readonly int _RandomizeStartLifetime = Shader.PropertyToID("_RandomizeStartLifetime");
        static readonly int _StartLifetimeMode =
            Shader.PropertyToID("_StartLifetimeMode");
        static readonly int _StartLifetimeLUT =
            Shader.PropertyToID("_StartLifetimeLUT");
        static readonly int _StartLifetimeLUTInvWidth =
            Shader.PropertyToID("_StartLifetimeLUTInvWidth");
        static readonly int _RingBufferMode =
            Shader.PropertyToID("_RingBufferMode");
        static readonly int _RingBufferLoopRange =
            Shader.PropertyToID("_RingBufferLoopRange");
        static readonly int _StartSpeed = Shader.PropertyToID("_StartSpeed");
        static readonly int _StartSpeedMin = Shader.PropertyToID("_StartSpeedMin");
        static readonly int _RandomizeStartSpeed = Shader.PropertyToID("_RandomizeStartSpeed");
        static readonly int _StartSpeedMode = Shader.PropertyToID("_StartSpeedMode");
        static readonly int _StartSpeedLUT = Shader.PropertyToID("_StartSpeedLUT");
        static readonly int _StartSpeedLUTInvWidth =
            Shader.PropertyToID("_StartSpeedLUTInvWidth");
        static readonly int _StartSize  = Shader.PropertyToID("_StartSize");
        static readonly int _StartSizeMin = Shader.PropertyToID("_StartSizeMin");
        static readonly int _RandomizeStartSize = Shader.PropertyToID("_RandomizeStartSize");
        static readonly int _StartSizeMode = Shader.PropertyToID("_StartSizeMode");
        static readonly int _StartSizeLUT = Shader.PropertyToID("_StartSizeLUT");
        static readonly int _StartSizeLUTInvWidth =
            Shader.PropertyToID("_StartSizeLUTInvWidth");
        static readonly int _UseSeparateSizeAxes =
            Shader.PropertyToID("_UseSeparateSizeAxes");
        static readonly int _StartSize3D = Shader.PropertyToID("_StartSize3D");
        static readonly int _SizeOverLifetimeSeparateAxes =
            Shader.PropertyToID("_SizeOverLifetimeSeparateAxes");
        static readonly int _SizeBySpeedSeparateAxes =
            Shader.PropertyToID("_SizeBySpeedSeparateAxes");
        static readonly int _StartSizeY = Shader.PropertyToID("_StartSizeY");
        static readonly int _StartSizeYMin = Shader.PropertyToID("_StartSizeYMin");
        static readonly int _StartSizeYMode = Shader.PropertyToID("_StartSizeYMode");
        static readonly int _StartSizeYLUT = Shader.PropertyToID("_StartSizeYLUT");
        static readonly int _StartSizeYLUTInvWidth =
            Shader.PropertyToID("_StartSizeYLUTInvWidth");
        static readonly int _StartSizeZ = Shader.PropertyToID("_StartSizeZ");
        static readonly int _StartSizeZMin = Shader.PropertyToID("_StartSizeZMin");
        static readonly int _StartSizeZMode = Shader.PropertyToID("_StartSizeZMode");
        static readonly int _StartSizeZLUT = Shader.PropertyToID("_StartSizeZLUT");
        static readonly int _StartSizeZLUTInvWidth =
            Shader.PropertyToID("_StartSizeZLUTInvWidth");
        static readonly int _StartColor = Shader.PropertyToID("_StartColor");
        static readonly int _StartColorMin = Shader.PropertyToID("_StartColorMin");
        static readonly int _RandomizeStartColor = Shader.PropertyToID("_RandomizeStartColor");
        static readonly int _StartColorMode = Shader.PropertyToID("_StartColorMode");
        static readonly int _StartColorLUT = Shader.PropertyToID("_StartColorLUT");
        static readonly int _StartColorLUTInvWidth =
            Shader.PropertyToID("_StartColorLUTInvWidth");
        static readonly int _StartRotation = Shader.PropertyToID("_StartRotation");
        static readonly int _StartRotationMin = Shader.PropertyToID("_StartRotationMin");
        static readonly int _RandomizeStartRotation = Shader.PropertyToID("_RandomizeStartRotation");
        static readonly int _StartRotationMode =
            Shader.PropertyToID("_StartRotationMode");
        static readonly int _StartRotationLUT =
            Shader.PropertyToID("_StartRotationLUT");
        static readonly int _StartRotationLUTInvWidth =
            Shader.PropertyToID("_StartRotationLUTInvWidth");
        static readonly int _StartRotation3D =
            Shader.PropertyToID("_StartRotation3D");
        static readonly int _StartRotationX =
            Shader.PropertyToID("_StartRotationX");
        static readonly int _StartRotationXMin =
            Shader.PropertyToID("_StartRotationXMin");
        static readonly int _StartRotationXMode =
            Shader.PropertyToID("_StartRotationXMode");
        static readonly int _StartRotationXLUT =
            Shader.PropertyToID("_StartRotationXLUT");
        static readonly int _StartRotationXLUTInvWidth =
            Shader.PropertyToID("_StartRotationXLUTInvWidth");
        static readonly int _StartRotationY =
            Shader.PropertyToID("_StartRotationY");
        static readonly int _StartRotationYMin =
            Shader.PropertyToID("_StartRotationYMin");
        static readonly int _StartRotationYMode =
            Shader.PropertyToID("_StartRotationYMode");
        static readonly int _StartRotationYLUT =
            Shader.PropertyToID("_StartRotationYLUT");
        static readonly int _StartRotationYLUTInvWidth =
            Shader.PropertyToID("_StartRotationYLUTInvWidth");
        static readonly int _FlipRotation = Shader.PropertyToID("_FlipRotation");
        static readonly int _RotationOverLifetime = Shader.PropertyToID("_RotationOverLifetime");
        static readonly int _RotationOverLifetimeMin = Shader.PropertyToID("_RotationOverLifetimeMin");
        static readonly int _RandomizeRotationOverLifetime = Shader.PropertyToID("_RandomizeRotationOverLifetime");
        static readonly int _RotationOverLifetimeIntegralLUT =
            Shader.PropertyToID("_RotationOverLifetimeIntegralLUT");
        static readonly int _RotationOverLifetimeIntegralLUTInvWidth =
            Shader.PropertyToID("_RotationOverLifetimeIntegralLUTInvWidth");
        static readonly int _RotationOverLifetimeSeparateAxes =
            Shader.PropertyToID("_RotationOverLifetimeSeparateAxes");
        static readonly int _RotationOverLifetimeXIntegralLUT =
            Shader.PropertyToID("_RotationOverLifetimeXIntegralLUT");
        static readonly int _RotationOverLifetimeXIntegralLUTInvWidth =
            Shader.PropertyToID(
                "_RotationOverLifetimeXIntegralLUTInvWidth");
        static readonly int _RotationOverLifetimeYIntegralLUT =
            Shader.PropertyToID("_RotationOverLifetimeYIntegralLUT");
        static readonly int _RotationOverLifetimeYIntegralLUTInvWidth =
            Shader.PropertyToID(
                "_RotationOverLifetimeYIntegralLUTInvWidth");
        static readonly int _UseRotationOverLifetimeIntegralLUT =
            Shader.PropertyToID("_UseRotationOverLifetimeIntegralLUT");
        static readonly int _GravityWS  = Shader.PropertyToID("_GravityWS");
        static readonly int _GravityWSMin = Shader.PropertyToID("_GravityWSMin");
        static readonly int _RandomizeGravityModifier = Shader.PropertyToID("_RandomizeGravityModifier");
        static readonly int _GravityBase = Shader.PropertyToID("_GravityBase");
        static readonly int _GravityModifierMode =
            Shader.PropertyToID("_GravityModifierMode");
        static readonly int _GravityModifierLUT =
            Shader.PropertyToID("_GravityModifierLUT");
        static readonly int _GravityModifierLUTInvWidth =
            Shader.PropertyToID("_GravityModifierLUTInvWidth");
        static readonly int _SimulationSpace = Shader.PropertyToID("_SimulationSpace");
        static readonly int _EmitStart  = Shader.PropertyToID("_EmitStart");
        static readonly int _EmitCount  = Shader.PropertyToID("_EmitCount");
        static readonly int _EmitCarryPrev = Shader.PropertyToID("_EmitCarryPrev");
        static readonly int _EmissionRate = Shader.PropertyToID("_EmissionRate");
        static readonly int _ContinuousEmitCount = Shader.PropertyToID("_ContinuousEmitCount");
        static readonly int _ContinuousEmissionWindowStart =
            Shader.PropertyToID("_ContinuousEmissionWindowStart");
        static readonly int _DistanceEmitCount = Shader.PropertyToID("_DistanceEmitCount");
        static readonly int _EmissionTimeAfterStep =
            Shader.PropertyToID("_EmissionTimeAfterStep");
        static readonly int _EmissionStartDelay =
            Shader.PropertyToID("_EmissionStartDelay");
        static readonly int _EmissionDuration = Shader.PropertyToID("_EmissionDuration");
        static readonly int _EmissionLooping = Shader.PropertyToID("_EmissionLooping");
        static readonly int _BurstCounts0 = Shader.PropertyToID("_BurstCounts0");
        static readonly int _BurstCounts1 = Shader.PropertyToID("_BurstCounts1");
        static readonly int _BurstAges0 = Shader.PropertyToID("_BurstAges0");
        static readonly int _BurstAges1 = Shader.PropertyToID("_BurstAges1");
        static readonly int _InitialDir = Shader.PropertyToID("_InitialDir");
        static readonly int _SimulationTick = Shader.PropertyToID("_SimulationTick");
        static readonly int _ForceOverLifetimeLUT = Shader.PropertyToID("_ForceOverLifetimeLUT");
        static readonly int _ForceOverLifetimeEnabled = Shader.PropertyToID("_ForceOverLifetimeEnabled");
        static readonly int _ForceOverLifetimeSpace = Shader.PropertyToID("_ForceOverLifetimeSpace");
        static readonly int _ForceOverLifetimeRandomized = Shader.PropertyToID("_ForceOverLifetimeRandomized");
        static readonly int _VelocityOverLifetimeLUT = Shader.PropertyToID("_VelocityOverLifetimeLUT");
        static readonly int _VelocityOverLifetimeOrbitalLUT =
            Shader.PropertyToID("_VelocityOverLifetimeOrbitalLUT");
        static readonly int _VelocityOverLifetimeOrbitalOffsetLUT =
            Shader.PropertyToID("_VelocityOverLifetimeOrbitalOffsetLUT");
        static readonly int _VelocityOverLifetimeEnabled = Shader.PropertyToID("_VelocityOverLifetimeEnabled");
        static readonly int _VelocityOverLifetimeSpace = Shader.PropertyToID("_VelocityOverLifetimeSpace");
        static readonly int _VelocityOverLifetimeSpeedModifierEnabled =
            Shader.PropertyToID("_VelocityOverLifetimeSpeedModifierEnabled");
        static readonly int _VelocityOverLifetimeOrbitalEnabled =
            Shader.PropertyToID("_VelocityOverLifetimeOrbitalEnabled");
        static readonly int _LimitVelocityLUT =
            Shader.PropertyToID("_LimitVelocityLUT");
        static readonly int _LimitVelocityLUTInvWidth =
            Shader.PropertyToID("_LimitVelocityLUTInvWidth");
        static readonly int _LimitVelocityEnabled =
            Shader.PropertyToID("_LimitVelocityEnabled");
        static readonly int _LimitVelocitySeparateAxes =
            Shader.PropertyToID("_LimitVelocitySeparateAxes");
        static readonly int _LimitVelocitySpace =
            Shader.PropertyToID("_LimitVelocitySpace");
        static readonly int _LimitVelocityDampen =
            Shader.PropertyToID("_LimitVelocityDampen");
        static readonly int _LimitVelocityMultiplyDragBySize =
            Shader.PropertyToID("_LimitVelocityMultiplyDragBySize");
        static readonly int _LimitVelocityMultiplyDragByVelocity =
            Shader.PropertyToID("_LimitVelocityMultiplyDragByVelocity");
        static readonly int _InheritVelocityLUT =
            Shader.PropertyToID("_InheritVelocityLUT");
        static readonly int _InheritVelocityLUTInvWidth =
            Shader.PropertyToID("_InheritVelocityLUTInvWidth");
        static readonly int _InheritVelocityEnabled =
            Shader.PropertyToID("_InheritVelocityEnabled");
        static readonly int _InheritVelocityMode =
            Shader.PropertyToID("_InheritVelocityMode");
        static readonly int _EmitterPreviousVelocityWS =
            Shader.PropertyToID("_EmitterPreviousVelocityWS");
        static readonly int _EmitterVelocityWS =
            Shader.PropertyToID("_EmitterVelocityWS");
        static readonly int _LifetimeByEmitterSpeedLUT =
            Shader.PropertyToID("_LifetimeByEmitterSpeedLUT");
        static readonly int _LifetimeByEmitterSpeedLUTInvWidth =
            Shader.PropertyToID("_LifetimeByEmitterSpeedLUTInvWidth");
        static readonly int _LifetimeByEmitterSpeedEnabled =
            Shader.PropertyToID("_LifetimeByEmitterSpeedEnabled");
        static readonly int _LifetimeByEmitterSpeedRange =
            Shader.PropertyToID("_LifetimeByEmitterSpeedRange");
        static readonly int _NoiseEnabled = Shader.PropertyToID("_NoiseEnabled");
        static readonly int _NoiseSeparateAxes =
            Shader.PropertyToID("_NoiseSeparateAxes");
        static readonly int _NoiseStrengthLUT =
            Shader.PropertyToID("_NoiseStrengthLUT");
        static readonly int _NoiseStrengthLUTInvWidth =
            Shader.PropertyToID("_NoiseStrengthLUTInvWidth");
        static readonly int _NoiseAmountsLUT =
            Shader.PropertyToID("_NoiseAmountsLUT");
        static readonly int _NoiseAmountsLUTInvWidth =
            Shader.PropertyToID("_NoiseAmountsLUTInvWidth");
        static readonly int _NoiseRemapEnabled =
            Shader.PropertyToID("_NoiseRemapEnabled");
        static readonly int _NoiseRemapLUT =
            Shader.PropertyToID("_NoiseRemapLUT");
        static readonly int _NoiseRemapLUTInvWidth =
            Shader.PropertyToID("_NoiseRemapLUTInvWidth");
        static readonly int _NoiseFrequency =
            Shader.PropertyToID("_NoiseFrequency");
        static readonly int _NoiseDamping =
            Shader.PropertyToID("_NoiseDamping");
        static readonly int _NoiseQuality =
            Shader.PropertyToID("_NoiseQuality");
        static readonly int _NoiseOctaveCount =
            Shader.PropertyToID("_NoiseOctaveCount");
        static readonly int _NoiseOctaveMultiplier =
            Shader.PropertyToID("_NoiseOctaveMultiplier");
        static readonly int _NoiseOctaveScale =
            Shader.PropertyToID("_NoiseOctaveScale");
        static readonly int _CollisionEnabled =
            Shader.PropertyToID("_CollisionEnabled");
        static readonly int _CollisionPlaneCount =
            Shader.PropertyToID("_CollisionPlaneCount");
        static readonly int _CollisionPlanes =
            Shader.PropertyToID("_CollisionPlanes");
        static readonly int _CollisionParametersLUT =
            Shader.PropertyToID("_CollisionParametersLUT");
        static readonly int _CollisionParametersLUTInvWidth =
            Shader.PropertyToID("_CollisionParametersLUTInvWidth");
        static readonly int _CollisionMinKillSpeed =
            Shader.PropertyToID("_CollisionMinKillSpeed");
        static readonly int _CollisionMaxKillSpeed =
            Shader.PropertyToID("_CollisionMaxKillSpeed");
        static readonly int _CollisionRadiusScale =
            Shader.PropertyToID("_CollisionRadiusScale");
        static readonly int _CollisionParticleScaleWS =
            Shader.PropertyToID("_CollisionParticleScaleWS");
        static readonly int _ColorOverLifetimeMode = Shader.PropertyToID("_ColorOverLifetimeMode");
        static readonly int _GradLUTInvWidth = Shader.PropertyToID("_GradLUTInvWidth");
        static readonly int _SizeLUTInvWidth = Shader.PropertyToID("_SizeLUTInvWidth");
        static readonly int _SizeYLUT = Shader.PropertyToID("_SizeYLUT");
        static readonly int _SizeYLUTInvWidth = Shader.PropertyToID("_SizeYLUTInvWidth");
        static readonly int _SizeZLUT = Shader.PropertyToID("_SizeZLUT");
        static readonly int _SizeZLUTInvWidth = Shader.PropertyToID("_SizeZLUTInvWidth");
        static readonly int _ForceOverLifetimeLUTInvWidth =
            Shader.PropertyToID("_ForceOverLifetimeLUTInvWidth");
        static readonly int _VelocityOverLifetimeLUTInvWidth =
            Shader.PropertyToID("_VelocityOverLifetimeLUTInvWidth");
        static readonly int _VelocityOverLifetimeOrbitalLUTInvWidth =
            Shader.PropertyToID("_VelocityOverLifetimeOrbitalLUTInvWidth");
        static readonly int _VelocityOverLifetimeOrbitalOffsetLUTInvWidth =
            Shader.PropertyToID("_VelocityOverLifetimeOrbitalOffsetLUTInvWidth");
        static readonly int _ColorBySpeedLUT = Shader.PropertyToID("_ColorBySpeedLUT");
        static readonly int _ColorBySpeedLUTInvWidth =
            Shader.PropertyToID("_ColorBySpeedLUTInvWidth");
        static readonly int _ColorBySpeedEnabled = Shader.PropertyToID("_ColorBySpeedEnabled");
        static readonly int _ColorBySpeedMode = Shader.PropertyToID("_ColorBySpeedMode");
        static readonly int _ColorBySpeedRange = Shader.PropertyToID("_ColorBySpeedRange");
        static readonly int _SizeBySpeedLUT = Shader.PropertyToID("_SizeBySpeedLUT");
        static readonly int _SizeBySpeedLUTInvWidth =
            Shader.PropertyToID("_SizeBySpeedLUTInvWidth");
        static readonly int _SizeBySpeedYLUT = Shader.PropertyToID("_SizeBySpeedYLUT");
        static readonly int _SizeBySpeedYLUTInvWidth =
            Shader.PropertyToID("_SizeBySpeedYLUTInvWidth");
        static readonly int _SizeBySpeedZLUT = Shader.PropertyToID("_SizeBySpeedZLUT");
        static readonly int _SizeBySpeedZLUTInvWidth =
            Shader.PropertyToID("_SizeBySpeedZLUTInvWidth");
        static readonly int _SizeBySpeedEnabled = Shader.PropertyToID("_SizeBySpeedEnabled");
        static readonly int _SizeBySpeedRange = Shader.PropertyToID("_SizeBySpeedRange");
        static readonly int _RotationBySpeedLUT = Shader.PropertyToID("_RotationBySpeedLUT");
        static readonly int _RotationBySpeedLUTInvWidth =
            Shader.PropertyToID("_RotationBySpeedLUTInvWidth");
        static readonly int _RotationBySpeedXLUT =
            Shader.PropertyToID("_RotationBySpeedXLUT");
        static readonly int _RotationBySpeedXLUTInvWidth =
            Shader.PropertyToID("_RotationBySpeedXLUTInvWidth");
        static readonly int _RotationBySpeedYLUT =
            Shader.PropertyToID("_RotationBySpeedYLUT");
        static readonly int _RotationBySpeedYLUTInvWidth =
            Shader.PropertyToID("_RotationBySpeedYLUTInvWidth");
        static readonly int _RotationBySpeedEnabled =
            Shader.PropertyToID("_RotationBySpeedEnabled");
        static readonly int _RotationBySpeedSeparateAxes =
            Shader.PropertyToID("_RotationBySpeedSeparateAxes");
        static readonly int _RotationBySpeedRange =
            Shader.PropertyToID("_RotationBySpeedRange");

        // shape params
        static readonly int _ShapeType = Shader.PropertyToID("_ShapeType");
        static readonly int _ShapeEmitFrom = Shader.PropertyToID("_ShapeEmitFrom");
        static readonly int _AlignToDirection = Shader.PropertyToID("_AlignToDirection");
        static readonly int _ShapeRandomDirectionAmount =
            Shader.PropertyToID("_ShapeRandomDirectionAmount");
        static readonly int _ShapeSphericalDirectionAmount =
            Shader.PropertyToID("_ShapeSphericalDirectionAmount");
        static readonly int _ShapeRandomPositionScale =
            Shader.PropertyToID("_ShapeRandomPositionScale");
        static readonly int _ShapeConeAngleRad = Shader.PropertyToID("_ShapeConeAngleRad");
        static readonly int _ShapeConeRadius = Shader.PropertyToID("_ShapeConeRadius");
        static readonly int _ShapeConeLength = Shader.PropertyToID("_ShapeConeLength");
        static readonly int _ShapeRadiusThickness = Shader.PropertyToID("_ShapeRadiusThickness");
        static readonly int _ShapeConeArcRad = Shader.PropertyToID("_ShapeConeArcRad");
        static readonly int _ShapeArcMode = Shader.PropertyToID("_ShapeArcMode");
        static readonly int _ShapeArcSpread = Shader.PropertyToID("_ShapeArcSpread");
        static readonly int _ShapeArcSpeedMode =
            Shader.PropertyToID("_ShapeArcSpeedMode");
        static readonly int _ShapeArcSpeedIntegralLUT =
            Shader.PropertyToID("_ShapeArcSpeedIntegralLUT");
        static readonly int _ShapeArcSpeedIntegralLUTInvWidth =
            Shader.PropertyToID("_ShapeArcSpeedIntegralLUTInvWidth");
        static readonly int _ShapeBoxSize = Shader.PropertyToID("_ShapeBoxSize");
        static readonly int _ShapeSphereRadius = Shader.PropertyToID("_ShapeSphereRadius");
        static readonly int _ShapeDonutRadius = Shader.PropertyToID("_ShapeDonutRadius");
        static readonly int _ShapeDonutThickness = Shader.PropertyToID("_ShapeDonutThickness");
        static readonly int _ShapeCircleRadius = Shader.PropertyToID("_ShapeCircleRadius");
        static readonly int _ShapeEdgeLength = Shader.PropertyToID("_ShapeEdgeLength");
        static readonly int _ShapeRectangleSize = Shader.PropertyToID("_ShapeRectangleSize");
        static readonly int _ShapePosL = Shader.PropertyToID("_ShapePosL");
        static readonly int _ShapeRightL = Shader.PropertyToID("_ShapeRightL");
        static readonly int _ShapeUpL = Shader.PropertyToID("_ShapeUpL");
        static readonly int _ShapeFwdL = Shader.PropertyToID("_ShapeFwdL");

        static readonly int _EmitterLocalToWorld = Shader.PropertyToID("_EmitterLocalToWorld");
        static readonly int _EmitterWorldToLocal = Shader.PropertyToID("_EmitterWorldToLocal");
        static readonly int _SimulationLocalToWorld =
            Shader.PropertyToID("_SimulationLocalToWorld");
        static readonly int _SimulationWorldToLocal =
            Shader.PropertyToID("_SimulationWorldToLocal");
        static readonly int _EmitterToSimulationDirection =
            Shader.PropertyToID("_EmitterToSimulationDirection");
        static readonly int _SimulationToEmitterDirection =
            Shader.PropertyToID("_SimulationToEmitterDirection");
        static readonly int _WorldToSimulationDirection =
            Shader.PropertyToID("_WorldToSimulationDirection");
        static readonly int _SimulationToWorldDirection =
            Shader.PropertyToID("_SimulationToWorldDirection");
        static readonly int _ShapeLocalToWorld = Shader.PropertyToID("_ShapeLocalToWorld");
        static readonly int _EmitterPreviousPositionWS = Shader.PropertyToID("_EmitterPreviousPositionWS");
        static readonly int _EmitterCurrentPositionWS = Shader.PropertyToID("_EmitterCurrentPositionWS");

        // render shader ids
        static readonly int _EmitterLocalToWorld_Render = Shader.PropertyToID("_EmitterLocalToWorld");
        static readonly int _ParticleScaleWorld = Shader.PropertyToID("_ParticleScaleWorld");
        static readonly int _CameraRightWS = Shader.PropertyToID("_CameraRightWS");
        static readonly int _CameraUpWS = Shader.PropertyToID("_CameraUpWS");
        static readonly int _CameraPosWS = Shader.PropertyToID("_CameraPosWS");
        static readonly int _CameraVelWS = Shader.PropertyToID("_CameraVelWS");
        static readonly int _MinAlphaCull = Shader.PropertyToID("_MinAlphaCull");

        static readonly int _RenderMode = Shader.PropertyToID("_RenderMode");
        static readonly int _RenderAlignment = Shader.PropertyToID("_RenderAlignment");
        static readonly int _AllowRoll = Shader.PropertyToID("_AllowRoll");
        static readonly int _NormalDirection = Shader.PropertyToID("_NormalDirection");
        static readonly int _Pivot = Shader.PropertyToID("_Pivot");
        static readonly int _MeshBoundsSize = Shader.PropertyToID("_MeshBoundsSize");
        static readonly int _RendererFlip = Shader.PropertyToID("_RendererFlip");
        static readonly int _ScreenSpaceSizeClampEnabled =
            Shader.PropertyToID("_ScreenSpaceSizeClampEnabled");
        static readonly int _MinParticleSize =
            Shader.PropertyToID("_MinParticleSize");
        static readonly int _MaxParticleSize =
            Shader.PropertyToID("_MaxParticleSize");
        static readonly int _LenScale = Shader.PropertyToID("_LenScale");
        static readonly int _VelScale = Shader.PropertyToID("_VelScale");
        static readonly int _CamVelScale = Shader.PropertyToID("_CamVelScale");
        static readonly int _Freeform = Shader.PropertyToID("_Freeform");
        static readonly int _RotateWithStretch = Shader.PropertyToID("_RotateWithStretch");
        static readonly int _TextureSheetEnabled =
            Shader.PropertyToID("_TextureSheetEnabled");
        static readonly int _TextureSheetTilesX =
            Shader.PropertyToID("_TextureSheetTilesX");
        static readonly int _TextureSheetTilesY =
            Shader.PropertyToID("_TextureSheetTilesY");
        static readonly int _TextureSheetAnimation =
            Shader.PropertyToID("_TextureSheetAnimation");
        static readonly int _TextureSheetTimeMode =
            Shader.PropertyToID("_TextureSheetTimeMode");
        static readonly int _TextureSheetRowMode =
            Shader.PropertyToID("_TextureSheetRowMode");
        static readonly int _TextureSheetRowIndex =
            Shader.PropertyToID("_TextureSheetRowIndex");
        static readonly int _TextureSheetCycleCount =
            Shader.PropertyToID("_TextureSheetCycleCount");
        static readonly int _TextureSheetFps =
            Shader.PropertyToID("_TextureSheetFps");
        static readonly int _TextureSheetSpeedRange =
            Shader.PropertyToID("_TextureSheetSpeedRange");
        static readonly int _TextureSheetFrameBlending =
            Shader.PropertyToID("_TextureSheetFrameBlending");
        static readonly int _TextureSheetBlendNextUV =
            Shader.PropertyToID("_TextureSheetBlendNextUV");
        static readonly int _TextureSheetFrameOverTimeLUT =
            Shader.PropertyToID("_TextureSheetFrameOverTimeLUT");
        static readonly int _TextureSheetStartFrameLUT =
            Shader.PropertyToID("_TextureSheetStartFrameLUT");
        static readonly int _MaterialBaseColor =
            Shader.PropertyToID("_BaseColor");
        static readonly int _MaterialColorMode =
            Shader.PropertyToID("_ParticleColorMode");
        static readonly int _MaterialBlendOperation =
            Shader.PropertyToID("_BlendOp");
        static readonly int _MaterialSourceBlend =
            Shader.PropertyToID("_SrcBlend");
        static readonly int _MaterialDestinationBlend =
            Shader.PropertyToID("_DstBlend");
        static readonly int _MaterialSourceBlendAlpha =
            Shader.PropertyToID("_SrcBlendAlpha");
        static readonly int _MaterialDestinationBlendAlpha =
            Shader.PropertyToID("_DstBlendAlpha");
        static readonly int _MaterialAlphaPremultiply =
            Shader.PropertyToID("_AlphaPremultiply");
        static readonly int _MaterialAlphaModulate =
            Shader.PropertyToID("_AlphaModulate");
        static readonly int _MaterialZWrite =
            Shader.PropertyToID("_ZWrite");
        static readonly int _MaterialAlphaClip =
            Shader.PropertyToID("_AlphaClip");
        static readonly int _MaterialAlphaCutoff =
            Shader.PropertyToID("_Cutoff");
        static readonly int _MaterialSoftParticles =
            Shader.PropertyToID("_SoftParticlesEnabled");
        static readonly int _MaterialSoftParticleFadeParams =
            Shader.PropertyToID("_SoftParticleFadeParams");
        static readonly int _MaterialCameraFading =
            Shader.PropertyToID("_CameraFadingEnabled");
        static readonly int _MaterialCameraFadeParams =
            Shader.PropertyToID("_CameraFadeParams");
        static readonly int _TextureSheetFrameLUTInvWidth =
            Shader.PropertyToID("_TextureSheetFrameLUTInvWidth");
        static readonly int _TextureSheetStartLUTInvWidth =
            Shader.PropertyToID("_TextureSheetStartLUTInvWidth");

        internal RenderTexture CurrentPositionLifetimeTexture => posLife[ping];
        internal RenderTexture CurrentVelocitySizeTexture => velSize[ping];
        internal RenderTexture CurrentColorTexture => colorRT[ping];
        internal RenderTexture CurrentRotationPhaseTexture => rotationPhaseRT[ping];
        internal RenderTexture CurrentRotationXYPhaseTexture =>
            rotationXYPhaseRT[ping];

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            EnsureMaterials();
            RecreateTargetsIfNeeded(true);
            InitializePlaybackFromSettings();
        }

        void Update()
        {
            ApplyPendingStopAction();
        }

        void OnDisable()
        {
            Active.Remove(this);
            ReleaseTargets();
            lastSimulatedFrame = -1;
        }

        void OnValidate()
        {
            maxParticles = Mathf.Max(1, maxParticles);
            startLifetime = Mathf.Max(1e-3f, startLifetime);
            startLifetimeMin = Mathf.Max(1e-3f, startLifetimeMin);
            startSize = Mathf.Max(0f, startSize);
            startSizeMin = Mathf.Max(0f, startSizeMin);
            emissionRateOverTime = Mathf.Max(0f, emissionRateOverTime);
            emissionRateOverTimeMin = Mathf.Max(0f, emissionRateOverTimeMin);
            emissionRateOverTimeCurveMultiplier = Mathf.Max(0f, emissionRateOverTimeCurveMultiplier);
            emissionRateOverDistance = Mathf.Max(0f, emissionRateOverDistance);
            emissionRateOverDistanceMin = Mathf.Max(0f, emissionRateOverDistanceMin);
            emissionRateOverDistanceCurveMultiplier =
                Mathf.Max(0f, emissionRateOverDistanceCurveMultiplier);
            emissionDuration = Mathf.Max(0.05f, emissionDuration);
            emissionStartDelayMin = Mathf.Max(0f, emissionStartDelayMin);
            emissionStartDelay = Mathf.Max(0f, emissionStartDelay);
            ringBufferLoopRange = Ordered01Range(ringBufferLoopRange);
            flipRotation = Mathf.Clamp01(flipRotation);
            shapeRandomDirectionAmount = Mathf.Clamp01(
                shapeRandomDirectionAmount);
            shapeSphericalDirectionAmount = Mathf.Clamp01(
                shapeSphericalDirectionAmount);
            shapeRandomPositionAmount = Mathf.Max(
                0f,
                shapeRandomPositionAmount);
            shapeArcSpread = Mathf.Clamp01(shapeArcSpread);
            noiseFrequency = Mathf.Max(0.0001f, noiseFrequency);
            noiseOctaveCount = Mathf.Clamp(noiseOctaveCount, 1, 4);
            noiseOctaveMultiplier = Mathf.Max(0f, noiseOctaveMultiplier);
            noiseOctaveScale = Mathf.Max(1f, noiseOctaveScale);
            collisionMinKillSpeed = Mathf.Max(0f, collisionMinKillSpeed);
            collisionMaxKillSpeed = Mathf.Max(
                collisionMinKillSpeed,
                collisionMaxKillSpeed);
            collisionRadiusScale = Mathf.Max(0f, collisionRadiusScale);
            if (collisionPlanes == null)
            {
                collisionPlanes = System.Array.Empty<Transform>();
            }
            Vector3 cullingSize = localCullingBounds.size;
            localCullingBounds.size = new Vector3(
                Mathf.Max(0.0002f, Mathf.Abs(cullingSize.x)),
                Mathf.Max(0.0002f, Mathf.Abs(cullingSize.y)),
                Mathf.Max(0.0002f, Mathf.Abs(cullingSize.z)));
            colorBySpeedRange = OrderedRange(colorBySpeedRange);
            sizeBySpeedRange = OrderedRange(sizeBySpeedRange);
            rotationBySpeedRange = OrderedRange(rotationBySpeedRange);
            lifetimeByEmitterSpeedRange = OrderedRange(
                lifetimeByEmitterSpeedRange);
            textureSheetTilesX = Mathf.Max(1, textureSheetTilesX);
            textureSheetTilesY = Mathf.Max(1, textureSheetTilesY);
            textureSheetRowIndex = Mathf.Clamp(
                textureSheetRowIndex, 0, textureSheetTilesY - 1);
            textureSheetCycleCount = Mathf.Max(1, textureSheetCycleCount);
            textureSheetFps = Mathf.Max(0f, textureSheetFps);
            textureSheetSpeedRange = OrderedRange(textureSheetSpeedRange);
            rendererFlip = new Vector3(
                Mathf.Clamp01(rendererFlip.x),
                Mathf.Clamp01(rendererFlip.y),
                Mathf.Clamp01(rendererFlip.z));
            if (emissionBursts == null) emissionBursts = System.Array.Empty<GPUEmissionBurst>();
            EnsureMaterials();
            RecreateTargetsIfNeeded(false);
        }

        void EnsureMaterials()
        {
            if (simulateProperties == null)
            {
                simulateProperties = new MaterialPropertyBlock();
            }

            if (simulateMaterial == null)
            {
                var sim = Shader.Find("Hidden/GPUParticles/SimulateMRT");
                if (sim != null) simulateMaterial = CoreUtils.CreateEngineMaterial(sim);
            }
            if (renderMaterial == null)
            {
                var r = Shader.Find("GPUParticles/UnlitBillboardURP");
                if (r != null) renderMaterial = CoreUtils.CreateEngineMaterial(r);
            }
        }

        int CeilSqrt(int n) => Mathf.CeilToInt(Mathf.Sqrt(n));

        bool RequiresRotationXYPhase()
        {
            return renderMode == GPURenderMode.Mesh &&
                   rotationBySpeedEnabled &&
                   rotationBySpeedSeparateAxes;
        }

        void RecreateTargetsIfNeeded(bool force)
        {
            int newGrid = CeilSqrt(maxParticles);
            bool baseTargetsReady = newGrid == gridSize &&
                                    posLife[0] != null &&
                                    rotationPhaseRT[0] != null;
            bool requiresRotationXYPhase = RequiresRotationXYPhase();
            if (!force && baseTargetsReady)
            {
                if (requiresRotationXYPhase &&
                    rotationXYPhaseRT[0] == null)
                {
                    CreateRT(
                        ref rotationXYPhaseRT[0],
                        RenderTextureFormat.RGFloat);
                    CreateRT(
                        ref rotationXYPhaseRT[1],
                        RenderTextureFormat.RGFloat);
                    ClearRT(rotationXYPhaseRT[0]);
                    ClearRT(rotationXYPhaseRT[1]);
                    rotationXYPhaseActive = false;
                }
                return;
            }

            ReleaseTargets();
            gridSize = newGrid;
            capacity = gridSize * gridSize;

            CreateRT(ref posLife[0], RenderTextureFormat.ARGBFloat);
            CreateRT(ref posLife[1], RenderTextureFormat.ARGBFloat);
            CreateRT(ref velSize[0], RenderTextureFormat.ARGBFloat);
            CreateRT(ref velSize[1], RenderTextureFormat.ARGBFloat);
            CreateRT(ref colorRT[0], RenderTextureFormat.ARGBHalf);
            CreateRT(ref colorRT[1], RenderTextureFormat.ARGBHalf);
            // X stores rotation phase. YZW stores the emitter velocity captured
            // at birth for Inherit Velocity Initial and Lifetime by Emitter Speed,
            // keeping the MRT count at 4.
            CreateRT(ref rotationPhaseRT[0], RenderTextureFormat.ARGBFloat);
            CreateRT(ref rotationPhaseRT[1], RenderTextureFormat.ARGBFloat);
            // XY stores the extra Rotation by Speed phases used only by
            // separate-axis Mesh particles. Allocate it only for that feature
            // and keep its update in a conditional second pass, preserving the
            // four-target mobile MRT path and baseline memory footprint.
            // Phase is accumulated every simulation step. Half precision adds
            // a visible multi-degree drift within one lifetime, so retain the
            // same float precision as the Z phase in moduleState.
            if (requiresRotationXYPhase)
            {
                CreateRT(
                    ref rotationXYPhaseRT[0],
                    RenderTextureFormat.RGFloat);
                CreateRT(
                    ref rotationXYPhaseRT[1],
                    RenderTextureFormat.RGFloat);
            }

            ClearRT(posLife[0]); ClearRT(posLife[1]);
            ClearRT(velSize[0]); ClearRT(velSize[1]);
            ClearRT(colorRT[0]); ClearRT(colorRT[1]);
            ClearRT(rotationPhaseRT[0]); ClearRT(rotationPhaseRT[1]);
            if (rotationXYPhaseRT[0] != null)
            {
                ClearRT(rotationXYPhaseRT[0]);
                ClearRT(rotationXYPhaseRT[1]);
            }

            ping = 0;
            emitCursor = 0;
            emitCarry = 0f;
            distanceEmitCarry = 0f;
            emissionTime = 0f;
            previousEmitterPositionWS = transform.position;
            previousEmitterPositionValid = true;
            previousEmitterVelocityWS = Vector3.zero;
            stepBurstGroupCount = 0;
            System.Array.Clear(stepBurstCounts, 0, stepBurstCounts.Length);
            System.Array.Clear(stepBurstAges, 0, stepBurstAges.Length);
            simulationTick = 0;
            rotationXYPhaseActive = false;
            lastSimulatedFrame = -1;
            ResetCullingTracking();
        }

        void CreateRT(ref RenderTexture rt, RenderTextureFormat fmt)
        {
            rt = new RenderTexture(gridSize, gridSize, 0, fmt, RenderTextureReadWrite.Linear);
            rt.name = $"GPUParticles_{fmt}_{gridSize}";
            rt.enableRandomWrite = false;
            rt.useMipMap = false;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Point;
            rt.Create();
        }

        void ClearRT(RenderTexture rt)
        {
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
        }

        void ReleaseTargets()
        {
            for (int i = 0; i < 2; i++)
            {
                if (posLife[i] != null) { posLife[i].Release(); Object.DestroyImmediate(posLife[i]); posLife[i] = null; }
                if (velSize[i] != null) { velSize[i].Release(); Object.DestroyImmediate(velSize[i]); velSize[i] = null; }
                if (colorRT[i] != null) { colorRT[i].Release(); Object.DestroyImmediate(colorRT[i]); colorRT[i] = null; }
                if (rotationPhaseRT[i] != null)
                {
                    rotationPhaseRT[i].Release();
                    Object.DestroyImmediate(rotationPhaseRT[i]);
                    rotationPhaseRT[i] = null;
                }
                if (rotationXYPhaseRT[i] != null)
                {
                    rotationXYPhaseRT[i].Release();
                    Object.DestroyImmediate(rotationXYPhaseRT[i]);
                    rotationXYPhaseRT[i] = null;
                }
            }
            rotationXYPhaseActive = false;
        }

        internal void ResetSimulation()
        {
            EnsureMaterials();
            RecreateTargetsIfNeeded(true);
        }

        internal void InitializePlaybackFromSettings()
        {
            ResetEmissionTimeline();
            ResetCullingTracking();
            playbackState = !Application.isPlaying || playOnAwake
                ? PlaybackState.Playing
                : PlaybackState.Stopped;
            resumeState = PlaybackState.Playing;
            stoppingElapsed = 0f;
            stoppingDuration = 0f;
            prewarmPending = ShouldPrewarm();
            stopActionPending = false;
            stopActionInvoked = false;
            lastSimulatedFrame = -1;
        }

        void ResetCullingTracking()
        {
            visibilityFrame = -1;
            visibleThisFrame = false;
            lastCullingClock = CurrentCullingClock();
            cullingClockValid = Application.isPlaying;
            wasCulled = false;
            trackedCullingMode = cullingMode;
        }

        public void Play(bool withChildren = true)
        {
            PlaySelf();
            if (withChildren)
            {
                ApplyToChildren(system => system.PlaySelf());
            }
        }

        public void Pause(bool withChildren = true)
        {
            PauseSelf();
            if (withChildren)
            {
                ApplyToChildren(system => system.PauseSelf());
            }
        }

        public void Stop(
            bool withChildren = true,
            ParticleSystemStopBehavior stopBehavior =
                ParticleSystemStopBehavior.StopEmitting)
        {
            StopSelf(stopBehavior);
            if (withChildren)
            {
                ApplyToChildren(system => system.StopSelf(stopBehavior));
            }
        }

        public void Clear(bool withChildren = true)
        {
            ClearSelf();
            if (withChildren)
            {
                ApplyToChildren(system => system.ClearSelf());
            }
        }

        void PlaySelf()
        {
            if (playbackState == PlaybackState.Playing) return;

            bool resumeFromPause = playbackState == PlaybackState.Paused;
            if (!resumeFromPause)
            {
                ResetEmissionTimeline();
            }

            playbackState = PlaybackState.Playing;
            resumeState = PlaybackState.Playing;
            stoppingElapsed = 0f;
            stoppingDuration = 0f;
            prewarmPending = !resumeFromPause && ShouldPrewarm();
            stopActionPending = false;
            stopActionInvoked = false;
            lastSimulatedFrame = -1;
            ResetCullingTracking();
        }

        void PauseSelf()
        {
            if (!isPlaying) return;

            resumeState = playbackState;
            playbackState = PlaybackState.Paused;
            lastSimulatedFrame = -1;
        }

        void StopSelf(ParticleSystemStopBehavior stopBehavior)
        {
            bool wasActive = playbackState != PlaybackState.Stopped;
            if (!wasActive)
            {
                if (stopBehavior ==
                    ParticleSystemStopBehavior.StopEmittingAndClear)
                {
                    ResetSimulation();
                }
                return;
            }

            playbackState = stopBehavior ==
                ParticleSystemStopBehavior.StopEmitting
                    ? PlaybackState.Stopping
                    : PlaybackState.Stopped;
            resumeState = PlaybackState.Playing;
            stoppingElapsed = 0f;
            stoppingDuration = playbackState == PlaybackState.Stopping
                ? MaximumParticleLifetime()
                : 0f;
            prewarmPending = false;
            lastSimulatedFrame = -1;

            if (stopBehavior ==
                ParticleSystemStopBehavior.StopEmittingAndClear)
            {
                ResetSimulation();
                QueueStopAction();
            }
        }

        void CompletePlayback()
        {
            playbackState = PlaybackState.Stopped;
            resumeState = PlaybackState.Playing;
            stoppingElapsed = stoppingDuration;
            prewarmPending = false;
            QueueStopAction();
        }

        void QueueStopAction()
        {
            if (stopActionInvoked ||
                stopAction == ParticleSystemStopAction.None)
            {
                return;
            }

            stopActionPending = true;
        }

        void ApplyPendingStopAction()
        {
            if (!stopActionPending || stopActionInvoked) return;

            stopActionPending = false;
            stopActionInvoked = true;
            GameObject target = stopActionTarget != null
                ? stopActionTarget
                : gameObject;
            if (target == null) return;

            switch (stopAction)
            {
                case ParticleSystemStopAction.Callback:
                    target.SendMessage(
                        "OnParticleSystemStopped",
                        SendMessageOptions.DontRequireReceiver);
                    break;

                case ParticleSystemStopAction.Disable:
                    target.SetActive(false);
                    break;

                case ParticleSystemStopAction.Destroy:
                    Destroy(target);
                    break;
            }
        }

        void ClearSelf()
        {
            PlaybackState savedPlaybackState = playbackState;
            PlaybackState savedResumeState = resumeState;
            float savedStoppingElapsed = stoppingElapsed;
            float savedStoppingDuration = stoppingDuration;
            bool savedPrewarmPending = prewarmPending;
            float savedEmissionTime = emissionTime;
            float savedEmitCarry = emitCarry;
            float savedDistanceEmitCarry = distanceEmitCarry;
            Vector3 savedPreviousEmitterPosition = previousEmitterPositionWS;
            bool savedPreviousEmitterPositionValid =
                previousEmitterPositionValid;
            Vector3 savedPreviousEmitterVelocity = previousEmitterVelocityWS;
            uint savedSimulationTick = simulationTick;

            ResetSimulation();

            playbackState = savedPlaybackState;
            resumeState = savedResumeState;
            stoppingElapsed = savedStoppingElapsed;
            stoppingDuration = savedStoppingDuration;
            prewarmPending = savedPrewarmPending;
            emissionTime = savedEmissionTime;
            emitCarry = savedEmitCarry;
            distanceEmitCarry = savedDistanceEmitCarry;
            previousEmitterPositionWS = savedPreviousEmitterPosition;
            previousEmitterPositionValid =
                savedPreviousEmitterPositionValid;
            previousEmitterVelocityWS = savedPreviousEmitterVelocity;
            simulationTick = savedSimulationTick;
        }

        void ResetEmissionTimeline()
        {
            emitCursor = 0;
            emitCarry = 0f;
            distanceEmitCarry = 0f;
            emissionTime = 0f;
            latestParticleBirthTime = 0f;
            particleBirthObserved = false;
            previousEmitterPositionWS = transform.position;
            previousEmitterPositionValid = true;
            previousEmitterVelocityWS = Vector3.zero;
            stepBurstGroupCount = 0;
            System.Array.Clear(stepBurstCounts, 0, stepBurstCounts.Length);
            System.Array.Clear(stepBurstAges, 0, stepBurstAges.Length);
            simulationTick = 0;
        }

        void ObserveParticleBirthTime(float birthTime)
        {
            latestParticleBirthTime = particleBirthObserved
                ? Mathf.Max(latestParticleBirthTime, birthTime)
                : birthTime;
            particleBirthObserved = true;
        }

        bool IsActivelyEmitting()
        {
            if (playbackState != PlaybackState.Playing || !emissionEnabled)
            {
                return false;
            }

            float startDelay = ResolveEmissionStartDelay();
            if (emissionTime < startDelay) return false;
            return emissionLooping ||
                   emissionTime < startDelay + Mathf.Max(0.05f, emissionDuration);
        }

        bool ShouldPrewarm()
        {
            return prewarm &&
                   emissionLooping &&
                   ResolveEmissionStartDelay() <= 1e-6f;
        }

        void ApplyPendingPrewarm(CommandBuffer cmd)
        {
            if (!prewarmPending) return;

            prewarmPending = false;
            float remaining = Mathf.Max(0.05f, emissionDuration);
            while (remaining > PrewarmStep + 1e-6f)
            {
                SimulateStep(cmd, PrewarmStep, true);
                remaining -= PrewarmStep;
            }

            if (remaining > 1e-6f)
            {
                SimulateStep(cmd, remaining, true);
            }
        }

        float MaximumParticleLifetime()
        {
            float maximumLifetime = Mathf.Max(
                0.001f,
                Mathf.Max(startLifetime, startLifetimeMin));
            if (startLifetimeMode == ParticleSystemCurveMode.Curve ||
                startLifetimeMode == ParticleSystemCurveMode.TwoCurves)
            {
                maximumLifetime = MaximumLUTValue(
                    startLifetimeLUT,
                    maximumLifetime);
            }

            if (lifetimeByEmitterSpeedEnabled)
            {
                float maximumMultiplier = MaximumLUTValue(
                    lifetimeByEmitterSpeedLUT,
                    1f);
                maximumLifetime *= Mathf.Max(0f, maximumMultiplier);
            }

            return Mathf.Max(0.001f, maximumLifetime);
        }

        static float MaximumLUTValue(Texture2D texture, float fallback)
        {
            if (texture == null || !texture.isReadable) return fallback;

            try
            {
                Color[] pixels = texture.GetPixels();
                float maximum = float.NegativeInfinity;
                for (int i = 0; i < pixels.Length; i++)
                {
                    maximum = Mathf.Max(maximum, pixels[i].r);
                }
                return float.IsNegativeInfinity(maximum)
                    ? fallback
                    : maximum;
            }
            catch (UnityException)
            {
                return fallback;
            }
        }

        void ApplyToChildren(System.Action<GPUParticleSystem> action)
        {
            GPUParticleSystem[] systems =
                GetComponentsInChildren<GPUParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                GPUParticleSystem system = systems[i];
                if (system != null && system != this)
                {
                    action(system);
                }
            }
        }

        public void SetStartLifetimeRange(float minimum, float maximum)
        {
            startLifetimeMin = Mathf.Max(0.001f, Mathf.Min(minimum, maximum));
            startLifetime = Mathf.Max(0.001f, Mathf.Max(minimum, maximum));
            randomizeStartLifetime = !Mathf.Approximately(startLifetimeMin, startLifetime);
            startLifetimeMode = randomizeStartLifetime
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartSpeedRange(float minimum, float maximum)
        {
            startSpeedMin = Mathf.Min(minimum, maximum);
            startSpeed = Mathf.Max(minimum, maximum);
            randomizeStartSpeed = !Mathf.Approximately(startSpeedMin, startSpeed);
            startSpeedMode = randomizeStartSpeed
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartSizeRange(float minimum, float maximum)
        {
            startSizeMin = Mathf.Max(0f, Mathf.Min(minimum, maximum));
            startSize = Mathf.Max(0f, Mathf.Max(minimum, maximum));
            randomizeStartSize = !Mathf.Approximately(startSizeMin, startSize);
            startSizeMode = randomizeStartSize
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartSizeYRange(float minimum, float maximum)
        {
            startSizeYMin = Mathf.Max(0f, Mathf.Min(minimum, maximum));
            startSizeY = Mathf.Max(0f, Mathf.Max(minimum, maximum));
            randomizeStartSizeY = !Mathf.Approximately(startSizeYMin, startSizeY);
            startSizeYMode = randomizeStartSizeY
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartSizeZRange(float minimum, float maximum)
        {
            startSizeZMin = Mathf.Max(0f, Mathf.Min(minimum, maximum));
            startSizeZ = Mathf.Max(0f, Mathf.Max(minimum, maximum));
            randomizeStartSizeZ = !Mathf.Approximately(startSizeZMin, startSizeZ);
            startSizeZMode = randomizeStartSizeZ
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartColorRange(Color minimum, Color maximum, bool randomized)
        {
            startColorMin = minimum;
            startColor = maximum;
            randomizeStartColor = randomized;
            startColorMode = randomized
                ? ParticleSystemGradientMode.TwoColors
                : ParticleSystemGradientMode.Color;
        }

        public void SetGravityModifierRange(float minimum, float maximum)
        {
            gravityModifierMin = Mathf.Min(minimum, maximum);
            gravityModifier = Mathf.Max(minimum, maximum);
            randomizeGravityModifier = !Mathf.Approximately(gravityModifierMin, gravityModifier);
            gravityModifierMode = randomizeGravityModifier
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartRotationRange(float minimum, float maximum)
        {
            startRotationMin = Mathf.Min(minimum, maximum);
            startRotation = Mathf.Max(minimum, maximum);
            randomizeStartRotation = !Mathf.Approximately(startRotationMin, startRotation);
            startRotationMode = randomizeStartRotation
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartRotationXRange(float minimum, float maximum)
        {
            startRotationXMin = Mathf.Min(minimum, maximum);
            startRotationX = Mathf.Max(minimum, maximum);
            randomizeStartRotationX =
                !Mathf.Approximately(startRotationXMin, startRotationX);
            startRotationXMode = randomizeStartRotationX
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetStartRotationYRange(float minimum, float maximum)
        {
            startRotationYMin = Mathf.Min(minimum, maximum);
            startRotationY = Mathf.Max(minimum, maximum);
            randomizeStartRotationY =
                !Mathf.Approximately(startRotationYMin, startRotationY);
            startRotationYMode = randomizeStartRotationY
                ? ParticleSystemCurveMode.TwoConstants
                : ParticleSystemCurveMode.Constant;
        }

        public void SetRotationOverLifetimeRange(float minimum, float maximum)
        {
            rotationOverLifetimeMin = Mathf.Min(minimum, maximum);
            rotationOverLifetime = Mathf.Max(minimum, maximum);
            randomizeRotationOverLifetime =
                !Mathf.Approximately(rotationOverLifetimeMin, rotationOverLifetime);
        }

        public void SetColorBySpeedRange(Vector2 range)
        {
            colorBySpeedRange = OrderedRange(range);
        }

        public void SetSizeBySpeedRange(Vector2 range)
        {
            sizeBySpeedRange = OrderedRange(range);
        }

        public void SetRotationBySpeedRange(Vector2 range)
        {
            rotationBySpeedRange = OrderedRange(range);
        }

        public void SetLifetimeByEmitterSpeedRange(Vector2 range)
        {
            lifetimeByEmitterSpeedRange = OrderedRange(range);
        }

        public void SetRingBufferLoopRange(Vector2 range)
        {
            ringBufferLoopRange = Ordered01Range(range);
        }

        public void SetTextureSheetSpeedRange(Vector2 range)
        {
            textureSheetSpeedRange = OrderedRange(range);
        }

        static Vector2 OrderedRange(Vector2 range)
        {
            return new Vector2(
                Mathf.Min(range.x, range.y),
                Mathf.Max(range.x, range.y));
        }

        static Vector2 Ordered01Range(Vector2 range)
        {
            Vector2 ordered = OrderedRange(range);
            return new Vector2(
                Mathf.Clamp01(ordered.x),
                Mathf.Clamp01(ordered.y));
        }

        static float InverseTextureWidth(Texture2D texture)
        {
            return 1f / Mathf.Max(1, texture != null ? texture.width : 1);
        }

        public void SetEmissionRateOverTime(ParticleSystem.MinMaxCurve curve)
        {
            emissionRateOverTimeMode = curve.mode;
            emissionRateOverTimeCurveMultiplier = Mathf.Max(0f, curve.curveMultiplier);
            emissionRateOverTimeCurveMin = curve.curveMin ?? curve.curve;
            emissionRateOverTimeCurveMax = curve.curveMax ?? curve.curve;

            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    emissionRateOverTimeMin = Mathf.Max(0f, curve.constant);
                    emissionRateOverTime = emissionRateOverTimeMin;
                    break;

                case ParticleSystemCurveMode.TwoConstants:
                    emissionRateOverTimeMin = Mathf.Max(0f,
                        Mathf.Min(curve.constantMin, curve.constantMax));
                    emissionRateOverTime = Mathf.Max(0f,
                        Mathf.Max(curve.constantMin, curve.constantMax));
                    break;

                case ParticleSystemCurveMode.Curve:
                    float curveValue = emissionRateOverTimeCurveMax != null
                        ? emissionRateOverTimeCurveMax.Evaluate(0f)
                        : 0f;
                    emissionRateOverTime = Mathf.Max(0f,
                        curveValue * emissionRateOverTimeCurveMultiplier);
                    emissionRateOverTimeMin = emissionRateOverTime;
                    break;

                case ParticleSystemCurveMode.TwoCurves:
                    float minimumCurveValue = emissionRateOverTimeCurveMin != null
                        ? emissionRateOverTimeCurveMin.Evaluate(0f)
                        : 0f;
                    float maximumCurveValue = emissionRateOverTimeCurveMax != null
                        ? emissionRateOverTimeCurveMax.Evaluate(0f)
                        : 0f;
                    emissionRateOverTimeMin = Mathf.Max(0f,
                        minimumCurveValue * emissionRateOverTimeCurveMultiplier);
                    emissionRateOverTime = Mathf.Max(0f,
                        maximumCurveValue * emissionRateOverTimeCurveMultiplier);
                    break;
            }
        }

        public void SetEmissionRateOverDistance(ParticleSystem.MinMaxCurve curve)
        {
            emissionRateOverDistanceMode = curve.mode;
            emissionRateOverDistanceCurveMultiplier = Mathf.Max(0f, curve.curveMultiplier);
            emissionRateOverDistanceCurveMin = curve.curveMin ?? curve.curve;
            emissionRateOverDistanceCurveMax = curve.curveMax ?? curve.curve;

            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    emissionRateOverDistanceMin = Mathf.Max(0f, curve.constant);
                    emissionRateOverDistance = emissionRateOverDistanceMin;
                    break;

                case ParticleSystemCurveMode.TwoConstants:
                    emissionRateOverDistanceMin = Mathf.Max(0f,
                        Mathf.Min(curve.constantMin, curve.constantMax));
                    emissionRateOverDistance = Mathf.Max(0f,
                        Mathf.Max(curve.constantMin, curve.constantMax));
                    break;

                case ParticleSystemCurveMode.Curve:
                    float curveValue = emissionRateOverDistanceCurveMax != null
                        ? emissionRateOverDistanceCurveMax.Evaluate(0f)
                        : 0f;
                    emissionRateOverDistance = Mathf.Max(0f,
                        curveValue * emissionRateOverDistanceCurveMultiplier);
                    emissionRateOverDistanceMin = emissionRateOverDistance;
                    break;

                case ParticleSystemCurveMode.TwoCurves:
                    float minimumCurveValue = emissionRateOverDistanceCurveMin != null
                        ? emissionRateOverDistanceCurveMin.Evaluate(0f)
                        : 0f;
                    float maximumCurveValue = emissionRateOverDistanceCurveMax != null
                        ? emissionRateOverDistanceCurveMax.Evaluate(0f)
                        : 0f;
                    emissionRateOverDistanceMin = Mathf.Max(0f,
                        minimumCurveValue * emissionRateOverDistanceCurveMultiplier);
                    emissionRateOverDistance = Mathf.Max(0f,
                        maximumCurveValue * emissionRateOverDistanceCurveMultiplier);
                    break;
            }
        }

        public void SetEmissionStartDelayRange(float minimum, float maximum)
        {
            emissionStartDelayMin = Mathf.Max(0f, Mathf.Min(minimum, maximum));
            emissionStartDelay = Mathf.Max(0f, Mathf.Max(minimum, maximum));
            randomizeEmissionStartDelay =
                !Mathf.Approximately(emissionStartDelayMin, emissionStartDelay);
        }

        public void SetEmissionBursts(ParticleSystem.Burst[] bursts)
        {
            if (bursts == null || bursts.Length == 0)
            {
                emissionBursts = System.Array.Empty<GPUEmissionBurst>();
                return;
            }

            var mapped = new GPUEmissionBurst[bursts.Length];
            for (int i = 0; i < bursts.Length; i++)
            {
                mapped[i] = GPUEmissionBurst.FromShuriken(bursts[i]);
            }
            emissionBursts = mapped;
        }

        internal float ResolveStartLifetime(int particleId)
        {
            return ResolveStartLifetime(particleId, 0f, Vector3.zero);
        }

        internal float ResolveStartLifetime(
            int particleId,
            Vector3 birthEmitterVelocityWS)
        {
            return ResolveStartLifetime(
                particleId,
                0f,
                birthEmitterVelocityWS);
        }

        internal float ResolveStartLifetime(
            int particleId,
            float particleAge,
            Vector3 birthEmitterVelocityWS)
        {
            uint id = (uint)particleId;
            ParticleSystemCurveMode mode = EffectiveStartLifetimeMode();
            float baseLifetime;
            if (mode == ParticleSystemCurveMode.Curve ||
                mode == ParticleSystemCurveMode.TwoCurves)
            {
                float activeTime = Mathf.Max(
                    0f,
                    emissionTime - ResolveEmissionStartDelay());
                float birthActiveTime = Mathf.Max(0f, activeTime - particleAge);
                float duration = Mathf.Max(0.05f, emissionDuration);
                float sampleDeltaTime = Mathf.Max(
                    1e-6f,
                    lastSimulationDeltaTime);
                float sampledBirthTime = Mathf.Ceil(
                    Mathf.Max(0f, birthActiveTime - 1e-6f) /
                    sampleDeltaTime) * sampleDeltaTime +
                    sampleDeltaTime * StartLifetimeCurveTickPhase;
                float normalizedBirthTime = emissionLooping
                    ? Mathf.Repeat(sampledBirthTime, duration) / duration
                    : Mathf.Clamp01(sampledBirthTime / duration);
                float minimum = Mathf.Max(
                    0.001f,
                    SampleLUTRow(startLifetimeLUT, normalizedBirthTime, 0));
                float maximum = Mathf.Max(
                    0.001f,
                    SampleLUTRow(startLifetimeLUT, normalizedBirthTime, 1));
                baseLifetime = mode == ParticleSystemCurveMode.TwoCurves
                    ? Mathf.LerpUnclamped(
                        minimum,
                        maximum,
                        Hash01(id ^ 0x68E31DA4u))
                    : maximum;
            }
            else
            {
                baseLifetime = ResolveRandomRange(
                    mode == ParticleSystemCurveMode.TwoConstants,
                    startLifetimeMin,
                    startLifetime,
                    id,
                    0x68E31DA4u);
            }
            if (!lifetimeByEmitterSpeedEnabled ||
                lifetimeByEmitterSpeedLUT == null)
            {
                return baseLifetime;
            }

            float rangeWidth = lifetimeByEmitterSpeedRange.y -
                               lifetimeByEmitterSpeedRange.x;
            float speed = birthEmitterVelocityWS.magnitude;
            float speedPosition = rangeWidth > 1e-6f
                ? Mathf.Clamp01(
                    (speed - lifetimeByEmitterSpeedRange.x) / rangeWidth)
                : speed > lifetimeByEmitterSpeedRange.x ? 1f : 0f;
            float minimumMultiplier = SampleLUTRow(
                lifetimeByEmitterSpeedLUT, speedPosition, 0);
            float maximumMultiplier = SampleLUTRow(
                lifetimeByEmitterSpeedLUT, speedPosition, 1);
            float blend = Hash01((uint)particleId ^ 0x94D049BBu);
            float multiplier = Mathf.Max(
                0f,
                Mathf.LerpUnclamped(
                    minimumMultiplier,
                    maximumMultiplier,
                    blend));
            return baseLifetime * multiplier;
        }

        internal void ResolveParticleLifetimeState(
            int particleId,
            float lifetimeState,
            Vector3 birthEmitterVelocityWS,
            out float particleStartLifetime,
            out float particleAge,
            out float remainingLifetime)
        {
            ResolveParticleLifetimeStateDetailed(
                particleId,
                lifetimeState,
                birthEmitterVelocityWS,
                out particleStartLifetime,
                out _,
                out particleAge,
                out remainingLifetime);
        }

        void ResolveParticleLifetimeStateDetailed(
            int particleId,
            float lifetimeState,
            Vector3 birthEmitterVelocityWS,
            out float particleStartLifetime,
            out float totalParticleAge,
            out float particleAge,
            out float remainingLifetime)
        {
            if (UsesParticleAgeLifetimeState())
            {
                totalParticleAge = Mathf.Max(0f, lifetimeState - 1f);
                particleStartLifetime = ResolveStartLifetime(
                    particleId,
                    totalParticleAge,
                    birthEmitterVelocityWS);
            }
            else
            {
                particleStartLifetime = ResolveStartLifetime(
                    particleId,
                    birthEmitterVelocityWS);
                totalParticleAge = Mathf.Max(
                    0f,
                    particleStartLifetime - Mathf.Max(0f, lifetimeState));
            }

            particleAge = ResolveRingBufferParticleAge(
                totalParticleAge,
                particleStartLifetime);
            remainingLifetime = Mathf.Max(
                0f,
                particleStartLifetime - particleAge);
        }

        float ResolveRingBufferParticleAge(
            float totalParticleAge,
            float particleStartLifetime)
        {
            float lifetime = Mathf.Max(0.001f, particleStartLifetime);
            float totalAge = Mathf.Max(0f, totalParticleAge);
            if (ringBufferMode ==
                ParticleSystemRingBufferMode.PauseUntilReplaced)
            {
                return Mathf.Min(totalAge, lifetime);
            }

            if (ringBufferMode !=
                ParticleSystemRingBufferMode.LoopUntilReplaced)
            {
                return totalAge;
            }

            Vector2 loopRange = Ordered01Range(ringBufferLoopRange);
            float loopStart = loopRange.x * lifetime;
            float loopEnd = loopRange.y * lifetime;
            float loopLength = loopEnd - loopStart;
            if (totalAge < loopEnd)
            {
                return totalAge;
            }
            if (loopLength <= 1e-6f)
            {
                return loopStart;
            }
            return loopStart + Mathf.Repeat(
                totalAge - loopStart,
                loopLength);
        }

        internal float ResolveParticleRotationRadians(
            int particleId,
            float lifetimeState,
            float rotationBySpeedPhase = 0f,
            Vector3 birthEmitterVelocityWS = default)
        {
            return ResolveParticleRotationEulerRadians(
                particleId,
                lifetimeState,
                new Vector3(0f, 0f, rotationBySpeedPhase),
                birthEmitterVelocityWS).z;
        }

        internal Vector3 ResolveParticleRotationEulerRadians(
            int particleId,
            float lifetimeState,
            Vector3 rotationBySpeedPhase,
            Vector3 birthEmitterVelocityWS = default)
        {
            uint id = (uint)particleId;
            ResolveParticleLifetimeStateDetailed(
                particleId,
                lifetimeState,
                birthEmitterVelocityWS,
                out float particleStartLifetime,
                out float totalParticleAge,
                out _,
                out _);
            Vector3 particleStartRotation = new Vector3(
                ResolveStartRotationAxis(id, totalParticleAge, 0),
                ResolveStartRotationAxis(id, totalParticleAge, 1),
                ResolveStartRotationAxis(id, totalParticleAge, 2));
            float rotationDirection = ResolveRotationDirection(id);

            Vector3 lifetimeRotation = Vector3.zero;
            if (rotationOverLifetimeSeparateAxes)
            {
                lifetimeRotation.x = ResolveRotationOverLifetimeAngle(
                    id,
                    totalParticleAge,
                    particleStartLifetime,
                    0);
                lifetimeRotation.y = ResolveRotationOverLifetimeAngle(
                    id,
                    totalParticleAge,
                    particleStartLifetime,
                    1);
            }
            if (rotationOverLifetimeIntegralLUT == null)
            {
                lifetimeRotation.z = ResolveRandomRange(
                    randomizeRotationOverLifetime,
                    rotationOverLifetimeMin,
                    rotationOverLifetime,
                    id,
                    0xD3A2646Cu) * totalParticleAge;
            }
            else
            {
                lifetimeRotation.z = ResolveRotationOverLifetimeAngle(
                    id,
                    totalParticleAge,
                    particleStartLifetime,
                    2);
            }

            return rotationDirection *
                   (particleStartRotation +
                    lifetimeRotation +
                    rotationBySpeedPhase);
        }

        float ResolveRotationOverLifetimeAngle(
            uint particleId,
            float totalParticleAge,
            float particleStartLifetime,
            int axis)
        {
            float lifetime = Mathf.Max(0.001f, particleStartLifetime);
            float totalAge = Mathf.Max(0f, totalParticleAge);
            float normalizedTotalAge = totalAge / lifetime;
            if (ringBufferMode == ParticleSystemRingBufferMode.Disabled)
            {
                float leadDuration = Mathf.Min(
                    totalAge,
                    Mathf.Max(0f, lastSimulationDeltaTime) * 0.5f);
                float sampledAge = Mathf.Max(0f, totalAge - leadDuration);
                return SampleRotationIntegral(
                           particleId,
                           Mathf.Clamp01(sampledAge / lifetime),
                           axis) *
                       lifetime +
                       SampleRotationAngularVelocity(
                           particleId,
                           0f,
                           axis) * leadDuration;
            }

            if (ringBufferMode ==
                ParticleSystemRingBufferMode.PauseUntilReplaced)
            {
                if (totalAge <= lifetime)
                {
                    return SampleRotationIntegral(
                               particleId,
                               Mathf.Clamp01(normalizedTotalAge),
                               axis) *
                           lifetime;
                }

                float endIntegral =
                    SampleRotationIntegral(particleId, 1f, axis) * lifetime;
                float endAngularVelocity = axis == 2
                    ? ResolveRandomRange(
                        randomizeRotationOverLifetime,
                        rotationOverLifetimeMin,
                        rotationOverLifetime,
                        particleId,
                        0xD3A2646Cu)
                    : SampleRotationAngularVelocity(particleId, 1f, axis);
                return endIntegral +
                       (totalAge - lifetime) * endAngularVelocity;
            }

            Vector2 loopRange = Ordered01Range(ringBufferLoopRange);
            float loopStart = loopRange.x;
            float loopEnd = loopRange.y;
            if (normalizedTotalAge < loopEnd)
            {
                return SampleRotationIntegral(
                           particleId,
                           Mathf.Clamp01(normalizedTotalAge),
                           axis) *
                       lifetime;
            }

            float loopLength = loopEnd - loopStart;
            if (loopLength <= 1e-6f)
            {
                float heldIntegral =
                    SampleRotationIntegral(
                        particleId,
                        loopStart,
                        axis) * lifetime;
                float heldAngularVelocity =
                    SampleRotationAngularVelocity(
                        particleId,
                        loopStart,
                        axis);
                return heldIntegral +
                       Mathf.Max(0f, totalAge - loopStart * lifetime) *
                           heldAngularVelocity;
            }

            float elapsedLoopTime = Mathf.Max(
                0f,
                normalizedTotalAge - loopStart);
            float completedLoops = Mathf.Floor(
                elapsedLoopTime / loopLength);
            float loopRemainder = elapsedLoopTime -
                completedLoops * loopLength;
            float loopIntegral =
                SampleRotationIntegral(particleId, loopEnd, axis) -
                SampleRotationIntegral(particleId, loopStart, axis);
            float partialIntegral =
                SampleRotationIntegral(
                    particleId,
                    loopStart + loopRemainder,
                    axis) -
                SampleRotationIntegral(particleId, loopStart, axis);
            return (SampleRotationIntegral(particleId, loopStart, axis) +
                    completedLoops * loopIntegral + partialIntegral) *
                   lifetime;
        }

        float SampleRotationIntegral(
            uint particleId,
            float normalizedAge,
            int axis)
        {
            Texture2D integralLUT = RotationOverLifetimeIntegralLUT(axis);
            float minimumIntegral = SampleLUTRow(
                integralLUT,
                normalizedAge,
                0);
            float maximumIntegral = SampleLUTRow(
                integralLUT,
                normalizedAge,
                1);
            float blend = Hash01(
                particleId ^ RotationOverLifetimeRandomSalt(axis));
            return Mathf.LerpUnclamped(
                minimumIntegral,
                maximumIntegral,
                blend);
        }

        float SampleRotationAngularVelocity(
            uint particleId,
            float normalizedAge,
            int axis)
        {
            Texture2D integralLUT = RotationOverLifetimeIntegralLUT(axis);
            float sampleStep = Mathf.Max(
                1f / Mathf.Max(2, integralLUT != null ? integralLUT.width : 2),
                1f / 1024f);
            float lowerAge = Mathf.Max(0f, normalizedAge - sampleStep);
            float upperAge = Mathf.Min(1f, normalizedAge + sampleStep);
            float ageWidth = Mathf.Max(1e-6f, upperAge - lowerAge);
            return (SampleRotationIntegral(particleId, upperAge, axis) -
                    SampleRotationIntegral(particleId, lowerAge, axis)) /
                   ageWidth;
        }

        Texture2D RotationOverLifetimeIntegralLUT(int axis)
        {
            if (axis == 0) return rotationOverLifetimeXIntegralLUT;
            if (axis == 1) return rotationOverLifetimeYIntegralLUT;
            return rotationOverLifetimeIntegralLUT;
        }

        static uint RotationOverLifetimeRandomSalt(int axis)
        {
            if (axis == 0) return 0x3C6EF372u;
            if (axis == 1) return 0xDAA66D2Bu;
            return 0xD3A2646Cu;
        }

        float ResolveRotationDirection(uint particleId)
        {
            return Hash01(particleId ^ 0xF1357AEAu) <
                   Mathf.Clamp01(flipRotation)
                ? -1f
                : 1f;
        }

        internal Vector2 ResolveParticleBillboardSize(
            int particleId,
            float lifetimeState,
            float currentSizeX,
            float particleSpeed,
            Vector3 birthEmitterVelocityWS = default)
        {
            bool useSeparateAxes = startSize3D ||
                                   sizeOverLifetimeSeparateAxes ||
                                   (sizeBySpeedEnabled &&
                                    sizeBySpeedSeparateAxes);
            if (!useSeparateAxes)
            {
                return new Vector2(currentSizeX, currentSizeX);
            }

            ResolveParticleLifetimeStateDetailed(
                particleId,
                lifetimeState,
                birthEmitterVelocityWS,
                out float particleStartLifetime,
                out float totalParticleAge,
                out float particleAge,
                out _);
            float normalizedAge = particleStartLifetime > 1e-6f
                ? Mathf.Clamp01(particleAge / particleStartLifetime)
                : 0f;
            uint id = (uint)particleId;

            float startY = ResolveStartSizeAxis(
                id,
                totalParticleAge,
                startSize3D ? EffectiveStartSizeYMode() : EffectiveStartSizeMode(),
                startSize3D ? startSizeYMin : startSizeMin,
                startSize3D ? startSizeY : startSize,
                startSize3D ? startSizeYLUT : startSizeLUT,
                startSize3D ? 0xC13FA9A9u : 0x1B56C4E9u);
            Texture2D lifetimeYLUT = sizeOverLifetimeSeparateAxes
                ? sizeOverLifetimeYLUT
                : sizeOverLifetimeLUT;
            uint lifetimeSalt = sizeOverLifetimeSeparateAxes
                ? 0xA24BAED4u
                : 0x91E10DA5u;
            float lifetimeMultiplier = ResolveMinMaxCurveLUT(
                lifetimeYLUT,
                normalizedAge,
                id,
                lifetimeSalt,
                1f);

            float speedMultiplier = 1f;
            if (sizeBySpeedEnabled)
            {
                float rangeWidth = sizeBySpeedRange.y - sizeBySpeedRange.x;
                float speedPosition = rangeWidth > 1e-6f
                    ? Mathf.Clamp01(
                        (particleSpeed - sizeBySpeedRange.x) / rangeWidth)
                    : particleSpeed > sizeBySpeedRange.x ? 1f : 0f;
                speedMultiplier = ResolveMinMaxCurveLUT(
                    sizeBySpeedSeparateAxes
                        ? sizeBySpeedYLUT
                        : sizeBySpeedLUT,
                    speedPosition,
                    id,
                    sizeBySpeedSeparateAxes
                        ? 0xB5297A4Du
                        : 0xD192ED03u,
                    1f);
            }

            return new Vector2(
                currentSizeX,
                startY * lifetimeMultiplier * speedMultiplier);
        }

        float ResolveStartSizeAxis(
            uint particleId,
            float particleAge,
            ParticleSystemCurveMode mode,
            float minimum,
            float maximum,
            Texture2D lut,
            uint salt)
        {
            if (mode == ParticleSystemCurveMode.Constant ||
                mode == ParticleSystemCurveMode.TwoConstants)
            {
                return ResolveRandomRange(
                    mode == ParticleSystemCurveMode.TwoConstants,
                    minimum,
                    maximum,
                    particleId,
                    salt);
            }

            float activeTime = Mathf.Max(
                0f,
                emissionTime - ResolveEmissionStartDelay() - particleAge);
            float duration = Mathf.Max(0.05f, emissionDuration);
            float systemTime = emissionLooping
                ? Mathf.Repeat(activeTime, duration) / duration
                : Mathf.Clamp01(activeTime / duration);
            float minimumValue = SampleLUTRow(lut, systemTime, 0);
            float maximumValue = SampleLUTRow(lut, systemTime, 1);
            return mode == ParticleSystemCurveMode.TwoCurves
                ? Mathf.LerpUnclamped(
                    minimumValue,
                    maximumValue,
                    Hash01(particleId ^ salt))
                : maximumValue;
        }

        static float ResolveMinMaxCurveLUT(
            Texture2D lut,
            float normalizedPosition,
            uint particleId,
            uint salt,
            float defaultValue)
        {
            if (lut == null) return defaultValue;
            float minimum = SampleLUTRow(lut, normalizedPosition, 0);
            float maximum = SampleLUTRow(lut, normalizedPosition, 1);
            return Mathf.LerpUnclamped(
                minimum,
                maximum,
                Hash01(particleId ^ salt));
        }

        ParticleSystemCurveMode EffectiveStartLifetimeMode()
        {
            return startLifetimeMode == ParticleSystemCurveMode.Constant &&
                   randomizeStartLifetime
                ? ParticleSystemCurveMode.TwoConstants
                : startLifetimeMode;
        }

        ParticleSystemCurveMode EffectiveStartSizeMode()
        {
            return startSizeMode == ParticleSystemCurveMode.Constant &&
                   randomizeStartSize
                ? ParticleSystemCurveMode.TwoConstants
                : startSizeMode;
        }

        ParticleSystemCurveMode EffectiveStartSizeYMode()
        {
            return startSizeYMode == ParticleSystemCurveMode.Constant &&
                   randomizeStartSizeY
                ? ParticleSystemCurveMode.TwoConstants
                : startSizeYMode;
        }

        ParticleSystemCurveMode EffectiveStartSizeZMode()
        {
            return startSizeZMode == ParticleSystemCurveMode.Constant &&
                   randomizeStartSizeZ
                ? ParticleSystemCurveMode.TwoConstants
                : startSizeZMode;
        }

        internal bool IsStartLifetimeCurveMode()
        {
            ParticleSystemCurveMode mode = EffectiveStartLifetimeMode();
            return mode == ParticleSystemCurveMode.Curve ||
                   mode == ParticleSystemCurveMode.TwoCurves;
        }

        internal bool UsesParticleAgeLifetimeState()
        {
            return IsStartLifetimeCurveMode() ||
                   ringBufferMode !=
                       ParticleSystemRingBufferMode.Disabled;
        }

        internal float ResolveStartRotation(uint particleId, float particleAge)
        {
            ParticleSystemCurveMode mode = EffectiveStartRotationMode();
            if (mode == ParticleSystemCurveMode.Constant ||
                mode == ParticleSystemCurveMode.TwoConstants)
            {
                return ResolveRandomRange(
                    mode == ParticleSystemCurveMode.TwoConstants,
                    startRotationMin,
                    startRotation,
                    particleId,
                    0x165667B1u);
            }

            float activeTime = Mathf.Max(
                0f,
                emissionTime - ResolveEmissionStartDelay());
            float birthActiveTime = Mathf.Max(0f, activeTime - particleAge);
            float duration = Mathf.Max(0.05f, emissionDuration);
            float normalizedBirthTime = emissionLooping
                ? Mathf.Repeat(birthActiveTime, duration) / duration
                : Mathf.Clamp01(birthActiveTime / duration);
            float minimum = SampleLUTRow(
                startRotationLUT,
                normalizedBirthTime,
                0);
            float maximum = SampleLUTRow(
                startRotationLUT,
                normalizedBirthTime,
                1);
            return mode == ParticleSystemCurveMode.TwoCurves
                ? Mathf.LerpUnclamped(
                    minimum,
                    maximum,
                    Hash01(particleId ^ 0x165667B1u))
                : maximum;
        }

        float ResolveStartRotationAxis(
            uint particleId,
            float particleAge,
            int axis)
        {
            if (axis == 2)
            {
                return ResolveStartRotation(particleId, particleAge);
            }
            if (!startRotation3D)
            {
                return 0f;
            }

            ParticleSystemCurveMode mode = axis == 0
                ? EffectiveStartRotationXMode()
                : EffectiveStartRotationYMode();
            float maximumValue = axis == 0
                ? startRotationX
                : startRotationY;
            float minimumValue = axis == 0
                ? startRotationXMin
                : startRotationYMin;
            uint randomSalt = axis == 0
                ? 0x9E3779B9u
                : 0xBB67AE85u;
            if (mode == ParticleSystemCurveMode.Constant ||
                mode == ParticleSystemCurveMode.TwoConstants)
            {
                return ResolveRandomRange(
                    mode == ParticleSystemCurveMode.TwoConstants,
                    minimumValue,
                    maximumValue,
                    particleId,
                    randomSalt);
            }

            float activeTime = Mathf.Max(
                0f,
                emissionTime - ResolveEmissionStartDelay());
            float birthActiveTime = Mathf.Max(0f, activeTime - particleAge);
            float duration = Mathf.Max(0.05f, emissionDuration);
            float normalizedBirthTime = emissionLooping
                ? Mathf.Repeat(birthActiveTime, duration) / duration
                : Mathf.Clamp01(birthActiveTime / duration);
            Texture2D lut = axis == 0
                ? startRotationXLUT
                : startRotationYLUT;
            float minimum = SampleLUTRow(lut, normalizedBirthTime, 0);
            float maximum = SampleLUTRow(lut, normalizedBirthTime, 1);
            return mode == ParticleSystemCurveMode.TwoCurves
                ? Mathf.LerpUnclamped(
                    minimum,
                    maximum,
                    Hash01(particleId ^ randomSalt))
                : maximum;
        }

        ParticleSystemCurveMode EffectiveStartRotationMode()
        {
            return startRotationMode == ParticleSystemCurveMode.Constant &&
                   randomizeStartRotation
                ? ParticleSystemCurveMode.TwoConstants
                : startRotationMode;
        }

        ParticleSystemCurveMode EffectiveStartRotationXMode()
        {
            return startRotationXMode == ParticleSystemCurveMode.Constant &&
                   randomizeStartRotationX
                ? ParticleSystemCurveMode.TwoConstants
                : startRotationXMode;
        }

        ParticleSystemCurveMode EffectiveStartRotationYMode()
        {
            return startRotationYMode == ParticleSystemCurveMode.Constant &&
                   randomizeStartRotationY
                ? ParticleSystemCurveMode.TwoConstants
                : startRotationYMode;
        }

        static float SampleLUTRow(Texture2D texture, float normalizedPosition, int row)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0) return 0f;

            float samplePosition = Mathf.Clamp01(normalizedPosition) * (texture.width - 1);
            int lower = Mathf.Clamp(Mathf.FloorToInt(samplePosition), 0, texture.width - 1);
            int upper = Mathf.Min(lower + 1, texture.width - 1);
            float blend = samplePosition - lower;
            int y = Mathf.Clamp(row, 0, texture.height - 1);
            return Mathf.LerpUnclamped(
                texture.GetPixel(lower, y).r,
                texture.GetPixel(upper, y).r,
                blend);
        }

        static float ResolveRandomRange(
            bool randomized,
            float minimum,
            float maximum,
            uint particleId,
            uint salt)
        {
            if (!randomized) return maximum;
            return Mathf.LerpUnclamped(minimum, maximum, Hash01(particleId ^ salt));
        }

        static float Hash01(uint value)
        {
            unchecked
            {
                value += value << 10;
                value ^= value >> 6;
                value += value << 3;
                value ^= value >> 11;
                value += value << 15;
            }
            return (value & 0x00FFFFFFu) / 16777216f;
        }

        float ResolveEmissionStartDelay()
        {
            if (!randomizeEmissionStartDelay)
            {
                return emissionStartDelay;
            }

            if (!emissionStartDelayCacheValid ||
                !Mathf.Approximately(
                    cachedEmissionStartDelayMinimum,
                    emissionStartDelayMin) ||
                !Mathf.Approximately(
                    cachedEmissionStartDelayMaximum,
                    emissionStartDelay) ||
                cachedEmissionStartDelaySeed != emissionRandomSeed)
            {
                cachedResolvedEmissionStartDelay = Mathf.LerpUnclamped(
                    emissionStartDelayMin,
                    emissionStartDelay,
                    ShurikenMinMaxUtility.SampleSystemRandomValue(
                        emissionRandomSeed));
                cachedEmissionStartDelayMinimum = emissionStartDelayMin;
                cachedEmissionStartDelayMaximum = emissionStartDelay;
                cachedEmissionStartDelaySeed = emissionRandomSeed;
                emissionStartDelayCacheValid = true;
            }

            return cachedResolvedEmissionStartDelay;
        }

        void CalculateContinuousEmission(
            float stepStart,
            float stepEnd,
            float startDelay,
            out float emissionAmount,
            out float effectiveRate,
            out float windowStart)
        {
            emissionAmount = 0f;
            effectiveRate = 0f;
            windowStart = 0f;

            float duration = Mathf.Max(0.05f, emissionDuration);
            float activeStart = Mathf.Max(stepStart, startDelay);
            float activeEnd = stepEnd;
            if (!emissionLooping)
            {
                float timelineEnd = startDelay + duration;
                // Shuriken does not integrate a partial Rate-over-Time step when a
                // simulation update crosses the non-looping duration boundary.
                activeEnd = stepStart < timelineEnd && stepEnd > timelineEnd + 1e-6f
                    ? stepStart
                    : Mathf.Min(activeEnd, timelineEnd);
            }

            if (activeEnd <= activeStart) return;

            windowStart = activeStart - stepStart;
            float activeDuration = activeEnd - activeStart;
            float localStart = activeStart - startDelay;
            float localEnd = activeEnd - startDelay;

            if (!emissionLooping)
            {
                emissionAmount = IntegrateEmissionRate(
                    localEnd / duration,
                    0,
                    activeDuration);
            }
            else
            {
                float cursor = localStart;
                while (cursor < localEnd - 1e-6f)
                {
                    int loopIndex = Mathf.Max(0, Mathf.FloorToInt(cursor / duration));
                    float loopStart = loopIndex * duration;
                    float segmentEnd = Mathf.Min(localEnd, loopStart + duration);
                    float segmentDuration = segmentEnd - cursor;
                    emissionAmount += IntegrateEmissionRate(
                        (segmentEnd - loopStart) / duration,
                        loopIndex,
                        segmentDuration);
                    cursor = segmentEnd;
                }
            }

            emissionAmount = Mathf.Max(0f, emissionAmount);
            effectiveRate = activeDuration > 1e-6f
                ? emissionAmount / activeDuration
                : 0f;
        }

        float IntegrateEmissionRate(
            float normalizedEnd,
            int loopIndex,
            float duration)
        {
            // Shuriken samples the system-time emission curve at the end of each
            // simulation step, then applies that rate across the step. Mirroring the
            // discrete sample keeps threshold-crossing frames aligned with Shuriken.
            return EvaluateEmissionRate(normalizedEnd, loopIndex) * duration;
        }

        float EvaluateEmissionRate(float normalizedTime, int loopIndex)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            uint seed;
            unchecked
            {
                seed = emissionRandomSeed ^ ((uint)loopIndex * 0x9E3779B9u) ^ 0xD1B54A35u;
            }
            float randomValue = Hash01(seed);

            switch (emissionRateOverTimeMode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(0f, Mathf.LerpUnclamped(
                        emissionRateOverTimeMin, emissionRateOverTime, randomValue));

                case ParticleSystemCurveMode.Curve:
                    return emissionRateOverTimeCurveMax != null
                        ? Mathf.Max(0f,
                            emissionRateOverTimeCurveMax.Evaluate(normalizedTime) *
                            emissionRateOverTimeCurveMultiplier)
                        : Mathf.Max(0f, emissionRateOverTime);

                case ParticleSystemCurveMode.TwoCurves:
                    float minimum = emissionRateOverTimeCurveMin != null
                        ? emissionRateOverTimeCurveMin.Evaluate(normalizedTime) *
                          emissionRateOverTimeCurveMultiplier
                        : emissionRateOverTimeMin;
                    float maximum = emissionRateOverTimeCurveMax != null
                        ? emissionRateOverTimeCurveMax.Evaluate(normalizedTime) *
                          emissionRateOverTimeCurveMultiplier
                        : emissionRateOverTime;
                    return Mathf.Max(0f, Mathf.LerpUnclamped(minimum, maximum, randomValue));

                default:
                    return Mathf.Max(0f, emissionRateOverTime);
            }
        }

        void CalculateDistanceEmission(
            float stepStart,
            float stepEnd,
            float startDelay,
            float stepDistance,
            out float emissionAmount)
        {
            emissionAmount = 0f;

            float stepDuration = stepEnd - stepStart;
            if (stepDuration <= 1e-6f || stepDistance <= 1e-6f) return;

            float duration = Mathf.Max(0.05f, emissionDuration);
            float activeStart = Mathf.Max(stepStart, startDelay);
            float activeEnd = stepEnd;
            if (!emissionLooping)
            {
                float timelineEnd = startDelay + duration;
                activeEnd = stepStart < timelineEnd && stepEnd > timelineEnd + 1e-6f
                    ? stepStart
                    : Mathf.Min(activeEnd, timelineEnd);
            }

            if (activeEnd <= activeStart) return;

            float windowDuration = activeEnd - activeStart;
            float activeDistance = stepDistance * (windowDuration / stepDuration);
            if (activeDistance <= 1e-6f) return;

            float localStart = activeStart - startDelay;
            float localEnd = activeEnd - startDelay;
            if (!emissionLooping)
            {
                emissionAmount = EvaluateEmissionRateOverDistance(
                    localEnd / duration, 0) * activeDistance;
            }
            else
            {
                float cursor = localStart;
                while (cursor < localEnd - 1e-6f)
                {
                    int loopIndex = Mathf.Max(0, Mathf.FloorToInt(cursor / duration));
                    float loopStart = loopIndex * duration;
                    float segmentEnd = Mathf.Min(localEnd, loopStart + duration);
                    float segmentDuration = segmentEnd - cursor;
                    float segmentDistance = stepDistance * (segmentDuration / stepDuration);
                    emissionAmount += EvaluateEmissionRateOverDistance(
                        (segmentEnd - loopStart) / duration, loopIndex) * segmentDistance;
                    cursor = segmentEnd;
                }
            }

            emissionAmount = Mathf.Max(0f, emissionAmount);
        }

        float EvaluateEmissionRateOverDistance(float normalizedTime, int loopIndex)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            uint seed;
            unchecked
            {
                seed = emissionRandomSeed ^ ((uint)loopIndex * 0x9E3779B9u) ^ 0x94D049BBu;
            }
            float randomValue = Hash01(seed);

            switch (emissionRateOverDistanceMode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(0f, Mathf.LerpUnclamped(
                        emissionRateOverDistanceMin, emissionRateOverDistance, randomValue));

                case ParticleSystemCurveMode.Curve:
                    return emissionRateOverDistanceCurveMax != null
                        ? Mathf.Max(0f,
                            emissionRateOverDistanceCurveMax.Evaluate(normalizedTime) *
                            emissionRateOverDistanceCurveMultiplier)
                        : Mathf.Max(0f, emissionRateOverDistance);

                case ParticleSystemCurveMode.TwoCurves:
                    float minimum = emissionRateOverDistanceCurveMin != null
                        ? emissionRateOverDistanceCurveMin.Evaluate(normalizedTime) *
                          emissionRateOverDistanceCurveMultiplier
                        : emissionRateOverDistanceMin;
                    float maximum = emissionRateOverDistanceCurveMax != null
                        ? emissionRateOverDistanceCurveMax.Evaluate(normalizedTime) *
                          emissionRateOverDistanceCurveMultiplier
                        : emissionRateOverDistance;
                    return Mathf.Max(0f, Mathf.LerpUnclamped(minimum, maximum, randomValue));

                default:
                    return Mathf.Max(0f, emissionRateOverDistance);
            }
        }

        void ScheduleBurstEmission(
            float stepStart,
            float stepEnd,
            float startDelay)
        {
            stepBurstGroupCount = 0;
            System.Array.Clear(stepBurstCounts, 0, stepBurstCounts.Length);
            System.Array.Clear(stepBurstAges, 0, stepBurstAges.Length);

            if (emissionBursts == null || emissionBursts.Length == 0 || stepEnd < startDelay)
            {
                return;
            }

            float duration = Mathf.Max(0.05f, emissionDuration);
            float activeEnd = stepEnd - startDelay;
            if (!emissionLooping) activeEnd = Mathf.Min(activeEnd, duration);
            if (activeEnd < 0f) return;

            int firstLoop = emissionLooping
                ? Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, stepStart - startDelay) / duration))
                : 0;
            int lastLoop = emissionLooping
                ? Mathf.Max(0, Mathf.FloorToInt(activeEnd / duration))
                : 0;

            for (int loopIndex = firstLoop; loopIndex <= lastLoop; loopIndex++)
            {
                float loopOffset = loopIndex * duration;
                for (int burstIndex = 0; burstIndex < emissionBursts.Length; burstIndex++)
                {
                    GPUEmissionBurst burst = emissionBursts[burstIndex];
                    if (burst == null || burst.time > duration + 1e-5f) continue;

                    float interval = Mathf.Max(0f, burst.repeatInterval);
                    int availableCycles = interval > 1e-6f
                        ? Mathf.FloorToInt((duration - burst.time + 1e-5f) / interval) + 1
                        : 1;
                    int cycles = burst.cycleCount == 0
                        ? availableCycles
                        : Mathf.Min(Mathf.Max(1, burst.cycleCount), availableCycles);
                    cycles = Mathf.Min(cycles, MaxBurstCyclesPerLoop);

                    for (int cycleIndex = 0; cycleIndex < cycles; cycleIndex++)
                    {
                        float eventLocalTime = burst.time + cycleIndex * interval;
                        float eventTime = startDelay + loopOffset + eventLocalTime;
                        bool occursThisStep = eventTime > stepStart + 1e-6f &&
                                              eventTime <= stepEnd + 1e-6f;
                        if (simulationTick == 0 && Mathf.Abs(eventTime - stepStart) <= 1e-6f)
                        {
                            occursThisStep = true;
                        }
                        if (!occursThisStep) continue;

                        uint eventSeed;
                        unchecked
                        {
                            eventSeed = emissionRandomSeed ^
                                        ((uint)(burstIndex + 1) * 0x85EBCA6Bu) ^
                                        ((uint)loopIndex * 0xC2B2AE35u) ^
                                        ((uint)cycleIndex * 0x27D4EB2Fu);
                        }
                        float probability = Mathf.Clamp01(burst.probability);
                        if (probability <= 0f ||
                            (probability < 1f &&
                             Hash01(eventSeed ^ 0x165667B1u) >= probability))
                        {
                            continue;
                        }

                        float normalizedTime = Mathf.Clamp01(eventLocalTime / duration);
                        int count = Mathf.Max(0, Mathf.RoundToInt(burst.EvaluateCount(
                            normalizedTime, Hash01(eventSeed ^ 0xD3A2646Cu))));
                        if (count == 0) continue;

                        count = Mathf.Min(count, maxParticles);
                        // Shuriken gives repeated Burst cycles, and the first Burst at a
                        // loop boundary, one full simulation step of age. First-cycle
                        // Bursts inside the loop retain their sub-frame event age.
                        float age = cycleIndex > 0 ||
                                    (loopIndex > 0 && eventLocalTime <= 1e-6f)
                            ? stepEnd - stepStart
                            : stepEnd - eventTime;
                        age = Mathf.Clamp(age, 0f, stepEnd - stepStart);
                        AddBurstStepGroup(count, age);
                    }
                }
            }
        }

        void AddBurstStepGroup(int count, float age)
        {
            if (count <= 0) return;

            if (stepBurstGroupCount > 0 &&
                Mathf.Abs(stepBurstAges[stepBurstGroupCount - 1] - age) <= 1e-6f)
            {
                int index = stepBurstGroupCount - 1;
                stepBurstCounts[index] = Mathf.Min(maxParticles, stepBurstCounts[index] + count);
                return;
            }

            if (stepBurstGroupCount < MaxBurstGroupsPerStep)
            {
                stepBurstCounts[stepBurstGroupCount] = count;
                stepBurstAges[stepBurstGroupCount] = age;
                stepBurstGroupCount++;
                return;
            }

            int last = MaxBurstGroupsPerStep - 1;
            int previousCount = stepBurstCounts[last];
            int combinedCount = Mathf.Min(maxParticles, previousCount + count);
            if (combinedCount > 0)
            {
                stepBurstAges[last] =
                    ((stepBurstAges[last] * previousCount) + (age * count)) /
                    (previousCount + count);
            }
            stepBurstCounts[last] = combinedCount;
        }

        int TrimBurstStepGroups(int maximumCount)
        {
            int emittedCount = 0;
            for (int i = 0; i < MaxBurstGroupsPerStep; i++)
            {
                int available = Mathf.Max(0, maximumCount - emittedCount);
                int count = i < stepBurstGroupCount
                    ? Mathf.Min(stepBurstCounts[i], available)
                    : 0;
                stepBurstCounts[i] = count;
                if (count == 0) stepBurstAges[i] = 0f;
                emittedCount += count;
            }
            return emittedCount;
        }

        internal bool IsVisibleFrom(Camera camera)
        {
            int frame = Time.frameCount;
            if (visibilityFrame != frame)
            {
                visibilityFrame = frame;
                visibleThisFrame = false;
            }

            bool cameraVisible = CameraIntersectsCullingBounds(camera);
            visibleThisFrame |= cameraVisible;
            return cameraVisible;
        }

        bool CameraIntersectsCullingBounds(Camera camera)
        {
            if (camera == null) return true;
            if (!renderEnabled) return false;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0)
            {
                return false;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, cullingPlanes);
            return GeometryUtility.TestPlanesAABB(
                cullingPlanes,
                WorldCullingBounds());
        }

        Bounds WorldCullingBounds()
        {
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            Vector3 localExtents = localCullingBounds.extents;
            Vector3 axisX = localToWorld.MultiplyVector(
                new Vector3(localExtents.x, 0f, 0f));
            Vector3 axisY = localToWorld.MultiplyVector(
                new Vector3(0f, localExtents.y, 0f));
            Vector3 axisZ = localToWorld.MultiplyVector(
                new Vector3(0f, 0f, localExtents.z));
            Vector3 worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) +
                    Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) +
                    Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) +
                    Mathf.Abs(axisZ.z));
            worldExtents = Vector3.Max(
                worldExtents,
                Vector3.one * 0.0001f);
            return new Bounds(
                localToWorld.MultiplyPoint3x4(localCullingBounds.center),
                worldExtents * 2f);
        }

        internal void Simulate(CommandBuffer cmd, Camera camera)
        {
            if (simulateMaterial == null) return;

            bool advance = !Application.isPlaying ||
                           playbackState == PlaybackState.Playing ||
                           playbackState == PlaybackState.Stopping;
            if (!advance) return;

            bool cameraVisible = !Application.isPlaying ||
                                 IsVisibleFrom(camera);
            float currentCullingClock = CurrentCullingClock();

            if (Application.isPlaying &&
                trackedCullingMode != cullingMode)
            {
                trackedCullingMode = cullingMode;
                lastCullingClock = currentCullingClock;
                cullingClockValid = true;
                wasCulled = false;
                SynchronizePausedEmitterState();
            }

            if (Application.isPlaying && !cameraVisible &&
                PausesWhenInvisible())
            {
                ObserveCulledFrame(currentCullingClock);
                return;
            }

            // A renderer feature executes once per camera. Shuriken advances once per
            // player-loop frame, so advancing here for Scene/Game/overlay cameras would
            // make the GPU system run faster whenever more than one camera renders.
            if (Application.isPlaying)
            {
                int frame = Time.frameCount;
                if (lastSimulatedFrame == frame) return;
                lastSimulatedFrame = frame;
            }

            ApplyPendingPrewarm(cmd);

            float frameDt = FrameDeltaTime() * simulationSpeed;
            float simulatedDt = frameDt;
            bool allowEmission = !Application.isPlaying ||
                                 playbackState == PlaybackState.Playing;
            bool catchUp = Application.isPlaying && cameraVisible &&
                           cullingMode ==
                           ParticleSystemCullingMode.PauseAndCatchup &&
                           cullingClockValid && wasCulled;

            if (catchUp)
            {
                float elapsed = Mathf.Max(
                    0f,
                    currentCullingClock - lastCullingClock);
                simulatedDt = Mathf.Max(frameDt, elapsed * simulationSpeed);
                SimulateCatchup(cmd, simulatedDt, allowEmission);
            }
            else
            {
                if (wasCulled && PausesWithoutCatchup())
                {
                    SynchronizePausedEmitterState();
                }
                SimulateStep(cmd, frameDt, allowEmission);
            }

            lastCullingClock = currentCullingClock;
            cullingClockValid = Application.isPlaying;
            wasCulled = false;

            if (!Application.isPlaying) return;

            if (playbackState == PlaybackState.Playing &&
                HasNaturallyCompleted())
            {
                CompletePlayback();
            }
            else if (playbackState == PlaybackState.Stopping)
            {
                stoppingElapsed += simulatedDt;
                if (stoppingElapsed + 1e-5f >= stoppingDuration)
                {
                    CompletePlayback();
                }
            }
        }

        bool PausesWhenInvisible()
        {
            return cullingMode == ParticleSystemCullingMode.Pause ||
                   cullingMode ==
                       ParticleSystemCullingMode.PauseAndCatchup ||
                   (cullingMode == ParticleSystemCullingMode.Automatic &&
                    emissionLooping);
        }

        bool PausesWithoutCatchup()
        {
            return cullingMode == ParticleSystemCullingMode.Pause ||
                   (cullingMode == ParticleSystemCullingMode.Automatic &&
                    emissionLooping);
        }

        void ObserveCulledFrame(float currentClock)
        {
            wasCulled = true;
            if (!PausesWithoutCatchup()) return;

            // Pause ignores Transform movement that occurred while the renderer
            // was culled. Keep only the current emitter state for the first
            // visible simulation step.
            SynchronizePausedEmitterState();
            lastCullingClock = currentClock;
            cullingClockValid = true;
        }

        void SynchronizePausedEmitterState()
        {
            previousEmitterPositionWS = transform.position;
            previousEmitterPositionValid = true;
            previousEmitterVelocityWS = ResolveEmitterVelocityWS(Vector3.zero);
        }

        void SimulateCatchup(
            CommandBuffer cmd,
            float duration,
            bool allowEmission)
        {
            duration = Mathf.Max(0f, duration);
            if (duration <= 1e-6f)
            {
                SimulateStep(cmd, 0f, allowEmission);
                return;
            }

            int stepCount = Mathf.Max(
                1,
                Mathf.CeilToInt(duration / CullingCatchupStep));
            float stepDt = duration / stepCount;
            Vector3 startPosition = previousEmitterPositionValid
                ? previousEmitterPositionWS
                : transform.position;
            Vector3 endPosition = transform.position;

            for (int step = 0; step < stepCount; step++)
            {
                float positionT = (step + 1f) / stepCount;
                SimulateStep(
                    cmd,
                    stepDt,
                    allowEmission,
                    Vector3.LerpUnclamped(
                        startPosition,
                        endPosition,
                        positionT));
            }
        }

        float CurrentCullingClock()
        {
            if (!Application.isPlaying) return 0f;
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }

        bool HasNaturallyCompleted()
        {
            if (emissionLooping) return false;

            // Ring-buffer particles intentionally remain alive after their
            // lifetime. A one-shot system can only drain naturally when it did
            // not create any particles; otherwise replacement or an explicit
            // StopEmittingAndClear call owns their removal.
            if (ringBufferMode != ParticleSystemRingBufferMode.Disabled &&
                particleBirthObserved)
            {
                return false;
            }

            float emissionEnd = ResolveEmissionStartDelay() +
                                Mathf.Max(0.05f, emissionDuration);
            float drainEnd = particleBirthObserved
                ? latestParticleBirthTime + MaximumParticleLifetime()
                : emissionEnd;
            float completionTime = Mathf.Max(emissionEnd, drainEnd);
            return emissionTime + 1e-5f >= completionTime;
        }

        float FrameDeltaTime()
        {
            if (!Application.isPlaying) return 1f / 60f;
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        Matrix4x4 ParticleLocalToWorldMatrix()
        {
            return ParticleLocalToWorldMatrix(transform.position);
        }

        Matrix4x4 ParticleLocalToWorldMatrix(Vector3 emitterPositionWS)
        {
            switch (scalingMode)
            {
                case ParticleSystemScalingMode.Local:
                    Transform source = scalingSource != null
                        ? scalingSource
                        : transform;
                    return Matrix4x4.TRS(
                        emitterPositionWS,
                        transform.rotation,
                        source.localScale);

                case ParticleSystemScalingMode.Shape:
                    return Matrix4x4.TRS(
                        emitterPositionWS,
                        transform.rotation,
                        Vector3.one);

                default:
                    Matrix4x4 hierarchyMatrix =
                        transform.localToWorldMatrix;
                    hierarchyMatrix.SetColumn(
                        3,
                        new Vector4(
                            emitterPositionWS.x,
                            emitterPositionWS.y,
                            emitterPositionWS.z,
                            1f));
                    return hierarchyMatrix;
            }
        }

        Matrix4x4 SimulationLocalToWorldMatrix(
            Matrix4x4 particleLocalToWorld)
        {
            if (simulationSpace == SimulationSpace.World)
            {
                return Matrix4x4.identity;
            }

            if (simulationSpace == SimulationSpace.Custom &&
                customSimulationSpace != null)
            {
                return customSimulationSpace.localToWorldMatrix;
            }

            return particleLocalToWorld;
        }

        Quaternion SimulationRotation()
        {
            if (simulationSpace == SimulationSpace.World)
            {
                return Quaternion.identity;
            }

            if (simulationSpace == SimulationSpace.Custom &&
                customSimulationSpace != null)
            {
                return customSimulationSpace.rotation;
            }

            return transform.rotation;
        }

        Vector3 WorldDirectionToSimulation(Vector3 directionWorld)
        {
            if (simulationSpace == SimulationSpace.World)
            {
                return directionWorld;
            }

            if (simulationSpace == SimulationSpace.Custom &&
                customSimulationSpace != null)
            {
                return customSimulationSpace.InverseTransformDirection(
                    directionWorld);
            }

            return transform.InverseTransformDirection(directionWorld);
        }

        Vector3 WorldAccelerationToSimulation(
            Vector3 accelerationWorld,
            Matrix4x4 simulationWorldToLocal)
        {
            if (simulationSpace == SimulationSpace.Custom &&
                customSimulationSpace != null)
            {
                // Particle positions and velocities are stored in custom-space
                // coordinate units. Account for its scale so transforming the
                // integrated result back to world preserves world acceleration.
                return simulationWorldToLocal.MultiplyVector(
                    accelerationWorld);
            }

            return WorldDirectionToSimulation(accelerationWorld);
        }

        Matrix4x4 ShapeLocalToWorldMatrix(Matrix4x4 particleLocalToWorld)
        {
            return ShapeLocalToWorldMatrix(
                particleLocalToWorld,
                transform.position);
        }

        Matrix4x4 ShapeLocalToWorldMatrix(
            Matrix4x4 particleLocalToWorld,
            Vector3 emitterPositionWS)
        {
            if (scalingMode == ParticleSystemScalingMode.Local)
            {
                return particleLocalToWorld;
            }

            Matrix4x4 shapeMatrix = transform.localToWorldMatrix;
            shapeMatrix.SetColumn(
                3,
                new Vector4(
                    emitterPositionWS.x,
                    emitterPositionWS.y,
                    emitterPositionWS.z,
                    1f));
            return shapeMatrix;
        }

        Matrix4x4 ParticleScaleWorldMatrix(Matrix4x4 particleLocalToWorld)
        {
            // Billboard axes are built in world space. Rotate them back into the
            // particle system's frame before applying its selected scaling matrix,
            // so an unscaled rotated emitter does not rotate a view-facing quad.
            return particleLocalToWorld *
                   Matrix4x4.Rotate(Quaternion.Inverse(transform.rotation));
        }

        Matrix4x4 ParticleRenderWorldMatrix(
            Matrix4x4 particleLocalToWorld)
        {
            if (renderMode != GPURenderMode.Mesh ||
                renderAlignment != GPUAlignment.Local)
            {
                return ParticleScaleWorldMatrix(particleLocalToWorld);
            }

            // Local-aligned mesh vertices inherit the particle system transform
            // independently of the simulation space used for particle positions.
            return particleLocalToWorld;
        }

        float CollisionParticleScaleWS(Matrix4x4 particleLocalToWorld)
        {
            float x = particleLocalToWorld.MultiplyVector(Vector3.right).magnitude;
            float y = particleLocalToWorld.MultiplyVector(Vector3.up).magnitude;
            float z = particleLocalToWorld.MultiplyVector(Vector3.forward).magnitude;
            return Mathf.Max(0f, Mathf.Max(x, Mathf.Max(y, z)));
        }

        int PopulateCollisionPlaneEquations()
        {
            int count = 0;
            if (collisionPlanes != null)
            {
                for (int i = 0;
                     i < collisionPlanes.Length && count < collisionPlaneEquations.Length;
                     i++)
                {
                    Transform plane = collisionPlanes[i];
                    if (plane == null) continue;

                    Vector3 normal = plane.up.normalized;
                    Vector3 position = plane.position;
                    collisionPlaneEquations[count++] = new Vector4(
                        normal.x,
                        normal.y,
                        normal.z,
                        -Vector3.Dot(normal, position));
                }
            }

            for (int i = count; i < collisionPlaneEquations.Length; i++)
            {
                collisionPlaneEquations[i] = Vector4.zero;
            }
            return count;
        }

        Vector3 ResolveEmitterVelocityWS(Vector3 transformVelocityWS)
        {
            switch (emitterVelocityMode)
            {
                case ParticleSystemEmitterVelocityMode.Custom:
                    return customEmitterVelocity;

                case ParticleSystemEmitterVelocityMode.Rigidbody:
                    if (emitterVelocitySource != null)
                    {
                        return emitterVelocitySource.main.emitterVelocity;
                    }

                    if (TryGetComponent(out Rigidbody rigidbody))
                    {
                        return rigidbody.velocity;
                    }

                    if (TryGetComponent(out Rigidbody2D rigidbody2D))
                    {
                        Vector2 velocity = rigidbody2D.velocity;
                        return new Vector3(velocity.x, velocity.y, 0f);
                    }

                    return Vector3.zero;

                default:
                    return transformVelocityWS;
            }
        }

        internal void SimulateStep(CommandBuffer cmd, float dt)
        {
            SimulateStep(
                cmd,
                dt,
                !Application.isPlaying ||
                playbackState == PlaybackState.Playing);
        }

        void SimulateStep(CommandBuffer cmd, float dt, bool allowEmission)
        {
            SimulateStep(cmd, dt, allowEmission, transform.position);
        }

        void SimulateStep(
            CommandBuffer cmd,
            float dt,
            bool allowEmission,
            Vector3 emitterCurrentPositionWS)
        {
            dt = Mathf.Max(0f, dt);
            lastSimulationDeltaTime = dt;
            Matrix4x4 particleLocalToWorld = ParticleLocalToWorldMatrix(
                emitterCurrentPositionWS);
            Matrix4x4 particleWorldToLocal = particleLocalToWorld.inverse;
            Matrix4x4 simulationLocalToWorld =
                SimulationLocalToWorldMatrix(particleLocalToWorld);
            Matrix4x4 simulationWorldToLocal =
                simulationLocalToWorld.inverse;
            Quaternion simulationRotation = SimulationRotation();
            Matrix4x4 emitterToSimulationDirection = Matrix4x4.Rotate(
                Quaternion.Inverse(simulationRotation) * transform.rotation);
            Matrix4x4 simulationToEmitterDirection = Matrix4x4.Rotate(
                Quaternion.Inverse(transform.rotation) * simulationRotation);
            Matrix4x4 worldToSimulationDirection = Matrix4x4.Rotate(
                Quaternion.Inverse(simulationRotation));
            Matrix4x4 simulationToWorldDirection = Matrix4x4.Rotate(
                simulationRotation);
            Matrix4x4 shapeLocalToWorld =
                ShapeLocalToWorldMatrix(
                    particleLocalToWorld,
                    emitterCurrentPositionWS);
            Vector3 emitterPreviousPositionWS = previousEmitterPositionValid
                ? previousEmitterPositionWS
                : emitterCurrentPositionWS;
            Vector3 transformEmitterVelocityWS = dt > 1e-6f
                ? (emitterCurrentPositionWS - emitterPreviousPositionWS) / dt
                : Vector3.zero;
            Vector3 emitterVelocityWS = ResolveEmitterVelocityWS(
                transformEmitterVelocityWS);
            float emitterDistance = emitterVelocityWS.magnitude * dt;
            Vector3 emitterVelocityBeforeStepWS = previousEmitterVelocityWS;
            float stepStart = emissionTime;
            float stepEnd = stepStart + dt;
            float startDelay = ResolveEmissionStartDelay();
            float emitCarryPrev = emitCarry;
            float distanceEmitCarryPrev = distanceEmitCarry;
            float emissionAmount = 0f;
            float emissionRate = 0f;
            float emissionWindowStart = 0f;
            int continuousEmitCount = 0;
            float distanceEmissionAmount = 0f;
            int distanceEmitCount = 0;

            if (allowEmission && emissionEnabled)
            {
                CalculateContinuousEmission(
                    stepStart,
                    stepEnd,
                    startDelay,
                    out emissionAmount,
                    out emissionRate,
                    out emissionWindowStart);
                float continuousTotal = Mathf.Max(0f, emissionAmount + emitCarryPrev);
                continuousEmitCount = continuousTotal >= int.MaxValue
                    ? int.MaxValue
                    : Mathf.FloorToInt(continuousTotal);
                emitCarry = continuousTotal - Mathf.Floor(continuousTotal);

                CalculateDistanceEmission(
                    stepStart,
                    stepEnd,
                    startDelay,
                    emitterDistance,
                    out distanceEmissionAmount);
                float distanceTotal = Mathf.Max(
                    0f, distanceEmissionAmount + distanceEmitCarryPrev);
                // Shuriken waits until accumulated movement strictly exceeds a
                // distance threshold. Retaining an exact threshold as carry delays
                // that particle until the next non-zero Transform movement step.
                distanceEmitCount = distanceTotal >= int.MaxValue
                    ? int.MaxValue
                    : Mathf.Max(0, Mathf.FloorToInt(distanceTotal - 1e-5f));
                distanceEmitCarry = distanceTotal - distanceEmitCount;

                if (!emissionLooping &&
                    stepEnd >= startDelay + Mathf.Max(0.05f, emissionDuration) - 1e-6f)
                {
                    emitCarry = 0f;
                    distanceEmitCarry = 0f;
                }

                ScheduleBurstEmission(stepStart, stepEnd, startDelay);
            }
            else
            {
                emitCarry = 0f;
                distanceEmitCarry = 0f;
                stepBurstGroupCount = 0;
                System.Array.Clear(stepBurstCounts, 0, stepBurstCounts.Length);
                System.Array.Clear(stepBurstAges, 0, stepBurstAges.Length);
            }

            continuousEmitCount = Mathf.Min(continuousEmitCount, maxParticles);
            distanceEmitCount = Mathf.Min(
                distanceEmitCount, maxParticles - continuousEmitCount);
            int burstEmitCount = TrimBurstStepGroups(
                maxParticles - continuousEmitCount - distanceEmitCount);
            int emitCount = continuousEmitCount + distanceEmitCount + burstEmitCount;
            if (continuousEmitCount > 0)
            {
                float latestSpawnOffset = emissionRate > 1e-6f
                    ? emissionWindowStart +
                      (continuousEmitCount - emitCarryPrev) / emissionRate
                    : dt;
                ObserveParticleBirthTime(
                    stepStart + Mathf.Clamp(latestSpawnOffset, 0f, dt));
            }
            if (distanceEmitCount > 0)
            {
                ObserveParticleBirthTime(stepEnd);
            }
            if (burstEmitCount > 0)
            {
                float latestBurstBirthTime = float.NegativeInfinity;
                for (int i = 0; i < stepBurstGroupCount; i++)
                {
                    if (stepBurstCounts[i] <= 0) continue;
                    latestBurstBirthTime = Mathf.Max(
                        latestBurstBirthTime,
                        stepEnd - Mathf.Max(0f, stepBurstAges[i]));
                }
                if (!float.IsNegativeInfinity(latestBurstBirthTime))
                {
                    ObserveParticleBirthTime(latestBurstBirthTime);
                }
            }
            int emitStart = emitCursor;
            emitCursor = (emitCursor + emitCount) % maxParticles;
            emissionTime = stepEnd;
            previousEmitterPositionWS = emitterCurrentPositionWS;
            previousEmitterPositionValid = true;
            previousEmitterVelocityWS = emitterVelocityWS;

            int src = ping, dst = 1 - ping;

            simulateProperties.Clear();
            simulateProperties.SetTexture(_CurPosLife, posLife[src]);
            simulateProperties.SetTexture(_CurVelSize, velSize[src]);
            simulateProperties.SetTexture(_CurColor,   colorRT[src]);
            simulateProperties.SetTexture(_CurRotationPhase, rotationPhaseRT[src]);
            Texture2D selectedStartLifetimeLUT = startLifetimeLUT != null
                ? startLifetimeLUT
                : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedStartSpeedLUT = startSpeedLUT != null
                ? startSpeedLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedStartSizeLUT = startSizeLUT != null
                ? startSizeLUT
                : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedStartColorLUT = startColorLUT != null
                ? startColorLUT
                : GradientLUTBuilder.GetDefaultWhiteLUT();
            Texture2D selectedGravityModifierLUT = gravityModifierLUT != null
                ? gravityModifierLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedColorOverLifetimeLUT = colorOverLifetimeLUT != null
                ? colorOverLifetimeLUT
                : GradientLUTBuilder.GetDefaultWhiteLUT();
            Texture2D selectedSizeOverLifetimeLUT = sizeOverLifetimeLUT != null
                ? sizeOverLifetimeLUT
                : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedColorBySpeedLUT = colorBySpeedLUT != null
                ? colorBySpeedLUT
                : GradientLUTBuilder.GetDefaultWhiteLUT();
            Texture2D selectedSizeBySpeedLUT = sizeBySpeedLUT != null
                ? sizeBySpeedLUT
                : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedRotationBySpeedLUT = rotationBySpeedLUT != null
                ? rotationBySpeedLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedRotationBySpeedXLUT =
                rotationBySpeedSeparateAxes && rotationBySpeedXLUT != null
                    ? rotationBySpeedXLUT
                    : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedRotationBySpeedYLUT =
                rotationBySpeedSeparateAxes && rotationBySpeedYLUT != null
                    ? rotationBySpeedYLUT
                    : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedForceOverLifetimeLUT = forceOverLifetimeLUT != null
                ? forceOverLifetimeLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedVelocityOverLifetimeLUT = velocityOverLifetimeLUT != null
                ? velocityOverLifetimeLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultVelocityLUT();
            Texture2D selectedVelocityOverLifetimeOrbitalLUT =
                velocityOverLifetimeOrbitalLUT != null
                    ? velocityOverLifetimeOrbitalLUT
                    : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedVelocityOverLifetimeOrbitalOffsetLUT =
                velocityOverLifetimeOrbitalOffsetLUT != null
                    ? velocityOverLifetimeOrbitalOffsetLUT
                    : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedLimitVelocityLUT = limitVelocityOverLifetimeLUT != null
                ? limitVelocityOverLifetimeLUT
                : LimitVelocityLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedInheritVelocityLUT = inheritVelocityLUT != null
                ? inheritVelocityLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedLifetimeByEmitterSpeedLUT =
                lifetimeByEmitterSpeedLUT != null
                    ? lifetimeByEmitterSpeedLUT
                    : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedNoiseStrengthLUT = noiseStrengthLUT != null
                ? noiseStrengthLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultUnitVectorLUT();
            Texture2D selectedNoiseAmountsLUT = noiseAmountsLUT != null
                ? noiseAmountsLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultNoiseAmountsLUT();
            Texture2D selectedNoiseRemapLUT = noiseRemapLUT != null
                ? noiseRemapLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultSignedIdentityLUT();
            Texture2D selectedCollisionParametersLUT =
                collisionParametersLUT != null
                    ? collisionParametersLUT
                    : MinMaxCurveVector3LUTBuilder
                        .GetDefaultCollisionParametersLUT();

            simulateProperties.SetTexture("_GradLUT", selectedColorOverLifetimeLUT);
            simulateProperties.SetTexture(
                _StartLifetimeLUT, selectedStartLifetimeLUT);
            simulateProperties.SetTexture(_StartSpeedLUT, selectedStartSpeedLUT);
            simulateProperties.SetTexture(_StartSizeLUT, selectedStartSizeLUT);
            simulateProperties.SetTexture(_StartColorLUT, selectedStartColorLUT);
            simulateProperties.SetTexture(
                _GravityModifierLUT, selectedGravityModifierLUT);
            simulateProperties.SetTexture("_SizeLUT", selectedSizeOverLifetimeLUT);
            simulateProperties.SetTexture(_ColorBySpeedLUT, selectedColorBySpeedLUT);
            simulateProperties.SetTexture(_SizeBySpeedLUT, selectedSizeBySpeedLUT);
            simulateProperties.SetTexture(
                _RotationBySpeedLUT, selectedRotationBySpeedLUT);
            simulateProperties.SetTexture(
                _RotationBySpeedXLUT, selectedRotationBySpeedXLUT);
            simulateProperties.SetTexture(
                _RotationBySpeedYLUT, selectedRotationBySpeedYLUT);
            simulateProperties.SetTexture(
                _ForceOverLifetimeLUT, selectedForceOverLifetimeLUT);
            simulateProperties.SetTexture(
                _VelocityOverLifetimeLUT, selectedVelocityOverLifetimeLUT);
            simulateProperties.SetTexture(
                _VelocityOverLifetimeOrbitalLUT,
                selectedVelocityOverLifetimeOrbitalLUT);
            simulateProperties.SetTexture(
                _VelocityOverLifetimeOrbitalOffsetLUT,
                selectedVelocityOverLifetimeOrbitalOffsetLUT);
            simulateProperties.SetTexture(
                _LimitVelocityLUT, selectedLimitVelocityLUT);
            simulateProperties.SetTexture(
                _InheritVelocityLUT, selectedInheritVelocityLUT);
            simulateProperties.SetTexture(
                _LifetimeByEmitterSpeedLUT,
                selectedLifetimeByEmitterSpeedLUT);
            simulateProperties.SetTexture(
                _NoiseStrengthLUT, selectedNoiseStrengthLUT);
            simulateProperties.SetTexture(
                _NoiseAmountsLUT, selectedNoiseAmountsLUT);
            simulateProperties.SetTexture(
                _NoiseRemapLUT, selectedNoiseRemapLUT);
            simulateProperties.SetTexture(
                _CollisionParametersLUT,
                selectedCollisionParametersLUT);
            simulateProperties.SetFloat(
                _GradLUTInvWidth, InverseTextureWidth(selectedColorOverLifetimeLUT));
            simulateProperties.SetFloat(
                _StartLifetimeLUTInvWidth,
                InverseTextureWidth(selectedStartLifetimeLUT));
            simulateProperties.SetFloat(
                _StartSpeedLUTInvWidth,
                InverseTextureWidth(selectedStartSpeedLUT));
            simulateProperties.SetFloat(
                _StartSizeLUTInvWidth,
                InverseTextureWidth(selectedStartSizeLUT));
            simulateProperties.SetFloat(
                _StartColorLUTInvWidth,
                InverseTextureWidth(selectedStartColorLUT));
            simulateProperties.SetFloat(
                _GravityModifierLUTInvWidth,
                InverseTextureWidth(selectedGravityModifierLUT));
            simulateProperties.SetFloat(
                _SizeLUTInvWidth, InverseTextureWidth(selectedSizeOverLifetimeLUT));
            simulateProperties.SetFloat(
                _ColorBySpeedLUTInvWidth, InverseTextureWidth(selectedColorBySpeedLUT));
            simulateProperties.SetFloat(
                _SizeBySpeedLUTInvWidth, InverseTextureWidth(selectedSizeBySpeedLUT));
            simulateProperties.SetFloat(
                _RotationBySpeedLUTInvWidth,
                InverseTextureWidth(selectedRotationBySpeedLUT));
            simulateProperties.SetFloat(
                _RotationBySpeedXLUTInvWidth,
                InverseTextureWidth(selectedRotationBySpeedXLUT));
            simulateProperties.SetFloat(
                _RotationBySpeedYLUTInvWidth,
                InverseTextureWidth(selectedRotationBySpeedYLUT));
            simulateProperties.SetFloat(
                _ForceOverLifetimeLUTInvWidth,
                InverseTextureWidth(selectedForceOverLifetimeLUT));
            simulateProperties.SetFloat(
                _VelocityOverLifetimeLUTInvWidth,
                InverseTextureWidth(selectedVelocityOverLifetimeLUT));
            simulateProperties.SetFloat(
                _VelocityOverLifetimeOrbitalLUTInvWidth,
                InverseTextureWidth(selectedVelocityOverLifetimeOrbitalLUT));
            simulateProperties.SetFloat(
                _VelocityOverLifetimeOrbitalOffsetLUTInvWidth,
                InverseTextureWidth(selectedVelocityOverLifetimeOrbitalOffsetLUT));
            simulateProperties.SetFloat(
                _LimitVelocityLUTInvWidth,
                InverseTextureWidth(selectedLimitVelocityLUT));
            simulateProperties.SetFloat(
                _InheritVelocityLUTInvWidth,
                InverseTextureWidth(selectedInheritVelocityLUT));
            simulateProperties.SetFloat(
                _LifetimeByEmitterSpeedLUTInvWidth,
                InverseTextureWidth(selectedLifetimeByEmitterSpeedLUT));
            simulateProperties.SetFloat(
                _NoiseStrengthLUTInvWidth,
                InverseTextureWidth(selectedNoiseStrengthLUT));
            simulateProperties.SetFloat(
                _NoiseAmountsLUTInvWidth,
                InverseTextureWidth(selectedNoiseAmountsLUT));
            simulateProperties.SetFloat(
                _NoiseRemapLUTInvWidth,
                InverseTextureWidth(selectedNoiseRemapLUT));
            simulateProperties.SetFloat(
                _CollisionParametersLUTInvWidth,
                InverseTextureWidth(selectedCollisionParametersLUT));

            simulateProperties.SetInt(_GridSize, gridSize);
            simulateProperties.SetInt(_MaxParticles, maxParticles);
            simulateProperties.SetFloat(_DeltaTime, dt);
            simulateProperties.SetFloat(_StartLifetime, startLifetime);
            simulateProperties.SetFloat(_StartLifetimeMin, startLifetimeMin);
            simulateProperties.SetInt(_RandomizeStartLifetime, randomizeStartLifetime ? 1 : 0);
            simulateProperties.SetInt(
                _StartLifetimeMode,
                (int)EffectiveStartLifetimeMode());
            simulateProperties.SetInt(
                _RingBufferMode,
                (int)ringBufferMode);
            Vector2 selectedRingBufferLoopRange =
                Ordered01Range(ringBufferLoopRange);
            simulateProperties.SetVector(
                _RingBufferLoopRange,
                new Vector4(
                    selectedRingBufferLoopRange.x,
                    selectedRingBufferLoopRange.y,
                    0f,
                    0f));
            simulateProperties.SetFloat(_StartSpeed, startSpeed);
            simulateProperties.SetFloat(_StartSpeedMin, startSpeedMin);
            simulateProperties.SetInt(_RandomizeStartSpeed, randomizeStartSpeed ? 1 : 0);
            ParticleSystemCurveMode selectedStartSpeedMode =
                startSpeedMode == ParticleSystemCurveMode.Constant &&
                randomizeStartSpeed
                    ? ParticleSystemCurveMode.TwoConstants
                    : startSpeedMode;
            simulateProperties.SetInt(
                _StartSpeedMode, (int)selectedStartSpeedMode);
            simulateProperties.SetFloat(_StartSize, startSize);
            simulateProperties.SetFloat(_StartSizeMin, startSizeMin);
            simulateProperties.SetInt(_RandomizeStartSize, randomizeStartSize ? 1 : 0);
            ParticleSystemCurveMode selectedStartSizeMode =
                startSizeMode == ParticleSystemCurveMode.Constant &&
                randomizeStartSize
                    ? ParticleSystemCurveMode.TwoConstants
                    : startSizeMode;
            simulateProperties.SetInt(
                _StartSizeMode, (int)selectedStartSizeMode);
            simulateProperties.SetColor(_StartColor, startColor);
            simulateProperties.SetColor(_StartColorMin, startColorMin);
            simulateProperties.SetInt(_RandomizeStartColor, randomizeStartColor ? 1 : 0);
            ParticleSystemGradientMode selectedStartColorMode =
                startColorMode == ParticleSystemGradientMode.Color &&
                randomizeStartColor
                    ? ParticleSystemGradientMode.TwoColors
                    : startColorMode;
            simulateProperties.SetInt(
                _StartColorMode, (int)selectedStartColorMode);

            Vector3 gravityWorld = ResolveGravityWorld();
            Vector3 gWorld = gravityWorld * gravityModifier;
            Vector3 gWorldMin = gravityWorld * gravityModifierMin;
            Vector3 gravityBase = WorldAccelerationToSimulation(
                gravityWorld,
                simulationWorldToLocal);
            Vector3 gSim = WorldAccelerationToSimulation(
                gWorld,
                simulationWorldToLocal);
            Vector3 gSimMin = WorldAccelerationToSimulation(
                gWorldMin,
                simulationWorldToLocal);
            simulateProperties.SetVector(_GravityWS, new Vector4(gSim.x, gSim.y, gSim.z, 0));
            simulateProperties.SetVector(_GravityWSMin,
                new Vector4(gSimMin.x, gSimMin.y, gSimMin.z, 0));
            simulateProperties.SetInt(_RandomizeGravityModifier, randomizeGravityModifier ? 1 : 0);
            simulateProperties.SetVector(
                _GravityBase,
                new Vector4(gravityBase.x, gravityBase.y, gravityBase.z, 0f));
            ParticleSystemCurveMode selectedGravityModifierMode =
                gravityModifierMode == ParticleSystemCurveMode.Constant &&
                randomizeGravityModifier
                    ? ParticleSystemCurveMode.TwoConstants
                    : gravityModifierMode;
            simulateProperties.SetInt(
                _GravityModifierMode,
                (int)selectedGravityModifierMode);

            simulateProperties.SetInt(_SimulationSpace, (int)simulationSpace);
            simulateProperties.SetInt(_EmitStart, emitStart);
            simulateProperties.SetInt(_EmitCount, emitCount);
            simulateProperties.SetFloat(_EmitCarryPrev, emitCarryPrev);
            simulateProperties.SetFloat(_EmissionRate, emissionRate);
            simulateProperties.SetInt(_ContinuousEmitCount, continuousEmitCount);
            simulateProperties.SetFloat(_ContinuousEmissionWindowStart, emissionWindowStart);
            simulateProperties.SetInt(_DistanceEmitCount, distanceEmitCount);
            simulateProperties.SetFloat(_EmissionTimeAfterStep, stepEnd);
            simulateProperties.SetFloat(_EmissionStartDelay, startDelay);
            simulateProperties.SetFloat(
                _EmissionDuration, Mathf.Max(0.05f, emissionDuration));
            simulateProperties.SetInt(_EmissionLooping, emissionLooping ? 1 : 0);
            simulateProperties.SetVector(_BurstCounts0, new Vector4(
                stepBurstCounts[0], stepBurstCounts[1], stepBurstCounts[2], stepBurstCounts[3]));
            simulateProperties.SetVector(_BurstCounts1, new Vector4(
                stepBurstCounts[4], stepBurstCounts[5], stepBurstCounts[6], stepBurstCounts[7]));
            simulateProperties.SetVector(_BurstAges0, new Vector4(
                stepBurstAges[0], stepBurstAges[1], stepBurstAges[2], stepBurstAges[3]));
            simulateProperties.SetVector(_BurstAges1, new Vector4(
                stepBurstAges[4], stepBurstAges[5], stepBurstAges[6], stepBurstAges[7]));
            simulateProperties.SetInt(_SimulationTick, unchecked((int)simulationTick));
            simulateProperties.SetInt(_ForceOverLifetimeEnabled, forceOverLifetimeEnabled ? 1 : 0);
            simulateProperties.SetInt(_ForceOverLifetimeSpace, (int)forceOverLifetimeSpace);
            simulateProperties.SetInt(_ForceOverLifetimeRandomized, forceOverLifetimeRandomized ? 1 : 0);
            simulateProperties.SetInt(
                _VelocityOverLifetimeEnabled, velocityOverLifetimeEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _VelocityOverLifetimeSpace, (int)velocityOverLifetimeSpace);
            simulateProperties.SetInt(
                _VelocityOverLifetimeSpeedModifierEnabled,
                velocityOverLifetimeSpeedModifierEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _VelocityOverLifetimeOrbitalEnabled,
                velocityOverLifetimeOrbitalEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _LimitVelocityEnabled,
                limitVelocityOverLifetimeEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _LimitVelocitySeparateAxes,
                limitVelocityOverLifetimeSeparateAxes ? 1 : 0);
            simulateProperties.SetInt(
                _LimitVelocitySpace,
                (int)limitVelocityOverLifetimeSpace);
            simulateProperties.SetFloat(
                _LimitVelocityDampen,
                Mathf.Clamp01(limitVelocityOverLifetimeDampen));
            simulateProperties.SetInt(
                _LimitVelocityMultiplyDragBySize,
                limitVelocityMultiplyDragBySize ? 1 : 0);
            simulateProperties.SetInt(
                _LimitVelocityMultiplyDragByVelocity,
                limitVelocityMultiplyDragByVelocity ? 1 : 0);
            simulateProperties.SetInt(
                _InheritVelocityEnabled,
                inheritVelocityEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _InheritVelocityMode,
                (int)inheritVelocityMode);
            simulateProperties.SetInt(
                _LifetimeByEmitterSpeedEnabled,
                lifetimeByEmitterSpeedEnabled ? 1 : 0);
            simulateProperties.SetVector(
                _LifetimeByEmitterSpeedRange,
                new Vector4(
                    lifetimeByEmitterSpeedRange.x,
                    lifetimeByEmitterSpeedRange.y,
                    0f,
                    0f));
            simulateProperties.SetInt(_NoiseEnabled, noiseEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _NoiseSeparateAxes, noiseSeparateAxes ? 1 : 0);
            simulateProperties.SetInt(
                _NoiseRemapEnabled, noiseRemapEnabled ? 1 : 0);
            simulateProperties.SetFloat(
                _NoiseFrequency, Mathf.Max(0.0001f, noiseFrequency));
            simulateProperties.SetInt(_NoiseDamping, noiseDamping ? 1 : 0);
            simulateProperties.SetInt(_NoiseQuality, (int)noiseQuality);
            simulateProperties.SetInt(
                _NoiseOctaveCount, Mathf.Clamp(noiseOctaveCount, 1, 4));
            simulateProperties.SetFloat(
                _NoiseOctaveMultiplier, Mathf.Max(0f, noiseOctaveMultiplier));
            simulateProperties.SetFloat(
                _NoiseOctaveScale, Mathf.Max(1f, noiseOctaveScale));
            int collisionPlaneCount = PopulateCollisionPlaneEquations();
            bool planeCollisionEnabled = collisionEnabled &&
                collisionType == ParticleSystemCollisionType.Planes &&
                collisionPlaneCount > 0;
            simulateProperties.SetInt(
                _CollisionEnabled, planeCollisionEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _CollisionPlaneCount, collisionPlaneCount);
            simulateProperties.SetVectorArray(
                _CollisionPlanes, collisionPlaneEquations);
            simulateProperties.SetFloat(
                _CollisionMinKillSpeed,
                Mathf.Max(0f, collisionMinKillSpeed));
            simulateProperties.SetFloat(
                _CollisionMaxKillSpeed,
                Mathf.Max(collisionMinKillSpeed, collisionMaxKillSpeed));
            simulateProperties.SetFloat(
                _CollisionRadiusScale,
                Mathf.Max(0f, collisionRadiusScale));
            simulateProperties.SetFloat(
                _CollisionParticleScaleWS,
                CollisionParticleScaleWS(particleLocalToWorld));
            simulateProperties.SetInt(_ColorOverLifetimeMode, (int)colorOverLifetimeMode);
            simulateProperties.SetInt(_ColorBySpeedEnabled, colorBySpeedEnabled ? 1 : 0);
            simulateProperties.SetInt(_ColorBySpeedMode, (int)colorBySpeedMode);
            simulateProperties.SetVector(
                _ColorBySpeedRange,
                new Vector4(colorBySpeedRange.x, colorBySpeedRange.y, 0f, 0f));
            simulateProperties.SetInt(_SizeBySpeedEnabled, sizeBySpeedEnabled ? 1 : 0);
            simulateProperties.SetVector(
                _SizeBySpeedRange,
                new Vector4(sizeBySpeedRange.x, sizeBySpeedRange.y, 0f, 0f));
            simulateProperties.SetInt(
                _RotationBySpeedEnabled, rotationBySpeedEnabled ? 1 : 0);
            simulateProperties.SetInt(
                _RotationBySpeedSeparateAxes,
                rotationBySpeedSeparateAxes ? 1 : 0);
            simulateProperties.SetVector(
                _RotationBySpeedRange,
                new Vector4(
                    rotationBySpeedRange.x,
                    rotationBySpeedRange.y,
                    0f,
                    0f));

            Vector3 dirInitW = initialDirectionWS.sqrMagnitude > 1e-6f
                ? initialDirectionWS.normalized
                : transform.forward;
            Vector3 dirInitSim = WorldDirectionToSimulation(dirInitW);
            simulateProperties.SetVector(_InitialDir, new Vector4(dirInitSim.x, dirInitSim.y, dirInitSim.z, 0));

            simulateProperties.SetInt(_ShapeType, (int)shapeType);
            simulateProperties.SetInt(_ShapeEmitFrom, (int)shapeEmitFrom);
            simulateProperties.SetInt(_AlignToDirection, alignToDirection ? 1 : 0);
            simulateProperties.SetFloat(
                _ShapeRandomDirectionAmount,
                Mathf.Clamp01(shapeRandomDirectionAmount));
            simulateProperties.SetFloat(
                _ShapeSphericalDirectionAmount,
                Mathf.Clamp01(shapeSphericalDirectionAmount));
            Vector3 randomPositionScale = shapeLocalScale *
                Mathf.Max(0f, shapeRandomPositionAmount);
            simulateProperties.SetVector(
                _ShapeRandomPositionScale,
                new Vector4(
                    randomPositionScale.x,
                    randomPositionScale.y,
                    randomPositionScale.z,
                    0f));
            simulateProperties.SetFloat(_ShapeRadiusThickness, Mathf.Clamp01(shapeRadiusThickness));
            simulateProperties.SetFloat(_ShapeConeArcRad,
                Mathf.Clamp(shapeConeArcDeg, 0f, 360f) * Mathf.Deg2Rad);
            simulateProperties.SetInt(_ShapeArcMode, (int)shapeArcMode);
            simulateProperties.SetFloat(
                _ShapeArcSpread,
                Mathf.Clamp01(shapeArcSpread));
            simulateProperties.SetInt(
                _ShapeArcSpeedMode,
                (int)shapeArcSpeedMode);
            Texture2D selectedShapeArcSpeedLUT = shapeArcSpeedIntegralLUT != null
                ? shapeArcSpeedIntegralLUT
                : CurveLUTBuilder.GetDefaultLinear01LUT();
            simulateProperties.SetTexture(
                _ShapeArcSpeedIntegralLUT,
                selectedShapeArcSpeedLUT);
            simulateProperties.SetFloat(
                _ShapeArcSpeedIntegralLUTInvWidth,
                InverseTextureWidth(selectedShapeArcSpeedLUT));

            float avgScale = (shapeLocalScale.x + shapeLocalScale.y + shapeLocalScale.z) / 3f;

            // 根据shapeType设置对应的参数
            switch (shapeType)
            {
                case ShapeTypeGPU.Sphere:
                case ShapeTypeGPU.Hemisphere:
                    simulateProperties.SetFloat(_ShapeSphereRadius, Mathf.Max(0f, shapeSphereRadius * avgScale));
                    break;

                case ShapeTypeGPU.Cone:
                    simulateProperties.SetFloat(_ShapeConeAngleRad, shapeConeAngle * Mathf.Deg2Rad);
                    float coneRadiusScaled = shapeConeRadius * 0.5f * (shapeLocalScale.x + shapeLocalScale.y);
                    float coneLengthScaled = shapeConeLength * shapeLocalScale.z;
                    simulateProperties.SetFloat(_ShapeConeRadius, coneRadiusScaled);
                    simulateProperties.SetFloat(_ShapeConeLength, coneLengthScaled);
                    break;

                case ShapeTypeGPU.Donut:
                    simulateProperties.SetFloat(_ShapeDonutRadius, Mathf.Max(0f, shapeDonutRadius * avgScale));
                    simulateProperties.SetFloat(_ShapeDonutThickness, Mathf.Max(0f, shapeDonutThickness * avgScale));
                    break;

                case ShapeTypeGPU.Box:
                    Vector3 boxSizeScaled = Vector3.Scale(shapeBoxSize, shapeLocalScale);
                    simulateProperties.SetVector(_ShapeBoxSize, new Vector4(boxSizeScaled.x, boxSizeScaled.y, boxSizeScaled.z, 0));
                    break;

                case ShapeTypeGPU.Circle:
                    simulateProperties.SetFloat(_ShapeCircleRadius, Mathf.Max(0f, shapeCircleRadius * avgScale));
                    break;

                case ShapeTypeGPU.Edge:
                    simulateProperties.SetFloat(
                        _ShapeEdgeLength,
                        Mathf.Max(0f, shapeEdgeLength * shapeLocalScale.x));
                    break;

                case ShapeTypeGPU.Rectangle:
                    Vector2 rectSizeScaled = new Vector2(
                        shapeRectangleSize.x * shapeLocalScale.x,
                        shapeRectangleSize.y * shapeLocalScale.y
                    );
                    simulateProperties.SetVector(_ShapeRectangleSize, new Vector4(rectSizeScaled.x, rectSizeScaled.y, 0, 0));
                    break;
            }

            Quaternion q = Quaternion.Euler(shapeLocalRotationEuler);
            Vector3 rightL = q * Vector3.right;
            Vector3 upL = q * Vector3.up;
            Vector3 fwdL = q * Vector3.forward;
            Vector3 posL = shapeLocalPosition;
            simulateProperties.SetVector(_ShapePosL, new Vector4(posL.x, posL.y, posL.z, 0));
            simulateProperties.SetVector(_ShapeRightL, new Vector4(rightL.x, rightL.y, rightL.z, 0));
            simulateProperties.SetVector(_ShapeUpL, new Vector4(upL.x, upL.y, upL.z, 0));
            simulateProperties.SetVector(_ShapeFwdL, new Vector4(fwdL.x, fwdL.y, fwdL.z, 0));

            simulateProperties.SetMatrix(_EmitterLocalToWorld, particleLocalToWorld);
            simulateProperties.SetMatrix(_EmitterWorldToLocal, particleWorldToLocal);
            simulateProperties.SetMatrix(
                _SimulationLocalToWorld, simulationLocalToWorld);
            simulateProperties.SetMatrix(
                _SimulationWorldToLocal, simulationWorldToLocal);
            simulateProperties.SetMatrix(
                _EmitterToSimulationDirection,
                emitterToSimulationDirection);
            simulateProperties.SetMatrix(
                _SimulationToEmitterDirection,
                simulationToEmitterDirection);
            simulateProperties.SetMatrix(
                _WorldToSimulationDirection,
                worldToSimulationDirection);
            simulateProperties.SetMatrix(
                _SimulationToWorldDirection,
                simulationToWorldDirection);
            simulateProperties.SetMatrix(_ShapeLocalToWorld, shapeLocalToWorld);
            simulateProperties.SetVector(_EmitterPreviousPositionWS,
                new Vector4(
                    emitterPreviousPositionWS.x,
                    emitterPreviousPositionWS.y,
                    emitterPreviousPositionWS.z,
                    0f));
            simulateProperties.SetVector(_EmitterCurrentPositionWS,
                new Vector4(
                    emitterCurrentPositionWS.x,
                    emitterCurrentPositionWS.y,
                    emitterCurrentPositionWS.z,
                    0f));
            simulateProperties.SetVector(_EmitterPreviousVelocityWS,
                new Vector4(
                    emitterVelocityBeforeStepWS.x,
                    emitterVelocityBeforeStepWS.y,
                    emitterVelocityBeforeStepWS.z,
                    0f));
            simulateProperties.SetVector(_EmitterVelocityWS,
                new Vector4(
                    emitterVelocityWS.x,
                    emitterVelocityWS.y,
                    emitterVelocityWS.z,
                    0f));

            var mrt = new RenderTargetIdentifier[] {
                new RenderTargetIdentifier(posLife[dst]),
                new RenderTargetIdentifier(velSize[dst]),
                new RenderTargetIdentifier(colorRT[dst]),
                new RenderTargetIdentifier(rotationPhaseRT[dst]),
            };
            cmd.SetRenderTarget(mrt, posLife[dst]);
            cmd.SetViewport(new Rect(0, 0, gridSize, gridSize));
            CoreUtils.DrawFullScreen(cmd, simulateMaterial, simulateProperties, 0);

            bool useRotationXYPhase = RequiresRotationXYPhase();
            if (useRotationXYPhase)
            {
                if (!rotationXYPhaseActive)
                {
                    cmd.SetRenderTarget(rotationXYPhaseRT[src]);
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    cmd.SetRenderTarget(rotationXYPhaseRT[dst]);
                    cmd.ClearRenderTarget(false, true, Color.clear);
                }

                simulateProperties.SetTexture(
                    _CurRotationXYPhase,
                    rotationXYPhaseRT[src]);
                simulateProperties.SetTexture(_NextPosLife, posLife[dst]);
                simulateProperties.SetTexture(_NextVelSize, velSize[dst]);
                cmd.SetRenderTarget(rotationXYPhaseRT[dst]);
                cmd.SetViewport(new Rect(0, 0, gridSize, gridSize));
                CoreUtils.DrawFullScreen(
                    cmd,
                    simulateMaterial,
                    simulateProperties,
                    1);
            }
            rotationXYPhaseActive = useRotationXYPhase;

            ping = dst;
            simulationTick++;
        }

        Vector3 ResolveGravityWorld()
        {
            if (gravitySource == ParticleSystemGravitySource.Physics2D)
            {
                Vector2 gravity2D = Physics2D.gravity;
                return new Vector3(gravity2D.x, gravity2D.y, 0f);
            }

            return Physics.gravity;
        }

        internal void Render(CommandBuffer cmd, Camera camera)
        {
            if (!renderEnabled || renderMaterial == null) return;
            if (!IsVisibleFrom(camera)) return;

            renderMaterial.SetTexture(_CurPosLife, posLife[ping]);
            renderMaterial.SetTexture(_CurVelSize, velSize[ping]);
            renderMaterial.SetTexture(_CurColor,   colorRT[ping]);
            renderMaterial.SetTexture(_CurRotationPhase, rotationPhaseRT[ping]);
            renderMaterial.SetTexture(
                _CurRotationXYPhase,
                rotationXYPhaseRT[ping] != null
                    ? (Texture)rotationXYPhaseRT[ping]
                    : Texture2D.blackTexture);
            renderMaterial.SetTexture("_BaseMap", baseMap != null ? baseMap : Texture2D.whiteTexture);
            renderMaterial.SetColor(_MaterialBaseColor, materialBaseColor);
            renderMaterial.SetInt(
                _MaterialColorMode,
                Mathf.Clamp((int)materialColorMode, 0, 5));
            renderMaterial.SetInt(
                _MaterialBlendOperation,
                (int)materialBlendOperation);
            renderMaterial.SetInt(
                _MaterialSourceBlend,
                (int)materialSourceBlend);
            renderMaterial.SetInt(
                _MaterialDestinationBlend,
                (int)materialDestinationBlend);
            renderMaterial.SetInt(
                _MaterialSourceBlendAlpha,
                (int)materialSourceBlendAlpha);
            renderMaterial.SetInt(
                _MaterialDestinationBlendAlpha,
                (int)materialDestinationBlendAlpha);
            renderMaterial.SetInt(
                _MaterialAlphaPremultiply,
                materialAlphaPremultiply ? 1 : 0);
            renderMaterial.SetInt(
                _MaterialAlphaModulate,
                materialAlphaModulate ? 1 : 0);
            renderMaterial.SetInt(
                _MaterialZWrite,
                materialZWrite ? 1 : 0);
            renderMaterial.SetInt(
                _MaterialAlphaClip,
                materialAlphaClip ? 1 : 0);
            renderMaterial.SetFloat(
                _MaterialAlphaCutoff,
                Mathf.Clamp01(materialAlphaCutoff));
            renderMaterial.SetInt(
                _MaterialSoftParticles,
                materialSoftParticles ? 1 : 0);
            renderMaterial.SetVector(
                _MaterialSoftParticleFadeParams,
                new Vector4(
                    materialSoftParticleFadeParams.x,
                    materialSoftParticleFadeParams.y,
                    0f,
                    0f));
            renderMaterial.SetInt(
                _MaterialCameraFading,
                materialCameraFading ? 1 : 0);
            renderMaterial.SetVector(
                _MaterialCameraFadeParams,
                new Vector4(
                    materialCameraFadeParams.x,
                    materialCameraFadeParams.y,
                    0f,
                    0f));

            renderMaterial.SetInt(_GridSize, gridSize);
            renderMaterial.SetInt(_MaxParticles, maxParticles);
            renderMaterial.SetInt(_SimulationSpace, (int)simulationSpace);
            renderMaterial.SetFloat(_StartLifetime, startLifetime);
            renderMaterial.SetFloat(_StartLifetimeMin, startLifetimeMin);
            renderMaterial.SetInt(_RandomizeStartLifetime, randomizeStartLifetime ? 1 : 0);
            Texture2D selectedStartLifetimeLUT = startLifetimeLUT != null
                ? startLifetimeLUT
                : CurveLUTBuilder.GetDefaultUnitLUT();
            renderMaterial.SetTexture(
                _StartLifetimeLUT,
                selectedStartLifetimeLUT);
            renderMaterial.SetFloat(
                _StartLifetimeLUTInvWidth,
                InverseTextureWidth(selectedStartLifetimeLUT));
            renderMaterial.SetInt(
                _StartLifetimeMode,
                (int)EffectiveStartLifetimeMode());
            renderMaterial.SetInt(
                _RingBufferMode,
                (int)ringBufferMode);
            Vector2 selectedRingBufferLoopRange =
                Ordered01Range(ringBufferLoopRange);
            renderMaterial.SetVector(
                _RingBufferLoopRange,
                new Vector4(
                    selectedRingBufferLoopRange.x,
                    selectedRingBufferLoopRange.y,
                    0f,
                    0f));
            renderMaterial.SetFloat(_EmissionTimeAfterStep, emissionTime);
            renderMaterial.SetFloat(
                _EmissionStartDelay,
                ResolveEmissionStartDelay());
            renderMaterial.SetFloat(
                _EmissionDuration,
                Mathf.Max(0.05f, emissionDuration));
            renderMaterial.SetInt(
                _EmissionLooping,
                emissionLooping ? 1 : 0);
            renderMaterial.SetFloat(
                _DeltaTime,
                lastSimulationDeltaTime);
            bool useSeparateSizeAxes = startSize3D ||
                                       sizeOverLifetimeSeparateAxes ||
                                       (sizeBySpeedEnabled &&
                                        sizeBySpeedSeparateAxes);
            Texture2D selectedStartSizeLUT = startSizeLUT != null
                ? startSizeLUT
                : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedStartSizeYLUT = startSize3D &&
                                              startSizeYLUT != null
                ? startSizeYLUT
                : selectedStartSizeLUT;
            Texture2D selectedStartSizeZLUT = startSize3D &&
                                              startSizeZLUT != null
                ? startSizeZLUT
                : selectedStartSizeLUT;
            Texture2D selectedSizeOverLifetimeLUT =
                sizeOverLifetimeLUT != null
                    ? sizeOverLifetimeLUT
                    : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedSizeOverLifetimeYLUT =
                sizeOverLifetimeSeparateAxes &&
                sizeOverLifetimeYLUT != null
                    ? sizeOverLifetimeYLUT
                    : selectedSizeOverLifetimeLUT;
            Texture2D selectedSizeOverLifetimeZLUT =
                sizeOverLifetimeSeparateAxes &&
                sizeOverLifetimeZLUT != null
                    ? sizeOverLifetimeZLUT
                    : selectedSizeOverLifetimeLUT;
            Texture2D selectedSizeBySpeedLUT = sizeBySpeedLUT != null
                ? sizeBySpeedLUT
                : CurveLUTBuilder.GetDefaultUnitLUT();
            Texture2D selectedSizeBySpeedYLUT =
                sizeBySpeedSeparateAxes && sizeBySpeedYLUT != null
                    ? sizeBySpeedYLUT
                    : selectedSizeBySpeedLUT;
            Texture2D selectedSizeBySpeedZLUT =
                sizeBySpeedSeparateAxes && sizeBySpeedZLUT != null
                    ? sizeBySpeedZLUT
                    : selectedSizeBySpeedLUT;
            renderMaterial.SetInt(
                _UseSeparateSizeAxes,
                useSeparateSizeAxes ? 1 : 0);
            renderMaterial.SetInt(_StartSize3D, startSize3D ? 1 : 0);
            renderMaterial.SetInt(
                _SizeOverLifetimeSeparateAxes,
                sizeOverLifetimeSeparateAxes ? 1 : 0);
            renderMaterial.SetInt(
                _SizeBySpeedSeparateAxes,
                sizeBySpeedSeparateAxes ? 1 : 0);
            renderMaterial.SetFloat(_StartSize, startSize);
            renderMaterial.SetFloat(_StartSizeMin, startSizeMin);
            renderMaterial.SetInt(
                _StartSizeMode,
                (int)EffectiveStartSizeMode());
            renderMaterial.SetTexture(_StartSizeLUT, selectedStartSizeLUT);
            renderMaterial.SetFloat(
                _StartSizeLUTInvWidth,
                InverseTextureWidth(selectedStartSizeLUT));
            renderMaterial.SetFloat(
                _StartSizeY,
                startSize3D ? startSizeY : startSize);
            renderMaterial.SetFloat(
                _StartSizeYMin,
                startSize3D ? startSizeYMin : startSizeMin);
            renderMaterial.SetInt(
                _StartSizeYMode,
                (int)(startSize3D
                    ? EffectiveStartSizeYMode()
                    : EffectiveStartSizeMode()));
            renderMaterial.SetTexture(
                _StartSizeYLUT,
                selectedStartSizeYLUT);
            renderMaterial.SetFloat(
                _StartSizeYLUTInvWidth,
                InverseTextureWidth(selectedStartSizeYLUT));
            renderMaterial.SetFloat(
                _StartSizeZ,
                startSize3D ? startSizeZ : startSize);
            renderMaterial.SetFloat(
                _StartSizeZMin,
                startSize3D ? startSizeZMin : startSizeMin);
            renderMaterial.SetInt(
                _StartSizeZMode,
                (int)(startSize3D
                    ? EffectiveStartSizeZMode()
                    : EffectiveStartSizeMode()));
            renderMaterial.SetTexture(
                _StartSizeZLUT,
                selectedStartSizeZLUT);
            renderMaterial.SetFloat(
                _StartSizeZLUTInvWidth,
                InverseTextureWidth(selectedStartSizeZLUT));
            renderMaterial.SetTexture(
                "_SizeLUT",
                selectedSizeOverLifetimeLUT);
            renderMaterial.SetFloat(
                _SizeLUTInvWidth,
                InverseTextureWidth(selectedSizeOverLifetimeLUT));
            renderMaterial.SetTexture(
                _SizeYLUT,
                selectedSizeOverLifetimeYLUT);
            renderMaterial.SetFloat(
                _SizeYLUTInvWidth,
                InverseTextureWidth(selectedSizeOverLifetimeYLUT));
            renderMaterial.SetTexture(
                _SizeZLUT,
                selectedSizeOverLifetimeZLUT);
            renderMaterial.SetFloat(
                _SizeZLUTInvWidth,
                InverseTextureWidth(selectedSizeOverLifetimeZLUT));
            renderMaterial.SetInt(
                _SizeBySpeedEnabled,
                sizeBySpeedEnabled ? 1 : 0);
            renderMaterial.SetVector(
                _SizeBySpeedRange,
                new Vector4(
                    sizeBySpeedRange.x,
                    sizeBySpeedRange.y,
                    0f,
                    0f));
            renderMaterial.SetTexture(
                _SizeBySpeedLUT,
                selectedSizeBySpeedLUT);
            renderMaterial.SetFloat(
                _SizeBySpeedLUTInvWidth,
                InverseTextureWidth(selectedSizeBySpeedLUT));
            renderMaterial.SetTexture(
                _SizeBySpeedYLUT,
                selectedSizeBySpeedYLUT);
            renderMaterial.SetFloat(
                _SizeBySpeedYLUTInvWidth,
                InverseTextureWidth(selectedSizeBySpeedYLUT));
            renderMaterial.SetTexture(
                _SizeBySpeedZLUT,
                selectedSizeBySpeedZLUT);
            renderMaterial.SetFloat(
                _SizeBySpeedZLUTInvWidth,
                InverseTextureWidth(selectedSizeBySpeedZLUT));
            Texture2D selectedLifetimeByEmitterSpeedLUT =
                lifetimeByEmitterSpeedLUT != null
                    ? lifetimeByEmitterSpeedLUT
                    : CurveLUTBuilder.GetDefaultUnitLUT();
            renderMaterial.SetTexture(
                _LifetimeByEmitterSpeedLUT,
                selectedLifetimeByEmitterSpeedLUT);
            renderMaterial.SetFloat(
                _LifetimeByEmitterSpeedLUTInvWidth,
                InverseTextureWidth(selectedLifetimeByEmitterSpeedLUT));
            renderMaterial.SetInt(
                _LifetimeByEmitterSpeedEnabled,
                lifetimeByEmitterSpeedEnabled ? 1 : 0);
            renderMaterial.SetVector(
                _LifetimeByEmitterSpeedRange,
                new Vector4(
                    lifetimeByEmitterSpeedRange.x,
                    lifetimeByEmitterSpeedRange.y,
                    0f,
                    0f));
            Texture2D selectedTextureSheetFrameLUT =
                textureSheetFrameOverTimeLUT != null
                    ? textureSheetFrameOverTimeLUT
                    : CurveLUTBuilder.GetDefaultLinear01LUT();
            Texture2D selectedTextureSheetStartLUT =
                textureSheetStartFrameLUT != null
                    ? textureSheetStartFrameLUT
                    : CurveLUTBuilder.GetDefaultZeroLUT();
            bool textureSheetAffectsUV0 =
                (textureSheetUVChannelMask & UVChannelFlags.UV0) != 0;
            bool useTextureSheet = textureSheetAnimationEnabled &&
                                   textureSheetMode == ParticleSystemAnimationMode.Grid &&
                                   textureSheetAffectsUV0;
            int textureSheetRowCount = Mathf.Max(1, textureSheetTilesY);
            renderMaterial.SetInt(
                _TextureSheetEnabled, useTextureSheet ? 1 : 0);
            renderMaterial.SetInt(
                _TextureSheetTilesX, Mathf.Max(1, textureSheetTilesX));
            renderMaterial.SetInt(
                _TextureSheetTilesY, textureSheetRowCount);
            renderMaterial.SetInt(
                _TextureSheetAnimation, (int)textureSheetAnimation);
            renderMaterial.SetInt(
                _TextureSheetTimeMode, (int)textureSheetTimeMode);
            renderMaterial.SetInt(
                _TextureSheetRowMode, (int)textureSheetRowMode);
            renderMaterial.SetInt(
                _TextureSheetRowIndex,
                Mathf.Clamp(textureSheetRowIndex, 0, textureSheetRowCount - 1));
            renderMaterial.SetInt(
                _TextureSheetCycleCount, Mathf.Max(1, textureSheetCycleCount));
            renderMaterial.SetFloat(
                _TextureSheetFps, Mathf.Max(0f, textureSheetFps));
            renderMaterial.SetVector(
                _TextureSheetSpeedRange,
                new Vector4(
                    textureSheetSpeedRange.x,
                    textureSheetSpeedRange.y,
                    0f,
                    0f));
            bool textureSheetAffectsUV1 =
                (textureSheetUVChannelMask & UVChannelFlags.UV1) != 0;
            renderMaterial.SetInt(
                _TextureSheetFrameBlending,
                useTextureSheet && textureSheetFrameBlending ? 1 : 0);
            renderMaterial.SetInt(
                _TextureSheetBlendNextUV,
                textureSheetAffectsUV1 ? 1 : 0);
            renderMaterial.SetTexture(
                _TextureSheetFrameOverTimeLUT,
                selectedTextureSheetFrameLUT);
            renderMaterial.SetTexture(
                _TextureSheetStartFrameLUT,
                selectedTextureSheetStartLUT);
            renderMaterial.SetFloat(
                _TextureSheetFrameLUTInvWidth,
                InverseTextureWidth(selectedTextureSheetFrameLUT));
            renderMaterial.SetFloat(
                _TextureSheetStartLUTInvWidth,
                InverseTextureWidth(selectedTextureSheetStartLUT));
            renderMaterial.SetFloat(_StartRotation, startRotation);
            renderMaterial.SetFloat(_StartRotationMin, startRotationMin);
            renderMaterial.SetInt(_RandomizeStartRotation, randomizeStartRotation ? 1 : 0);
            Texture2D selectedStartRotationLUT = startRotationLUT != null
                ? startRotationLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            renderMaterial.SetTexture(
                _StartRotationLUT,
                selectedStartRotationLUT);
            renderMaterial.SetFloat(
                _StartRotationLUTInvWidth,
                InverseTextureWidth(selectedStartRotationLUT));
            renderMaterial.SetInt(
                _StartRotationMode,
                (int)EffectiveStartRotationMode());
            Texture2D selectedStartRotationXLUT = startRotation3D &&
                                                   startRotationXLUT != null
                ? startRotationXLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedStartRotationYLUT = startRotation3D &&
                                                   startRotationYLUT != null
                ? startRotationYLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            renderMaterial.SetInt(
                _StartRotation3D,
                startRotation3D ? 1 : 0);
            renderMaterial.SetFloat(
                _StartRotationX,
                startRotation3D ? startRotationX : 0f);
            renderMaterial.SetFloat(
                _StartRotationXMin,
                startRotation3D ? startRotationXMin : 0f);
            renderMaterial.SetInt(
                _StartRotationXMode,
                (int)(startRotation3D
                    ? EffectiveStartRotationXMode()
                    : ParticleSystemCurveMode.Constant));
            renderMaterial.SetTexture(
                _StartRotationXLUT,
                selectedStartRotationXLUT);
            renderMaterial.SetFloat(
                _StartRotationXLUTInvWidth,
                InverseTextureWidth(selectedStartRotationXLUT));
            renderMaterial.SetFloat(
                _StartRotationY,
                startRotation3D ? startRotationY : 0f);
            renderMaterial.SetFloat(
                _StartRotationYMin,
                startRotation3D ? startRotationYMin : 0f);
            renderMaterial.SetInt(
                _StartRotationYMode,
                (int)(startRotation3D
                    ? EffectiveStartRotationYMode()
                    : ParticleSystemCurveMode.Constant));
            renderMaterial.SetTexture(
                _StartRotationYLUT,
                selectedStartRotationYLUT);
            renderMaterial.SetFloat(
                _StartRotationYLUTInvWidth,
                InverseTextureWidth(selectedStartRotationYLUT));
            renderMaterial.SetFloat(_FlipRotation, Mathf.Clamp01(flipRotation));
            renderMaterial.SetFloat(_RotationOverLifetime, rotationOverLifetime);
            renderMaterial.SetFloat(_RotationOverLifetimeMin, rotationOverLifetimeMin);
            renderMaterial.SetInt(_RandomizeRotationOverLifetime,
                randomizeRotationOverLifetime ? 1 : 0);
            bool useRotationIntegralLUT =
                rotationOverLifetimeIntegralLUT != null ||
                (rotationOverLifetimeSeparateAxes &&
                 (rotationOverLifetimeXIntegralLUT != null ||
                  rotationOverLifetimeYIntegralLUT != null));
            Texture2D selectedRotationIntegralLUT = useRotationIntegralLUT
                ? rotationOverLifetimeIntegralLUT != null
                    ? rotationOverLifetimeIntegralLUT
                    : CurveLUTBuilder.GetDefaultZeroLUT()
                : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedRotationXIntegralLUT =
                rotationOverLifetimeSeparateAxes &&
                rotationOverLifetimeXIntegralLUT != null
                    ? rotationOverLifetimeXIntegralLUT
                    : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedRotationYIntegralLUT =
                rotationOverLifetimeSeparateAxes &&
                rotationOverLifetimeYIntegralLUT != null
                    ? rotationOverLifetimeYIntegralLUT
                    : CurveLUTBuilder.GetDefaultZeroLUT();
            renderMaterial.SetTexture(
                _RotationOverLifetimeIntegralLUT, selectedRotationIntegralLUT);
            renderMaterial.SetFloat(
                _RotationOverLifetimeIntegralLUTInvWidth,
                InverseTextureWidth(selectedRotationIntegralLUT));
            renderMaterial.SetInt(
                _RotationOverLifetimeSeparateAxes,
                rotationOverLifetimeSeparateAxes ? 1 : 0);
            renderMaterial.SetTexture(
                _RotationOverLifetimeXIntegralLUT,
                selectedRotationXIntegralLUT);
            renderMaterial.SetFloat(
                _RotationOverLifetimeXIntegralLUTInvWidth,
                InverseTextureWidth(selectedRotationXIntegralLUT));
            renderMaterial.SetTexture(
                _RotationOverLifetimeYIntegralLUT,
                selectedRotationYIntegralLUT);
            renderMaterial.SetFloat(
                _RotationOverLifetimeYIntegralLUTInvWidth,
                InverseTextureWidth(selectedRotationYIntegralLUT));
            renderMaterial.SetInt(
                _UseRotationOverLifetimeIntegralLUT, useRotationIntegralLUT ? 1 : 0);
            renderMaterial.SetInt(
                _RotationBySpeedSeparateAxes,
                rotationBySpeedSeparateAxes ? 1 : 0);
            Matrix4x4 particleLocalToWorld = ParticleLocalToWorldMatrix();
            Matrix4x4 simulationLocalToWorld =
                SimulationLocalToWorldMatrix(particleLocalToWorld);
            renderMaterial.SetMatrix(
                _EmitterLocalToWorld_Render,
                simulationLocalToWorld);
            renderMaterial.SetMatrix(
                _ParticleScaleWorld,
                ParticleRenderWorldMatrix(particleLocalToWorld));
            renderMaterial.SetVector(_CameraRightWS, camera.transform.right);
            renderMaterial.SetVector(_CameraUpWS, camera.transform.up);

            // camera position & velocity
            Vector3 camPos = camera.transform.position;
            Vector3 camVel = Vector3.zero;
            float dt = FrameDeltaTime();
            if (prevCamPosValid && dt > 1e-6f) camVel = (camPos - prevCamPos) / dt;
            prevCamPos = camPos; prevCamPosValid = true;
            renderMaterial.SetVector(_CameraPosWS, camPos);
            renderMaterial.SetVector(_CameraVelWS, camVel);

            // renderer params
            renderMaterial.SetInt(_RenderMode, (int)renderMode);
            renderMaterial.SetInt(_RenderAlignment, (int)renderAlignment);
            renderMaterial.SetInt(_AllowRoll, allowRoll ? 1 : 0);
            renderMaterial.SetFloat(_NormalDirection, normalDirection);
            renderMaterial.SetVector(
                _Pivot,
                new Vector4(pivot.x, pivot.y, pivotDepth, 0f));
            Vector3 meshBoundsSize = renderMode == GPURenderMode.Mesh &&
                                     renderMesh != null
                ? renderMesh.bounds.size
                : Vector3.one;
            renderMaterial.SetVector(_MeshBoundsSize, meshBoundsSize);
            renderMaterial.SetVector(
                _RendererFlip,
                new Vector4(
                    Mathf.Clamp01(rendererFlip.x),
                    Mathf.Clamp01(rendererFlip.y),
                    Mathf.Clamp01(rendererFlip.z),
                    0f));
            renderMaterial.SetInt(
                _ScreenSpaceSizeClampEnabled,
                screenSpaceSizeClampEnabled ? 1 : 0);
            renderMaterial.SetFloat(
                _MinParticleSize,
                Mathf.Clamp01(minParticleSize));
            renderMaterial.SetFloat(
                _MaxParticleSize,
                Mathf.Clamp01(maxParticleSize));
            renderMaterial.SetFloat(_LenScale, stretchedLengthScale);
            renderMaterial.SetFloat(_VelScale, stretchedVelocityScale);
            renderMaterial.SetFloat(_CamVelScale, stretchedCameraVelocityScale);
            renderMaterial.SetInt(_Freeform, freeformStretching ? 1 : 0);
            renderMaterial.SetInt(_RotateWithStretch, rotateWithStretchDirection ? 1 : 0);
            renderMaterial.SetFloat(_MinAlphaCull, minAlphaCull);

            if (renderMode == GPURenderMode.Mesh && renderMesh != null)
            {
                renderMaterial.enableInstancing = true;
                int subMeshCount = renderMesh.subMeshCount;
                for (int subMeshIndex = 0;
                     subMeshIndex < subMeshCount;
                     subMeshIndex++)
                {
                    cmd.DrawMeshInstancedProcedural(
                        renderMesh,
                        subMeshIndex,
                        renderMaterial,
                        0,
                        maxParticles);
                }
                return;
            }

            // draw quads: 6 verts per particle
            int vertexCount = maxParticles * 6;
            cmd.DrawProcedural(
                Matrix4x4.identity,
                renderMaterial,
                0,
                MeshTopology.Triangles,
                vertexCount,
                1);
        }
    }
}
