using System.Collections.Generic;
using UnityEngine;

namespace VFX
{
    // 라이트닝 구체의 실제 비주얼을 만들고 매 프레임 갱신하는 드라이버.
    // 런타임에선 Update가, 에디터 미리보기에선 툴이 Tick(time)을 직접 호출한다(파티클의 Simulate 대체).
    // 코어는 펄스하는 발광 quad, 아크는 '글로우 라인 + 코어 라인' 2겹 LineRenderer로 깜빡인다.
    public class LightningOrbDriver : MonoBehaviour
    {
        LightningOrbParams param;
        Transform core;
        Texture2D coreTex;            // Disc/Ring일 때 생성한 텍스처(이 드라이버가 파괴 책임). Glow는 공유 캐시.
        LineRenderer[] glowLines;
        LineRenderer[] coreLines;
        float[] arcAngles;            // 아크별 기준 각도(깜빡여도 대체로 유지)
        static readonly List<Vector3> sBuf = new List<Vector3>();

        // 빌더가 1회 호출: 자식(코어/아크)을 구성한다.
        public void Setup(LightningOrbParams p)
        {
            param = p;

            // 코어 = 부모 트랜스폼(펄스로 스케일) 아래에 [발광 헤일로] + [스타일 본체]를 겹친다.
            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(transform, false);
            core = coreGo.transform;

            // Glow는 밝게(흰색 많이 섞음), Disc/Ring은 색을 살려 형태가 드러나게 한다.
            float flMix = p.coreStyle == CoreStyle.Glow ? 0.65f : 0.28f;
            Color bodyCol = Color.Lerp(p.color, Color.white, flMix);
            bodyCol.a = p.coreAlpha;

            // Disc/Ring은 가장자리 바로 바깥에만 얇은 발광 테두리를 깐다(형태를 죽이지 않게 본체보다 작게).
            if (p.coreStyle != CoreStyle.Glow)
            {
                Color rimCol = Color.Lerp(p.color, Color.white, 0.3f); rimCol.a = p.coreAlpha * 0.35f;
                var rim = LightningBolt.MakeGlowQuad(core, "CoreRimGlow", p.sortingOrder + 2, rimCol, null);
                rim.transform.localScale = Vector3.one * 1.18f;
            }

            Texture cTexUse = null;   // null=머티리얼 기본(Glow 공유 텍스처)
            if (p.coreStyle == CoreStyle.Disc) { coreTex = LightningBolt.MakeSphere(128); cTexUse = coreTex; }
            else if (p.coreStyle == CoreStyle.Ring) { coreTex = LightningBolt.MakeRing(128, p.coreThickness); cTexUse = coreTex; }

            LightningBolt.MakeGlowQuad(core, "CoreBody", p.sortingOrder + 3, bodyCol, cTexUse);

            int nArc = Mathf.Max(0, p.arcCount);
            glowLines = new LineRenderer[nArc];
            coreLines = new LineRenderer[nArc];
            arcAngles = new float[nArc];

            Color glowCol = p.color; glowCol.a = 0.45f;
            Color brightCol = Color.Lerp(p.color, Color.white, 0.7f);

            for (int nI = 0; nI < nArc; nI++)
            {
                arcAngles[nI] = (nI + 0.5f) / nArc * Mathf.PI * 2f;
                glowLines[nI] = LightningBolt.CreateLine(transform, "ArcGlow", p.coreWidth * p.glowWidthMul, p.sortingOrder, glowCol);
                coreLines[nI] = LightningBolt.CreateLine(transform, "ArcCore", p.coreWidth, p.sortingOrder + 1, brightCol);
            }
        }


        void Update()
        {
            if (Application.isPlaying) Tick(Time.time);
        }


        void OnDestroy()
        {
            if (coreTex == null) return;
            if (Application.isPlaying) Destroy(coreTex);
            else DestroyImmediate(coreTex);
            coreTex = null;
        }


        // time초 시점의 모양으로 코어 펄스와 모든 아크 경로를 갱신한다.
        public void Tick(float flTime)
        {
            if (param == null) return;

            if (core != null)
            {
                float flPulse = 1f + param.corePulse * Mathf.Sin(flTime * param.pulseSpeed);
                core.localScale = Vector3.one * (param.coreSize * flPulse);
            }

            int nArc = glowLines != null ? glowLines.Length : 0;
            for (int nI = 0; nI < nArc; nI++)
            {
                var cRng = new LightningBolt.Rng(LightningBolt.FrameSeed(flTime, param.flickerInterval, nI * 911 + 1));

                float flAngle = arcAngles[nI] + cRng.Range(-0.35f, 0.35f);
                float flLen = cRng.Range(param.arcLengthMin, param.arcLengthMax);
                Vector3 dir = new Vector3(Mathf.Cos(flAngle), Mathf.Sin(flAngle), 0f);
                // 시작점: 중심(코어 크기 무관) 또는 코어 표면. 끝점 = 시작 + 아크 길이 → 코어를 키워도 아크 길이 유지.
                float flStart = param.arcOrigin == ArcOrigin.Surface ? param.coreSize * 0.5f : 0f;
                Vector3 a = dir * flStart;
                Vector3 b = dir * (flStart + flLen);

                LightningBolt.FillBolt(sBuf, a, b, param.generations, param.displace, param.damp, ref cRng);
                LightningBolt.SetLine(glowLines[nI], sBuf);
                LightningBolt.SetLine(coreLines[nI], sBuf);
            }
        }
    }
}
