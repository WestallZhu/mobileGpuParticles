using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace GPUParticles
{
    public enum SimulationSpace { Local = 0, World = 1 }

    public enum ShapeTypeGPU { Sphere = 0, Hemisphere = 1, Cone = 2, Donut = 3, Box = 4, Circle = 5, Edge = 6, Rectangle = 7, Point = 8 }
    public enum ShapeEmitFromGPU
    {
        Volume = 0,
        Surface = 1,
        Base = 2,
        Edge = 3
    }

    public enum GPURenderMode { Billboard = 0, HorizontalBillboard = 1, VerticalBillboard = 2, StretchedBillboard = 3 }
    public enum GPUAlignment  { View = 0, Facing = 1, World = 2, Local = 3, Velocity = 4 }

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
        public float startSpeed = 5.0f;
        public ParticleSystemCurveMode startSpeedMode =
            ParticleSystemCurveMode.Constant;
        public Texture2D startSpeedLUT;
        [Min(0.0f)] public float startSize = 0.1f;
        public Color startColor = Color.white;
        public ParticleSystemGradientMode startColorMode =
            ParticleSystemGradientMode.Color;
        public Texture2D startColorLUT;
        public float gravityModifier = 0.0f;
        [Min(0.0f)] public float simulationSpeed = 1.0f;
        public float startRotation = 0.0f;
        public float rotationOverLifetime = 0.0f;
        public SimulationSpace simulationSpace = SimulationSpace.Local;

        [Header("Main Random Between Two Constants")]
        public bool randomizeStartLifetime;
        [Min(0.001f)] public float startLifetimeMin = 2.0f;
        public bool randomizeStartSpeed;
        public float startSpeedMin = 5.0f;
        public bool randomizeStartSize;
        [Min(0.0f)] public float startSizeMin = 0.1f;
        public bool randomizeStartColor;
        public Color startColorMin = Color.white;
        public bool randomizeGravityModifier;
        public float gravityModifierMin;
        public bool randomizeStartRotation;
        public float startRotationMin;
        public bool randomizeRotationOverLifetime;
        public float rotationOverLifetimeMin;

        [Header("Over Lifetime (LUTs)")]
        public Texture2D rotationOverLifetimeIntegralLUT;
        public Texture2D colorOverLifetimeLUT;
        public ParticleSystemGradientMode colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
        public Texture2D sizeOverLifetimeLUT;

        [Header("By Speed (LUTs)")]
        public bool colorBySpeedEnabled;
        public Texture2D colorBySpeedLUT;
        public ParticleSystemGradientMode colorBySpeedMode = ParticleSystemGradientMode.Gradient;
        public Vector2 colorBySpeedRange = new Vector2(0f, 1f);
        public bool sizeBySpeedEnabled;
        public Texture2D sizeBySpeedLUT;
        public Vector2 sizeBySpeedRange = new Vector2(0f, 1f);
        public bool rotationBySpeedEnabled;
        public Texture2D rotationBySpeedLUT;
        public Vector2 rotationBySpeedRange = new Vector2(0f, 1f);

        [Header("Force Over Lifetime")]
        public bool forceOverLifetimeEnabled;
        public Texture2D forceOverLifetimeLUT;
        public SimulationSpace forceOverLifetimeSpace = SimulationSpace.Local;
        public bool forceOverLifetimeRandomized;

        [Header("Velocity Over Lifetime (Linear XYZ + Speed Modifier)")]
        public bool velocityOverLifetimeEnabled;
        public Texture2D velocityOverLifetimeLUT;
        public SimulationSpace velocityOverLifetimeSpace = SimulationSpace.Local;
        public bool velocityOverLifetimeSpeedModifierEnabled;

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
        public Texture2D textureSheetFrameOverTimeLUT;
        public Texture2D textureSheetStartFrameLUT;

        [Header("Rendering")]
        public Texture2D baseMap;
        [Range(0,1)] public float minAlphaCull = 0.001f;
        public bool renderEnabled = true;

        [Header("Emitter Direction (fallback)")]
        public Vector3 initialDirectionWS = Vector3.forward;

        // --------- Shape ---------
        [Header("Shape (Point/Sphere/Hemisphere/Cone/Donut/Box/Circle/Edge/Rectangle)")]
        public ShapeTypeGPU shapeType = ShapeTypeGPU.Cone;
        public ShapeEmitFromGPU shapeEmitFrom = ShapeEmitFromGPU.Volume;
        public bool alignToDirection = false; // default false (match Shuriken)

        // Sphere/Hemisphere
        public float shapeSphereRadius = 0.5f;

        // Cone
        [Range(0,90)] public float shapeConeAngle = 25f;
        public float shapeConeRadius = 0.25f;
        public float shapeConeLength = 1.0f;
        [Range(0,1)] public float shapeRadiusThickness = 1.0f;
        [Range(0,360)] public float shapeConeArcDeg = 360f;

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
        public GPUAlignment  renderAlignment = GPUAlignment.View; // ignored in Stretched per Unity
        public bool allowRoll = true;
        [Range(0,1)] public float normalDirection = 1.0f; // billboard only
        public Vector2 pivot = Vector2.zero;
        [Min(0)] public float minParticleSize = 0f; // TODO: screen-space clamp (optional)
        [Min(0)] public float maxParticleSize = 0f;

        [Header("Stretched Billboard")]
        public float stretchedLengthScale = 1.0f;   // 1 = neutral
        public float stretchedVelocityScale = 0.0f;
        public float stretchedCameraVelocityScale = 0.0f;
        public bool  freeformStretching = false;
        public bool  rotateWithStretchDirection = true; // for lit/UV rotation pipelines

        // --------- Runtime ---------
        public static readonly List<GPUParticleSystem> Active = new List<GPUParticleSystem>();

        Material simulateMaterial;
        Material renderMaterial;

        RenderTexture[] posLife = new RenderTexture[2];
        RenderTexture[] velSize = new RenderTexture[2];
        RenderTexture[] colorRT = new RenderTexture[2];
        RenderTexture[] rotationPhaseRT = new RenderTexture[2];

        int ping;
        int gridSize;
        int capacity;

        // emission cursor
        int emitCursor = 0;
        float emitCarry = 0f;
        float distanceEmitCarry;
        float emissionTime;
        Vector3 previousEmitterPositionWS;
        bool previousEmitterPositionValid;
        Vector3 previousEmitterVelocityWS;
        uint simulationTick;
        int lastSimulatedFrame = -1;
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
        static readonly int _GridSize   = Shader.PropertyToID("_GridSize");
        static readonly int _MaxParticles = Shader.PropertyToID("_MaxParticles");
        static readonly int _DeltaTime  = Shader.PropertyToID("_DeltaTime");
        static readonly int _StartLifetime = Shader.PropertyToID("_StartLifetime");
        static readonly int _StartLifetimeMin = Shader.PropertyToID("_StartLifetimeMin");
        static readonly int _RandomizeStartLifetime = Shader.PropertyToID("_RandomizeStartLifetime");
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
        static readonly int _RotationOverLifetime = Shader.PropertyToID("_RotationOverLifetime");
        static readonly int _RotationOverLifetimeMin = Shader.PropertyToID("_RotationOverLifetimeMin");
        static readonly int _RandomizeRotationOverLifetime = Shader.PropertyToID("_RandomizeRotationOverLifetime");
        static readonly int _RotationOverLifetimeIntegralLUT =
            Shader.PropertyToID("_RotationOverLifetimeIntegralLUT");
        static readonly int _RotationOverLifetimeIntegralLUTInvWidth =
            Shader.PropertyToID("_RotationOverLifetimeIntegralLUTInvWidth");
        static readonly int _UseRotationOverLifetimeIntegralLUT =
            Shader.PropertyToID("_UseRotationOverLifetimeIntegralLUT");
        static readonly int _GravityWS  = Shader.PropertyToID("_GravityWS");
        static readonly int _GravityWSMin = Shader.PropertyToID("_GravityWSMin");
        static readonly int _RandomizeGravityModifier = Shader.PropertyToID("_RandomizeGravityModifier");
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
        static readonly int _VelocityOverLifetimeEnabled = Shader.PropertyToID("_VelocityOverLifetimeEnabled");
        static readonly int _VelocityOverLifetimeSpace = Shader.PropertyToID("_VelocityOverLifetimeSpace");
        static readonly int _VelocityOverLifetimeSpeedModifierEnabled =
            Shader.PropertyToID("_VelocityOverLifetimeSpeedModifierEnabled");
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
        static readonly int _ColorOverLifetimeMode = Shader.PropertyToID("_ColorOverLifetimeMode");
        static readonly int _GradLUTInvWidth = Shader.PropertyToID("_GradLUTInvWidth");
        static readonly int _SizeLUTInvWidth = Shader.PropertyToID("_SizeLUTInvWidth");
        static readonly int _ForceOverLifetimeLUTInvWidth =
            Shader.PropertyToID("_ForceOverLifetimeLUTInvWidth");
        static readonly int _VelocityOverLifetimeLUTInvWidth =
            Shader.PropertyToID("_VelocityOverLifetimeLUTInvWidth");
        static readonly int _ColorBySpeedLUT = Shader.PropertyToID("_ColorBySpeedLUT");
        static readonly int _ColorBySpeedLUTInvWidth =
            Shader.PropertyToID("_ColorBySpeedLUTInvWidth");
        static readonly int _ColorBySpeedEnabled = Shader.PropertyToID("_ColorBySpeedEnabled");
        static readonly int _ColorBySpeedMode = Shader.PropertyToID("_ColorBySpeedMode");
        static readonly int _ColorBySpeedRange = Shader.PropertyToID("_ColorBySpeedRange");
        static readonly int _SizeBySpeedLUT = Shader.PropertyToID("_SizeBySpeedLUT");
        static readonly int _SizeBySpeedLUTInvWidth =
            Shader.PropertyToID("_SizeBySpeedLUTInvWidth");
        static readonly int _SizeBySpeedEnabled = Shader.PropertyToID("_SizeBySpeedEnabled");
        static readonly int _SizeBySpeedRange = Shader.PropertyToID("_SizeBySpeedRange");
        static readonly int _RotationBySpeedLUT = Shader.PropertyToID("_RotationBySpeedLUT");
        static readonly int _RotationBySpeedLUTInvWidth =
            Shader.PropertyToID("_RotationBySpeedLUTInvWidth");
        static readonly int _RotationBySpeedEnabled =
            Shader.PropertyToID("_RotationBySpeedEnabled");
        static readonly int _RotationBySpeedRange =
            Shader.PropertyToID("_RotationBySpeedRange");

        // shape params
        static readonly int _ShapeType = Shader.PropertyToID("_ShapeType");
        static readonly int _ShapeEmitFrom = Shader.PropertyToID("_ShapeEmitFrom");
        static readonly int _AlignToDirection = Shader.PropertyToID("_AlignToDirection");
        static readonly int _ShapeConeAngleRad = Shader.PropertyToID("_ShapeConeAngleRad");
        static readonly int _ShapeConeRadius = Shader.PropertyToID("_ShapeConeRadius");
        static readonly int _ShapeConeLength = Shader.PropertyToID("_ShapeConeLength");
        static readonly int _ShapeRadiusThickness = Shader.PropertyToID("_ShapeRadiusThickness");
        static readonly int _ShapeConeArcRad = Shader.PropertyToID("_ShapeConeArcRad");
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
        static readonly int _EmitterPreviousPositionWS = Shader.PropertyToID("_EmitterPreviousPositionWS");
        static readonly int _EmitterCurrentPositionWS = Shader.PropertyToID("_EmitterCurrentPositionWS");

        // render shader ids
        static readonly int _EmitterLocalToWorld_Render = Shader.PropertyToID("_EmitterLocalToWorld");
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
        static readonly int _TextureSheetFrameOverTimeLUT =
            Shader.PropertyToID("_TextureSheetFrameOverTimeLUT");
        static readonly int _TextureSheetStartFrameLUT =
            Shader.PropertyToID("_TextureSheetStartFrameLUT");
        static readonly int _TextureSheetFrameLUTInvWidth =
            Shader.PropertyToID("_TextureSheetFrameLUTInvWidth");
        static readonly int _TextureSheetStartLUTInvWidth =
            Shader.PropertyToID("_TextureSheetStartLUTInvWidth");

        internal RenderTexture CurrentPositionLifetimeTexture => posLife[ping];
        internal RenderTexture CurrentVelocitySizeTexture => velSize[ping];
        internal RenderTexture CurrentColorTexture => colorRT[ping];
        internal RenderTexture CurrentRotationPhaseTexture => rotationPhaseRT[ping];

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            EnsureMaterials();
            RecreateTargetsIfNeeded(true);
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
            if (emissionBursts == null) emissionBursts = System.Array.Empty<GPUEmissionBurst>();
            EnsureMaterials();
            RecreateTargetsIfNeeded(false);
        }

        void EnsureMaterials()
        {
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

        void RecreateTargetsIfNeeded(bool force)
        {
            int newGrid = CeilSqrt(maxParticles);
            if (!force &&
                newGrid == gridSize &&
                posLife[0] != null &&
                rotationPhaseRT[0] != null)
            {
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

            ClearRT(posLife[0]); ClearRT(posLife[1]);
            ClearRT(velSize[0]); ClearRT(velSize[1]);
            ClearRT(colorRT[0]); ClearRT(colorRT[1]);
            ClearRT(rotationPhaseRT[0]); ClearRT(rotationPhaseRT[1]);

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
            lastSimulatedFrame = -1;
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
            }
        }

        internal void ResetSimulation()
        {
            EnsureMaterials();
            RecreateTargetsIfNeeded(true);
        }

        public void SetStartLifetimeRange(float minimum, float maximum)
        {
            startLifetimeMin = Mathf.Max(0.001f, Mathf.Min(minimum, maximum));
            startLifetime = Mathf.Max(0.001f, Mathf.Max(minimum, maximum));
            randomizeStartLifetime = !Mathf.Approximately(startLifetimeMin, startLifetime);
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
        }

        public void SetStartRotationRange(float minimum, float maximum)
        {
            startRotationMin = Mathf.Min(minimum, maximum);
            startRotation = Mathf.Max(minimum, maximum);
            randomizeStartRotation = !Mathf.Approximately(startRotationMin, startRotation);
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
            return ResolveStartLifetime(particleId, Vector3.zero);
        }

        internal float ResolveStartLifetime(
            int particleId,
            Vector3 birthEmitterVelocityWS)
        {
            float baseLifetime = ResolveRandomRange(
                randomizeStartLifetime,
                startLifetimeMin,
                startLifetime,
                (uint)particleId,
                0x68E31DA4u);
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

        internal float ResolveParticleRotationRadians(
            int particleId,
            float remainingLifetime,
            float rotationBySpeedPhase = 0f,
            Vector3 birthEmitterVelocityWS = default)
        {
            uint id = (uint)particleId;
            float particleStartLifetime = ResolveStartLifetime(
                particleId,
                birthEmitterVelocityWS);
            float age = Mathf.Max(0f, particleStartLifetime - remainingLifetime);
            float particleStartRotation = ResolveRandomRange(
                randomizeStartRotation,
                startRotationMin,
                startRotation,
                id,
                0x165667B1u);

            if (rotationOverLifetimeIntegralLUT == null)
            {
                float angularVelocity = ResolveRandomRange(
                    randomizeRotationOverLifetime,
                    rotationOverLifetimeMin,
                    rotationOverLifetime,
                    id,
                    0xD3A2646Cu);
                return particleStartRotation + angularVelocity * age + rotationBySpeedPhase;
            }

            float normalizedAge = particleStartLifetime > 1e-6f
                ? Mathf.Clamp01(age / particleStartLifetime)
                : 0f;
            float minimumIntegral = SampleLUTRow(
                rotationOverLifetimeIntegralLUT, normalizedAge, 0);
            float maximumIntegral = SampleLUTRow(
                rotationOverLifetimeIntegralLUT, normalizedAge, 1);
            float blend = Hash01(id ^ 0xD3A2646Cu);
            float integral = Mathf.LerpUnclamped(minimumIntegral, maximumIntegral, blend);
            return particleStartRotation + integral * particleStartLifetime +
                   rotationBySpeedPhase;
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
            return ResolveRandomRange(
                randomizeEmissionStartDelay,
                emissionStartDelayMin,
                emissionStartDelay,
                emissionRandomSeed,
                0xA24BAED4u);
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

        internal void Simulate(CommandBuffer cmd, Camera camera)
        {
            if (simulateMaterial == null) return;

            // A renderer feature executes once per camera. Shuriken advances once per
            // player-loop frame, so advancing here for Scene/Game/overlay cameras would
            // make the GPU system run faster whenever more than one camera renders.
            if (Application.isPlaying)
            {
                int frame = Time.frameCount;
                if (lastSimulatedFrame == frame) return;
                lastSimulatedFrame = frame;
            }

            float dt = Application.isPlaying ? Time.deltaTime : (1f / 60f);
            dt *= simulationSpeed;
            SimulateStep(cmd, dt);
        }

        internal void SimulateStep(CommandBuffer cmd, float dt)
        {
            dt = Mathf.Max(0f, dt);
            Vector3 emitterCurrentPositionWS = transform.position;
            Vector3 emitterPreviousPositionWS = previousEmitterPositionValid
                ? previousEmitterPositionWS
                : emitterCurrentPositionWS;
            float emitterDistance = Vector3.Distance(
                emitterPreviousPositionWS, emitterCurrentPositionWS);
            Vector3 emitterVelocityWS = dt > 1e-6f
                ? (emitterCurrentPositionWS - emitterPreviousPositionWS) / dt
                : Vector3.zero;
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

            if (emissionEnabled)
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
            int emitStart = emitCursor;
            emitCursor = (emitCursor + emitCount) % maxParticles;
            emissionTime = stepEnd;
            previousEmitterPositionWS = emitterCurrentPositionWS;
            previousEmitterPositionValid = true;
            previousEmitterVelocityWS = emitterVelocityWS;

            int src = ping, dst = 1 - ping;

            simulateMaterial.SetTexture(_CurPosLife, posLife[src]);
            simulateMaterial.SetTexture(_CurVelSize, velSize[src]);
            simulateMaterial.SetTexture(_CurColor,   colorRT[src]);
            simulateMaterial.SetTexture(_CurRotationPhase, rotationPhaseRT[src]);
            Texture2D selectedStartSpeedLUT = startSpeedLUT != null
                ? startSpeedLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedStartColorLUT = startColorLUT != null
                ? startColorLUT
                : GradientLUTBuilder.GetDefaultWhiteLUT();
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
            Texture2D selectedForceOverLifetimeLUT = forceOverLifetimeLUT != null
                ? forceOverLifetimeLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            Texture2D selectedVelocityOverLifetimeLUT = velocityOverLifetimeLUT != null
                ? velocityOverLifetimeLUT
                : MinMaxCurveVector3LUTBuilder.GetDefaultVelocityLUT();
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

            simulateMaterial.SetTexture("_GradLUT", selectedColorOverLifetimeLUT);
            simulateMaterial.SetTexture(_StartSpeedLUT, selectedStartSpeedLUT);
            simulateMaterial.SetTexture(_StartColorLUT, selectedStartColorLUT);
            simulateMaterial.SetTexture("_SizeLUT", selectedSizeOverLifetimeLUT);
            simulateMaterial.SetTexture(_ColorBySpeedLUT, selectedColorBySpeedLUT);
            simulateMaterial.SetTexture(_SizeBySpeedLUT, selectedSizeBySpeedLUT);
            simulateMaterial.SetTexture(
                _RotationBySpeedLUT, selectedRotationBySpeedLUT);
            simulateMaterial.SetTexture(
                _ForceOverLifetimeLUT, selectedForceOverLifetimeLUT);
            simulateMaterial.SetTexture(
                _VelocityOverLifetimeLUT, selectedVelocityOverLifetimeLUT);
            simulateMaterial.SetTexture(
                _LimitVelocityLUT, selectedLimitVelocityLUT);
            simulateMaterial.SetTexture(
                _InheritVelocityLUT, selectedInheritVelocityLUT);
            simulateMaterial.SetTexture(
                _LifetimeByEmitterSpeedLUT,
                selectedLifetimeByEmitterSpeedLUT);
            simulateMaterial.SetFloat(
                _GradLUTInvWidth, InverseTextureWidth(selectedColorOverLifetimeLUT));
            simulateMaterial.SetFloat(
                _StartSpeedLUTInvWidth,
                InverseTextureWidth(selectedStartSpeedLUT));
            simulateMaterial.SetFloat(
                _StartColorLUTInvWidth,
                InverseTextureWidth(selectedStartColorLUT));
            simulateMaterial.SetFloat(
                _SizeLUTInvWidth, InverseTextureWidth(selectedSizeOverLifetimeLUT));
            simulateMaterial.SetFloat(
                _ColorBySpeedLUTInvWidth, InverseTextureWidth(selectedColorBySpeedLUT));
            simulateMaterial.SetFloat(
                _SizeBySpeedLUTInvWidth, InverseTextureWidth(selectedSizeBySpeedLUT));
            simulateMaterial.SetFloat(
                _RotationBySpeedLUTInvWidth,
                InverseTextureWidth(selectedRotationBySpeedLUT));
            simulateMaterial.SetFloat(
                _ForceOverLifetimeLUTInvWidth,
                InverseTextureWidth(selectedForceOverLifetimeLUT));
            simulateMaterial.SetFloat(
                _VelocityOverLifetimeLUTInvWidth,
                InverseTextureWidth(selectedVelocityOverLifetimeLUT));
            simulateMaterial.SetFloat(
                _LimitVelocityLUTInvWidth,
                InverseTextureWidth(selectedLimitVelocityLUT));
            simulateMaterial.SetFloat(
                _InheritVelocityLUTInvWidth,
                InverseTextureWidth(selectedInheritVelocityLUT));
            simulateMaterial.SetFloat(
                _LifetimeByEmitterSpeedLUTInvWidth,
                InverseTextureWidth(selectedLifetimeByEmitterSpeedLUT));

            simulateMaterial.SetInt(_GridSize, gridSize);
            simulateMaterial.SetInt(_MaxParticles, maxParticles);
            simulateMaterial.SetFloat(_DeltaTime, dt);
            simulateMaterial.SetFloat(_StartLifetime, startLifetime);
            simulateMaterial.SetFloat(_StartLifetimeMin, startLifetimeMin);
            simulateMaterial.SetInt(_RandomizeStartLifetime, randomizeStartLifetime ? 1 : 0);
            simulateMaterial.SetFloat(_StartSpeed, startSpeed);
            simulateMaterial.SetFloat(_StartSpeedMin, startSpeedMin);
            simulateMaterial.SetInt(_RandomizeStartSpeed, randomizeStartSpeed ? 1 : 0);
            ParticleSystemCurveMode selectedStartSpeedMode =
                startSpeedMode == ParticleSystemCurveMode.Constant &&
                randomizeStartSpeed
                    ? ParticleSystemCurveMode.TwoConstants
                    : startSpeedMode;
            simulateMaterial.SetInt(
                _StartSpeedMode, (int)selectedStartSpeedMode);
            simulateMaterial.SetFloat(_StartSize, startSize);
            simulateMaterial.SetFloat(_StartSizeMin, startSizeMin);
            simulateMaterial.SetInt(_RandomizeStartSize, randomizeStartSize ? 1 : 0);
            simulateMaterial.SetColor(_StartColor, startColor);
            simulateMaterial.SetColor(_StartColorMin, startColorMin);
            simulateMaterial.SetInt(_RandomizeStartColor, randomizeStartColor ? 1 : 0);
            ParticleSystemGradientMode selectedStartColorMode =
                startColorMode == ParticleSystemGradientMode.Color &&
                randomizeStartColor
                    ? ParticleSystemGradientMode.TwoColors
                    : startColorMode;
            simulateMaterial.SetInt(
                _StartColorMode, (int)selectedStartColorMode);

            Vector3 gWorld = Physics.gravity * gravityModifier;
            Vector3 gWorldMin = Physics.gravity * gravityModifierMin;
            Vector3 gSim = (simulationSpace == SimulationSpace.World) ? gWorld : transform.InverseTransformDirection(gWorld);
            Vector3 gSimMin = simulationSpace == SimulationSpace.World
                ? gWorldMin
                : transform.InverseTransformDirection(gWorldMin);
            simulateMaterial.SetVector(_GravityWS, new Vector4(gSim.x, gSim.y, gSim.z, 0));
            simulateMaterial.SetVector(_GravityWSMin,
                new Vector4(gSimMin.x, gSimMin.y, gSimMin.z, 0));
            simulateMaterial.SetInt(_RandomizeGravityModifier, randomizeGravityModifier ? 1 : 0);

            simulateMaterial.SetInt(_SimulationSpace, (int)simulationSpace);
            simulateMaterial.SetInt(_EmitStart, emitStart);
            simulateMaterial.SetInt(_EmitCount, emitCount);
            simulateMaterial.SetFloat(_EmitCarryPrev, emitCarryPrev);
            simulateMaterial.SetFloat(_EmissionRate, emissionRate);
            simulateMaterial.SetInt(_ContinuousEmitCount, continuousEmitCount);
            simulateMaterial.SetFloat(_ContinuousEmissionWindowStart, emissionWindowStart);
            simulateMaterial.SetInt(_DistanceEmitCount, distanceEmitCount);
            simulateMaterial.SetFloat(_EmissionTimeAfterStep, stepEnd);
            simulateMaterial.SetFloat(_EmissionStartDelay, startDelay);
            simulateMaterial.SetFloat(
                _EmissionDuration, Mathf.Max(0.05f, emissionDuration));
            simulateMaterial.SetInt(_EmissionLooping, emissionLooping ? 1 : 0);
            simulateMaterial.SetVector(_BurstCounts0, new Vector4(
                stepBurstCounts[0], stepBurstCounts[1], stepBurstCounts[2], stepBurstCounts[3]));
            simulateMaterial.SetVector(_BurstCounts1, new Vector4(
                stepBurstCounts[4], stepBurstCounts[5], stepBurstCounts[6], stepBurstCounts[7]));
            simulateMaterial.SetVector(_BurstAges0, new Vector4(
                stepBurstAges[0], stepBurstAges[1], stepBurstAges[2], stepBurstAges[3]));
            simulateMaterial.SetVector(_BurstAges1, new Vector4(
                stepBurstAges[4], stepBurstAges[5], stepBurstAges[6], stepBurstAges[7]));
            simulateMaterial.SetInt(_SimulationTick, unchecked((int)simulationTick));
            simulateMaterial.SetInt(_ForceOverLifetimeEnabled, forceOverLifetimeEnabled ? 1 : 0);
            simulateMaterial.SetInt(_ForceOverLifetimeSpace, (int)forceOverLifetimeSpace);
            simulateMaterial.SetInt(_ForceOverLifetimeRandomized, forceOverLifetimeRandomized ? 1 : 0);
            simulateMaterial.SetInt(
                _VelocityOverLifetimeEnabled, velocityOverLifetimeEnabled ? 1 : 0);
            simulateMaterial.SetInt(
                _VelocityOverLifetimeSpace, (int)velocityOverLifetimeSpace);
            simulateMaterial.SetInt(
                _VelocityOverLifetimeSpeedModifierEnabled,
                velocityOverLifetimeSpeedModifierEnabled ? 1 : 0);
            simulateMaterial.SetInt(
                _LimitVelocityEnabled,
                limitVelocityOverLifetimeEnabled ? 1 : 0);
            simulateMaterial.SetInt(
                _LimitVelocitySeparateAxes,
                limitVelocityOverLifetimeSeparateAxes ? 1 : 0);
            simulateMaterial.SetInt(
                _LimitVelocitySpace,
                (int)limitVelocityOverLifetimeSpace);
            simulateMaterial.SetFloat(
                _LimitVelocityDampen,
                Mathf.Clamp01(limitVelocityOverLifetimeDampen));
            simulateMaterial.SetInt(
                _LimitVelocityMultiplyDragBySize,
                limitVelocityMultiplyDragBySize ? 1 : 0);
            simulateMaterial.SetInt(
                _LimitVelocityMultiplyDragByVelocity,
                limitVelocityMultiplyDragByVelocity ? 1 : 0);
            simulateMaterial.SetInt(
                _InheritVelocityEnabled,
                inheritVelocityEnabled ? 1 : 0);
            simulateMaterial.SetInt(
                _InheritVelocityMode,
                (int)inheritVelocityMode);
            simulateMaterial.SetInt(
                _LifetimeByEmitterSpeedEnabled,
                lifetimeByEmitterSpeedEnabled ? 1 : 0);
            simulateMaterial.SetVector(
                _LifetimeByEmitterSpeedRange,
                new Vector4(
                    lifetimeByEmitterSpeedRange.x,
                    lifetimeByEmitterSpeedRange.y,
                    0f,
                    0f));
            simulateMaterial.SetInt(_ColorOverLifetimeMode, (int)colorOverLifetimeMode);
            simulateMaterial.SetInt(_ColorBySpeedEnabled, colorBySpeedEnabled ? 1 : 0);
            simulateMaterial.SetInt(_ColorBySpeedMode, (int)colorBySpeedMode);
            simulateMaterial.SetVector(
                _ColorBySpeedRange,
                new Vector4(colorBySpeedRange.x, colorBySpeedRange.y, 0f, 0f));
            simulateMaterial.SetInt(_SizeBySpeedEnabled, sizeBySpeedEnabled ? 1 : 0);
            simulateMaterial.SetVector(
                _SizeBySpeedRange,
                new Vector4(sizeBySpeedRange.x, sizeBySpeedRange.y, 0f, 0f));
            simulateMaterial.SetInt(
                _RotationBySpeedEnabled, rotationBySpeedEnabled ? 1 : 0);
            simulateMaterial.SetVector(
                _RotationBySpeedRange,
                new Vector4(
                    rotationBySpeedRange.x,
                    rotationBySpeedRange.y,
                    0f,
                    0f));

            Vector3 dirInitW = initialDirectionWS.sqrMagnitude > 1e-6f ? initialDirectionWS.normalized : transform.forward;
            Vector3 dirInitSim = (simulationSpace == SimulationSpace.World) ? dirInitW : transform.InverseTransformDirection(dirInitW);
            simulateMaterial.SetVector(_InitialDir, new Vector4(dirInitSim.x, dirInitSim.y, dirInitSim.z, 0));

            simulateMaterial.SetInt(_ShapeType, (int)shapeType);
            simulateMaterial.SetInt(_ShapeEmitFrom, (int)shapeEmitFrom);
            simulateMaterial.SetInt(_AlignToDirection, alignToDirection ? 1 : 0);
            simulateMaterial.SetFloat(_ShapeRadiusThickness, Mathf.Clamp01(shapeRadiusThickness));
            simulateMaterial.SetFloat(_ShapeConeArcRad,
                Mathf.Clamp(shapeConeArcDeg, 0f, 360f) * Mathf.Deg2Rad);

            float avgScale = (shapeLocalScale.x + shapeLocalScale.y + shapeLocalScale.z) / 3f;

            // 根据shapeType设置对应的参数
            switch (shapeType)
            {
                case ShapeTypeGPU.Sphere:
                case ShapeTypeGPU.Hemisphere:
                    simulateMaterial.SetFloat(_ShapeSphereRadius, Mathf.Max(0f, shapeSphereRadius * avgScale));
                    break;

                case ShapeTypeGPU.Cone:
                    simulateMaterial.SetFloat(_ShapeConeAngleRad, shapeConeAngle * Mathf.Deg2Rad);
                    float coneRadiusScaled = shapeConeRadius * 0.5f * (shapeLocalScale.x + shapeLocalScale.y);
                    float coneLengthScaled = shapeConeLength * shapeLocalScale.z;
                    simulateMaterial.SetFloat(_ShapeConeRadius, coneRadiusScaled);
                    simulateMaterial.SetFloat(_ShapeConeLength, coneLengthScaled);
                    break;

                case ShapeTypeGPU.Donut:
                    simulateMaterial.SetFloat(_ShapeDonutRadius, Mathf.Max(0f, shapeDonutRadius * avgScale));
                    simulateMaterial.SetFloat(_ShapeDonutThickness, Mathf.Max(0f, shapeDonutThickness * avgScale));
                    break;

                case ShapeTypeGPU.Box:
                    Vector3 boxSizeScaled = Vector3.Scale(shapeBoxSize, shapeLocalScale);
                    simulateMaterial.SetVector(_ShapeBoxSize, new Vector4(boxSizeScaled.x, boxSizeScaled.y, boxSizeScaled.z, 0));
                    break;

                case ShapeTypeGPU.Circle:
                    simulateMaterial.SetFloat(_ShapeCircleRadius, Mathf.Max(0f, shapeCircleRadius * avgScale));
                    break;

                case ShapeTypeGPU.Edge:
                    simulateMaterial.SetFloat(
                        _ShapeEdgeLength,
                        Mathf.Max(0f, shapeEdgeLength * shapeLocalScale.x));
                    break;

                case ShapeTypeGPU.Rectangle:
                    Vector2 rectSizeScaled = new Vector2(
                        shapeRectangleSize.x * shapeLocalScale.x,
                        shapeRectangleSize.y * shapeLocalScale.y
                    );
                    simulateMaterial.SetVector(_ShapeRectangleSize, new Vector4(rectSizeScaled.x, rectSizeScaled.y, 0, 0));
                    break;
            }

            Quaternion q = Quaternion.Euler(shapeLocalRotationEuler);
            Vector3 rightL = q * Vector3.right;
            Vector3 upL = q * Vector3.up;
            Vector3 fwdL = q * Vector3.forward;
            Vector3 posL = shapeLocalPosition;
            simulateMaterial.SetVector(_ShapePosL, new Vector4(posL.x, posL.y, posL.z, 0));
            simulateMaterial.SetVector(_ShapeRightL, new Vector4(rightL.x, rightL.y, rightL.z, 0));
            simulateMaterial.SetVector(_ShapeUpL, new Vector4(upL.x, upL.y, upL.z, 0));
            simulateMaterial.SetVector(_ShapeFwdL, new Vector4(fwdL.x, fwdL.y, fwdL.z, 0));

            simulateMaterial.SetMatrix(_EmitterLocalToWorld, transform.localToWorldMatrix);
            simulateMaterial.SetMatrix(_EmitterWorldToLocal, transform.worldToLocalMatrix);
            simulateMaterial.SetVector(_EmitterPreviousPositionWS,
                new Vector4(
                    emitterPreviousPositionWS.x,
                    emitterPreviousPositionWS.y,
                    emitterPreviousPositionWS.z,
                    0f));
            simulateMaterial.SetVector(_EmitterCurrentPositionWS,
                new Vector4(
                    emitterCurrentPositionWS.x,
                    emitterCurrentPositionWS.y,
                    emitterCurrentPositionWS.z,
                    0f));
            simulateMaterial.SetVector(_EmitterPreviousVelocityWS,
                new Vector4(
                    emitterVelocityBeforeStepWS.x,
                    emitterVelocityBeforeStepWS.y,
                    emitterVelocityBeforeStepWS.z,
                    0f));
            simulateMaterial.SetVector(_EmitterVelocityWS,
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
            CoreUtils.DrawFullScreen(cmd, simulateMaterial, null, 0);

            ping = dst;
            simulationTick++;
        }

        internal void Render(CommandBuffer cmd, Camera camera)
        {
            if (!renderEnabled || renderMaterial == null) return;

            renderMaterial.SetTexture(_CurPosLife, posLife[ping]);
            renderMaterial.SetTexture(_CurVelSize, velSize[ping]);
            renderMaterial.SetTexture(_CurColor,   colorRT[ping]);
            renderMaterial.SetTexture(_CurRotationPhase, rotationPhaseRT[ping]);
            renderMaterial.SetTexture("_BaseMap", baseMap != null ? baseMap : Texture2D.whiteTexture);

            renderMaterial.SetInt(_GridSize, gridSize);
            renderMaterial.SetInt(_MaxParticles, maxParticles);
            renderMaterial.SetInt(_SimulationSpace, (int)simulationSpace);
            renderMaterial.SetFloat(_StartLifetime, startLifetime);
            renderMaterial.SetFloat(_StartLifetimeMin, startLifetimeMin);
            renderMaterial.SetInt(_RandomizeStartLifetime, randomizeStartLifetime ? 1 : 0);
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
            renderMaterial.SetFloat(_RotationOverLifetime, rotationOverLifetime);
            renderMaterial.SetFloat(_RotationOverLifetimeMin, rotationOverLifetimeMin);
            renderMaterial.SetInt(_RandomizeRotationOverLifetime,
                randomizeRotationOverLifetime ? 1 : 0);
            bool useRotationIntegralLUT = rotationOverLifetimeIntegralLUT != null;
            Texture2D selectedRotationIntegralLUT = useRotationIntegralLUT
                ? rotationOverLifetimeIntegralLUT
                : CurveLUTBuilder.GetDefaultZeroLUT();
            renderMaterial.SetTexture(
                _RotationOverLifetimeIntegralLUT, selectedRotationIntegralLUT);
            renderMaterial.SetFloat(
                _RotationOverLifetimeIntegralLUTInvWidth,
                InverseTextureWidth(selectedRotationIntegralLUT));
            renderMaterial.SetInt(
                _UseRotationOverLifetimeIntegralLUT, useRotationIntegralLUT ? 1 : 0);
            renderMaterial.SetMatrix(_EmitterLocalToWorld_Render, transform.localToWorldMatrix);
            renderMaterial.SetVector(_CameraRightWS, camera.transform.right);
            renderMaterial.SetVector(_CameraUpWS, camera.transform.up);

            // camera position & velocity
            Vector3 camPos = camera.transform.position;
            Vector3 camVel = Vector3.zero;
            float dt = Application.isPlaying ? Time.deltaTime : (1f/60f);
            if (prevCamPosValid && dt > 1e-6f) camVel = (camPos - prevCamPos) / dt;
            prevCamPos = camPos; prevCamPosValid = true;
            renderMaterial.SetVector(_CameraPosWS, camPos);
            renderMaterial.SetVector(_CameraVelWS, camVel);

            // renderer params
            renderMaterial.SetInt(_RenderMode, (int)renderMode);
            renderMaterial.SetInt(_RenderAlignment, (int)renderAlignment);
            renderMaterial.SetInt(_AllowRoll, allowRoll ? 1 : 0);
            renderMaterial.SetFloat(_NormalDirection, normalDirection);
            renderMaterial.SetVector(_Pivot, pivot);
            renderMaterial.SetFloat(_LenScale, stretchedLengthScale);
            renderMaterial.SetFloat(_VelScale, stretchedVelocityScale);
            renderMaterial.SetFloat(_CamVelScale, stretchedCameraVelocityScale);
            renderMaterial.SetInt(_Freeform, freeformStretching ? 1 : 0);
            renderMaterial.SetInt(_RotateWithStretch, rotateWithStretchDirection ? 1 : 0);
            renderMaterial.SetFloat(_MinAlphaCull, minAlphaCull);

            // draw quads: 6 verts per particle
            int vertexCount = maxParticles * 6;
            cmd.DrawProcedural(Matrix4x4.identity, renderMaterial, 0, MeshTopology.Triangles, vertexCount, 1);
        }
    }
}
