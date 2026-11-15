// Langit realistis GixBoy: hamburan atmosfer (Rayleigh + Mie, model Preetham),
// awan volumetrik semu yang bergerak, matahari, serta bintang & bulan di malam hari.
// Arah matahari & faktor malam diatur oleh DayNightController (_GixSunDir, _GixNight).
Shader "GixBoy/RealisticSky"
{
    Properties
    {
        _Exposure ("Exposure", Range(0, 4)) = 1
        _SkyExposureLin ("Kecerahan Langit", Range(0.1, 3)) = 1
        _SkyTint ("Sky Tint", Color) = (0.5, 0.5, 0.5, 1)
        _Turbidity ("Turbidity (kabut)", Range(1, 20)) = 4.5
        _Rayleigh ("Rayleigh", Range(0, 4)) = 1.6
        _MieCoefficient ("Mie", Range(0, 0.1)) = 0.005
        _MieDirectionalG ("Mie G", Range(0, 0.999)) = 0.8
        _SunSize ("Ukuran Matahari", Range(0.9990, 0.99999)) = 0.99994
        _CloudCoverage ("Tutupan Awan", Range(0, 1)) = 0.5
        _CloudDensity ("Kepadatan Awan", Range(0, 4)) = 1.6
        _CloudScale ("Skala Awan", Range(0.1, 5)) = 1.0
        _CloudSpeed ("Kecepatan Awan", Range(0, 0.1)) = 0.006
        _StarIntensity ("Bintang", Range(0, 3)) = 1.2
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            float _Exposure, _Turbidity, _Rayleigh, _MieCoefficient, _MieDirectionalG, _SunSize;
            float _CloudCoverage, _CloudDensity, _CloudScale, _CloudSpeed, _StarIntensity;
            float4 _SkyTint;
            float _SkyExposureLin;
            float4 _GixSunDir;   // xyz = arah ke matahari/bulan
            float _GixNight;     // 0 = siang, 1 = malam

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            // ---------- atmosfer (Preetham) ----------
            static const float3 UP = float3(0, 1, 0);
            static const float PI = 3.14159265;
            static const float3 TOTAL_RAYLEIGH = float3(5.804542996261093E-6, 1.3562911419845635E-5, 3.0265902468824876E-5);
            static const float3 MIE_CONST = float3(1.8399918514433978E14, 2.7798023919660528E14, 4.0790479543861094E14);
            static const float RAYLEIGH_ZENITH = 8.4E3;
            static const float MIE_ZENITH = 1.25E3;
            static const float SUN_INTENSITY = 1000.0;

            float SunIntensity(float zenithCos)
            {
                float cutoff = 1.6110731556870734;
                float steep = 1.5;
                return SUN_INTENSITY * max(0.0, 1.0 - exp(-((cutoff - acos(clamp(zenithCos, -1, 1))) / steep)));
            }

            float3 TotalMie(float T) { float c = (0.2 * T) * 10E-18; return 0.434 * c * MIE_CONST; }

            float RayleighPhase(float c) { return (3.0 / (16.0 * PI)) * (1.0 + c * c); }
            float HGPhase(float c, float g) { float g2 = g * g; return (1.0 / (4.0 * PI)) * ((1.0 - g2) / pow(max(1.0 - 2.0 * g * c + g2, 1e-4), 1.5)); }

            float3 Atmosphere(float3 v, float3 sunDir, out float3 sunExt, out float sunfade, out float3 sunDisk)
            {
                float sunE = SunIntensity(dot(sunDir, UP));
                sunfade = 1.0 - saturate(1.0 - exp(sunDir.y / 0.45));
                float rayleighCoef = _Rayleigh - (1.0 * (1.0 - sunfade));
                float3 betaR = TOTAL_RAYLEIGH * rayleighCoef;
                float3 betaM = TotalMie(_Turbidity) * _MieCoefficient;

                float vy = max(v.y, 0.0);
                float zen = acos(vy);
                float inv = 1.0 / (cos(zen) + 0.15 * pow(max(93.885 - degrees(zen), 0.0001), -1.253));
                float sR = RAYLEIGH_ZENITH * inv;
                float sM = MIE_ZENITH * inv;
                float3 Fex = exp(-(betaR * sR + betaM * sM));

                // pelemahan cahaya matahari (untuk warna awan)
                float szen = acos(max(sunDir.y, 0.0));
                float sinv = 1.0 / (cos(szen) + 0.15 * pow(max(93.885 - degrees(szen), 0.0001), -1.253));
                sunExt = exp(-(betaR * RAYLEIGH_ZENITH * sinv + betaM * MIE_ZENITH * sinv));

                float cosTheta = dot(v, sunDir);
                float3 betaRTheta = betaR * RayleighPhase(cosTheta * 0.5 + 0.5);
                float3 betaMTheta = betaM * HGPhase(cosTheta, _MieDirectionalG);

                float3 Lin = pow(max(sunE * ((betaRTheta + betaMTheta) / (betaR + betaM)) * (1.0 - Fex), 0.0), 1.5);
                Lin *= lerp(1.0, pow(max(sunE * ((betaRTheta + betaMTheta) / (betaR + betaM)) * Fex, 0.0), 0.5), saturate(pow(1.0 - dot(UP, sunDir), 5.0)));

                float3 L0 = 0.1 * Fex;
                float sd = smoothstep(_SunSize, _SunSize + 0.00002, cosTheta);
                sunDisk = sunE * 19000.0 * Fex * sd;
                L0 += sunDisk;

                float3 tex = (Lin + L0) * 0.04 + float3(0.0, 0.0003, 0.00075);
                return pow(max(tex, 0.0), 1.0 / (1.2 + 1.2 * sunfade));
            }

            // tone mapping ACES + konversi ke ruang tampilan (proyek memakai Gamma color space)
            float3 ACES(float3 x) { return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)); }
            float3 ToDisplay(float3 lin) { return pow(ACES(lin), 1.0 / 2.2); }

            // ---------- noise ----------
            float hash12(float2 p) { float3 p3 = frac(float3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash12(i), hash12(i + float2(1, 0)), u.x), lerp(hash12(i + float2(0, 1)), hash12(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
                for (int i = 0; i < 6; i++) { s += a * vnoise(p); p = mul(m, p); a *= 0.5; }
                return s;
            }
            float CloudDensityAt(float2 uv, float t)
            {
                float2 wind = float2(t, t * 0.35);
                float2 q = float2(fbm(uv * 0.6 + wind * 0.5), fbm(uv * 0.6 + float2(5.2, 1.3) - wind * 0.3));
                float n = fbm(uv + q * 0.9 + wind);
                float detail = fbm(uv * 3.1 - wind * 2.0) * 0.25;
                float c = 1.0 - _CloudCoverage;
                return saturate((n + detail - c * 0.85 - 0.12) * 2.2);
            }

            float3 frag(v2f i) : SV_Target
            {
                float3 v = normalize(i.dir);
                float3 lightDir = normalize(_GixSunDir.xyz + float3(0, 1e-4, 0));
                float night = saturate(_GixNight);

                // siang: matahari = arah cahaya; malam: matahari di bawah horizon
                float3 sunDir = night > 0.5 ? normalize(float3(lightDir.x, -0.25, lightDir.z)) : lightDir;
                float3 sunExt; float sunfade; float3 sunDisk;
                float3 sky = ToDisplay(Atmosphere(v, sunDir, sunExt, sunfade, sunDisk) * 0.5 * _SkyExposureLin);
                float skyL = dot(sky, float3(0.299, 0.587, 0.114));
                sky = saturate(lerp(skyL.xxx, sky, 1.35));

                // langit malam: biru tua + bulan + bintang
                float3 nightSky = lerp(float3(0.035, 0.05, 0.10), float3(0.008, 0.012, 0.03), saturate(v.y * 1.5));
                nightSky += float3(0.06, 0.05, 0.08) * pow(1.0 - saturate(v.y), 6.0); // cahaya kota di horizon
                float3 moonDir = normalize(lightDir);
                float md = dot(v, moonDir);
                nightSky += float3(0.9, 0.93, 1.0) * smoothstep(0.99955, 0.99965, md) * 1.6;
                nightSky += float3(0.25, 0.3, 0.45) * pow(saturate(md), 400.0) * 0.6;
                float2 sp = v.xz / max(v.y + 0.15, 0.05) * 140.0;
                float2 cell = floor(sp);
                float star = hash12(cell);
                float tw = 0.6 + 0.4 * sin(_Time.y * (2.0 + star * 4.0) + star * 40.0);
                float sdist = length(frac(sp) - 0.5);
                nightSky += (star > 0.985 ? 1.0 : 0.0) * smoothstep(0.18, 0.0, sdist) * tw * _StarIntensity * saturate(v.y * 4.0);

                float3 col = lerp(sky, nightSky, night);

                // tanah di bawah horizon: gelap mengikuti warna langit horizon
                if (v.y < 0.0)
                {
                    float3 hz = col;
                    col = lerp(hz, hz * 0.35, saturate(-v.y * 6.0));
                }

                // ---------- awan ----------
                if (v.y > 0.0)
                {
                    float t = _Time.y * _CloudSpeed;
                    float2 uv = (v.xz / (v.y + 0.08)) * 0.55 * _CloudScale;
                    float d = CloudDensityAt(uv, t);
                    if (d > 0.001)
                    {
                        float2 toSun = normalize(sunDir.xz + 1e-4) * 0.09;
                        float d1 = CloudDensityAt(uv + toSun, t);
                        float d2 = CloudDensityAt(uv + toSun * 2.2, t);
                        float shadow = exp(-(d1 + d2 * 0.6) * _CloudDensity * 1.4);
                        float3 sunCol = lerp(float3(1.0, 0.55, 0.3), float3(1.0, 0.98, 0.95), saturate(sunDir.y * 3.0));
                        sunCol *= saturate(sunExt * 1.6 + 0.15);
                        float3 ambient = lerp(float3(0.5, 0.56, 0.68), float3(0.78, 0.82, 0.9), saturate(sunDir.y * 2.0)) * saturate(sunDir.y * 4.0 + 0.35);
                        float silver = pow(saturate(dot(v, sunDir)), 12.0) * 1.5;
                        float3 lit = ambient * 0.72 + sunCol * (shadow * 0.75 + silver * (1.0 - d)) * 0.8;
                        float3 nightCloud = float3(0.06, 0.07, 0.10) + float3(0.25, 0.27, 0.32) * shadow * pow(saturate(dot(v, moonDir) * 0.5 + 0.5), 4.0) * 0.5;
                        float3 cc = lerp(lit, nightCloud, night);
                        float alpha = saturate(1.0 - exp(-d * _CloudDensity * 2.5));
                        alpha *= smoothstep(0.0, 0.22, v.y); // memudar ke horizon (kabut jauh)
                        // matahari tertutup awan tebal
                        col = lerp(col, cc, alpha);
                    }
                }

                col *= lerp(1.0, _Exposure, 0.35) * lerp(float3(1, 1, 1), _SkyTint.rgb * 2.0, 0.12);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
