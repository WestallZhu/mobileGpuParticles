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
            TEXTURE2D(_GradLUT);         SAMPLER(sampler_GradLUT);
            TEXTURE2D(_SizeLUT);         SAMPLER(sampler_SizeLUT);

            // --- Params ---
            CBUFFER_START(UnityPerMaterial)
                int     _GridSize;
                int     _MaxParticles;
                float   _DeltaTime;
                float   _StartLifetime;
                float   _StartSpeed;
                float   _StartSize;
                float4  _StartColor;
                float3  _GravityWS;          // NOTE: contains space-correct gravity (WS or LS)
                int     _SimulationSpace;    // 0 Local, 1 World
                uint    _EmitStart;
                uint    _EmitCount;
                float   _EmitCarryPrev;
                float   _EmissionRate;

                // Initial direction already in simulation space
                float3  _InitialDir;
                float   _Pad0;

                // ----- Shape (generic) -----
                int     _ShapeType;          // 0 Sphere, 1 Hemisphere, 2 Cone, 3 Donut, 4 Box, 5 Circle, 6 Edge, 7 Rectangle
                int     _ShapeEmitFrom;      // 0 Volume, 1 Surface, 2 Base (for Cone)
                int     _AlignToDirection;   // bool

                // Cone
                float   _ShapeConeAngleRad;
                float   _ShapeConeRadius;
                float   _ShapeConeLength;
                float   _ShapeRadiusThickness; // 0 shell edge, 1 full
                float   _ShapeConeArcRad;    // 0..2PI

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

            struct FragOut {
                float4 PosLife : SV_Target0;
                float4 VelSize : SV_Target1;
                float4 Color   : SV_Target2;
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

            float SpawnAgeThisFrame(uint emitOrdinal)
            {
                if (_EmissionRate <= 1e-6)
                {
                    return 0.0;
                }

                float spawnTime = ((float)emitOrdinal + 1.0 - _EmitCarryPrev) / _EmissionRate;
                return clamp(_DeltaTime - spawnTime, 0.0, _DeltaTime);
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

            // Transform emitter-local point to world if needed
            float3 ToSimSpacePos(float3 pLocal)
            {
                if (_SimulationSpace == 1) // World
                {
                    float4 ws = mul(_EmitterLocalToWorld, float4(pLocal,1));
                    return ws.xyz;
                }
                return pLocal;
            }
            // Transform emitter-local vector (no translation) to world if needed
            float3 ToSimSpaceVec(float3 vLocal)
            {
                if (_SimulationSpace == 1) // World
                {
                    float4 ws = mul(_EmitterLocalToWorld, float4(vLocal,0));
                    return ws.xyz;
                }
                return vLocal;
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

                // Default: pass through
                float3 pos = curPosLife.xyz;
                float  life= curPosLife.w;
                float3 vel = curVelSize.xyz;
                float  size= curVelSize.w;
                float4 col = curColor;

                // Out-of-cap pixels remain dead
                if (id >= _MaxParticles)
                {
                    o.PosLife = float4(0,0,0,0);
                    o.VelSize = float4(0,0,0,0);
                    o.Color   = float4(0,0,0,0);
                    return o;
                }

                // Spawn?
                bool spawn = InEmit(id, _EmitStart, _EmitCount, (uint)_MaxParticles);
                float stepDt = 0.0;
                if (spawn)
                {
                    float3 urnd = Hash03(id * 9781u + 0x9E3779B9u);
                    float2 u2a = urnd.xy;
                    float2 u2b = float2(urnd.y, urnd.z);

                    float3 posL = 0;
                    float3 velL = 0;

                    // 0. Sphere
                    if (_ShapeType == 0)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        float R = _ShapeSphereRadius;
                        float3 dirL = SampleSphereDir(u2a);
                        float r;
                        if (_ShapeEmitFrom == 1) // Surface (SphereShell)
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

                        float3 vdirL = (_AlignToDirection != 0) ? normalize(pL) : fwd;
                        velL = vdirL * _StartSpeed;
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
                        if (_ShapeEmitFrom == 1) // Surface (HemisphereShell)
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

                        float3 vdirL = (_AlignToDirection != 0) ? normalize(pL) : fwd;
                        velL = vdirL * _StartSpeed;
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
                            float2 d = SampleDisk(urnd.xy, innerR, _ShapeConeRadius);
                            posL = _ShapePosL + right * d.x + up * d.y;

                            float3 dirL = BuildConeVelocity(d, _ShapeConeRadius, right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * _StartSpeed;
                        }
                        else if (_ShapeEmitFrom == 0) // Volume
                        {
                            float z  = _ShapeConeLength * pow(urnd.x, 1.0/3.0);
                            float Rz = (_ShapeConeLength > 1e-5) ? (_ShapeConeRadius * (z / _ShapeConeLength)) : 0.0;

                            float Ri = Rz * saturate(1.0 - _ShapeRadiusThickness);
                            float  r = sqrt(lerp(Ri*Ri, Rz*Rz, urnd.y));
                            float  phi = 6.28318530718 * urnd.z;
                            float2 d = float2(r * cos(phi), r * sin(phi));

                            posL = _ShapePosL + right * d.x + up * d.y + fwd * z;

                            float3 dirL = BuildConeVelocity(d, max(Rz, 1e-6), right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * _StartSpeed;
                        }
                    }
                    // 3. Donut
                    else if (_ShapeType == 3)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        // 在XY平面生成圆环（主圆环）
                        float phi = 6.28318530718 * urnd.x; // [0, 2π]
                        float R = _ShapeDonutRadius;
                        
                        // 环的厚度采样
                        float r;
                        if (_ShapeEmitFrom == 1) // Surface
                        {
                            r = _ShapeDonutThickness * 0.5; // 表面：使用固定厚度
                        }
                        else // Volume
                        {
                            r = _ShapeDonutThickness * 0.5 * sqrt(urnd.y); // 体积：均匀分布
                        }
                        
                        // 在圆环截面内采样
                        float theta = 6.28318530718 * urnd.z; // 截面角度
                        float2 offset = float2(r * cos(theta), r * sin(theta));
                        
                        // 主圆环中心位置
                        float2 mainRing = float2(R * cos(phi), R * sin(phi));
                        
                        // 最终位置（在XY平面）
                        float2 pos2D = mainRing + offset;
                        posL = _ShapePosL + right * pos2D.x + up * pos2D.y;
                        
                        // 方向：从圆环中心指向粒子位置
                        float3 vdirL = (_AlignToDirection != 0) ? normalize(right * pos2D.x + up * pos2D.y) : fwd;
                        velL = vdirL * _StartSpeed;
                    }
                    // 4. Box
                    else if (_ShapeType == 4)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        float3 sizeB = _ShapeBoxSize;

                        if (_ShapeEmitFrom == 1) // Surface
                        {
                            float3 nLocal;
                            float3 pB = SampleBoxSurface(urnd, sizeB, nLocal);
                            posL = _ShapePosL + right*pB.x + up*pB.y + fwd*pB.z;

                            float3 dirL = (_AlignToDirection != 0) ? nLocal : fwd;
                            velL = dirL * _StartSpeed;
                        }
                        else // Volume
                        {
                            float3 pB = SampleBoxVolume(urnd, sizeB);
                            posL = _ShapePosL + right*pB.x + up*pB.y + fwd*pB.z;

                            float3 dirL = fwd;
                            velL = dirL * _StartSpeed;
                        }
                    }
                    // 5. Circle
                    else if (_ShapeType == 5)
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);
                        
                        float R = _ShapeCircleRadius;
                        float phi = 6.28318530718 * urnd.x; // [0, 2π]
                        
                        float r;
                        if (_ShapeEmitFrom == 1) // Edge (表面)
                        {
                            r = R; // 固定半径，边缘发射
                        }
                        else // Volume
                        {
                            r = R * sqrt(urnd.y); // 体积：均匀分布
                        }
                        
                        float2 pos2D = float2(r * cos(phi), r * sin(phi));
                        posL = _ShapePosL + right * pos2D.x + up * pos2D.y;
                        
                        float3 vdirL = (_AlignToDirection != 0) ? normalize(right * pos2D.x + up * pos2D.y) : fwd;
                        velL = vdirL * _StartSpeed;
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
                        
                        float3 vdirL = (_AlignToDirection != 0) ? right : fwd;
                        velL = vdirL * _StartSpeed;
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
                        
                        float3 vdirL = (_AlignToDirection != 0) ? normalize(right * pos2D.x + up * pos2D.y) : fwd;
                        velL = vdirL * _StartSpeed;
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
                            float2 d = SampleDisk(urnd.xy, innerR, _ShapeConeRadius);
                            posL = _ShapePosL + right * d.x + up * d.y;

                            float3 dirL = BuildConeVelocity(d, _ShapeConeRadius, right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * _StartSpeed;
                        }
                        else if (_ShapeEmitFrom == 0) // Volume
                        {
                            float z  = _ShapeConeLength * pow(urnd.x, 1.0/3.0);
                            float Rz = (_ShapeConeLength > 1e-5) ? (_ShapeConeRadius * (z / _ShapeConeLength)) : 0.0;

                            float Ri = Rz * saturate(1.0 - _ShapeRadiusThickness);
                            float  r = sqrt(lerp(Ri*Ri, Rz*Rz, urnd.y));
                            float  phi = 6.28318530718 * urnd.z;
                            float2 d = float2(r * cos(phi), r * sin(phi));

                            posL = _ShapePosL + right * d.x + up * d.y + fwd * z;

                            float3 dirL = BuildConeVelocity(d, max(Rz, 1e-6), right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * _StartSpeed;
                        }
                        else // Surface fallback to Base
                        {
                            float innerR = _ShapeConeRadius * saturate(1.0 - _ShapeRadiusThickness);
                            float2 d = SampleDisk(urnd.xy, innerR, _ShapeConeRadius);
                            posL = _ShapePosL + right * d.x + up * d.y;

                            float3 dirL = BuildConeVelocity(d, _ShapeConeRadius, right, up, fwd, _ShapeConeAngleRad);
                            velL = dirL * _StartSpeed;
                        }
                    }

                    // finalize spawn in sim space
                    pos  = ToSimSpacePos(posL);
                    vel  = ToSimSpaceVec(velL);
                    life = _StartLifetime;
                    
                    float tSpawn = 0.0;
                    float4 lutColSpawn  = SAMPLE_TEXTURE2D_LOD(_GradLUT, sampler_GradLUT, float2(tSpawn, 0.5), 0);
                    float  lutSizeSpawn = SAMPLE_TEXTURE2D_LOD(_SizeLUT, sampler_SizeLUT, float2(tSpawn, 0.5), 0).r;
                    col   = _StartColor * lutColSpawn;
                    size  = _StartSize * lutSizeSpawn;

                    uint emitOrdinal = EmitOrdinal(id, _EmitStart, (uint)_MaxParticles);
                    stepDt = SpawnAgeThisFrame(emitOrdinal);
                }
               
                // Update alive particles
                if (life > 0.0)
                {
                    if (!spawn)
                    {
                        stepDt = _DeltaTime;
                    }

                    life = max(0.0, life - stepDt);

                    // gravity already in correct space
                    vel += _GravityWS * stepDt;
                    pos += vel * stepDt;

                    // lifetime normalized 0..1 (0 birth, 1 death)
                    float t = saturate(1.0 - (life / max(_StartLifetime, 1e-5)));

                    // Color over lifetime & Size over lifetime via LUTs
                    float4 lutCol  = SAMPLE_TEXTURE2D_LOD(_GradLUT, sampler_GradLUT, float2(t, 0.5), 0);
                    float  lutSize = SAMPLE_TEXTURE2D_LOD(_SizeLUT, sampler_SizeLUT, float2(t, 0.5), 0).r;

                    col   = _StartColor * lutCol;
                    size  = _StartSize   * lutSize;
                }
                else
                {
                    // keep as dead
                    life = 0.0;
                    size = 0.0;
                    col.a = 0.0;
                }

                o.PosLife = float4(pos, life);
                o.VelSize = float4(vel, size);
                o.Color   = col;
                return o;
            }
            ENDHLSL
        }
    }
}
