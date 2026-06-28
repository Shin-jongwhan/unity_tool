using UnityEngine;

namespace VFX
{
    // 체인 라이트닝 VFX의 조절 가능한 파라미터 묶음. 에디터 툴/런타임 양쪽에서 공유한다.
    [System.Serializable]
    public class ChainLightningParams
    {
        public Color color = new Color(0.6f, 0.85f, 1f);

        [Header("Chain (노드 배치)")]
        public int nodeCount = 4;          // 노드 수(구간 = nodeCount-1). 시작 노드 포함.
        public float segmentLength = 1.6f; // 노드 사이 평균 거리(+X 방향으로 뻗음)
        public float spread = 0.7f;        // 노드의 좌우(Y) 퍼짐

        [Header("Bolt (줄기)")]
        public float coreWidth = 0.06f;    // 밝은 코어 라인 폭
        public float glowWidthMul = 3.2f;  // 글로우 라인 폭 = coreWidth * 이 값
        public int generations = 5;        // 지그재그 분할 횟수
        public float displace = 0.35f;     // 첫 분할 흔들림 폭
        public float damp = 0.55f;         // 세대별 감쇠

        [Header("Branches (곁가지)")]
        public int branchPerSegment = 2;   // 구간당 곁가지 슬롯 수
        public float branchChance = 0.5f;  // 매 깜빡임마다 각 곁가지가 보일 확률
        public float branchLength = 0.6f;  // 곁가지 길이

        [Header("Nodes (노드 섬광)")]
        public float nodeFlashSize = 0.45f;
        public float nodePulse = 0.25f;
        public float pulseSpeed = 14f;

        [Header("Flicker (깜빡임)")]
        public float flickerInterval = 0.045f;

        [Header("Render")]
        public int sortingOrder = 100;

        public ChainLightningParams Clone()
        {
            return (ChainLightningParams)MemberwiseClone();
        }
    }


    // 2D 체인 라이트닝 VFX. 여러 노드를 순서대로 잇는 번개 줄기 + 노드 섬광 + 곁가지를 코드로 생성한다.
    // 실제 생성/갱신은 ChainLightningDriver가 담당한다. (발광 머티리얼은 LightningBolt가 공유 → '흰 네모' 회피.)
    public static class ChainLightningVFX
    {
        // 이펙트를 생성해 반환한다(자동 삭제 없음). 에디터 미리보기/직접 제어용.
        public static GameObject Build(Vector3 pos, ChainLightningParams p)
        {
            if (p == null) p = new ChainLightningParams();

            var root = new GameObject("ChainLightning");
            root.transform.position = pos;

            var drv = root.AddComponent<ChainLightningDriver>();
            drv.Setup(p);
            drv.Tick(0f);
            return root;
        }


        // 런타임 편의: 생성 후 life초 뒤 자동 삭제(플레이 모드에서만 삭제 동작).
        public static GameObject Spawn(Vector3 pos, ChainLightningParams p, float life = 0.5f)
        {
            var go = Build(pos, p);
            if (Application.isPlaying) Object.Destroy(go, life);
            return go;
        }
    }
}
