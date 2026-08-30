Shader "GPUParticles/UnlitBillboardURP"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _MinAlphaCull("MinAlphaCull", Range(0,1)) = 0.001
    }
    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "GPUParticlesBillboard"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // MRT state from simulation
            TEXTURE2D(_CurPosLife); SAMPLER(sampler_CurPosLife);
            TEXTURE2D(_CurVelSize); SAMPLER(sampler_CurVelSize);
            TEXTURE2D(_CurColor);   SAMPLER(sampler_CurColor);
            TEXTURE2D(_CurRotationPhase); SAMPLER(sampler_CurRotationPhase);
            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_StartLifetimeLUT);
            TEXTURE2D(_StartSizeLUT); SAMPLER(sampler_StartSizeLUT);
            TEXTURE2D(_StartSizeYLUT); SAMPLER(sampler_StartSizeYLUT);
            TEXTURE2D(_SizeLUT); SAMPLER(sampler_SizeLUT);
            TEXTURE2D(_SizeYLUT); SAMPLER(sampler_SizeYLUT);
            TEXTURE2D(_SizeBySpeedLUT); SAMPLER(sampler_SizeBySpeedLUT);
            TEXTURE2D(_SizeBySpeedYLUT); SAMPLER(sampler_SizeBySpeedYLUT);
            TEXTURE2D(_StartRotationLUT);
            TEXTURE2D(_RotationOverLifetimeIntegralLUT);
            SAMPLER(sampler_RotationOverLifetimeIntegralLUT);
            TEXTURE2D(_LifetimeByEmitterSpeedLUT);
            SAMPLER(sampler_LifetimeByEmitterSpeedLUT);
            TEXTURE2D(_TextureSheetFrameOverTimeLUT);
            SAMPLER(sampler_TextureSheetFrameOverTimeLUT);
            TEXTURE2D(_TextureSheetStartFrameLUT);
            SAMPLER(sampler_TextureSheetStartFrameLUT);

            // Keep render-side lifetime reconstruction in the same measured
            // Unity 2022.3 Shuriken tick phase as simulation.
            static const float START_LIFETIME_CURVE_TICK_PHASE = 0.2;

            CBUFFER_START(UnityPerMaterial)
                int   _GridSize;
                int   _MaxParticles;
                int   _SimulationSpace;
                float _StartLifetime;
                float _StartLifetimeMin;
                int   _RandomizeStartLifetime;
                int   _StartLifetimeMode;
                float _StartLifetimeLUTInvWidth;
                float _EmissionTimeAfterStep;
                float _EmissionStartDelay;
                float _EmissionDuration;
                int   _EmissionLooping;
                float _DeltaTime;
                int   _UseSeparateSizeAxes;
                int   _StartSize3D;
                int   _SizeOverLifetimeSeparateAxes;
                int   _SizeBySpeedSeparateAxes;
                float _StartSize;
                float _StartSizeMin;
                int   _StartSizeMode;
                float _StartSizeLUTInvWidth;
                float _StartSizeY;
                float _StartSizeYMin;
                int   _StartSizeYMode;
                float _StartSizeYLUTInvWidth;
                float _SizeLUTInvWidth;
                float _SizeYLUTInvWidth;
                int   _SizeBySpeedEnabled;
                float2 _SizeBySpeedRange;
                float _SizeBySpeedLUTInvWidth;
                float _SizeBySpeedYLUTInvWidth;
                int   _LifetimeByEmitterSpeedEnabled;
                float2 _LifetimeByEmitterSpeedRange;
                float _LifetimeByEmitterSpeedLUTInvWidth;
                float _StartRotation;
                float _StartRotationMin;
                int   _RandomizeStartRotation;
                int   _StartRotationMode;
                float _StartRotationLUTInvWidth;
                float _RotationOverLifetime;
                float _RotationOverLifetimeMin;
                int   _RandomizeRotationOverLifetime;
                float _RotationOverLifetimeIntegralLUTInvWidth;
                int   _UseRotationOverLifetimeIntegralLUT;

                int   _TextureSheetEnabled;
                int   _TextureSheetTilesX;
                int   _TextureSheetTilesY;
                int   _TextureSheetAnimation;
                int   _TextureSheetTimeMode;
                int   _TextureSheetRowMode;
                int   _TextureSheetRowIndex;
                int   _TextureSheetCycleCount;
                float _TextureSheetFps;
                float2 _TextureSheetSpeedRange;
                float _TextureSheetFrameLUTInvWidth;
                float _TextureSheetStartLUTInvWidth;

                // emitter transforms (for LS->WS)
                float4x4 _EmitterLocalToWorld;
                float4x4 _ParticleScaleWorld;

                // camera
                float3 _CameraPosWS; float _pad0;
                float3 _CameraVelWS; float _pad1;
                float3 _CameraRightWS; float _pad2;
                float3 _CameraUpWS;    float _pad3;

                // renderer params
                int   _RenderMode;          // 0 Billbd, 1 Horiz, 2 Vert, 3 Stretch
                int   _RenderAlignment;     // 0 View,1 Facing,2 World,3 Local,4 Velocity
                int   _AllowRoll;
                float _NormalDirection;     // 0..1

                float2 _Pivot;
                float  _LenScale;
                float  _VelScale;
                float  _CamVelScale;

                int    _Freeform;
                int    _RotateWithStretch;  // placeholder; unlit不做UV旋转
                float  _MinAlphaCull;
                float  _padLast;

                int    _ScreenSpaceSizeClampEnabled;
                float  _MinParticleSize;
                float  _MaxParticleSize;
                float  _padScreenSizeClamp;
            CBUFFER_END

            // handy
            struct VOut{ float4 posHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 col:COLOR; };

            uint HashU32(uint x)
            {
                x += (x << 10u); x ^= (x >> 6u);
                x += (x << 3u);  x ^= (x >> 11u);
                x += (x << 15u);
                return x;
            }

            float Hash01(uint x)
            {
                return (HashU32(x) & 0x00FFFFFFu) / 16777216.0;
            }

            float RandomRange(uint id, uint salt, int randomized, float minimum, float maximum)
            {
                return randomized != 0 ? lerp(minimum, maximum, Hash01(id ^ salt)) : maximum;
            }

            float LUTCoordinate(float normalizedPosition, float inverseWidth)
            {
                return saturate(normalizedPosition) * (1.0 - inverseWidth) +
                       0.5 * inverseWidth;
            }

            float BirthSystemTime(float particleAge)
            {
                float activeTime = max(
                    0.0,
                    _EmissionTimeAfterStep - particleAge -
                    _EmissionStartDelay);
                float duration = max(0.05, _EmissionDuration);
                if (_EmissionLooping == 0)
                {
                    return saturate(activeTime / duration);
                }

                return frac(activeTime / duration);
            }

            float StartLifetimeAtBirth(uint id, float particleAge)
            {
                if (_StartLifetimeMode == 0) // Constant
                {
                    return max(0.001, _StartLifetime);
                }

                float randomValue = Hash01(id ^ 0x68E31DA4u);
                if (_StartLifetimeMode == 3) // TwoConstants
                {
                    return max(
                        0.001,
                        lerp(
                            _StartLifetimeMin,
                            _StartLifetime,
                            randomValue));
                }

                float birthActiveTime = max(
                    0.0,
                    _EmissionTimeAfterStep - particleAge -
                    _EmissionStartDelay);
                float sampleDeltaTime = max(1e-6, _DeltaTime);
                float sampledBirthTime =
                    ceil(max(0.0, birthActiveTime - 1e-6) /
                         sampleDeltaTime) * sampleDeltaTime +
                    sampleDeltaTime * START_LIFETIME_CURVE_TICK_PHASE;
                float duration = max(0.05, _EmissionDuration);
                float systemTime = _EmissionLooping != 0
                    ? frac(sampledBirthTime / duration)
                    : saturate(sampledBirthTime / duration);
                float x = LUTCoordinate(
                    systemTime,
                    _StartLifetimeLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartLifetimeLUT,
                    sampler_LifetimeByEmitterSpeedLUT,
                    float2(x, 0.75), 0).r;
                if (_StartLifetimeMode != 2) // Curve
                {
                    return max(0.001, maximum);
                }

                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartLifetimeLUT,
                    sampler_LifetimeByEmitterSpeedLUT,
                    float2(x, 0.25), 0).r;
                return max(
                    0.001,
                    lerp(minimum, maximum, randomValue));
            }

            float StartRotationAtBirth(uint id, float particleAge)
            {
                if (_StartRotationMode == 0) // Constant
                {
                    return _StartRotation;
                }

                float randomValue = Hash01(id ^ 0x165667B1u);
                if (_StartRotationMode == 3) // TwoConstants
                {
                    return lerp(
                        _StartRotationMin,
                        _StartRotation,
                        randomValue);
                }

                float x = LUTCoordinate(
                    BirthSystemTime(particleAge),
                    _StartRotationLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartRotationLUT,
                    sampler_RotationOverLifetimeIntegralLUT,
                    float2(x, 0.75), 0).r;
                if (_StartRotationMode != 2) // Curve
                {
                    return maximum;
                }

                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartRotationLUT,
                    sampler_RotationOverLifetimeIntegralLUT,
                    float2(x, 0.25), 0).r;
                return lerp(minimum, maximum, randomValue);
            }

            float StartSizeXAtBirth(uint id, float particleAge)
            {
                if (_StartSizeMode == 0)
                {
                    return _StartSize;
                }

                float randomValue = Hash01(id ^ 0x1B56C4E9u);
                if (_StartSizeMode == 3)
                {
                    return lerp(_StartSizeMin, _StartSize, randomValue);
                }

                float x = LUTCoordinate(
                    BirthSystemTime(particleAge),
                    _StartSizeLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartSizeLUT,
                    sampler_StartSizeLUT,
                    float2(x, 0.75), 0).r;
                if (_StartSizeMode != 2)
                {
                    return maximum;
                }
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartSizeLUT,
                    sampler_StartSizeLUT,
                    float2(x, 0.25), 0).r;
                return lerp(minimum, maximum, randomValue);
            }

            float StartSizeYAtBirth(uint id, float particleAge)
            {
                if (_StartSizeYMode == 0)
                {
                    return _StartSizeY;
                }

                uint salt = _StartSize3D != 0
                    ? 0xC13FA9A9u
                    : 0x1B56C4E9u;
                float randomValue = Hash01(id ^ salt);
                if (_StartSizeYMode == 3)
                {
                    return lerp(_StartSizeYMin, _StartSizeY, randomValue);
                }

                float x = LUTCoordinate(
                    BirthSystemTime(particleAge),
                    _StartSizeYLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartSizeYLUT,
                    sampler_StartSizeYLUT,
                    float2(x, 0.75), 0).r;
                if (_StartSizeYMode != 2)
                {
                    return maximum;
                }
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartSizeYLUT,
                    sampler_StartSizeYLUT,
                    float2(x, 0.25), 0).r;
                return lerp(minimum, maximum, randomValue);
            }

            float SizeOverLifetimeX(uint id, float normalizedAge)
            {
                float x = LUTCoordinate(normalizedAge, _SizeLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _SizeLUT, sampler_SizeLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _SizeLUT, sampler_SizeLUT,
                    float2(x, 0.75), 0).r;
                return lerp(
                    minimum,
                    maximum,
                    Hash01(id ^ 0x91E10DA5u));
            }

            float SizeOverLifetimeY(uint id, float normalizedAge)
            {
                float x = LUTCoordinate(normalizedAge, _SizeYLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _SizeYLUT, sampler_SizeYLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _SizeYLUT, sampler_SizeYLUT,
                    float2(x, 0.75), 0).r;
                uint salt = _SizeOverLifetimeSeparateAxes != 0
                    ? 0xA24BAED4u
                    : 0x91E10DA5u;
                return lerp(minimum, maximum, Hash01(id ^ salt));
            }

            float SizeBySpeedX(uint id, float speedPosition)
            {
                float x = LUTCoordinate(
                    speedPosition, _SizeBySpeedLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _SizeBySpeedLUT, sampler_SizeBySpeedLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _SizeBySpeedLUT, sampler_SizeBySpeedLUT,
                    float2(x, 0.75), 0).r;
                return lerp(
                    minimum,
                    maximum,
                    Hash01(id ^ 0xD192ED03u));
            }

            float SizeBySpeedY(uint id, float speedPosition)
            {
                float x = LUTCoordinate(
                    speedPosition, _SizeBySpeedYLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _SizeBySpeedYLUT, sampler_SizeBySpeedYLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _SizeBySpeedYLUT, sampler_SizeBySpeedYLUT,
                    float2(x, 0.75), 0).r;
                uint salt = _SizeBySpeedSeparateAxes != 0
                    ? 0xB5297A4Du
                    : 0xD192ED03u;
                return lerp(minimum, maximum, Hash01(id ^ salt));
            }

            float SpeedRangePosition(float speed, float2 speedRange)
            {
                float width = speedRange.y - speedRange.x;
                if (width <= 1e-6)
                {
                    return speed > speedRange.x ? 1.0 : 0.0;
                }
                return saturate((speed - speedRange.x) / width);
            }

            float2 BillboardSize(
                uint id,
                float particleAge,
                float normalizedAge,
                float particleSpeed,
                float currentSizeX)
            {
                if (_UseSeparateSizeAxes == 0)
                {
                    return currentSizeX.xx;
                }

                float sizeX = StartSizeXAtBirth(id, particleAge) *
                    SizeOverLifetimeX(id, normalizedAge);
                float sizeY = StartSizeYAtBirth(id, particleAge) *
                    SizeOverLifetimeY(id, normalizedAge);
                if (_SizeBySpeedEnabled != 0)
                {
                    float speedPosition = SpeedRangePosition(
                        particleSpeed,
                        _SizeBySpeedRange);
                    sizeX *= SizeBySpeedX(id, speedPosition);
                    sizeY *= SizeBySpeedY(id, speedPosition);
                }
                return float2(sizeX, sizeY);
            }

            float SampleRotationIntegral(uint id, float normalizedAge)
            {
                float x = LUTCoordinate(
                    normalizedAge, _RotationOverLifetimeIntegralLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _RotationOverLifetimeIntegralLUT,
                    sampler_RotationOverLifetimeIntegralLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _RotationOverLifetimeIntegralLUT,
                    sampler_RotationOverLifetimeIntegralLUT,
                    float2(x, 0.75), 0).r;
                return lerp(minimum, maximum, Hash01(id ^ 0xD3A2646Cu));
            }

            float LifetimeByEmitterSpeedMultiplier(
                uint id,
                float3 birthEmitterVelocityWS)
            {
                if (_LifetimeByEmitterSpeedEnabled == 0)
                {
                    return 1.0;
                }

                float speedPosition = SpeedRangePosition(
                    length(birthEmitterVelocityWS),
                    _LifetimeByEmitterSpeedRange);
                float x = LUTCoordinate(
                    speedPosition,
                    _LifetimeByEmitterSpeedLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _LifetimeByEmitterSpeedLUT,
                    sampler_LifetimeByEmitterSpeedLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _LifetimeByEmitterSpeedLUT,
                    sampler_LifetimeByEmitterSpeedLUT,
                    float2(x, 0.75), 0).r;
                return max(
                    0.0,
                    lerp(minimum, maximum, Hash01(id ^ 0x94D049BBu)));
            }

            float SampleTextureSheetFrame(uint id, float normalizedPosition)
            {
                float x = LUTCoordinate(
                    normalizedPosition, _TextureSheetFrameLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _TextureSheetFrameOverTimeLUT,
                    sampler_TextureSheetFrameOverTimeLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _TextureSheetFrameOverTimeLUT,
                    sampler_TextureSheetFrameOverTimeLUT,
                    float2(x, 0.75), 0).r;
                return lerp(minimum, maximum, Hash01(id ^ 0xC2B2AE35u));
            }

            float SampleTextureSheetStartFrame(uint id)
            {
                // Unity 2022.3 samples Start Frame curves at t=0 when the
                // particle is born; this X coordinate does not advance by age.
                float x = LUTCoordinate(0.0, _TextureSheetStartLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _TextureSheetStartFrameLUT,
                    sampler_TextureSheetStartFrameLUT,
                    float2(x, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _TextureSheetStartFrameLUT,
                    sampler_TextureSheetStartFrameLUT,
                    float2(x, 0.75), 0).r;
                return lerp(minimum, maximum, Hash01(id ^ 0x27D4EB2Fu));
            }

            int TextureSheetSequenceFrame(
                uint id,
                float normalizedAge,
                float particleAge,
                float particleSpeed)
            {
                int columns = max(1, _TextureSheetTilesX);
                int rows = max(1, _TextureSheetTilesY);
                int sequenceFrameCount = _TextureSheetAnimation == 0
                    ? columns * rows
                    : columns;

                float startPosition = saturate(SampleTextureSheetStartFrame(id));
                int startFrame = min(
                    (int)floor(startPosition * sequenceFrameCount),
                    sequenceFrameCount - 1);

                int animationFrame;
                if (_TextureSheetTimeMode == 2) // FPS
                {
                    animationFrame = (int)floor(
                        max(0.0, particleAge) * max(0.0, _TextureSheetFps));
                }
                else
                {
                    float curvePosition = _TextureSheetTimeMode == 1
                        ? SpeedRangePosition(particleSpeed, _TextureSheetSpeedRange)
                        : normalizedAge;
                    float progress = saturate(
                        SampleTextureSheetFrame(id, curvePosition));
                    int cycleCount = max(1, _TextureSheetCycleCount);
                    int cycleFrameCount = sequenceFrameCount * cycleCount;
                    animationFrame = min(
                        (int)floor(progress * cycleFrameCount),
                        cycleFrameCount - 1);
                }

                return (animationFrame + startFrame) % sequenceFrameCount;
            }

            float2 TextureSheetUV(
                uint id,
                float2 baseUV,
                float normalizedAge,
                float particleAge,
                float particleSpeed)
            {
                if (_TextureSheetEnabled == 0)
                {
                    return baseUV;
                }

                int columns = max(1, _TextureSheetTilesX);
                int rows = max(1, _TextureSheetTilesY);
                int sequenceFrame = TextureSheetSequenceFrame(
                    id, normalizedAge, particleAge, particleSpeed);
                int column = sequenceFrame % columns;
                int rowFromTop;
                if (_TextureSheetAnimation == 0) // Whole Sheet
                {
                    rowFromTop = sequenceFrame / columns;
                }
                else if (_TextureSheetRowMode == 1) // Random
                {
                    rowFromTop = min(
                        (int)floor(Hash01(id ^ 0x9E3779B9u) * rows),
                        rows - 1);
                }
                else
                {
                    // MeshIndex uses this Custom fallback because Mesh particle
                    // rendering is not supported by this renderer.
                    rowFromTop = clamp(_TextureSheetRowIndex, 0, rows - 1);
                }

                int rowFromBottom = rows - 1 - rowFromTop;
                return (baseUV + float2(column, rowFromBottom)) /
                       float2(columns, rows);
            }

            float3 Ortho(float3 v){ return normalize( any(abs(v) > 0.0) ? (abs(v.z)<0.999?cross(v,float3(0,0,1)):cross(v,float3(0,1,0))) : float3(1,0,0) ); }

            float2 IndexToUV(uint idx, int grid)
            {
                int x = idx % grid;
                int y = idx / grid;
                return (float2(x,y) + 0.5) / grid;
            }

            void ToWS(float3 posSim, float3 velSim, out float3 posWS, out float3 velWS)
            {
                if (_SimulationSpace==1) { posWS=posSim; velWS=velSim; }
                else {
                    posWS = mul(_EmitterLocalToWorld, float4(posSim,1)).xyz;
                    velWS = mul(_EmitterLocalToWorld, float4(velSim,0)).xyz;
                }
            }

            void BuildCameraBasis(out float3 cr, out float3 cu, out float3 cf)
            {
                cf = normalize( cross(_CameraRightWS, _CameraUpWS) ); // ensure orthogonal
                if (_AllowRoll!=0) { cr = normalize(_CameraRightWS); cu = normalize(_CameraUpWS); return; }
                // lock roll: rebuild with world up
                float3 up = float3(0,1,0);
                cr = normalize(cross(up, cf));
                cu = normalize(cross(cf, cr));
            }

            void Basis_Billboard_View(out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 cr,cu,cf; BuildCameraBasis(cr,cu,cf);
                Right=cr; Up=cu; Normal=cf;
            }

            void Basis_Billboard_Facing(float3 P, out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 toCam = normalize(_CameraPosWS - P);
                if (any(isnan(toCam)) || length(toCam)<1e-6)
                { Basis_Billboard_View(Right,Up,Normal); return; }
                float3 upW = float3(0,1,0);
                Right = normalize(cross(upW, toCam));
                if (length(Right)<1e-6) { Basis_Billboard_View(Right,Up,Normal); return; }
                Up    = normalize(cross(toCam, Right));
                Normal= toCam;
            }

            void Basis_Billboard_World(out float3 Right, out float3 Up, out float3 Normal)
            { Right=float3(1,0,0); Up=float3(0,1,0); Normal=normalize(cross(Right,Up)); }

            void Basis_Billboard_Local(out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 Rx = normalize(_EmitterLocalToWorld._m00_m10_m20);
                float3 Uy = normalize(_EmitterLocalToWorld._m01_m11_m21);
                Right=Rx; Up=Uy; Normal=normalize(cross(Right,Up));
            }

            void Basis_Billboard_Velocity(float3 V, out float3 Right, out float3 Up, out float3 Normal)
            {
                Normal = normalize(V);
                if (length(Normal)<1e-6) { Basis_Billboard_View(Right,Up,Normal); return; }
                Right = Ortho(Normal);
                Up    = normalize(cross(Normal, Right));
            }

            // stretched bases
            void Basis_Stretched_Classic(float3 V, out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 cr,cu,cf; BuildCameraBasis(cr,cu,cf);
                Normal = cf;
                // project V onto camera plane
                float3 T = V - dot(V, Normal) * Normal;
                if (length(T)<1e-6) T = cu; // fallback
                T = normalize(T);
                Right = normalize(cross(Normal, T));
                Up    = T;
            }

            void Basis_Stretched_Freeform(float3 P, float3 V, out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 view = normalize(_CameraPosWS - P);
                float3 T = normalize(V);
                if (length(T)<1e-6) { Basis_Stretched_Classic(V, Right, Up, Normal); return; }
                Right = normalize(cross(view, T));
                if (length(Right)<1e-6) Right = Ortho(T);
                Normal= normalize(cross(Right, T));
                Up    = T;
            }

            // quad corner from vertex id (0..5)
            int CornerFromTriVertex(uint triV)
            {
                // 0: (0,1,2) -> 0,1,2; 1: (0,2,3) -> 0,2,3
                const int map[6] = {0,1,2, 0,2,3};
                return map[triV];
            }

            float ProjectedAxisScreenWidthFraction(
                float3 centerWS,
                float3 axisWS,
                float axisLength)
            {
                if (abs(axisLength) <= 1e-8)
                {
                    return 0.0;
                }

                float4 centerHCS = TransformWorldToHClip(centerWS);
                float4 endpointHCS = TransformWorldToHClip(
                    centerWS + axisWS * axisLength);
                if (abs(centerHCS.w) <= 1e-8 ||
                    abs(endpointHCS.w) <= 1e-8)
                {
                    return 0.0;
                }

                float2 deltaNDC =
                    endpointHCS.xy / endpointHCS.w -
                    centerHCS.xy / centerHCS.w;
                float inverseAspect =
                    _ScreenParams.y / max(1.0, _ScreenParams.x);
                return length(
                    float2(deltaNDC.x, deltaNDC.y * inverseAspect)) *
                    0.5;
            }

            float ScreenSpaceSizeClampScale(
                float3 centerWS,
                float3 rightWS,
                float3 upWS,
                float width,
                float height)
            {
                if (_ScreenSpaceSizeClampEnabled == 0)
                {
                    return 1.0;
                }

                float projectedSize = max(
                    ProjectedAxisScreenWidthFraction(
                        centerWS, rightWS, width),
                    ProjectedAxisScreenWidthFraction(
                        centerWS, upWS, height));
                if (projectedSize <= 1e-8)
                {
                    return 1.0;
                }

                float minimum = saturate(_MinParticleSize);
                float maximum = max(minimum, saturate(_MaxParticleSize));
                return clamp(projectedSize, minimum, maximum) /
                       projectedSize;
            }

            VOut Vert(uint vid:SV_VertexID)
            {
                VOut o;

                uint quadId = vid / 6;
                uint tv     = vid % 6;
                int corner  = CornerFromTriVertex(tv);

                if (quadId >= (uint)_MaxParticles)
                {
                    o.posHCS = float4(0,0,0,0);
                    o.uv = 0; o.col = 0;
                    return o;
                }

                // sample particle
                float2 tuv = IndexToUV(quadId, _GridSize);
                float4 posLife = SAMPLE_TEXTURE2D_LOD(_CurPosLife, sampler_CurPosLife, tuv, 0);
                float4 velSize = SAMPLE_TEXTURE2D_LOD(_CurVelSize, sampler_CurVelSize, tuv, 0);
                float4 pcol    = SAMPLE_TEXTURE2D_LOD(_CurColor,   sampler_CurColor,   tuv, 0);
                float4 moduleState = SAMPLE_TEXTURE2D_LOD(
                    _CurRotationPhase, sampler_CurRotationPhase, tuv, 0);
                float rotationBySpeedPhase = moduleState.r;

                float3 posWS, velWS; ToWS(posLife.xyz, velSize.xyz, posWS, velWS);

                // kill if dead
                if (posLife.w <= 0.0 || pcol.a <= _MinAlphaCull)
                {
                    o.posHCS = float4(0,0,0,0);
                    o.uv = 0; o.col = 0; return o;
                }

                bool startLifetimeUsesAgeState =
                    _StartLifetimeMode == 1 || _StartLifetimeMode == 2;
                float particleAge = startLifetimeUsesAgeState
                    ? max(0.0, posLife.w - 1.0)
                    : 0.0;
                float particleStartLifetime = StartLifetimeAtBirth(
                    quadId,
                    particleAge) *
                    LifetimeByEmitterSpeedMultiplier(
                        quadId,
                        moduleState.gba);
                if (!startLifetimeUsesAgeState)
                {
                    particleAge = max(
                        0.0,
                        particleStartLifetime - posLife.w);
                }
                float normalizedAge = saturate(
                    particleAge / max(1e-6, particleStartLifetime));

                // choose basis
                float3 Right, Up, Normal;
                if (_RenderMode == 3) // Stretched
                {
                    if (_Freeform!=0)  Basis_Stretched_Freeform(posWS, velWS, Right, Up, Normal);
                    else                Basis_Stretched_Classic (velWS, Right, Up, Normal);
                }
                else if (_RenderMode == 1) // Horizontal
                {
                    Right = float3(1, 0, 0);
                    Up    = float3(0, 0, 1);
                    Normal= float3(0, 1, 0);
                }
                else if (_RenderMode == 2) // Vertical
                {
                    float3 up = float3(0, 1, 0);
                    float3 cameraForward = normalize(cross(_CameraRightWS, _CameraUpWS));
                    Right = normalize(cross(up, cameraForward));
                    if (length(Right) < 1e-6) Right = float3(1, 0, 0);
                    Up    = up;
                    Normal= normalize(cross(Right, Up));
                }
                else // Billboard
                {
                    if (_RenderAlignment==0)      Basis_Billboard_View(Right,Up,Normal);
                    else if (_RenderAlignment==1) Basis_Billboard_Facing(posWS, Right,Up,Normal);
                    else if (_RenderAlignment==2) Basis_Billboard_World(Right,Up,Normal);
                    else if (_RenderAlignment==3) Basis_Billboard_Local(Right,Up,Normal);
                    else                          Basis_Billboard_Velocity(velWS, Right,Up,Normal);
                }

                // Shuriken applies Main.scalingMode to billboard dimensions even
                // in world simulation space. The basis itself remains camera/world
                // aligned; only its selected hierarchy/local scale is applied.
                float3 RenderRight = mul(
                    _ParticleScaleWorld,
                    float4(Right, 0.0)).xyz;
                float3 RenderUp = mul(
                    _ParticleScaleWorld,
                    float4(Up, 0.0)).xyz;

                // size (W/L) & pivot
                float2 sizeXY = BillboardSize(
                    quadId,
                    particleAge,
                    normalizedAge,
                    length(velWS),
                    velSize.w);
                float W = sizeXY.x;
                float H = sizeXY.y;
                float L = (_RenderMode==3)
                    ? max(
                        1e-4,
                        H * (_LenScale + length(velWS)*_VelScale +
                             length(_CameraVelWS)*_CamVelScale))
                    : H;
                float screenClampWidth = W;
                float screenClampHeight = (_RenderMode == 3) ? L : H;
                if (_RenderMode == 1 || _RenderMode == 2)
                {
                    const float horizontalVerticalScale = 0.70710678;
                    screenClampWidth *= horizontalVerticalScale;
                    screenClampHeight *= horizontalVerticalScale;
                }
                float screenSizeScale = ScreenSpaceSizeClampScale(
                    posWS,
                    RenderRight,
                    RenderUp,
                    screenClampWidth,
                    screenClampHeight);
                W *= screenSizeScale;
                H *= screenSizeScale;
                L *= screenSizeScale;

                float2 quadUV[4] = { float2(0,0), float2(1,0), float2(1,1), float2(0,1) };
                float2 localXY[4]= { float2(-0.5,-0.5), float2(0.5,-0.5), float2(0.5,0.5), float2(-0.5,0.5) };
                
                float2 local;
                if (_RenderMode == 1 || _RenderMode == 2)
                {
                    float scale = 0.70710678; // 1/sqrt(2)
                    local = localXY[corner] * float2(W, H) * scale;
                }
                else
                {
                    local = localXY[corner] * float2(W, (_RenderMode==3)?L:H);
                }

                float2 pivotOff = float2(
                    _Pivot.x * W,
                    (_RenderMode==3) ? (_Pivot.y * L) : (_Pivot.y * H));
                local -= pivotOff;

                if (_RenderMode != 3)
                {
                    float particleStartRotation = StartRotationAtBirth(
                        quadId,
                        particleAge);
                    float particleRotationOverLifetime = RandomRange(
                        quadId, 0xD3A2646Cu, _RandomizeRotationOverLifetime,
                        _RotationOverLifetimeMin, _RotationOverLifetime);
                    float angle;
                    if (_UseRotationOverLifetimeIntegralLUT != 0)
                    {
                        angle = particleStartRotation +
                                SampleRotationIntegral(quadId, normalizedAge) *
                                particleStartLifetime;
                    }
                    else
                    {
                        angle = particleStartRotation +
                                particleRotationOverLifetime * particleAge;
                    }
                    angle += rotationBySpeedPhase;
                    float s = sin(angle);
                    float c = cos(angle);
                    local = float2(local.x * c - local.y * s, local.x * s + local.y * c);
                }

                float3 wpos =
                    posWS + RenderRight * local.x + RenderUp * local.y;

                o.posHCS = TransformWorldToHClip(wpos);
                o.uv = TextureSheetUV(
                    quadId,
                    quadUV[corner],
                    normalizedAge,
                    particleAge,
                    length(velSize.xyz));
                o.col = pcol;
                return o;
            }

            half4 Frag(VOut i) : SV_Target
            {
                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return baseCol * i.col;
            }
            ENDHLSL
        }
    }
}
