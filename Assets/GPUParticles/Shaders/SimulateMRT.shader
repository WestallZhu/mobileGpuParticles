Shader "Hidden/GPUParticles/SimulateMRT"
{
    Properties{}
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(uint vid : SV_VertexID)
            {
                Varyings o;
                // Fullscreen triangle
                float2 uv = float2((vid << 1) & 2, vid & 2);
                o.positionHCS = float4(uv * 2.0 - 1.0, 0, 1);
                o.uv = uv;
                return o;
            }

            // --- Current state ---
            TEXTURE2D(_CurPosLife);      SAMPLER(sampler_CurPosLife);
            TEXTURE2D(_CurVelSize);      SAMPLER(sampler_CurVelSize);
            TEXTURE2D(_CurColor);        SAMPLER(sampler_CurColor);
            TEXTURE2D(_CurRotationPhase); SAMPLER(sampler_CurRotationPhase);
            TEXTURE2D(_StartLifetimeLUT);
            TEXTURE2D(_StartSpeedLUT);
            TEXTURE2D(_StartSizeLUT);
            TEXTURE2D(_StartColorLUT);
            TEXTURE2D(_GravityModifierLUT);
            TEXTURE2D(_GradLUT);         SAMPLER(sampler_GradLUT);
            TEXTURE2D(_SizeLUT);         SAMPLER(sampler_SizeLUT);
            TEXTURE2D(_ColorBySpeedLUT); SAMPLER(sampler_ColorBySpeedLUT);
            TEXTURE2D(_SizeBySpeedLUT);  SAMPLER(sampler_SizeBySpeedLUT);
            TEXTURE2D(_RotationBySpeedLUT); SAMPLER(sampler_RotationBySpeedLUT);
            TEXTURE2D(_ForceOverLifetimeLUT); SAMPLER(sampler_ForceOverLifetimeLUT);
            TEXTURE2D(_VelocityOverLifetimeLUT); SAMPLER(sampler_VelocityOverLifetimeLUT);
            TEXTURE2D(_VelocityOverLifetimeOrbitalLUT);
            SAMPLER(sampler_VelocityOverLifetimeOrbitalLUT);
            TEXTURE2D(_VelocityOverLifetimeOrbitalOffsetLUT);
            SAMPLER(sampler_VelocityOverLifetimeOrbitalOffsetLUT);
            TEXTURE2D(_LimitVelocityLUT); SAMPLER(sampler_LimitVelocityLUT);
            TEXTURE2D(_InheritVelocityLUT); SAMPLER(sampler_InheritVelocityLUT);
            TEXTURE2D(_LifetimeByEmitterSpeedLUT);
            SAMPLER(sampler_LifetimeByEmitterSpeedLUT);
            TEXTURE2D(_ShapeArcSpeedIntegralLUT);
            TEXTURE2D(_NoiseStrengthLUT);
            TEXTURE2D(_NoiseAmountsLUT);
            TEXTURE2D(_NoiseRemapLUT);
            TEXTURE2D(_CollisionParametersLUT);

            // Unity 2022.3 Shuriken samples Main curves just after the
            // emission tick boundary; this is the measured tick phase.
            static const float START_LIFETIME_CURVE_TICK_PHASE = 0.2;

            // --- Params ---
            CBUFFER_START(UnityPerMaterial)
                int     _GridSize;
                int     _MaxParticles;
                float   _DeltaTime;
                float   _StartLifetime;
                float   _StartLifetimeMin;
                int     _RandomizeStartLifetime;
                int     _StartLifetimeMode;
                float   _StartLifetimeLUTInvWidth;
                int     _RingBufferMode;     // 0 Disabled, 1 Pause, 2 Loop
                float2  _RingBufferLoopRange;
                float   _PadRingBuffer;
                float   _StartSpeed;
                float   _StartSpeedMin;
                int     _RandomizeStartSpeed;
                int     _StartSpeedMode;
                float   _StartSpeedLUTInvWidth;
                float   _StartSize;
                float   _StartSizeMin;
                int     _RandomizeStartSize;
                int     _StartSizeMode;
                float   _StartSizeLUTInvWidth;
                float4  _StartColor;
                float4  _StartColorMin;
                int     _RandomizeStartColor;
                int     _StartColorMode;
                float   _StartColorLUTInvWidth;
                float3  _GravityWS;          // NOTE: contains space-correct gravity (WS or LS)
                float3  _GravityWSMin;
                int     _RandomizeGravityModifier;
                float3  _GravityBase;
                int     _GravityModifierMode;
                float   _GravityModifierLUTInvWidth;
                int     _SimulationSpace;    // 0 Local, 1 World, 2 Custom
                uint    _EmitStart;
                uint    _EmitCount;
                float   _EmitCarryPrev;
                float   _EmissionRate;
                uint    _ContinuousEmitCount;
                float   _ContinuousEmissionWindowStart;
                uint    _DistanceEmitCount;
                float   _EmissionTimeAfterStep;
                float   _EmissionStartDelay;
                float   _EmissionDuration;
                int     _EmissionLooping;
                float4  _BurstCounts0;
                float4  _BurstCounts1;
                float4  _BurstAges0;
                float4  _BurstAges1;
                uint    _SimulationTick;
                int     _ForceOverLifetimeEnabled;
                int     _ForceOverLifetimeSpace;       // 0 Local, 1 World
                int     _ForceOverLifetimeRandomized;
                int     _VelocityOverLifetimeEnabled;
                int     _VelocityOverLifetimeSpace;    // 0 Local, 1 World
                int     _VelocityOverLifetimeSpeedModifierEnabled;
                int     _VelocityOverLifetimeOrbitalEnabled;
                int     _LimitVelocityEnabled;
                int     _LimitVelocitySeparateAxes;
                int     _LimitVelocitySpace;            // 0 Local, 1 World
                float   _LimitVelocityDampen;
                int     _LimitVelocityMultiplyDragBySize;
                int     _LimitVelocityMultiplyDragByVelocity;
                float   _LimitVelocityLUTInvWidth;
                int     _InheritVelocityEnabled;
                int     _InheritVelocityMode;           // 0 Initial, 1 Current
                float   _InheritVelocityLUTInvWidth;
                int     _LifetimeByEmitterSpeedEnabled;
                float2  _LifetimeByEmitterSpeedRange;
                float   _LifetimeByEmitterSpeedLUTInvWidth;
                int     _NoiseEnabled;
                int     _NoiseSeparateAxes;
                float   _NoiseStrengthLUTInvWidth;
                float   _NoiseAmountsLUTInvWidth;
                int     _NoiseRemapEnabled;
                float   _NoiseRemapLUTInvWidth;
                float   _NoiseFrequency;
                int     _NoiseDamping;
                int     _NoiseQuality;       // 0 Low, 1 Medium, 2 High
                int     _NoiseOctaveCount;
                float   _NoiseOctaveMultiplier;
                float   _NoiseOctaveScale;
                int     _CollisionEnabled;
                int     _CollisionPlaneCount;
                float   _CollisionParametersLUTInvWidth;
                float   _CollisionMinKillSpeed;
                float   _CollisionMaxKillSpeed;
                float   _CollisionRadiusScale;
                float   _CollisionParticleScaleWS;
                float4  _CollisionPlanes[6];
                int     _ColorOverLifetimeMode;
                int     _ColorBySpeedEnabled;
                int     _ColorBySpeedMode;
                int     _SizeBySpeedEnabled;
                float2  _ColorBySpeedRange;
                float2  _SizeBySpeedRange;
                int     _RotationBySpeedEnabled;
                float2  _RotationBySpeedRange;
                float   _RotationBySpeedLUTInvWidth;
                float   _GradLUTInvWidth;
                float   _SizeLUTInvWidth;
                float   _ForceOverLifetimeLUTInvWidth;
                float   _VelocityOverLifetimeLUTInvWidth;
                float   _VelocityOverLifetimeOrbitalLUTInvWidth;
                float   _VelocityOverLifetimeOrbitalOffsetLUTInvWidth;
                float   _ColorBySpeedLUTInvWidth;
                float   _SizeBySpeedLUTInvWidth;

                // Initial direction already in simulation space
                float3  _InitialDir;
                float   _Pad0;

                // ----- Shape (generic) -----
                int     _ShapeType;          // 0..7 shapes, 8 point emitter (Shape disabled)
                int     _ShapeEmitFrom;      // 0 Volume, 1 Surface, 2 Base, 3 Edge
                int     _AlignToDirection;   // orientation metadata; not velocity
                float   _ShapeRandomDirectionAmount;
                float   _ShapeSphericalDirectionAmount;
                float3  _ShapeRandomPositionScale;

                // Cone
                float   _ShapeConeAngleRad;
                float   _ShapeConeRadius;
                float   _ShapeConeLength;
                float   _ShapeRadiusThickness; // 0 shell edge, 1 full
                float   _ShapeConeArcRad;    // 0..2PI
                int     _ShapeArcMode;       // 0 Random, 1 Loop, 2 PingPong, 3 BurstSpread
                float   _ShapeArcSpread;     // normalized step within the configured arc
                int     _ShapeArcSpeedMode;  // ParticleSystemCurveMode
                float   _ShapeArcSpeedIntegralLUTInvWidth;

                // Box
                float3  _ShapeBoxSize;       // full extents in local units
                float   _Pad1;

                // Sphere / Hemisphere
                float   _ShapeSphereRadius;  // scaled radius
                float   _Pad2; float _Pad3; float _Pad4;

                // Donut
                float   _ShapeDonutRadius;   // 主圆环半径
                float   _ShapeDonutThickness; // 环的厚度
                float   _Pad9; float _Pad10;

                // Circle
                float   _ShapeCircleRadius;  // 圆形半径
                float   _Pad11; float _Pad12; float _Pad13;

                // Edge
                float   _ShapeEdgeLength;     // 边缘长度
                float   _Pad14; float _Pad15; float _Pad16;

                // Rectangle
                float2  _ShapeRectangleSize;  // 矩形尺寸 (width, height)
                float   _Pad17; float _Pad18;

                // Shape local transform (relative to emitter local)
                float3  _ShapePosL;
                float   _Pad5;
                float3  _ShapeRightL;
                float   _Pad6;
                float3  _ShapeUpL;
                float   _Pad7;
                float3  _ShapeFwdL;
                float   _Pad8;

                // Emitter transforms
                float4x4 _EmitterLocalToWorld;
                float4x4 _EmitterWorldToLocal;
                float4x4 _SimulationLocalToWorld;
                float4x4 _SimulationWorldToLocal;
                float4x4 _EmitterToSimulationDirection;
                float4x4 _SimulationToEmitterDirection;
                float4x4 _WorldToSimulationDirection;
                float4x4 _SimulationToWorldDirection;
                float4x4 _ShapeLocalToWorld;
                float3 _EmitterPreviousPositionWS;
                float _Pad19;
                float3 _EmitterCurrentPositionWS;
                float _Pad20;
                float3 _EmitterPreviousVelocityWS;
                float _Pad21;
                float3 _EmitterVelocityWS;
                float _Pad22;
            CBUFFER_END

            // local hash helpers (rename to avoid conflict with URP Random.hlsl Hash)
            uint HashU32(uint x) {
                x += (x << 10u); x ^= (x >> 6u);
                x += (x << 3u);  x ^= (x >> 11u);
                x += (x << 15u);
                return x;
            }
            float Hash01(uint x) {
                return (HashU32(x) & 0x00FFFFFFu) / 16777216.0;
            }
            float2 Hash02(uint x) {
                return float2(Hash01(x * 1664525u + 1013904223u), Hash01(x * 22695477u + 1u));
            }
            float3 Hash03(uint x) {
                return float3(Hash01(x*747796405u+2891336453u), Hash01(x*2891336453u+1181783497u), Hash01(x*1181783497u+747796405u));
            }

            float RandomRange(uint id, uint salt, int randomized, float minimum, float maximum)
            {
                return randomized != 0 ? lerp(minimum, maximum, Hash01(id ^ salt)) : maximum;
            }

            struct FragOut {
                float4 PosLife : SV_Target0;
                float4 VelSize : SV_Target1;
                float4 Color   : SV_Target2;
                // X is rotation phase; YZW is Initial-mode birth emitter velocity.
                float4 ModuleState : SV_Target3;
            };

            bool InEmit(uint id, uint start, uint count, uint cap)
            {
                if (count == 0) return false;
                uint end = start + count;
                return (id >= start && id < end) || (end > cap && id < (end - cap));
            }

            uint EmitOrdinal(uint id, uint start, uint cap)
            {
                return (id + cap - start) % cap;
            }

            float BurstComponent(float4 first, float4 second, int index)
            {
                if (index == 0) return first.x;
                if (index == 1) return first.y;
                if (index == 2) return first.z;
                if (index == 3) return first.w;
                if (index == 4) return second.x;
                if (index == 5) return second.y;
                if (index == 6) return second.z;
                return second.w;
            }

            float SpawnAgeThisFrame(uint emitOrdinal)
            {
                if (emitOrdinal < _ContinuousEmitCount)
                {
                    if (_EmissionRate <= 1e-6)
                    {
                        return 0.0;
                    }

                    float spawnTime = _ContinuousEmissionWindowStart +
                        ((float)emitOrdinal + 1.0 - _EmitCarryPrev) / _EmissionRate;
                    return clamp(_DeltaTime - spawnTime, 0.0, _DeltaTime);
                }

                uint distanceOrdinal = emitOrdinal - _ContinuousEmitCount;
                if (distanceOrdinal < _DistanceEmitCount)
                {
                    // Unlike Rate over Time, Shuriken creates distance particles at
                    // the current-step emitter position with zero sub-frame age.
                    return 0.0;
                }

                uint burstOrdinal = distanceOrdinal - _DistanceEmitCount;
                uint cumulativeCount = 0u;
                [unroll]
                for (int burstIndex = 0; burstIndex < 8; burstIndex++)
                {
                    uint burstCount = (uint)round(BurstComponent(
                        _BurstCounts0, _BurstCounts1, burstIndex));
                    if (burstOrdinal < cumulativeCount + burstCount)
                    {
                        return clamp(BurstComponent(
                            _BurstAges0, _BurstAges1, burstIndex), 0.0, _DeltaTime);
                    }
                    cumulativeCount += burstCount;
                }

                return 0.0;
            }

            float BirthSystemTime(float particleAge)
            {
                float activeTime = max(
                    0.0,
                    _EmissionTimeAfterStep - particleAge - _EmissionStartDelay);
                float duration = max(0.05, _EmissionDuration);
                if (_EmissionLooping == 0)
                {
                    return saturate(activeTime / duration);
                }

                return frac(activeTime / duration);
            }

            float CurrentSystemTime()
            {
                float activeTime = max(
                    0.0,
                    _EmissionTimeAfterStep - _EmissionStartDelay);
                float duration = max(0.05, _EmissionDuration);
                if (_EmissionLooping == 0)
                {
                    return saturate(activeTime / duration);
                }

                return frac(activeTime / duration);
            }

            float RingBufferParticleAge(
                float totalAge,
                float particleStartLifetime)
            {
                float lifetime = max(0.001, particleStartLifetime);
                totalAge = max(0.0, totalAge);
                if (_RingBufferMode == 1) // Pause Until Replaced
                {
                    return min(totalAge, lifetime);
                }
                if (_RingBufferMode != 2) // Disabled
                {
                    return totalAge;
                }

                float2 loopRange = saturate(_RingBufferLoopRange);
                float loopStart = min(loopRange.x, loopRange.y) * lifetime;
                float loopEnd = max(loopRange.x, loopRange.y) * lifetime;
                float loopLength = loopEnd - loopStart;
                if (totalAge < loopEnd)
                {
                    return totalAge;
                }
                if (loopLength <= 1e-6)
                {
                    return loopStart;
                }
                return loopStart + fmod(
                    max(0.0, totalAge - loopStart),
                    loopLength);
            }

            float RingBufferNormalizedAge(
                float totalAge,
                float particleStartLifetime)
            {
                return saturate(
                    RingBufferParticleAge(
                        totalAge,
                        particleStartLifetime) /
                    max(0.001, particleStartLifetime));
            }

            float StartColorSystemTime(float particleAge)
            {
                float systemTime = BirthSystemTime(particleAge);
                // Shuriken assigns the final LUT bin to the next loop. A fixed
                // bin also keeps the birth color stable when frame time changes.
                const float loopBoundaryWindow = 1.0 / 256.0;
                return systemTime >= 1.0 - loopBoundaryWindow
                    ? 0.0
                    : systemTime;
            }

            // --- Helpers ---
            float3 Ortho(float3 v)
            {
                return normalize(abs(v.z) < 0.999 ? cross(v, float3(0,0,1)) : cross(v, float3(0,1,0)));
            }

            // Sample direction within a cone around axis (uniform solid angle)
            float3 SampleDirCone(float3 axis, float angRad, float2 u)
            {
                axis = normalize(axis);
                float cosA = cos(angRad);
                float cosTheta = lerp(cosA, 1.0, u.x);
                float sinTheta = sqrt(saturate(1.0 - cosTheta*cosTheta));
                float phi = 6.28318530718 * u.y;
                float3 uvec = Ortho(axis);
                float3 vvec = normalize(cross(axis, uvec));
                return normalize(uvec * (cos(phi)*sinTheta) + vvec * (sin(phi)*sinTheta) + axis * cosTheta);
            }

            // Random point on disk (inner..outer)
            float2 SampleDisk(float2 u, float innerR, float outerR)
            {
                float r2 = lerp(innerR*innerR, outerR*outerR, u.x);
                float r = sqrt(r2);
                float phi = 6.28318530718 * u.y;
                return float2(r*cos(phi), r*sin(phi));
            }

            float2 SampleDiskArc(float2 u, float innerR, float outerR, float arcRad)
            {
                float r2 = lerp(innerR * innerR, outerR * outerR, u.x);
                float r = sqrt(r2);
                float phi = clamp(arcRad, 0.0, 6.28318530718) * u.y;
                return float2(r * cos(phi), r * sin(phi));
            }

            // Sample inside a right cone with base at z=0 (radius R), apex at z=L along +Z
            void SampleConeVolume(float3 u3, float R, float L, out float3 pLocal)
            {
                float z = L * pow(u3.x, 1.0/3.0);                 // uniform along volume
                float Rz = R * (1.0 - z / max(L, 1e-5));          // radius at z
                float2 d = SampleDisk(u3.yz, 0.0, Rz);
                pLocal = float3(d.x, d.y, z);
            }

            // Sample inside a box of size (sx,sy,sz), centered at origin
            float3 SampleBoxVolume(float3 u3, float3 size)
            {
                return (u3 - 0.5) * size;
            }

            // Sample surface of box with area-weighted face selection
            float3 SampleBoxSurface(float3 u3, float3 size, out float3 nLocal)
            {
                float3 half = 0.5 * size;
                float ax = size.y * size.z; // area of +/-X faces
                float ay = size.x * size.z; // +/-Y
                float az = size.x * size.y; // +/-Z
                float sum = ax + ay + az;
                float r = u3.x * sum;
                float2 uv = u3.yz;

                if (r < ax) {
                    // +/-X
                    bool pos = (uv.x < 0.5);
                    float y = (uv.y - 0.5) * size.y;
                    float z = (frac(uv.x * 2.0) - 0.5) * size.z;
                    nLocal = float3(pos?1:-1, 0, 0);
                    return float3(pos?half.x:-half.x, y, z);
                }
                r -= ax;
                if (r < ay) {
                    // +/-Y
                    bool pos = (uv.x < 0.5);
                    float x = (uv.y - 0.5) * size.x;
                    float z = (frac(uv.x * 2.0) - 0.5) * size.z;
                    nLocal = float3(0, pos?1:-1, 0);
                    return float3(x, pos?half.y:-half.y, z);
                }
                // +/-Z
                bool pos = (uv.x < 0.5);
                float x = (uv.y - 0.5) * size.x;
                float y = (frac(uv.x * 2.0) - 0.5) * size.y;
                nLocal = float3(0,0,pos?1:-1);
                return float3(x, y, pos?half.z:-half.z);
            }

            // Sample the 12 box edges, weighted by edge length.
            float3 SampleBoxEdge(float u, float3 size)
            {
                float3 half = 0.5 * size;
                float xGroup = 4.0 * size.x;
                float yGroup = 4.0 * size.y;
                float zGroup = 4.0 * size.z;
                float total = max(1e-6, xGroup + yGroup + zGroup);
                float edgePosition = u * total;

                if (edgePosition < xGroup && size.x > 1e-6)
                {
                    float edge = edgePosition / size.x;
                    int edgeIndex = min(3, (int)floor(edge));
                    float x = (frac(edge) - 0.5) * size.x;
                    float y = (edgeIndex & 1) != 0 ? half.y : -half.y;
                    float z = (edgeIndex & 2) != 0 ? half.z : -half.z;
                    return float3(x, y, z);
                }

                edgePosition -= xGroup;
                if (edgePosition < yGroup && size.y > 1e-6)
                {
                    float edge = edgePosition / size.y;
                    int edgeIndex = min(3, (int)floor(edge));
                    float x = (edgeIndex & 1) != 0 ? half.x : -half.x;
                    float y = (frac(edge) - 0.5) * size.y;
                    float z = (edgeIndex & 2) != 0 ? half.z : -half.z;
                    return float3(x, y, z);
                }

                edgePosition -= yGroup;
                float safeSizeZ = max(1e-6, size.z);
                float edge = edgePosition / safeSizeZ;
                int edgeIndex = min(3, (int)floor(edge));
                float x = (edgeIndex & 1) != 0 ? half.x : -half.x;
                float y = (edgeIndex & 2) != 0 ? half.y : -half.y;
                float z = (frac(edge) - 0.5) * size.z;
                return float3(x, y, z);
            }

            // Uniform direction on unit sphere
            float3 SampleSphereDir(float2 u)
            {
                float z = 1.0 - 2.0 * u.x; // [-1,1]
                float phi = 6.28318530718 * u.y;
                float r = sqrt(saturate(1.0 - z*z));
                return float3(r*cos(phi), r*sin(phi), z);
            }
            // Uniform direction on hemisphere (local +Z)
            float3 SampleHemisphereDir(float2 u)
            {
                float z = u.x; // [0,1]
                float phi = 6.28318530718 * u.y;
                float r = sqrt(saturate(1.0 - z*z));
                return float3(r*cos(phi), r*sin(phi), z);
            }

            // radius with thickness [Ri,R], uniform volume
            float RadiusWithThickness(float u, float R, float thickness01)
            {
                float Ri = R * saturate(1.0 - thickness01);
                float R3 = R*R*R;
                float Ri3 = Ri*Ri*Ri;
                return pow(lerp(Ri3, R3, u), 1.0/3.0);
            }

            // Unity cone emitters diverge more as particles move away from the center line.
            float3 BuildConeVelocity(float2 radialOffset, float radiusAtSlice, float3 right, float3 up, float3 fwd, float angleRad)
            {
                float3 axis = normalize(fwd);
                float radialLen = length(radialOffset);
                if (radiusAtSlice <= 1e-6 || radialLen <= 1e-6 || angleRad <= 1e-6)
                {
                    return axis;
                }

                float3 radialDir = normalize(right * radialOffset.x + up * radialOffset.y);
                float radialRatio = saturate(radialLen / radiusAtSlice);
                float radialScale = tan(angleRad) * radialRatio;
                return normalize(axis + radialDir * radialScale);
            }

            // Shape scaling and particle scaling are independent in Shuriken.
            // Shape mode uses the full hierarchy for birth positions, then stores
            // them in a unit-scaled particle frame.
            float3 ToSimSpacePos(float3 pLocal)
            {
                float3 positionWS = mul(
                    _ShapeLocalToWorld,
                    float4(pLocal, 1.0)).xyz;
                if (_SimulationSpace == 1) // World
                {
                    return positionWS;
                }
                return mul(
                    _SimulationWorldToLocal,
                    float4(positionWS, 1.0)).xyz;
            }
            // Transform emitter-local vector (no translation) to world if needed
            float3 ToSimSpaceVec(float3 vLocal)
            {
                if (_SimulationSpace == 1) // World
                {
                    float4 ws = mul(_EmitterLocalToWorld, float4(vLocal,0));
                    return ws.xyz;
                }
                if (_SimulationSpace == 2) // Custom
                {
                    return mul(
                        _EmitterToSimulationDirection,
                        float4(vLocal, 0.0)).xyz;
                }
                return vLocal;
            }

            float3 ToSimSpaceSpawnVelocity(float3 velocityLocal)
            {
                if (_SimulationSpace != 2)
                {
                    return ToSimSpaceVec(velocityLocal);
                }

                float speed = length(velocityLocal);
                if (speed <= 1e-6)
                {
                    return 0.0;
                }

                float3 velocityWS = mul(
                    _EmitterLocalToWorld,
                    float4(velocityLocal, 0.0)).xyz;
                float3 velocityCustom = mul(
                    _SimulationWorldToLocal,
                    float4(velocityWS, 0.0)).xyz;
                return normalize(velocityCustom) * speed;
            }

            float3 SimPositionToEmitterLocal(float3 position)
            {
                if (_SimulationSpace == 1)
                {
                    return mul(_EmitterWorldToLocal, float4(position, 1.0)).xyz;
                }
                if (_SimulationSpace == 2)
                {
                    float3 positionWS = mul(
                        _SimulationLocalToWorld,
                        float4(position, 1.0)).xyz;
                    return mul(
                        _EmitterWorldToLocal,
                        float4(positionWS, 1.0)).xyz;
                }
                return position;
            }

            float3 WorldVectorToSimulationPositionSpace(float3 value)
            {
                if (_SimulationSpace == 1)
                {
                    return value;
                }
                return mul(
                    _SimulationWorldToLocal,
                    float4(value, 0.0)).xyz;
            }

            float3 ModuleVectorToSimSpace(float3 value, int moduleSpace)
            {
                if (moduleSpace == 1) // Module value is world-space.
                {
                    if (_SimulationSpace == 0)
                    {
                        return mul(_EmitterWorldToLocal, float4(value, 0.0)).xyz;
                    }
                    if (_SimulationSpace == 2)
                    {
                        return mul(
                            _WorldToSimulationDirection,
                            float4(value, 0.0)).xyz;
                    }
                    return value;
                }

                // Optional GPU-authored values can already be expressed in the
                // active custom simulation frame.
                if (moduleSpace == 2)
                {
                    return value;
                }

                // Module value is emitter-local.
                if (_SimulationSpace == 1)
                {
                    return mul(_EmitterLocalToWorld, float4(value, 0.0)).xyz;
                }
                if (_SimulationSpace == 2)
                {
                    return mul(
                        _EmitterToSimulationDirection,
                        float4(value, 0.0)).xyz;
                }
                return value;
            }

            float3 SimVectorToLimitAxisSpace(float3 value, int limitSpace)
            {
                // Separate-axis limits rotate their reference frame in the same
                // direction used to bring additive module vectors into simulation.
                return ModuleVectorToSimSpace(value, limitSpace);
            }

            float3 LimitAxisVectorToSimSpace(float3 value, int limitSpace)
            {
                if (limitSpace == 1) // Limit axes are world-space.
                {
                    if (_SimulationSpace == 0)
                    {
                        return mul(_EmitterLocalToWorld, float4(value, 0.0)).xyz;
                    }
                    if (_SimulationSpace == 2)
                    {
                        return mul(
                            _SimulationToWorldDirection,
                            float4(value, 0.0)).xyz;
                    }
                    return value;
                }

                if (limitSpace == 2)
                {
                    return value;
                }

                // Limit axes are emitter-local.
                if (_SimulationSpace == 1)
                {
                    return mul(_EmitterWorldToLocal, float4(value, 0.0)).xyz;
                }
                if (_SimulationSpace == 2)
                {
                    return mul(
                        _SimulationToEmitterDirection,
                        float4(value, 0.0)).xyz;
                }
                return value;
            }

            float LUTPosition(float position, float inverseWidth)
            {
                return saturate(position) * (1.0 - inverseWidth) +
                    0.5 * inverseWidth;
            }

            float PositiveRepeat(float value, float lengthValue)
            {
                return value - floor(value / lengthValue) * lengthValue;
            }

            float2 ShapeArcSpeedIntegralRows(float normalizedTime)
            {
                float lutPosition = LUTPosition(
                    normalizedTime,
                    _ShapeArcSpeedIntegralLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _ShapeArcSpeedIntegralLUT,
                    sampler_SizeLUT,
                    float2(lutPosition, 0.25),
                    0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _ShapeArcSpeedIntegralLUT,
                    sampler_SizeLUT,
                    float2(lutPosition, 0.75),
                    0).r;
                return float2(minimum, maximum);
            }

            float ShapeArcTravel(uint id, float particleAge)
            {
                float activeBirthTime = max(
                    0.0,
                    _EmissionTimeAfterStep - particleAge -
                    _EmissionStartDelay);
                float duration = max(0.05, _EmissionDuration);
                float completedSystemLoops = 0.0;
                float normalizedSystemTime;
                if (_EmissionLooping != 0)
                {
                    completedSystemLoops = floor(activeBirthTime / duration);
                    normalizedSystemTime = frac(activeBirthTime / duration);
                }
                else
                {
                    normalizedSystemTime = saturate(activeBirthTime / duration);
                }

                float2 partialIntegral = ShapeArcSpeedIntegralRows(
                    normalizedSystemTime);
                float2 fullIntegral = ShapeArcSpeedIntegralRows(1.0);
                float randomValue =
                    _ShapeArcSpeedMode == 2 || _ShapeArcSpeedMode == 3
                        ? Hash01(id ^ 0xB8D3A7E5u)
                        : 1.0;
                float integratedRevolutions = duration * lerp(
                    completedSystemLoops * fullIntegral.x +
                        partialIntegral.x,
                    completedSystemLoops * fullIntegral.y +
                        partialIntegral.y,
                    randomValue);
                return integratedRevolutions * 6.28318530718;
            }

            void ShapeArcBurstCoordinates(
                uint emitOrdinal,
                out uint groupOrdinal,
                out uint groupCount)
            {
                if (emitOrdinal < _ContinuousEmitCount)
                {
                    groupOrdinal = emitOrdinal;
                    groupCount = max(1u, _ContinuousEmitCount);
                    return;
                }

                uint distanceOrdinal = emitOrdinal - _ContinuousEmitCount;
                if (distanceOrdinal < _DistanceEmitCount)
                {
                    groupOrdinal = distanceOrdinal;
                    groupCount = max(1u, _DistanceEmitCount);
                    return;
                }

                uint burstOrdinal = distanceOrdinal - _DistanceEmitCount;
                uint cumulativeCount = 0u;
                [unroll]
                for (int burstIndex = 0; burstIndex < 8; burstIndex++)
                {
                    uint burstCount = (uint)round(BurstComponent(
                        _BurstCounts0,
                        _BurstCounts1,
                        burstIndex));
                    if (burstOrdinal < cumulativeCount + burstCount)
                    {
                        groupOrdinal = burstOrdinal - cumulativeCount;
                        groupCount = max(1u, burstCount);
                        return;
                    }
                    cumulativeCount += burstCount;
                }

                groupOrdinal = emitOrdinal;
                groupCount = max(1u, _EmitCount);
            }

            float ShapeArcAngle(
                uint id,
                uint emitOrdinal,
                float particleAge,
                float randomSample)
            {
                float arc = clamp(_ShapeConeArcRad, 0.0, 6.28318530718);
                if (arc <= 1e-6)
                {
                    return 0.0;
                }

                float angle;
                if (_ShapeArcMode == 0) // Random
                {
                    angle = arc * randomSample;
                }
                else if (_ShapeArcMode == 3) // BurstSpread
                {
                    uint groupOrdinal;
                    uint groupCount;
                    ShapeArcBurstCoordinates(
                        emitOrdinal,
                        groupOrdinal,
                        groupCount);
                    angle = groupCount > 1u
                        ? arc * (float)groupOrdinal /
                            (float)(groupCount - 1u)
                        : 0.0;
                }
                else
                {
                    float travel = ShapeArcTravel(id, particleAge);
                    if (_ShapeArcMode == 2) // PingPong
                    {
                        float phase = PositiveRepeat(travel, 2.0 * arc);
                        angle = arc - abs(phase - arc);
                    }
                    else // Loop
                    {
                        angle = PositiveRepeat(travel, arc);
                    }
                }

                float spread = saturate(_ShapeArcSpread);
                if (spread > 1e-6)
                {
                    float stepAngle = max(1e-6, arc * spread);
                    angle = floor((angle + 1e-7) / stepAngle) *
                        stepAngle;
                }
                return clamp(angle, 0.0, arc);
            }

            float NoiseLatticeValue(int3 cell, uint salt)
            {
                uint hash = (uint)cell.x * 0x8DA6B343u;
                hash ^= (uint)cell.y * 0xD8163841u;
                hash ^= (uint)cell.z * 0xCB1AB31Fu;
                return Hash01(hash ^ salt) * 2.0 - 1.0;
            }

            // Smooth value noise with analytic derivatives. Three offset scalar
            // fields are combined below as a divergence-free Curl field.
            float4 ValueNoiseDerivatives(float3 coordinate, uint salt)
            {
                int3 cell = (int3)floor(coordinate);
                float3 f = frac(coordinate);
                float3 blend = f * f * (3.0 - 2.0 * f);
                float3 blendDerivative = 6.0 * f * (1.0 - f);

                float n000 = NoiseLatticeValue(cell + int3(0, 0, 0), salt);
                float n100 = NoiseLatticeValue(cell + int3(1, 0, 0), salt);
                float n010 = NoiseLatticeValue(cell + int3(0, 1, 0), salt);
                float n110 = NoiseLatticeValue(cell + int3(1, 1, 0), salt);
                float n001 = NoiseLatticeValue(cell + int3(0, 0, 1), salt);
                float n101 = NoiseLatticeValue(cell + int3(1, 0, 1), salt);
                float n011 = NoiseLatticeValue(cell + int3(0, 1, 1), salt);
                float n111 = NoiseLatticeValue(cell + int3(1, 1, 1), salt);

                float nx00 = lerp(n000, n100, blend.x);
                float nx10 = lerp(n010, n110, blend.x);
                float nx01 = lerp(n001, n101, blend.x);
                float nx11 = lerp(n011, n111, blend.x);
                float nxy0 = lerp(nx00, nx10, blend.y);
                float nxy1 = lerp(nx01, nx11, blend.y);
                float value = lerp(nxy0, nxy1, blend.z);

                float derivativeX = blendDerivative.x * lerp(
                    lerp(n100 - n000, n110 - n010, blend.y),
                    lerp(n101 - n001, n111 - n011, blend.y),
                    blend.z);
                float derivativeY = blendDerivative.y * lerp(
                    lerp(n010 - n000, n110 - n100, blend.x),
                    lerp(n011 - n001, n111 - n101, blend.x),
                    blend.z);
                float derivativeZ = blendDerivative.z * (nxy1 - nxy0);
                return float4(
                    value,
                    derivativeX,
                    derivativeY,
                    derivativeZ);
            }

            float3 NoiseCurlLayer(float3 coordinate, int quality, uint salt)
            {
                float3 a = ValueNoiseDerivatives(
                    coordinate + float3(19.1, 7.7, 3.4),
                    salt ^ 0xA511E9B3u).yzw;

                if (quality <= 0)
                {
                    return float3(
                        a.y - a.z,
                        a.z - a.x,
                        a.x - a.y) * 0.65;
                }

                float3 b = ValueNoiseDerivatives(
                    coordinate + float3(5.2, 31.8, 11.6),
                    salt ^ 0x63D83595u).yzw;
                if (quality == 1)
                {
                    float3 c = a.zxy;
                    return float3(
                        c.y - b.z,
                        a.z - c.x,
                        b.x - a.y) * 0.55;
                }

                float3 cHigh = ValueNoiseDerivatives(
                    coordinate + float3(27.4, 13.5, 41.2),
                    salt ^ 0xB8D3A7E5u).yzw;
                return float3(
                    cHigh.y - b.z,
                    a.z - cHigh.x,
                    b.x - a.y) * 0.5;
            }

            float3 NoiseCurl(float3 coordinate)
            {
                float3 total = 0.0;
                float totalAmplitude = 0.0;
                float amplitude = 1.0;
                float frequency = max(0.0001, _NoiseFrequency);
                int octaveCount = clamp(_NoiseOctaveCount, 1, 4);
                [unroll]
                for (int octave = 0; octave < 4; octave++)
                {
                    if (octave < octaveCount)
                    {
                        total += NoiseCurlLayer(
                            coordinate * frequency,
                            _NoiseQuality,
                            0x9E3779B9u + (uint)octave * 0x85EBCA6Bu) *
                            amplitude;
                        totalAmplitude += amplitude;
                        amplitude *= max(0.0, _NoiseOctaveMultiplier);
                        frequency *= max(1.0, _NoiseOctaveScale);
                    }
                }
                return clamp(total / max(1e-5, totalAmplitude), -1.0, 1.0);
            }

            float3 NoiseStrength(uint id, float normalizedAge)
            {
                float x = LUTPosition(
                    normalizedAge, _NoiseStrengthLUTInvWidth);
                float3 minimum = SAMPLE_TEXTURE2D_LOD(
                    _NoiseStrengthLUT,
                    sampler_SizeLUT,
                    float2(x, 0.25),
                    0).rgb;
                float3 maximum = SAMPLE_TEXTURE2D_LOD(
                    _NoiseStrengthLUT,
                    sampler_SizeLUT,
                    float2(x, 0.75),
                    0).rgb;
                return float3(
                    lerp(minimum.x, maximum.x, Hash01(id ^ 0xD1B54A35u)),
                    lerp(minimum.y, maximum.y, Hash01(id ^ 0x94D049BBu)),
                    lerp(minimum.z, maximum.z, Hash01(id ^ 0x369DEA0Fu)));
            }

            float4 NoiseAmounts(uint id, float normalizedAge)
            {
                float x = LUTPosition(
                    normalizedAge, _NoiseAmountsLUTInvWidth);
                float4 minimum = SAMPLE_TEXTURE2D_LOD(
                    _NoiseAmountsLUT,
                    sampler_SizeLUT,
                    float2(x, 0.25),
                    0);
                float4 maximum = SAMPLE_TEXTURE2D_LOD(
                    _NoiseAmountsLUT,
                    sampler_SizeLUT,
                    float2(x, 0.75),
                    0);
                return float4(
                    lerp(minimum.x, maximum.x, Hash01(id ^ 0xDB4F0B91u)),
                    lerp(minimum.y, maximum.y, Hash01(id ^ 0xBBE05633u)),
                    lerp(minimum.z, maximum.z, Hash01(id ^ 0xA0F2EC75u)),
                    lerp(minimum.w, maximum.w, Hash01(id ^ 0x89E18285u)));
            }

            float3 RemapNoise(uint id, float3 value)
            {
                if (_NoiseRemapEnabled == 0)
                {
                    return value;
                }

                float3 position = saturate(value * 0.5 + 0.5);
                float3 lutX = float3(
                    LUTPosition(position.x, _NoiseRemapLUTInvWidth),
                    LUTPosition(position.y, _NoiseRemapLUTInvWidth),
                    LUTPosition(position.z, _NoiseRemapLUTInvWidth));
                float3 minimum = float3(
                    SAMPLE_TEXTURE2D_LOD(
                        _NoiseRemapLUT, sampler_SizeLUT,
                        float2(lutX.x, 0.25), 0).r,
                    SAMPLE_TEXTURE2D_LOD(
                        _NoiseRemapLUT, sampler_SizeLUT,
                        float2(lutX.y, 0.25), 0).g,
                    SAMPLE_TEXTURE2D_LOD(
                        _NoiseRemapLUT, sampler_SizeLUT,
                        float2(lutX.z, 0.25), 0).b);
                float3 maximum = float3(
                    SAMPLE_TEXTURE2D_LOD(
                        _NoiseRemapLUT, sampler_SizeLUT,
                        float2(lutX.x, 0.75), 0).r,
                    SAMPLE_TEXTURE2D_LOD(
                        _NoiseRemapLUT, sampler_SizeLUT,
                        float2(lutX.y, 0.75), 0).g,
                    SAMPLE_TEXTURE2D_LOD(
                        _NoiseRemapLUT, sampler_SizeLUT,
                        float2(lutX.z, 0.75), 0).b);
                return float3(
                    lerp(minimum.x, maximum.x, Hash01(id ^ 0xC2B2AE35u)),
                    lerp(minimum.y, maximum.y, Hash01(id ^ 0x27D4EB2Fu)),
                    lerp(minimum.z, maximum.z, Hash01(id ^ 0x165667B1u)));
            }

            float3 ParticleNoise(
                uint id,
                float3 position,
                float normalizedAge,
                out float4 amounts)
            {
                amounts = NoiseAmounts(id, normalizedAge);
                float scrollPhase = max(
                    0.0,
                    _EmissionTimeAfterStep - _EmissionStartDelay) *
                    amounts.w;
                float3 scrollDirection = normalize(float3(0.19, 0.53, 0.83));
                float3 field = NoiseCurl(
                    position + scrollDirection * scrollPhase);
                field = RemapNoise(id, field);
                float3 strength = NoiseStrength(id, normalizedAge);
                if (_NoiseDamping != 0)
                {
                    strength /= max(0.0001, _NoiseFrequency);
                }
                return field * strength;
            }

            float3 SimulationPositionToWorld(float3 position)
            {
                if (_SimulationSpace == 1)
                {
                    return position;
                }
                return mul(
                    _SimulationLocalToWorld,
                    float4(position, 1.0)).xyz;
            }

            float3 SimulationVectorToWorld(float3 value)
            {
                if (_SimulationSpace == 1)
                {
                    return value;
                }
                return mul(
                    _SimulationLocalToWorld,
                    float4(value, 0.0)).xyz;
            }

            float3 WorldPositionToSimulation(float3 position)
            {
                if (_SimulationSpace == 1)
                {
                    return position;
                }
                return mul(
                    _SimulationWorldToLocal,
                    float4(position, 1.0)).xyz;
            }

            float3 WorldVectorToSimulation(float3 value)
            {
                if (_SimulationSpace == 1)
                {
                    return value;
                }
                return mul(
                    _SimulationWorldToLocal,
                    float4(value, 0.0)).xyz;
            }

            float3 CollisionParameters(uint id, float normalizedAge)
            {
                float x = LUTPosition(
                    normalizedAge,
                    _CollisionParametersLUTInvWidth);
                float3 minimum = SAMPLE_TEXTURE2D_LOD(
                    _CollisionParametersLUT,
                    sampler_SizeLUT,
                    float2(x, 0.25),
                    0).rgb;
                float3 maximum = SAMPLE_TEXTURE2D_LOD(
                    _CollisionParametersLUT,
                    sampler_SizeLUT,
                    float2(x, 0.75),
                    0).rgb;
                return saturate(float3(
                    lerp(minimum.x, maximum.x, Hash01(id ^ 0x6E624EB7u)),
                    lerp(minimum.y, maximum.y, Hash01(id ^ 0x7383ED49u)),
                    lerp(minimum.z, maximum.z, Hash01(id ^ 0xDD49C23Bu))));
            }

            void ApplyPlaneCollisions(
                uint id,
                float3 positionBeforeStep,
                inout float3 position,
                inout float3 velocity,
                float particleSize,
                float particleStartLifetime,
                bool lifetimeUsesAgeState,
                inout float life,
                inout float normalizedAge,
                float stepDt)
            {
                float radius = max(0.0, particleSize) * 0.5 *
                    max(0.0, _CollisionRadiusScale) *
                    max(0.0, _CollisionParticleScaleWS);
                float3 previousPositionWS =
                    SimulationPositionToWorld(positionBeforeStep);
                float3 positionWS = SimulationPositionToWorld(position);
                float3 velocityWS = SimulationVectorToWorld(velocity);
                float3 parameters = CollisionParameters(id, normalizedAge);
                bool collided = false;

                [unroll]
                for (int planeIndex = 0; planeIndex < 6; planeIndex++)
                {
                    if (planeIndex < _CollisionPlaneCount && life > 0.0)
                    {
                        float4 plane = _CollisionPlanes[planeIndex];
                        float3 normal = plane.xyz;
                        float distance = dot(normal, positionWS) + plane.w;
                        float normalSpeed = dot(normal, velocityWS);

                        if (distance < radius && normalSpeed < 0.0)
                        {
                            float dampen = parameters.x;
                            float bounce = parameters.y;
                            velocityWS -= normal * normalSpeed * (1.0 + bounce);
                            velocityWS *= 1.0 - dampen;

                            // Shuriken restarts the collision tick from the prior
                            // position using the reflected velocity.
                            positionWS = previousPositionWS + velocityWS * stepDt;
                            distance = dot(normal, positionWS) + plane.w;
                            if (distance < radius)
                            {
                                positionWS += normal * (radius - distance);
                            }

                            float postCollisionSpeed = length(velocityWS);
                            if (postCollisionSpeed < _CollisionMinKillSpeed ||
                                postCollisionSpeed > _CollisionMaxKillSpeed)
                            {
                                life = 0.0;
                            }
                            else
                            {
                                float lifetimeLoss = parameters.z *
                                    particleStartLifetime;
                                if (lifetimeUsesAgeState)
                                {
                                    float totalParticleAge =
                                        max(0.0, life - 1.0) +
                                        lifetimeLoss;
                                    life = _RingBufferMode == 0 &&
                                           totalParticleAge + 1e-4 >=
                                               particleStartLifetime
                                        ? 0.0
                                        : totalParticleAge + 1.0;
                                    normalizedAge =
                                        RingBufferNormalizedAge(
                                            totalParticleAge,
                                            particleStartLifetime);
                                }
                                else
                                {
                                    life = max(0.0, life - lifetimeLoss);
                                    normalizedAge = saturate(
                                        1.0 - life /
                                        max(1e-5, particleStartLifetime));
                                }
                            }
                            collided = true;
                        }
                    }
                }

                if (collided)
                {
                    position = WorldPositionToSimulation(positionWS);
                    velocity = WorldVectorToSimulation(velocityWS);
                }
            }

            float3 ForceOverLifetime(uint id, float normalizedAge)
            {
                float lutPosition = LUTPosition(
                    normalizedAge, _ForceOverLifetimeLUTInvWidth);
                float3 minimum = SAMPLE_TEXTURE2D_LOD(
                    _ForceOverLifetimeLUT, sampler_ForceOverLifetimeLUT,
                    float2(lutPosition, 0.25), 0).rgb;
                float3 maximum = SAMPLE_TEXTURE2D_LOD(
                    _ForceOverLifetimeLUT, sampler_ForceOverLifetimeLUT,
                    float2(lutPosition, 0.75), 0).rgb;

                uint tick = _ForceOverLifetimeRandomized != 0 ? _SimulationTick : 0u;
                float3 randomValue = Hash03(id * 1597334677u + tick * 3812015801u + 0xA511E9B3u);
                return ModuleVectorToSimSpace(lerp(minimum, maximum, randomValue), _ForceOverLifetimeSpace);
            }

            float4 VelocityOverLifetimeParameters(uint id, float normalizedAge)
            {
                float lutPosition = LUTPosition(
                    normalizedAge, _VelocityOverLifetimeLUTInvWidth);
                float4 minimum = SAMPLE_TEXTURE2D_LOD(
                    _VelocityOverLifetimeLUT, sampler_VelocityOverLifetimeLUT,
                    float2(lutPosition, 0.25), 0);
                float4 maximum = SAMPLE_TEXTURE2D_LOD(
                    _VelocityOverLifetimeLUT, sampler_VelocityOverLifetimeLUT,
                    float2(lutPosition, 0.75), 0);
                float3 randomValue = Hash03(id * 2246822519u + 0x9E3779B9u);
                float3 linearVelocity = ModuleVectorToSimSpace(
                    lerp(minimum.rgb, maximum.rgb, randomValue),
                    _VelocityOverLifetimeSpace);
                float speedModifier = lerp(
                    minimum.a,
                    maximum.a,
                    Hash01(id ^ 0xD1B54A35u));
                return float4(linearVelocity, speedModifier);
            }

            float4 VelocityOverLifetimeOrbitalParameters(
                uint id,
                float normalizedAge)
            {
                float lutPosition = LUTPosition(
                    normalizedAge,
                    _VelocityOverLifetimeOrbitalLUTInvWidth);
                float4 minimum = SAMPLE_TEXTURE2D_LOD(
                    _VelocityOverLifetimeOrbitalLUT,
                    sampler_VelocityOverLifetimeOrbitalLUT,
                    float2(lutPosition, 0.25), 0);
                float4 maximum = SAMPLE_TEXTURE2D_LOD(
                    _VelocityOverLifetimeOrbitalLUT,
                    sampler_VelocityOverLifetimeOrbitalLUT,
                    float2(lutPosition, 0.75), 0);
                float3 randomValue = Hash03(
                    id * 3266489917u + 0x85EBCA77u);
                return float4(
                    lerp(minimum.rgb, maximum.rgb, randomValue),
                    lerp(
                        minimum.a,
                        maximum.a,
                        Hash01(id ^ 0xC2B2AE3Du)));
            }

            float3 VelocityOverLifetimeOrbitalOffset(
                uint id,
                float normalizedAge)
            {
                float lutPosition = LUTPosition(
                    normalizedAge,
                    _VelocityOverLifetimeOrbitalOffsetLUTInvWidth);
                float3 minimum = SAMPLE_TEXTURE2D_LOD(
                    _VelocityOverLifetimeOrbitalOffsetLUT,
                    sampler_VelocityOverLifetimeOrbitalOffsetLUT,
                    float2(lutPosition, 0.25), 0).rgb;
                float3 maximum = SAMPLE_TEXTURE2D_LOD(
                    _VelocityOverLifetimeOrbitalOffsetLUT,
                    sampler_VelocityOverLifetimeOrbitalOffsetLUT,
                    float2(lutPosition, 0.75), 0).rgb;
                return lerp(
                    minimum,
                    maximum,
                    Hash03(id * 668265263u + 0x27D4EB2Fu));
            }

            float3 VelocityOverLifetimeOrbitalVelocity(
                float3 position,
                float4 orbitalAndRadial,
                float3 orbitalOffset)
            {
                // Shuriken evaluates Orbital axes and Offset in emitter-local
                // space even when Linear XYZ uses World space.
                float3 localPosition = SimPositionToEmitterLocal(position);
                float3 relativePosition = localPosition - orbitalOffset;
                float radius = length(relativePosition);
                float3 localVelocity = cross(
                    orbitalAndRadial.xyz,
                    relativePosition);
                if (radius > 1e-6)
                {
                    localVelocity += relativePosition *
                        (orbitalAndRadial.w / radius);
                }
                return ToSimSpaceVec(localVelocity);
            }

            float InheritVelocityMultiplier(uint id, float normalizedAge)
            {
                float lutPosition = LUTPosition(
                    normalizedAge, _InheritVelocityLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _InheritVelocityLUT, sampler_InheritVelocityLUT,
                    float2(lutPosition, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _InheritVelocityLUT, sampler_InheritVelocityLUT,
                    float2(lutPosition, 0.75), 0).r;
                return lerp(minimum, maximum, Hash01(id ^ 0x7F4A7C15u));
            }

            float3 InheritVelocityContribution(
                uint id,
                float normalizedAge,
                float3 birthEmitterVelocityWS,
                float3 currentEmitterVelocityWS)
            {
                if (_InheritVelocityEnabled == 0 || _SimulationSpace == 0)
                {
                    return 0.0;
                }

                float3 sourceVelocity = _InheritVelocityMode == 0
                    ? birthEmitterVelocityWS
                    : currentEmitterVelocityWS;
                if (_SimulationSpace == 2)
                {
                    sourceVelocity = mul(
                        _WorldToSimulationDirection,
                        float4(sourceVelocity, 0.0)).xyz;
                }
                return sourceVelocity *
                    InheritVelocityMultiplier(id, normalizedAge);
            }

            float4 LimitVelocityParameters(uint id, float normalizedAge)
            {
                float lutPosition = LUTPosition(
                    normalizedAge, _LimitVelocityLUTInvWidth);
                float4 minimum = SAMPLE_TEXTURE2D_LOD(
                    _LimitVelocityLUT, sampler_LimitVelocityLUT,
                    float2(lutPosition, 0.25), 0);
                float4 maximum = SAMPLE_TEXTURE2D_LOD(
                    _LimitVelocityLUT, sampler_LimitVelocityLUT,
                    float2(lutPosition, 0.75), 0);
                float3 randomLimit = Hash03(
                    id * 3266489917u + 0x85EBCA6Bu);
                float randomDrag = Hash01(id ^ 0xC2B2AE35u);
                return max(
                    0.0,
                    lerp(
                        minimum,
                        maximum,
                        float4(randomLimit, randomDrag)));
            }

            float DampenedAxis(float value, float limit, float dampenFactor)
            {
                float magnitude = abs(value);
                if (magnitude <= limit)
                {
                    return value;
                }
                float dampenedMagnitude = limit +
                    (magnitude - limit) * dampenFactor;
                return value < 0.0 ? -dampenedMagnitude : dampenedMagnitude;
            }

            float3 ApplyLimitVelocity(
                uint id,
                float3 velocity,
                float particleSize,
                float normalizedAge,
                float stepDt)
            {
                float4 parameters = LimitVelocityParameters(id, normalizedAge);
                float dampen = saturate(_LimitVelocityDampen);
                float dampenFactor = dampen >= 1.0
                    ? 0.0
                    : pow(1.0 - dampen, stepDt * 30.0);

                if (_LimitVelocitySeparateAxes != 0)
                {
                    // Shuriken rotates the separate-axis reference frame in the
                    // additive-module direction, rather than using a conventional
                    // coordinate conversion. These helpers preserve that 2022.3 behavior.
                    float3 moduleVelocity = SimVectorToLimitAxisSpace(
                        velocity, _LimitVelocitySpace);
                    moduleVelocity.x = DampenedAxis(
                        moduleVelocity.x, parameters.x, dampenFactor);
                    moduleVelocity.y = DampenedAxis(
                        moduleVelocity.y, parameters.y, dampenFactor);
                    moduleVelocity.z = DampenedAxis(
                        moduleVelocity.z, parameters.z, dampenFactor);
                    velocity = LimitAxisVectorToSimSpace(
                        moduleVelocity, _LimitVelocitySpace);
                }
                else
                {
                    float speed = length(velocity);
                    if (speed > parameters.x && speed > 1e-6)
                    {
                        float dampenedSpeed = parameters.x +
                            (speed - parameters.x) * dampenFactor;
                        velocity *= dampenedSpeed / speed;
                    }
                }

                float speedAfterLimit = length(velocity);
                if (parameters.a > 0.0 && speedAfterLimit > 1e-6)
                {
                    float drag = parameters.a;
                    if (_LimitVelocityMultiplyDragBySize != 0)
                    {
                        float nonNegativeSize = max(0.0, particleSize);
                        drag *= 0.78539816339 *
                                nonNegativeSize * nonNegativeSize;
                    }
                    if (_LimitVelocityMultiplyDragByVelocity != 0)
                    {
                        drag *= speedAfterLimit * speedAfterLimit;
                    }

                    float draggedSpeed = max(
                        0.0, speedAfterLimit - drag * stepDt);
                    velocity *= draggedSpeed / speedAfterLimit;
                }

                return velocity;
            }

            float4 ColorOverLifetime(uint id, float normalizedAge)
            {
                float randomValue = Hash01(id ^ 0xC13FA9A9u);
                if (_ColorOverLifetimeMode == 4) // RandomColor
                {
                    return SAMPLE_TEXTURE2D_LOD(
                        _GradLUT, sampler_GradLUT,
                        float2(LUTPosition(randomValue, _GradLUTInvWidth), 0.25), 0);
                }

                float lutPosition = LUTPosition(normalizedAge, _GradLUTInvWidth);
                float4 minimum = SAMPLE_TEXTURE2D_LOD(
                    _GradLUT, sampler_GradLUT,
                    float2(lutPosition, 0.25), 0);
                float4 maximum = SAMPLE_TEXTURE2D_LOD(
                    _GradLUT, sampler_GradLUT,
                    float2(lutPosition, 0.75), 0);
                return lerp(minimum, maximum, randomValue);
            }

            float4 StartColorAtBirth(uint id, float particleAge)
            {
                if (_StartColorMode == 0) // Color
                {
                    return _StartColor;
                }
                if (_StartColorMode == 2) // TwoColors
                {
                    return lerp(
                        _StartColorMin,
                        _StartColor,
                        Hash01(id ^ 0xC2B2AE35u));
                }

                float randomValue = Hash01(id ^ 0xA511E9B3u);
                float sampleTime = _StartColorMode == 4 // RandomColor
                    ? randomValue
                    : StartColorSystemTime(particleAge);
                float lutPosition = LUTPosition(
                    sampleTime, _StartColorLUTInvWidth);
                float4 maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartColorLUT, sampler_GradLUT,
                    float2(lutPosition, 0.75), 0);
                if (_StartColorMode != 3) // Gradient or RandomColor
                {
                    return maximum;
                }

                float4 minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartColorLUT, sampler_GradLUT,
                    float2(lutPosition, 0.25), 0);
                return lerp(minimum, maximum, randomValue);
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
                float lutPosition = LUTPosition(
                    systemTime,
                    _StartLifetimeLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartLifetimeLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.75), 0).r;
                if (_StartLifetimeMode != 2) // Curve
                {
                    return max(0.001, maximum);
                }

                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartLifetimeLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.25), 0).r;
                return max(
                    0.001,
                    lerp(minimum, maximum, randomValue));
            }

            float StartSpeedAtBirth(uint id, float particleAge)
            {
                if (_StartSpeedMode == 0) // Constant
                {
                    return _StartSpeed;
                }

                float randomValue = Hash01(id ^ 0xB5297A4Du);
                if (_StartSpeedMode == 3) // TwoConstants
                {
                    return lerp(
                        _StartSpeedMin,
                        _StartSpeed,
                        randomValue);
                }

                float lutPosition = LUTPosition(
                    BirthSystemTime(particleAge),
                    _StartSpeedLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartSpeedLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.75), 0).r;
                if (_StartSpeedMode != 2) // Curve
                {
                    return maximum;
                }

                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartSpeedLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.25), 0).r;
                return lerp(minimum, maximum, randomValue);
            }

            float StartSizeAtBirth(uint id, float particleAge)
            {
                if (_StartSizeMode == 0) // Constant
                {
                    return _StartSize;
                }

                float randomValue = Hash01(id ^ 0x1B56C4E9u);
                if (_StartSizeMode == 3) // TwoConstants
                {
                    return lerp(
                        _StartSizeMin,
                        _StartSize,
                        randomValue);
                }

                float lutPosition = LUTPosition(
                    BirthSystemTime(particleAge),
                    _StartSizeLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _StartSizeLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.75), 0).r;
                if (_StartSizeMode != 2) // Curve
                {
                    return maximum;
                }

                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _StartSizeLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.25), 0).r;
                return lerp(minimum, maximum, randomValue);
            }

            float3 GravityForCurrentSystemTime(uint id)
            {
                if (_GravityModifierMode == 0) // Constant
                {
                    return _GravityWS;
                }

                float randomValue = Hash01(id ^ 0x27D4EB2Fu);
                if (_GravityModifierMode == 3) // TwoConstants
                {
                    return lerp(
                        _GravityWSMin,
                        _GravityWS,
                        randomValue);
                }

                float lutPosition = LUTPosition(
                    CurrentSystemTime(),
                    _GravityModifierLUTInvWidth);
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _GravityModifierLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.75), 0).r;
                if (_GravityModifierMode != 2) // Curve
                {
                    return _GravityBase * maximum;
                }

                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _GravityModifierLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.25), 0).r;
                return _GravityBase * lerp(minimum, maximum, randomValue);
            }

            float SizeOverLifetime(uint id, float normalizedAge)
            {
                float lutPosition = LUTPosition(normalizedAge, _SizeLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _SizeLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _SizeLUT, sampler_SizeLUT,
                    float2(lutPosition, 0.75), 0).r;
                return lerp(minimum, maximum, Hash01(id ^ 0x91E10DA5u));
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
                float lutPosition = LUTPosition(
                    speedPosition,
                    _LifetimeByEmitterSpeedLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _LifetimeByEmitterSpeedLUT,
                    sampler_LifetimeByEmitterSpeedLUT,
                    float2(lutPosition, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _LifetimeByEmitterSpeedLUT,
                    sampler_LifetimeByEmitterSpeedLUT,
                    float2(lutPosition, 0.75), 0).r;
                return max(
                    0.0,
                    lerp(minimum, maximum, Hash01(id ^ 0x94D049BBu)));
            }

            float4 ColorBySpeed(uint id, float speedPosition)
            {
                float randomValue = Hash01(id ^ 0x7F4A7C15u);
                if (_ColorBySpeedMode == 4) // RandomColor
                {
                    return SAMPLE_TEXTURE2D_LOD(
                        _ColorBySpeedLUT, sampler_ColorBySpeedLUT,
                        float2(
                            LUTPosition(randomValue, _ColorBySpeedLUTInvWidth),
                            0.25), 0);
                }

                float lutPosition = LUTPosition(
                    speedPosition, _ColorBySpeedLUTInvWidth);
                float4 minimum = SAMPLE_TEXTURE2D_LOD(
                    _ColorBySpeedLUT, sampler_ColorBySpeedLUT,
                    float2(lutPosition, 0.25), 0);
                float4 maximum = SAMPLE_TEXTURE2D_LOD(
                    _ColorBySpeedLUT, sampler_ColorBySpeedLUT,
                    float2(lutPosition, 0.75), 0);
                return lerp(minimum, maximum, randomValue);
            }

            float SizeBySpeed(uint id, float speedPosition)
            {
                float lutPosition = LUTPosition(
                    speedPosition, _SizeBySpeedLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _SizeBySpeedLUT, sampler_SizeBySpeedLUT,
                    float2(lutPosition, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _SizeBySpeedLUT, sampler_SizeBySpeedLUT,
                    float2(lutPosition, 0.75), 0).r;
                return lerp(minimum, maximum, Hash01(id ^ 0xD192ED03u));
            }

            float RotationBySpeed(uint id, float speedPosition)
            {
                float lutPosition = LUTPosition(
                    speedPosition, _RotationBySpeedLUTInvWidth);
                float minimum = SAMPLE_TEXTURE2D_LOD(
                    _RotationBySpeedLUT, sampler_RotationBySpeedLUT,
                    float2(lutPosition, 0.25), 0).r;
                float maximum = SAMPLE_TEXTURE2D_LOD(
                    _RotationBySpeedLUT, sampler_RotationBySpeedLUT,
                    float2(lutPosition, 0.75), 0).r;
                return lerp(minimum, maximum, Hash01(id ^ 0xA24BAED4u));
            }

            FragOut Frag(Varyings i)
            {
                FragOut o;

                // Map pixel to particle id
                // SV_POSITION is expressed in target pixel coordinates in the fragment
                // stage. Deriving the state index from it avoids interpolation/viewport
                // rounding mismatches between the written pixel and the sampled id.
                int2 pxy = int2(i.positionHCS.xy);
                uint id  = (uint)(pxy.y * _GridSize + pxy.x);
                float2 uv = (float2(pxy) + 0.5) / _GridSize;

                // Read current state (dead if out of range)
                float4 curPosLife = SAMPLE_TEXTURE2D_LOD(_CurPosLife, sampler_CurPosLife, uv, 0);
                float4 curVelSize = SAMPLE_TEXTURE2D_LOD(_CurVelSize, sampler_CurVelSize, uv, 0);
                float4 curColor   = SAMPLE_TEXTURE2D_LOD(_CurColor,   sampler_CurColor,   uv, 0);
                float4 curModuleState = SAMPLE_TEXTURE2D_LOD(
                    _CurRotationPhase, sampler_CurRotationPhase, uv, 0);

                // Default: pass through
                float3 pos = curPosLife.xyz;
                float  life= curPosLife.w;
                float3 vel = curVelSize.xyz;
                float  size= curVelSize.w;
                float4 col = curColor;
                float rotationPhase = curModuleState.x;
                float3 birthEmitterVelocityWS = curModuleState.yzw;

                bool lifetimeUsesAgeState =
                    _StartLifetimeMode == 1 ||
                    _StartLifetimeMode == 2 ||
                    _RingBufferMode != 0;
                float baseParticleStartLifetime = StartLifetimeAtBirth(id, 0.0);
                float particleStartLifetime = baseParticleStartLifetime *
                    LifetimeByEmitterSpeedMultiplier(
                        id,
                        birthEmitterVelocityWS);
                float particleStartSpeed = RandomRange(
                    id, 0xB5297A4Du, _RandomizeStartSpeed, _StartSpeedMin, _StartSpeed);
                float particleStartSize = RandomRange(
                    id, 0x1B56C4E9u, _RandomizeStartSize, _StartSizeMin, _StartSize);
                float4 particleStartColor = _StartColor;
                float3 particleGravity = GravityForCurrentSystemTime(id);

                // Out-of-cap pixels remain dead
                if (id >= _MaxParticles)
                {
                    o.PosLife = float4(0,0,0,0);
                    o.VelSize = float4(0,0,0,0);
                    o.Color   = float4(0,0,0,0);
                    o.ModuleState = 0.0;
                    return o;
                }

                // Spawn?
                bool spawn = InEmit(id, _EmitStart, _EmitCount, (uint)_MaxParticles);
                float stepDt = 0.0;
                if (spawn)
                {
                    uint emitOrdinal = EmitOrdinal(
                        id, _EmitStart, (uint)_MaxParticles);
                    stepDt = SpawnAgeThisFrame(emitOrdinal);
                    baseParticleStartLifetime = StartLifetimeAtBirth(
                        id,
                        stepDt);
                    particleStartSpeed = StartSpeedAtBirth(id, stepDt);
                    particleStartSize = StartSizeAtBirth(id, stepDt);
                    float3 urnd = Hash03(id * 9781u + 0x9E3779B9u);
                    float2 u2a = urnd.xy;
                    float2 u2b = float2(urnd.y, urnd.z);

                    float3 posL = 0;
                    float3 velL = 0;

                    // 8. Point (Shuriken Shape module disabled)
                    if (_ShapeType == 8)
                    {
                        float3 fwd = normalize(_ShapeFwdL);
                        posL = 0.0;
                        velL = fwd * particleStartSpeed;
                    }
                    // 0. Sphere
                    else if (_ShapeType == 0)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        float R = _ShapeSphereRadius;
                        float3 dirL = SampleSphereDir(u2a);
                        float r;
                        if (_ShapeEmitFrom == 1) // Surface shell
                        {
                            r = R; // 固定半径，表面发射
                        }
                        else // Volume
                        {
                            r = RadiusWithThickness(urnd.z, R, _ShapeRadiusThickness);
                        }
                        // 变换方向到Shape的局部坐标系
                        float3 dirTransformed = right * dirL.x + up * dirL.y + fwd * dirL.z;
                        float3 pL = dirTransformed * r;
                        posL = _ShapePosL + pL;

                        float3 vdirL = dirTransformed;
                        velL = vdirL * particleStartSpeed;
                    }
                    // 1. Hemisphere
                    else if (_ShapeType == 1)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        float R = _ShapeSphereRadius;
                        float3 dirL = SampleHemisphereDir(u2a); // z>=0
                        float r;
                        if (_ShapeEmitFrom == 1) // Surface shell
                        {
                            r = R; // 固定半径，表面发射
                        }
                        else // Volume
                        {
                            r = RadiusWithThickness(urnd.z, R, _ShapeRadiusThickness);
                        }
                        // 变换方向到Shape的局部坐标系
                        float3 dirTransformed = right * dirL.x + up * dirL.y + fwd * dirL.z;
                        float3 pL = dirTransformed * r;
                        posL = _ShapePosL + pL;

                        float3 vdirL = dirTransformed;
                        velL = vdirL * particleStartSpeed;
                    }
                    // 2. Cone
                    else if (_ShapeType == 2)
                    {
                        //return float4(0,0,0,1);
                        //o.Color   = float4(0,0,0,1);
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);

                        if (_ShapeEmitFrom == 2) // Base disc at shape position
                        {
                            float innerR = _ShapeConeRadius * saturate(1.0 - _ShapeRadiusThickness);
                            float r = sqrt(lerp(
                                innerR * innerR,
                                _ShapeConeRadius * _ShapeConeRadius,
                                urnd.x));
                            float phi = ShapeArcAngle(
                                id, emitOrdinal, stepDt, urnd.y);
                            float2 d = float2(r * cos(phi), r * sin(phi));
                            posL = _ShapePosL + right * d.x + up * d.y;

                            float3 dirL = BuildConeVelocity(d, _ShapeConeRadius, right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * particleStartSpeed;
                        }
                        else if (_ShapeEmitFrom == 0) // Volume
                        {
                            float z  = _ShapeConeLength * pow(urnd.x, 1.0/3.0);
                            float Rz = (_ShapeConeLength > 1e-5) ? (_ShapeConeRadius * (z / _ShapeConeLength)) : 0.0;

                            float Ri = Rz * saturate(1.0 - _ShapeRadiusThickness);
                            float  r = sqrt(lerp(Ri*Ri, Rz*Rz, urnd.y));
                            float  phi = ShapeArcAngle(
                                id, emitOrdinal, stepDt, urnd.z);
                            float2 d = float2(r * cos(phi), r * sin(phi));

                            posL = _ShapePosL + right * d.x + up * d.y + fwd * z;

                            float3 dirL = BuildConeVelocity(d, max(Rz, 1e-6), right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * particleStartSpeed;
                        }
                    }
                    // 3. Donut
                    else if (_ShapeType == 3)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);

                        float phi = ShapeArcAngle(
                            id, emitOrdinal, stepDt, urnd.x);
                        float theta = 6.28318530718 * urnd.z;
                        float outerRadius = max(0.0, _ShapeDonutThickness);
                        float innerRadius = _ShapeEmitFrom == 1
                            ? outerRadius
                            : outerRadius * saturate(
                                1.0 - _ShapeRadiusThickness);
                        float crossRadius = sqrt(lerp(
                            innerRadius * innerRadius,
                            outerRadius * outerRadius,
                            urnd.y));
                        float3 ringDirection = normalize(
                            right * cos(phi) + up * sin(phi));
                        float3 crossDirection = normalize(
                            ringDirection * cos(theta) + fwd * sin(theta));

                        posL = _ShapePosL +
                            ringDirection * _ShapeDonutRadius +
                            crossDirection * crossRadius;
                        velL = crossDirection * particleStartSpeed;
                    }
                    // 4. Box
                    else if (_ShapeType == 4)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        float3 sizeB = _ShapeBoxSize;

                        if (_ShapeEmitFrom == 3) // Edge
                        {
                            float3 pB = SampleBoxEdge(urnd.x, sizeB);
                            posL = _ShapePosL +
                                right * pB.x + up * pB.y + fwd * pB.z;
                            velL = fwd * particleStartSpeed;
                        }
                        else if (_ShapeEmitFrom == 1) // Surface
                        {
                            float3 nLocal;
                            float3 pB = SampleBoxSurface(urnd, sizeB, nLocal);
                            posL = _ShapePosL + right*pB.x + up*pB.y + fwd*pB.z;
                            velL = fwd * particleStartSpeed;
                        }
                        else // Volume
                        {
                            float3 pB = SampleBoxVolume(urnd, sizeB);
                            posL = _ShapePosL + right*pB.x + up*pB.y + fwd*pB.z;

                            float3 dirL = fwd;
                            velL = dirL * particleStartSpeed;
                        }
                    }
                    // 5. Circle
                    else if (_ShapeType == 5)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        float R = _ShapeCircleRadius;
                        float phi = ShapeArcAngle(
                            id, emitOrdinal, stepDt, urnd.x);
                        
                        float r;
                        if (_ShapeEmitFrom == 1) // Edge (表面)
                        {
                            r = R; // 固定半径，边缘发射
                        }
                        else // Volume
                        {
                            float innerR = R * saturate(1.0 - _ShapeRadiusThickness);
                            r = sqrt(lerp(innerR * innerR, R * R, urnd.y));
                        }
                        
                        float2 pos2D = float2(r * cos(phi), r * sin(phi));
                        posL = _ShapePosL + right * pos2D.x + up * pos2D.y;

                        float3 radialDirection = normalize(
                            right * cos(phi) + up * sin(phi));
                        velL = radialDirection * particleStartSpeed;
                    }
                    // 6. Edge
                    else if (_ShapeType == 6)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        float L = _ShapeEdgeLength;
                        float t = urnd.x; // [0, 1]
                        float pos1D = (t - 0.5) * L; // [-L/2, L/2]
                        
                        posL = _ShapePosL + right * pos1D;
                        
                        velL = up * particleStartSpeed;
                    }
                    // 7. Rectangle
                    else if (_ShapeType == 7)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        float2 size = _ShapeRectangleSize;
                        
                        float2 pos2D;
                        if (_ShapeEmitFrom == 1) // Edge (边缘)
                        {
                            // 从矩形边缘采样
                            float perimeter = 2.0 * (size.x + size.y);
                            float t = urnd.x * perimeter;
                            
                            if (t < size.x)
                            {
                                // 底边
                                pos2D = float2((t / size.x - 0.5) * size.x, -0.5 * size.y);
                            }
                            else if (t < size.x + size.y)
                            {
                                // 右边
                                pos2D = float2(0.5 * size.x, ((t - size.x) / size.y - 0.5) * size.y);
                            }
                            else if (t < 2.0 * size.x + size.y)
                            {
                                // 顶边
                                pos2D = float2(((t - size.x - size.y) / size.x - 0.5) * size.x, 0.5 * size.y);
                            }
                            else
                            {
                                // 左边
                                pos2D = float2(-0.5 * size.x, ((t - 2.0 * size.x - size.y) / size.y - 0.5) * size.y);
                            }
                        }
                        else // Volume
                        {
                            // 从矩形内部采样
                            pos2D = float2((urnd.x - 0.5) * size.x, (urnd.y - 0.5) * size.y);
                        }
                        
                        posL = _ShapePosL + right * pos2D.x + up * pos2D.y;

                        velL = fwd * particleStartSpeed;
                    }
                    // Fallback (默认使用Cone)
                    else
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);

                        if (_ShapeEmitFrom == 2) // Base disc at shape position
                        {
                            float innerR = _ShapeConeRadius * saturate(1.0 - _ShapeRadiusThickness);
                            float r = sqrt(lerp(
                                innerR * innerR,
                                _ShapeConeRadius * _ShapeConeRadius,
                                urnd.x));
                            float phi = ShapeArcAngle(
                                id, emitOrdinal, stepDt, urnd.y);
                            float2 d = float2(r * cos(phi), r * sin(phi));
                            posL = _ShapePosL + right * d.x + up * d.y;

                            float3 dirL = BuildConeVelocity(d, _ShapeConeRadius, right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * particleStartSpeed;
                        }
                        else if (_ShapeEmitFrom == 0) // Volume
                        {
                            float z  = _ShapeConeLength * pow(urnd.x, 1.0/3.0);
                            float Rz = (_ShapeConeLength > 1e-5) ? (_ShapeConeRadius * (z / _ShapeConeLength)) : 0.0;

                            float Ri = Rz * saturate(1.0 - _ShapeRadiusThickness);
                            float  r = sqrt(lerp(Ri*Ri, Rz*Rz, urnd.y));
                            float  phi = ShapeArcAngle(
                                id, emitOrdinal, stepDt, urnd.z);
                            float2 d = float2(r * cos(phi), r * sin(phi));

                            posL = _ShapePosL + right * d.x + up * d.y + fwd * z;

                            float3 dirL = BuildConeVelocity(d, max(Rz, 1e-6), right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * particleStartSpeed;
                        }
                        else // Surface fallback to Base
                        {
                            float innerR = _ShapeConeRadius * saturate(1.0 - _ShapeRadiusThickness);
                            float r = sqrt(lerp(
                                innerR * innerR,
                                _ShapeConeRadius * _ShapeConeRadius,
                                urnd.x));
                            float phi = ShapeArcAngle(
                                id, emitOrdinal, stepDt, urnd.y);
                            float2 d = float2(r * cos(phi), r * sin(phi));
                            posL = _ShapePosL + right * d.x + up * d.y;

                            float3 dirL = BuildConeVelocity(d, _ShapeConeRadius, right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * particleStartSpeed;
                        }
                    }

                    if (_ShapeType != 8)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up = normalize(_ShapeUpL);
                        float3 fwd = normalize(_ShapeFwdL);
                        float3 defaultDirection = fwd;
                        if (abs(particleStartSpeed) > 1e-6)
                        {
                            defaultDirection = velL / particleStartSpeed;
                        }

                        float3 randomDirection = SampleSphereDir(Hash02(
                            id * 0xA511E9B3u + 0x63D83595u));
                        float3 direction = lerp(
                            defaultDirection,
                            randomDirection,
                            saturate(_ShapeRandomDirectionAmount));

                        // Shuriken spherizes from the Shape origin using the
                        // sampled position before Randomize Position runs.
                        float3 sphericalOffset = posL - _ShapePosL;
                        float sphericalLength = length(sphericalOffset);
                        float3 sphericalDirection = sphericalLength > 1e-6
                            ? sphericalOffset / sphericalLength
                            : defaultDirection;
                        direction = lerp(
                            direction,
                            sphericalDirection,
                            saturate(_ShapeSphericalDirectionAmount));
                        float directionLength = length(direction);
                        direction = directionLength > 1e-6
                            ? direction / directionLength
                            : defaultDirection;
                        velL = direction * particleStartSpeed;

                        float3 randomPositionDirection = SampleSphereDir(Hash02(
                            id * 0xC2B2AE35u + 0x27D4EB2Fu));
                        posL += right *
                                    (randomPositionDirection.x *
                                     _ShapeRandomPositionScale.x) +
                                up *
                                    (randomPositionDirection.y *
                                     _ShapeRandomPositionScale.y) +
                                fwd *
                                    (randomPositionDirection.z *
                                     _ShapeRandomPositionScale.z);
                    }

                    // finalize spawn in sim space
                    pos  = ToSimSpacePos(posL);
                    vel  = ToSimSpaceSpawnVelocity(velL);
                    rotationPhase = 0.0;
                    birthEmitterVelocityWS =
                        _LifetimeByEmitterSpeedEnabled != 0 ||
                         (_InheritVelocityEnabled != 0 &&
                          _InheritVelocityMode == 0 &&
                         _SimulationSpace != 0)
                            ? _EmitterVelocityWS
                            : 0.0;
                    particleStartLifetime = baseParticleStartLifetime *
                        LifetimeByEmitterSpeedMultiplier(
                            id,
                            birthEmitterVelocityWS);
                    life = lifetimeUsesAgeState
                        ? 1.0
                        : particleStartLifetime;
                    
                    float tSpawn = 0.0;
                    float4 lutColSpawn = ColorOverLifetime(id, tSpawn);
                    float lutSizeSpawn = SizeOverLifetime(id, tSpawn);
                    float spawnSpeed = length(vel);
                    float spawnColorSpeedPosition = SpeedRangePosition(
                        spawnSpeed, _ColorBySpeedRange);
                    float spawnSizeSpeedPosition = SpeedRangePosition(
                        spawnSpeed, _SizeBySpeedRange);
                    float4 colorBySpeedSpawn = _ColorBySpeedEnabled != 0
                        ? ColorBySpeed(id, spawnColorSpeedPosition)
                        : 1.0;
                    float sizeBySpeedSpawn = _SizeBySpeedEnabled != 0
                        ? SizeBySpeed(id, spawnSizeSpeedPosition)
                        : 1.0;
                    col = particleStartColor * lutColSpawn * colorBySpeedSpawn;
                    size = particleStartSize * lutSizeSpawn * sizeBySpeedSpawn;

                    // ToSimSpacePos uses the current emitter transform. Move a
                    // world-space birth back to its sub-frame emitter position so
                    // moving Rate-over-Time, Rate-over-Distance and Burst particles
                    // are distributed along the actual emitter trajectory.
                    if (_SimulationSpace != 0 && _DeltaTime > 1e-6)
                    {
                        float spawnFraction = saturate(1.0 - stepDt / _DeltaTime);
                        float3 emitterPositionAtSpawn = lerp(
                            _EmitterPreviousPositionWS,
                            _EmitterCurrentPositionWS,
                            spawnFraction);
                        pos += WorldVectorToSimulationPositionSpace(
                            emitterPositionAtSpawn -
                            _EmitterCurrentPositionWS);
                    }
                }
               
                // Update alive particles
                if (life > 0.0)
                {
                    if (!spawn)
                    {
                        stepDt = _DeltaTime;
                    }

                    float totalParticleAgeBeforeStep;
                    float totalParticleAgeAfterStep;
                    float particleAgeBeforeStep;
                    float particleAgeAfterStep;
                    float normalizedAgeBeforeStep;
                    float normalizedAgeAfterStep;
                    if (lifetimeUsesAgeState)
                    {
                        totalParticleAgeBeforeStep =
                            max(0.0, life - 1.0);
                        totalParticleAgeAfterStep =
                            totalParticleAgeBeforeStep + stepDt;
                        baseParticleStartLifetime = StartLifetimeAtBirth(
                            id,
                            totalParticleAgeAfterStep);
                        particleStartLifetime = baseParticleStartLifetime *
                            LifetimeByEmitterSpeedMultiplier(
                                id,
                                birthEmitterVelocityWS);
                        particleAgeBeforeStep = RingBufferParticleAge(
                            totalParticleAgeBeforeStep,
                            particleStartLifetime);
                        particleAgeAfterStep = RingBufferParticleAge(
                            totalParticleAgeAfterStep,
                            particleStartLifetime);
                        normalizedAgeBeforeStep = saturate(
                            particleAgeBeforeStep /
                            max(particleStartLifetime, 1e-5));
                        normalizedAgeAfterStep = saturate(
                            particleAgeAfterStep /
                            max(particleStartLifetime, 1e-5));
                        life = _RingBufferMode == 0 &&
                               totalParticleAgeAfterStep + 1e-4 >=
                                   particleStartLifetime
                            ? 0.0
                            : totalParticleAgeAfterStep + 1.0;
                    }
                    else
                    {
                        particleAgeBeforeStep = max(
                            0.0,
                            particleStartLifetime - life);
                        normalizedAgeBeforeStep = saturate(
                            1.0 -
                            (life / max(particleStartLifetime, 1e-5)));
                        life = max(0.0, life - stepDt);
                        // Shuriken retires particles on the lifetime boundary.
                        // Floating-point subtraction can otherwise leave a tiny
                        // positive remainder for one extra GPU frame.
                        if (life <= 1e-5)
                        {
                            life = 0.0;
                        }
                        normalizedAgeAfterStep = saturate(
                            1.0 -
                            (life / max(particleStartLifetime, 1e-5)));
                        particleAgeAfterStep = max(
                            0.0,
                            particleStartLifetime - life);
                        totalParticleAgeBeforeStep =
                            particleAgeBeforeStep;
                        totalParticleAgeAfterStep =
                            particleAgeAfterStep;
                    }
                    particleStartColor = StartColorAtBirth(
                        id, totalParticleAgeAfterStep);
                    particleStartSize = StartSizeAtBirth(
                        id, totalParticleAgeAfterStep);
                    float3 positionBeforeStep = pos;
                    float speedBeforeStep = length(vel);
                    float3 particleNoiseValue = 0.0;
                    float4 particleNoiseAmounts = 0.0;

                    // Stored velocity includes the prior frame's inherited component
                    // so rendering and by-speed modules see effective motion. Remove it
                    // before updating the underlying particle velocity.
                    if (!spawn && _InheritVelocityEnabled != 0 &&
                        _SimulationSpace != 0)
                    {
                        vel -= InheritVelocityContribution(
                            id,
                            normalizedAgeBeforeStep,
                            birthEmitterVelocityWS,
                            _EmitterPreviousVelocityWS);
                    }

                    // Gravity is already in simulation space. Force over Lifetime is
                    // sampled with Shuriken's stable per-particle MinMax random values.
                    float3 acceleration = particleGravity;
                    if (_ForceOverLifetimeEnabled != 0)
                    {
                        acceleration += ForceOverLifetime(id, normalizedAgeBeforeStep);
                    }
                    vel += acceleration * stepDt;

                    float movementSpeedModifier = 1.0;
                    bool integratedOrbitalMotion =
                        _VelocityOverLifetimeEnabled != 0 &&
                        _VelocityOverLifetimeOrbitalEnabled != 0;
                    if (integratedOrbitalMotion)
                    {
                        float normalizedAgeMidStep =
                            (normalizedAgeBeforeStep +
                             normalizedAgeAfterStep) * 0.5;
                        float4 velocityBeforeStep =
                            VelocityOverLifetimeParameters(
                                id, normalizedAgeBeforeStep);
                        float4 velocityMidStep =
                            VelocityOverLifetimeParameters(
                                id, normalizedAgeMidStep);
                        float4 velocityAfterStep =
                            VelocityOverLifetimeParameters(
                                id, normalizedAgeAfterStep);
                        float4 orbitalBeforeStep =
                            VelocityOverLifetimeOrbitalParameters(
                                id, normalizedAgeBeforeStep);
                        float4 orbitalMidStep =
                            VelocityOverLifetimeOrbitalParameters(
                                id, normalizedAgeMidStep);
                        float4 orbitalAfterStep =
                            VelocityOverLifetimeOrbitalParameters(
                                id, normalizedAgeAfterStep);
                        float3 offsetBeforeStep =
                            VelocityOverLifetimeOrbitalOffset(
                                id, normalizedAgeBeforeStep);
                        float3 offsetMidStep =
                            VelocityOverLifetimeOrbitalOffset(
                                id, normalizedAgeMidStep);
                        float3 offsetAfterStep =
                            VelocityOverLifetimeOrbitalOffset(
                                id, normalizedAgeAfterStep);
                        float3 orbitalVelocityBeforeStep =
                            VelocityOverLifetimeOrbitalVelocity(
                                pos,
                                orbitalBeforeStep,
                                offsetBeforeStep);

                        // Keep the persistent state as the underlying velocity.
                        // Orbital and Radial are position-dependent module
                        // contributions, so remove the prior contribution before
                        // evaluating this step.
                        if (!spawn)
                        {
                            vel -= velocityBeforeStep.rgb +
                                   orbitalVelocityBeforeStep;
                        }

                        if (_VelocityOverLifetimeSpeedModifierEnabled != 0)
                        {
                            movementSpeedModifier = velocityMidStep.a;
                        }

                        // A midpoint integration keeps circular Shuriken orbits
                        // stable without adding another simulation render target.
                        float3 velocityAtStepStart = vel +
                            velocityBeforeStep.rgb +
                            orbitalVelocityBeforeStep;
                        velocityAtStepStart += InheritVelocityContribution(
                            id,
                            normalizedAgeBeforeStep,
                            birthEmitterVelocityWS,
                            _EmitterPreviousVelocityWS);
                        float3 midpointPosition = pos +
                            velocityAtStepStart *
                            (movementSpeedModifier * stepDt * 0.5);
                        float3 midpointVelocity = vel + velocityMidStep.rgb +
                            VelocityOverLifetimeOrbitalVelocity(
                                midpointPosition,
                                orbitalMidStep,
                                offsetMidStep);
                        midpointVelocity += InheritVelocityContribution(
                            id,
                            normalizedAgeMidStep,
                            birthEmitterVelocityWS,
                            (_EmitterPreviousVelocityWS +
                             _EmitterVelocityWS) * 0.5);
                        pos += midpointVelocity *
                               (movementSpeedModifier * stepDt);
                        vel += velocityAfterStep.rgb +
                            VelocityOverLifetimeOrbitalVelocity(
                                pos,
                                orbitalAfterStep,
                                offsetAfterStep);
                    }
                    else if (_VelocityOverLifetimeEnabled != 0)
                    {
                        float4 velocityBeforeStep =
                            VelocityOverLifetimeParameters(
                                id, normalizedAgeBeforeStep);
                        float4 velocityAfterStep =
                            VelocityOverLifetimeParameters(
                                id, normalizedAgeAfterStep);
                        if (_VelocityOverLifetimeSpeedModifierEnabled != 0)
                        {
                            movementSpeedModifier = velocityBeforeStep.a;
                        }
                        if (spawn)
                        {
                            vel += velocityAfterStep.rgb;
                        }
                        else
                        {
                            vel += velocityAfterStep.rgb -
                                   velocityBeforeStep.rgb;
                        }
                    }
                    if (_LimitVelocityEnabled != 0)
                    {
                        vel = ApplyLimitVelocity(
                            id,
                            vel,
                            size,
                            normalizedAgeAfterStep,
                            stepDt);
                    }
                    vel += InheritVelocityContribution(
                        id,
                        normalizedAgeAfterStep,
                        birthEmitterVelocityWS,
                        _EmitterVelocityWS);
                    if (!integratedOrbitalMotion)
                    {
                        pos += vel * movementSpeedModifier * stepDt;
                    }
                    if (_NoiseEnabled != 0)
                    {
                        particleNoiseValue = ParticleNoise(
                            id,
                            pos,
                            normalizedAgeAfterStep,
                            particleNoiseAmounts);
                        // Shuriken exposes Noise as animated position while the
                        // particle's stored velocity remains the module-free value.
                        pos += particleNoiseValue *
                               particleNoiseAmounts.x * stepDt;
                    }
                    if (_CollisionEnabled != 0)
                    {
                        float collisionSpeed = length(vel);
                        float collisionSize = particleStartSize *
                            SizeOverLifetime(id, normalizedAgeAfterStep);
                        if (_SizeBySpeedEnabled != 0)
                        {
                            collisionSize *= SizeBySpeed(
                                id,
                                SpeedRangePosition(
                                    collisionSpeed,
                                    _SizeBySpeedRange));
                        }
                        if (_NoiseEnabled != 0 && _NoiseSeparateAxes == 0)
                        {
                            float collisionNoiseScale =
                                _NoiseRemapEnabled != 0 ? 0.5 : 1.0;
                            collisionSize *= max(
                                0.0,
                                1.0 + particleNoiseValue.z *
                                      particleNoiseAmounts.z *
                                      collisionNoiseScale);
                        }
                        ApplyPlaneCollisions(
                            id,
                            positionBeforeStep,
                            pos,
                            vel,
                            collisionSize,
                            particleStartLifetime,
                            lifetimeUsesAgeState,
                            life,
                            normalizedAgeAfterStep,
                            stepDt);
                    }

                    // lifetime normalized 0..1 (0 birth, 1 death)
                    float t = normalizedAgeAfterStep;

                    // Color over lifetime & Size over lifetime via LUTs
                    float4 lutCol = ColorOverLifetime(id, t);
                    float lutSize = SizeOverLifetime(id, t);
                    float currentSpeed = length(vel);
                    if (_RotationBySpeedEnabled != 0)
                    {
                        float rotationSpeedPositionBefore = SpeedRangePosition(
                            speedBeforeStep, _RotationBySpeedRange);
                        float rotationSpeedPositionAfter = SpeedRangePosition(
                            currentSpeed, _RotationBySpeedRange);
                        float angularVelocityBefore = RotationBySpeed(
                            id, rotationSpeedPositionBefore);
                        float angularVelocityAfter = RotationBySpeed(
                            id, rotationSpeedPositionAfter);
                        rotationPhase += (angularVelocityBefore + angularVelocityAfter) *
                                         (0.5 * stepDt);
                    }
                    if (_NoiseEnabled != 0)
                    {
                        // Remapped rotation/size channels use Shuriken's signed
                        // half-range; position uses the remapped value directly.
                        float rotationNoiseScale =
                            _NoiseRemapEnabled != 0 ? 0.5 : 1.0;
                        rotationPhase += particleNoiseValue.z *
                            particleNoiseAmounts.y *
                            rotationNoiseScale *
                            0.01745329252 * stepDt;
                    }
                    float colorSpeedPosition = SpeedRangePosition(
                        currentSpeed, _ColorBySpeedRange);
                    float sizeSpeedPosition = SpeedRangePosition(
                        currentSpeed, _SizeBySpeedRange);
                    float4 speedColor = _ColorBySpeedEnabled != 0
                        ? ColorBySpeed(id, colorSpeedPosition)
                        : 1.0;
                    float speedSize = _SizeBySpeedEnabled != 0
                        ? SizeBySpeed(id, sizeSpeedPosition)
                        : 1.0;

                    col = particleStartColor * lutCol * speedColor;
                    size = particleStartSize * lutSize * speedSize;
                    if (_NoiseEnabled != 0 && _NoiseSeparateAxes == 0)
                    {
                        // Unity 2022.3 does not apply Noise Size Amount while
                        // Separate Axes is enabled.
                        float sizeNoiseScale =
                            _NoiseRemapEnabled != 0 ? 0.5 : 1.0;
                        size *= max(
                            0.0,
                            1.0 + particleNoiseValue.z *
                                  particleNoiseAmounts.z *
                                  sizeNoiseScale);
                    }
                }
                else
                {
                    // keep as dead
                    life = 0.0;
                    size = 0.0;
                    col.a = 0.0;
                    rotationPhase = 0.0;
                    birthEmitterVelocityWS = 0.0;
                }

                o.PosLife = float4(pos, life);
                o.VelSize = float4(vel, size);
                o.Color   = col;
                o.ModuleState = float4(rotationPhase, birthEmitterVelocityWS);
                return o;
            }
            ENDHLSL
        }
    }
}
