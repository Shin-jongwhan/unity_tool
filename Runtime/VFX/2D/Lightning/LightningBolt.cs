using System.Collections.Generic;
using UnityEngine;

namespace VFX
{
    // 라이트닝 계열 VFX 공용 모듈. (라이트닝 구체 / 체인 라이트닝이 공유)
    // - 공유 머티리얼: 2D 발광 라인. URP '흰 네모' 함정 회피를 위해 Sprites/Default + 부드러운 단면 텍스처를 쓴다.
    //   가산 셰이더 대신 '넓고 흐린 라인 + 가늘고 밝은 라인' 2겹으로 발광을 흉내 내 파이프라인 독립적으로 동작.
    // - 결정적 PRNG(xorshift32): 시간 시드 기반으로 번개를 재생성 → 에디터 프리뷰와 런타임이 동일하게 깜빡인다.
    // - 미드포인트 디스플레이스먼트로 지그재그 번개 줄기(폴리라인)를 만든다.
    public static class LightningBolt
    {
        static Material lineMat;
        static Material dotMat;

        // 전역 Random 상태를 건드리지 않는 가벼운 결정적 난수(xorshift32).
        public struct Rng
        {
            uint nState;

            public Rng(int nSeed)
            {
                nState = (uint)nSeed;
                if (nState == 0) nState = 0x9E3779B9u;
            }

            public float Next01()
            {
                nState ^= nState << 13;
                nState ^= nState >> 17;
                nState ^= nState << 5;
                return (nState & 0xFFFFFF) / 16777216f;
            }

            public float Range(float flMin, float flMax)
            {
                return flMin + (flMax - flMin) * Next01();
            }
        }


        // 시간을 flicker 간격으로 양자화해 프레임 시드를 만든다(같은 간격 = 같은 모양).
        public static int FrameSeed(float flTime, float flFlickerInterval, int nSalt)
        {
            int nFrame = flFlickerInterval <= 0f ? 0 : Mathf.FloorToInt(flTime / flFlickerInterval);
            return nFrame * 73856093 ^ nSalt * 19349663;
        }


        public static Material Material
        {
            get { EnsureMaterial(); return lineMat; }
        }


        static void EnsureMaterial()
        {
            if (lineMat != null) return;
            lineMat = new Material(Shader.Find("Sprites/Default"));
            lineMat.SetTexture("_MainTex", MakeLineTex(32));
        }


        static Texture2D dotTex;

        // 둥근 발광(코어 글로우 / 노드 임팩트 섬광)용 공유 머티리얼. 기본 텍스처는 부드러운 점(Glow).
        // 코어 표현 방식은 호출측이 MakeGlowQuad에 다른 텍스처를 넘겨 MaterialPropertyBlock로 교체한다.
        public static Material DotMaterial
        {
            get
            {
                if (dotMat == null)
                {
                    dotMat = new Material(Shader.Find("Sprites/Default"));
                    dotMat.SetTexture("_MainTex", DotTexture);
                }
                return dotMat;
            }
        }


        // Glow: 가운데 꽉 찬 발광 → 가장자리 투명. 공유 캐시(파괴하지 말 것).
        public static Texture2D DotTexture
        {
            get { if (dotTex == null) dotTex = MakeSoftDot(128); return dotTex; }
        }


        // 가운데 불투명 → 가장자리 투명한 둥근 점 텍스처.
        static Texture2D MakeSoftDot(int nSize)
        {
            var cTex = new Texture2D(nSize, nSize, TextureFormat.RGBA32, false);
            cTex.wrapMode = TextureWrapMode.Clamp;
            var center = new Vector2(nSize / 2f, nSize / 2f);
            float flRadius = nSize / 2f;
            var lsPx = new Color[nSize * nSize];
            for (int nY = 0; nY < nSize; nY++)
                for (int nX = 0; nX < nSize; nX++)
                {
                    float flD = Vector2.Distance(new Vector2(nX, nY), center) / flRadius;
                    float flA = Mathf.Clamp01(1f - flD);
                    flA = flA * flA;
                    lsPx[nY * nSize + nX] = new Color(1f, 1f, 1f, flA);
                }
            cTex.SetPixels(lsPx);
            cTex.Apply();
            return cTex;
        }


        // Disc: 입체 셰이딩한 '단단한 에너지 볼'. 명암(법선·빛 방향)을 RGB에 굽고 가장자리 림 라이트를 더한다.
        // 색은 호출측이 _Color로 곱하므로 여기선 흰색 명암만 만든다. 호출측이 소유·파괴한다.
        public static Texture2D MakeSphere(int nSize)
        {
            var cTex = new Texture2D(nSize, nSize, TextureFormat.RGBA32, false);
            cTex.wrapMode = TextureWrapMode.Clamp;
            float flHalf = nSize / 2f;
            Vector3 vL = new Vector3(-0.4f, 0.45f, 0.8f).normalized;  // 좌상단에서 비추는 빛
            var lsPx = new Color[nSize * nSize];
            for (int nY = 0; nY < nSize; nY++)
                for (int nX = 0; nX < nSize; nX++)
                {
                    float flDx = (nX + 0.5f - flHalf) / flHalf;
                    float flDy = (nY + 0.5f - flHalf) / flHalf;
                    float flD2 = flDx * flDx + flDy * flDy;
                    if (flD2 >= 1f) { lsPx[nY * nSize + nX] = new Color(1f, 1f, 1f, 0f); continue; }

                    float flNz = Mathf.Sqrt(1f - flD2);
                    float flNdotl = Mathf.Max(0f, flDx * vL.x + flDy * vL.y + flNz * vL.z);
                    float flShade = 0.5f + 0.5f * flNdotl;             // 발광체라 어두운 면도 충분히 밝게
                    float flRim = Mathf.Pow(1f - flNz, 2.2f) * 0.85f;  // 가장자리 림 라이트(흰빛)
                    float flVal = Mathf.Clamp01(flShade + flRim);

                    float flD = Mathf.Sqrt(flD2);
                    float flEdge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.92f, 1f, flD));
                    float flA = 1f - flEdge;                            // 안쪽 불투명, 가장자리만 부드럽게
                    lsPx[nY * nSize + nX] = new Color(flVal, flVal, flVal, flA);
                }
            cTex.SetPixels(lsPx);
            cTex.Apply();
            return cTex;
        }


        // Ring: 가장자리 근처 밝은 띠 + 옅은 내부 → 구 '껍질'. flThickness(0~1)로 띠 굵기 조절.
        // 호출측이 소유·파괴한다.
        public static Texture2D MakeRing(int nSize, float flThickness)
        {
            var cTex = new Texture2D(nSize, nSize, TextureFormat.RGBA32, false);
            cTex.wrapMode = TextureWrapMode.Clamp;
            var center = new Vector2(nSize / 2f, nSize / 2f);
            float flRadius = nSize / 2f;
            float flSigma = Mathf.Lerp(0.04f, 0.34f, Mathf.Clamp01(flThickness)); // 띠 폭
            float flPeak = 0.85f - flSigma * 0.4f;                                 // 두꺼우면 안쪽으로
            var lsPx = new Color[nSize * nSize];
            for (int nY = 0; nY < nSize; nY++)
                for (int nX = 0; nX < nSize; nX++)
                {
                    float flD = Vector2.Distance(new Vector2(nX, nY), center) / flRadius;
                    float flBand = Mathf.Exp(-((flD - flPeak) * (flD - flPeak)) / (2f * flSigma * flSigma));
                    float flInner = 0.10f * Mathf.Clamp01(1f - flD);
                    float flA = flD >= 1f ? 0f : Mathf.Clamp01(flBand + flInner);
                    flA *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.96f, 1f, flD));
                    lsPx[nY * nSize + nX] = new Color(1f, 1f, 1f, flA);
                }
            cTex.SetPixels(lsPx);
            cTex.Apply();
            return cTex;
        }


        // 라인 단면(폭 방향) 부드러운 falloff 텍스처. LineRenderer는 V를 폭 방향에 매핑하므로
        // 가운데 밝고 가장자리 투명 → 발광하는 가는 줄기처럼 보인다.
        static Texture2D MakeLineTex(int nAcross)
        {
            var cTex = new Texture2D(4, nAcross, TextureFormat.RGBA32, false);
            cTex.wrapMode = TextureWrapMode.Clamp;
            var lsPx = new Color[4 * nAcross];
            for (int nY = 0; nY < nAcross; nY++)
            {
                float flV = (nY + 0.5f) / nAcross;
                float flD = Mathf.Abs(flV - 0.5f) * 2f;
                float flA = Mathf.Clamp01(1f - flD);
                flA = flA * flA;
                for (int nX = 0; nX < 4; nX++)
                    lsPx[nY * 4 + nX] = new Color(1f, 1f, 1f, flA);
            }
            cTex.SetPixels(lsPx);
            cTex.Apply();
            return cTex;
        }


        // 둥근 발광 quad 자식 생성(코어 글로우/노드 임팩트 섬광용). DotMaterial을 공유하고
        // 색/텍스처는 MaterialPropertyBlock로 입혀 머티리얼 인스턴싱(누수) 없이 인스턴스별로 지정한다.
        // cTex가 null이면 머티리얼 기본(Glow) 텍스처를 쓴다. 크기는 호출측이 localScale로 제어.
        public static GameObject MakeGlowQuad(Transform cParent, string sName, int nOrder, Color color, Texture cTex = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = sName;
            go.transform.SetParent(cParent, false);

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = DotMaterial;
            r.sortingOrder = nOrder;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", color);
            if (cTex != null) mpb.SetTexture("_MainTex", cTex);
            r.SetPropertyBlock(mpb);
            return go;
        }


        // LineRenderer 자식 생성(로컬 좌표, 카메라 정면 정렬, 둥근 캡).
        public static LineRenderer CreateLine(Transform cParent, string sName, float flWidth, int nOrder, Color color)
        {
            var go = new GameObject(sName);
            go.transform.SetParent(cParent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.widthMultiplier = flWidth;
            lr.sharedMaterial = Material;
            lr.sortingOrder = nOrder;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }


        // 폴리라인 점들을 LineRenderer에 입힌다(GC 없이 점 단위 set).
        public static void SetLine(LineRenderer lr, List<Vector3> lsPts)
        {
            int nCount = lsPts.Count;
            lr.positionCount = nCount;
            for (int nI = 0; nI < nCount; nI++)
                lr.SetPosition(nI, lsPts[nI]);
        }


        // 미드포인트 디스플레이스먼트로 a→b 지그재그 번개 경로를 lsOut에 채운다(2D, XY 평면).
        // nGenerations: 분할 횟수(점 수 = 2^n + 1), flDisplace: 첫 분할의 수직 흔들림 폭, flDamp: 세대별 감쇠.
        public static void FillBolt(List<Vector3> lsOut, Vector3 a, Vector3 b, int nGenerations, float flDisplace, float flDamp, ref Rng cRng)
        {
            lsOut.Clear();
            lsOut.Add(a);
            lsOut.Add(b);

            float flAmp = flDisplace;
            for (int nGen = 0; nGen < nGenerations; nGen++)
            {
                for (int nI = 0; nI < lsOut.Count - 1; nI += 2)
                {
                    Vector3 p0 = lsOut[nI];
                    Vector3 p1 = lsOut[nI + 1];
                    Vector3 mid = (p0 + p1) * 0.5f;
                    Vector3 dir = (p1 - p0);
                    Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;
                    mid += perp * cRng.Range(-flAmp, flAmp);
                    lsOut.Insert(nI + 1, mid);
                }
                flAmp *= flDamp;
            }
        }
    }
}
