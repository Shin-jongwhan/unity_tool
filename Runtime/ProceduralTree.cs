using System.Collections.Generic;
using UnityEngine;

namespace SkyTools
{
    // 코드로 생성하는 로우폴리 침엽수(전나무). 참고 이미지 실루엣 재현:
    // 갈색 줄기 + 위로 갈수록 작아지는 원뿔 스커트 N단. 단 가장자리(림)를 정점별로 불규칙하게
    // 흔들어 들쭉날쭉한 나뭇가지 느낌. 색은 코드 생성 세로 그라데이션 텍스처(아래 진녹→위 연녹).
    // 메시/머티리얼은 정적 캐시(모든 나무가 공유) → 경량. 스케일/회전만 다르게 흩뿌리면 됨.
    public static class ProceduralTree
    {
        const int Seg = 12;        // 원뿔 둘레 분할(낮을수록 각진 로우폴리)
        const int Tiers = 6;       // 스커트 단 수(많을수록 풍성)
        const float TrunkH = 0.7f; // 줄기 높이
        const float CanopyH = 3.4f;// 잎 전체 높이
        const float BottomR = 1.5f, TopR = 0.30f;  // 밑단 넓게/꼭대기 뭉툭(덜 뾰족)

        static Mesh canopyMesh, trunkMesh;
        static Material leafMat, trunkMat;

        // 에디터 배치 툴이 에셋으로 저장/참조할 수 있게 공유 리소스 노출(런타임도 동일 리소스 사용).
        public static Mesh CanopyMesh { get { EnsureAssets(); return canopyMesh; } }
        public static Mesh TrunkMesh { get { EnsureAssets(); return trunkMesh; } }
        public static Material LeafMaterial { get { EnsureAssets(); return leafMat; } }
        public static Material TrunkMaterial { get { EnsureAssets(); return trunkMat; } }

        public static GameObject Build(Vector3 pos, float scale = 1f)
        {
            EnsureAssets();

            var root = new GameObject("Tree");
            root.transform.position = pos;
            root.transform.localScale = Vector3.one * scale;

            var canopy = new GameObject("Canopy", typeof(MeshFilter), typeof(MeshRenderer));
            canopy.transform.SetParent(root.transform, false);
            canopy.GetComponent<MeshFilter>().sharedMesh = canopyMesh;
            canopy.GetComponent<MeshRenderer>().sharedMaterial = leafMat;

            var trunk = new GameObject("Trunk", typeof(MeshFilter), typeof(MeshRenderer));
            trunk.transform.SetParent(root.transform, false);
            trunk.GetComponent<MeshFilter>().sharedMesh = trunkMesh;
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

            return root;
        }


        static void EnsureAssets()
        {
            if (canopyMesh == null) canopyMesh = BuildCanopyMesh();
            if (trunkMesh == null) trunkMesh = BuildTrunkMesh();
            if (leafMat == null)
            {
                leafMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                leafMat.SetColor("_BaseColor", Color.white);
                leafMat.SetTexture("_BaseMap", MakeLeafGradient());
                leafMat.SetFloat("_Smoothness", 0.1f);
                leafMat.SetFloat("_Cull", 0f);   // 양면 렌더(원뿔 안쪽도 보이게 → 와인딩 신경 안 씀)
            }
            if (trunkMat == null)
            {
                trunkMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                trunkMat.SetColor("_BaseColor", new Color(0.36f, 0.23f, 0.12f));
                trunkMat.SetFloat("_Smoothness", 0.12f);
            }
        }


        // 원뿔 스커트 N단을 한 메시로. 단마다 풍성하게 겹치고, 림을 정점별로 불규칙하게(반경 ±, 아래 드룹)
        // 흔들어 들쭉날쭉. UV.v를 나무 전체 높이 기준으로 매핑 → 아래 진녹 → 위 연녹 그라데이션.
        static Mesh BuildCanopyMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i < Tiers; i++)
            {
                float frac = i / (float)(Tiers - 1);
                float by = TrunkH + CanopyH * (i / (float)Tiers) * 0.78f;   // 더 겹쳐 풍성하게(덜 뾰족)
                float r = Mathf.Lerp(BottomR, TopR, frac);
                float span = (CanopyH / Tiers) * Mathf.Lerp(1.9f, 1.15f, frac);  // 위로 갈수록 원뿔 짧게 → 뭉툭
                float apexY = by + span;

                float baseV = Mathf.Lerp(0.12f, 0.72f, frac);   // 단별 그라데이션 기준값
                int baseIdx = verts.Count;
                for (int s = 0; s < Seg; s++)
                {
                    float a = (s / (float)Seg) * Mathf.PI * 2f;
                    float h1 = Hash(s * 1.7f + i * 9.3f);
                    float h2 = Hash(s * 3.1f + i * 4.7f + 11.1f);
                    float rr = r * (1f + (h1 - 0.5f) * 0.30f);        // 반경 ±15%(가장자리 들쭉날쭉)
                    float yy = by - span * 0.20f * (0.4f + h2);       // 림이 아래로 드룹 + 불규칙
                    verts.Add(new Vector3(Mathf.Cos(a) * rr, yy, Mathf.Sin(a) * rr));
                    uvs.Add(new Vector2(s / (float)Seg, baseV + h2 * 0.05f));
                }
                int apexIdx = verts.Count;
                verts.Add(new Vector3(0f, apexY, 0f));
                uvs.Add(new Vector2(0.5f, Mathf.Min(0.98f, baseV + 0.24f)));

                for (int s = 0; s < Seg; s++)
                {
                    int a0 = baseIdx + s;
                    int a1 = baseIdx + (s + 1) % Seg;
                    tris.Add(a0); tris.Add(apexIdx); tris.Add(a1);
                }
            }

            var m = new Mesh { name = "TreeCanopy" };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        // 0~1 의사난수(정점 인덱스 기반) — 가장자리 불규칙화에 사용.
        static float Hash(float x)
        {
            float s = Mathf.Sin(x * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }


        // 살짝 가늘어지는 줄기(로우폴리 원기둥).
        static Mesh BuildTrunkMesh()
        {
            int seg = 6; float rb = 0.18f, rt = 0.13f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int s = 0; s < seg; s++)
            {
                float a = (s / (float)seg) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * rb, 0f, Mathf.Sin(a) * rb));
                verts.Add(new Vector3(Mathf.Cos(a) * rt, TrunkH + 0.15f, Mathf.Sin(a) * rt));
            }
            for (int s = 0; s < seg; s++)
            {
                int b0 = s * 2, t0 = s * 2 + 1;
                int b1 = ((s + 1) % seg) * 2, t1 = ((s + 1) % seg) * 2 + 1;
                tris.Add(b0); tris.Add(t0); tris.Add(b1);
                tris.Add(b1); tris.Add(t0); tris.Add(t1);
            }
            var m = new Mesh { name = "TreeTrunk" };
            m.SetVertices(verts); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        // 세로 그라데이션 잎 텍스처: 아래 진녹 → 위 연녹 + 약한 노이즈(생기).
        static Texture2D MakeLeafGradient()
        {
            int W = 16, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, true) { name = "TreeLeafGradient" };
            Color bottom = new Color(0.13f, 0.33f, 0.16f);
            Color mid = new Color(0.27f, 0.50f, 0.23f);
            Color top = new Color(0.56f, 0.78f, 0.38f);
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                Color c = v < 0.5f ? Color.Lerp(bottom, mid, v / 0.5f) : Color.Lerp(mid, top, (v - 0.5f) / 0.5f);
                for (int x = 0; x < W; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.5f, y * 0.25f);
                    tex.SetPixel(x, y, c * Mathf.Lerp(0.88f, 1.08f, n));
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }
    }
}
