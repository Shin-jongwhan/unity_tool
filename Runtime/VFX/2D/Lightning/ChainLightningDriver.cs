using System.Collections.Generic;
using UnityEngine;

namespace VFX
{
    // 체인 라이트닝의 실제 비주얼을 만들고 매 프레임 갱신하는 드라이버.
    // 노드는 Setup 때 고정 배치되고(타깃 역할), 줄기/곁가지만 깜빡임마다 재생성된다.
    // 각 줄기는 '글로우 라인 + 코어 라인' 2겹, 노드마다 펄스하는 발광 quad를 둔다.
    public class ChainLightningDriver : MonoBehaviour
    {
        ChainLightningParams param;
        Vector3[] nodes;
        Transform[] nodeFlashes;
        LineRenderer[] segGlow;        // 구간별 줄기(글로우/코어)
        LineRenderer[] segCore;
        LineRenderer[] branchGlow;     // 곁가지(글로우/코어), 길이 = 구간수 * branchPerSegment
        LineRenderer[] branchCore;
        static readonly List<Vector3> sBuf = new List<Vector3>();

        // 빌더가 1회 호출: 노드 배치 + 줄기/곁가지/섬광 자식 구성.
        public void Setup(ChainLightningParams p)
        {
            param = p;

            int nNodes = Mathf.Max(2, p.nodeCount);
            int nSeg = nNodes - 1;
            nodes = new Vector3[nNodes];
            nodes[0] = Vector3.zero;
            for (int nI = 1; nI < nNodes; nI++)
            {
                var cRng = new LightningBolt.Rng(nI * 92821 + 7);
                float flX = nodes[nI - 1].x + p.segmentLength * cRng.Range(0.8f, 1.2f);
                float flY = cRng.Range(-p.spread, p.spread);
                nodes[nI] = new Vector3(flX, flY, 0f);
            }

            Color glowCol = p.color; glowCol.a = 0.45f;
            Color brightCol = Color.Lerp(p.color, Color.white, 0.7f);

            segGlow = new LineRenderer[nSeg];
            segCore = new LineRenderer[nSeg];
            for (int nI = 0; nI < nSeg; nI++)
            {
                segGlow[nI] = LightningBolt.CreateLine(transform, "SegGlow", p.coreWidth * p.glowWidthMul, p.sortingOrder, glowCol);
                segCore[nI] = LightningBolt.CreateLine(transform, "SegCore", p.coreWidth, p.sortingOrder + 1, brightCol);
            }

            int nBranch = nSeg * Mathf.Max(0, p.branchPerSegment);
            branchGlow = new LineRenderer[nBranch];
            branchCore = new LineRenderer[nBranch];
            Color bGlow = p.color; bGlow.a = 0.35f;
            for (int nI = 0; nI < nBranch; nI++)
            {
                branchGlow[nI] = LightningBolt.CreateLine(transform, "BranchGlow", p.coreWidth * p.glowWidthMul * 0.6f, p.sortingOrder, bGlow);
                branchCore[nI] = LightningBolt.CreateLine(transform, "BranchCore", p.coreWidth * 0.7f, p.sortingOrder + 1, brightCol);
            }

            nodeFlashes = new Transform[nNodes];
            for (int nI = 0; nI < nNodes; nI++)
            {
                var go = LightningBolt.MakeGlowQuad(transform, "NodeFlash", p.sortingOrder + 2, Color.Lerp(p.color, Color.white, 0.6f));
                go.transform.localPosition = nodes[nI];
                nodeFlashes[nI] = go.transform;
            }
        }


        void Update()
        {
            if (Application.isPlaying) Tick(Time.time);
        }


        // time초 시점의 모양으로 줄기/곁가지/노드 섬광을 갱신한다.
        public void Tick(float flTime)
        {
            if (param == null) return;

            int nSeg = segGlow != null ? segGlow.Length : 0;
            int nPer = Mathf.Max(0, param.branchPerSegment);

            for (int nS = 0; nS < nSeg; nS++)
            {
                // 줄기: 구간 길이에 비례해 흔들림을 키워 균일한 지그재그가 되게 한다.
                var cRng = new LightningBolt.Rng(LightningBolt.FrameSeed(flTime, param.flickerInterval, nS * 1009 + 1));
                Vector3 a = nodes[nS];
                Vector3 b = nodes[nS + 1];
                float flDisp = param.displace * Mathf.Clamp((b - a).magnitude / param.segmentLength, 0.5f, 2f);
                LightningBolt.FillBolt(sBuf, a, b, param.generations, flDisp, param.damp, ref cRng);
                LightningBolt.SetLine(segGlow[nS], sBuf);
                LightningBolt.SetLine(segCore[nS], sBuf);

                Vector3 dir = (b - a).normalized;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                for (int nK = 0; nK < nPer; nK++)
                {
                    int nBi = nS * nPer + nK;
                    var cBr = new LightningBolt.Rng(LightningBolt.FrameSeed(flTime, param.flickerInterval, nS * 1009 + 101 + nK));
                    bool bShow = cBr.Next01() < param.branchChance;
                    if (!bShow)
                    {
                        branchGlow[nBi].positionCount = 0;
                        branchCore[nBi].positionCount = 0;
                        continue;
                    }
                    Vector3 from = Vector3.Lerp(a, b, cBr.Range(0.25f, 0.75f));
                    float flSide = cBr.Next01() < 0.5f ? 1f : -1f;
                    Vector3 outDir = (perp * flSide + dir * cBr.Range(-0.4f, 0.4f)).normalized;
                    Vector3 to = from + outDir * (param.branchLength * cBr.Range(0.6f, 1.2f));
                    LightningBolt.FillBolt(sBuf, from, to, Mathf.Max(1, param.generations - 2), param.displace * 0.6f, param.damp, ref cBr);
                    LightningBolt.SetLine(branchGlow[nBi], sBuf);
                    LightningBolt.SetLine(branchCore[nBi], sBuf);
                }
            }

            int nNodes = nodeFlashes != null ? nodeFlashes.Length : 0;
            for (int nI = 0; nI < nNodes; nI++)
            {
                // 노드마다 위상을 어긋나게 해 번갈아 번쩍이게 한다.
                float flPulse = 1f + param.nodePulse * Mathf.Sin(flTime * param.pulseSpeed + nI * 1.7f);
                nodeFlashes[nI].localScale = Vector3.one * (param.nodeFlashSize * flPulse);
            }
        }
    }
}
