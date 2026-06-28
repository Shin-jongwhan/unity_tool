using UnityEngine;

namespace VFX
{
    // 코어 표현 방식: 꽉 찬 발광 / 입체 반투명 원반 / 표면 껍질.
    public enum CoreStyle { Glow, Disc, Ring }

    // 아크 시작점: 코어 중심에서 / 코어 표면에서.
    public enum ArcOrigin { Center, Surface }


    // 라이트닝 구체 VFX의 조절 가능한 파라미터 묶음. 에디터 툴/런타임 양쪽에서 공유한다.
    [System.Serializable]
    public class LightningOrbParams
    {
        public Color color = new Color(0.55f, 0.8f, 1f);

        [Header("Core (코어 발광)")]
        public CoreStyle coreStyle = CoreStyle.Glow;  // 코어 표현 방식
        public float coreSize = 0.7f;      // 코어 지름
        [Range(0f, 1f)] public float coreAlpha = 1f;  // 코어 불투명도
        [Range(0.05f, 1f)] public float coreThickness = 0.3f; // Ring 띠 두께
        public float corePulse = 0.18f;    // 펄스 진폭(0=고정)
        public float pulseSpeed = 6f;      // 펄스 속도

        [Header("Arcs (표면 번개)")]
        public ArcOrigin arcOrigin = ArcOrigin.Surface; // 아크 시작 위치
        public int arcCount = 8;           // 동시에 뻗는 아크 수
        public float arcLengthMin = 0.55f; // 시작점에서 뻗어나가는 아크 길이
        public float arcLengthMax = 1.15f;
        public float coreWidth = 0.05f;    // 밝은 코어 라인 폭
        public float glowWidthMul = 3.2f;  // 글로우 라인 폭 = coreWidth * 이 값
        public int generations = 4;        // 지그재그 분할 횟수
        public float displace = 0.18f;     // 첫 분할 흔들림 폭
        public float damp = 0.55f;         // 세대별 감쇠

        [Header("Flicker (깜빡임)")]
        public float flickerInterval = 0.05f;

        [Header("Render")]
        public int sortingOrder = 100;

        public LightningOrbParams Clone()
        {
            return (LightningOrbParams)MemberwiseClone();
        }
    }


    // 2D 라이트닝 구체 VFX. 발광 코어 + 표면에서 뻗는 깜빡이는 번개 아크들을 코드로 생성한다.
    // 실제 생성/갱신은 LightningOrbDriver가 담당하고, 이 정적 클래스는 진입점만 제공한다.
    // (URP '흰 네모' 함정 회피: LightningBolt가 Sprites/Default 기반 발광 머티리얼을 공유.)
    public static class LightningOrbVFX
    {
        // 이펙트를 생성해 반환한다(자동 삭제 없음). 에디터 미리보기/직접 제어용.
        public static GameObject Build(Vector3 pos, LightningOrbParams p)
        {
            if (p == null) p = new LightningOrbParams();

            var root = new GameObject("LightningOrb");
            root.transform.position = pos;

            var drv = root.AddComponent<LightningOrbDriver>();
            drv.Setup(p);
            drv.Tick(0f);
            return root;
        }


        // 런타임 편의: 생성 후 life초 뒤 자동 삭제(플레이 모드에서만 삭제 동작).
        public static GameObject Spawn(Vector3 pos, LightningOrbParams p, float life = 2f)
        {
            var go = Build(pos, p);
            if (Application.isPlaying) Object.Destroy(go, life);
            return go;
        }
    }
}
