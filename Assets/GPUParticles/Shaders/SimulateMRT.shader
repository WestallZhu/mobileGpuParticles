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

                // Initial direction already in simulation space
                float3  _InitialDir;
                float   _Pad0;

                // ----- Shape (generic) -----
                int     _ShapeType;          // 0 Point, 1 Cone, 2 Box, 3 Sphere, 4 Hemisphere
                int     _ShapeEmitFrom;      // 0 Volume, 1 Surface, 2 Base (for Cone), unused for Sphere/Box
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
                int2 pxy = int2(i.uv * _GridSize);
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
                if (spawn)
                {
                    float3 urnd = Hash03(id * 9781u + 0x9E3779B9u);
                    float2 u2a = urnd.xy;
                    float2 u2b = float2(urnd.y, urnd.z);

                    float3 posL = 0;
                    float3 velL = 0;

                    if (_ShapeType == 1) // Cone
                    {
                        float3 right = normalize(_ShapeRightL);
                        float3 up    = normalize(_ShapeUpL);
                        float3 fwd   = normalize(_ShapeFwdL);

                        if (_ShapeEmitFrom == 2) // Base disc at shape position
                        {
                            float innerR = _ShapeConeRadius * saturate(1.0 - _ShapeRadiusThickness);
                            float2 d = SampleDisk(urnd.xy, innerR, _ShapeConeRadius);
                            posL = _ShapePosL + right * d.x + up * d.y;

                            float3 dirL = SampleDirCone(fwd, _ShapeConeAngleRad, urnd.yz);
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

    
                            float2 u2 = float2(Hash01(id*1669u), Hash01(id*7331u)); 
                            float3 dirL = SampleDirCone(fwd, _ShapeConeAngleRad, u2);
                            velL = dirL * _StartSpeed;
                        }
                    }
                    else if (_ShapeType == 2) // Box
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

                            float3 dirL = float3(0, 0, 1);              // emitter-local forward (Z)
                            velL = dirL * _StartSpeed;
                        }
                    }
                    else if (_ShapeType == 3) // Sphere
                    {
                        float R = _ShapeSphereRadius;
                        float3 dirL = SampleSphereDir(u2a);
                        float  r    = RadiusWithThickness(urnd.z, R, _ShapeRadiusThickness);
                        float3 pL   = dirL * r;
                        posL = _ShapePosL + (normalize(_ShapeRightL) * pL.x + normalize(_ShapeUpL) * pL.y + normalize(_ShapeFwdL) * pL.z);

                        float3 vdirL = (_AlignToDirection != 0) ? normalize(pL) : float3(0,0,1);
                        velL = vdirL * _StartSpeed;
                    }
                    else if (_ShapeType == 4) // Hemisphere (local +Z half)
                    {
                        float R = _ShapeSphereRadius;
                        float3 dirL = SampleHemisphereDir(u2a); // z>=0
                        float  r    = RadiusWithThickness(urnd.z, R, _ShapeRadiusThickness);
                        float3 pL   = dirL * r;
                        posL = _ShapePosL + (normalize(_ShapeRightL) * pL.x + normalize(_ShapeUpL) * pL.y + normalize(_ShapeFwdL) * pL.z);

                        float3 vdirL = (_AlignToDirection != 0) ? normalize(pL) : float3(0,0,1);
                        velL = vdirL * _StartSpeed;
                    }
                    else // Point (fallback)
                    {
                        float3 dirL = _InitialDir;
                        posL = float3(0,0,0);
                        velL = normalize(dirL) * _StartSpeed;
                    }

                    // finalize spawn in sim space
                    pos  = ToSimSpacePos(posL);
                    vel  = ToSimSpaceVec(velL);
                    life = _StartLifetime;
                    size = _StartSize;
                    col  = _StartColor;
                }
               
                // Update alive particles
                if (life > 0.0)
                {
                    life = max(0.0, life - _DeltaTime);

                    // gravity already in correct space
                    vel += _GravityWS * _DeltaTime;
                    pos += vel * _DeltaTime;

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
