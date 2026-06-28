using UnityEngine;

namespace VFX
{
    // 씬 오브젝트에 붙여 프리셋(또는 직접 설정한 파라미터)으로 체인 라이트닝을 터뜨리는 컴포넌트.
    // 체인은 짧게 번쩍이는 타격형이라 명중 이펙트처럼 1회 재생 후 자동 삭제한다.
    // - playOnEnable: 켜질 때 자동 1회 재생
    // - Play(): 코드/이벤트에서 호출(예: 연쇄 공격 시)
    public class ChainLightningPlayer : MonoBehaviour
    {
        [Tooltip("연결하면 이 프리셋의 파라미터로 재생. 비우면 아래 inlineParams 사용.")]
        public ChainLightningPreset preset;
        public ChainLightningParams inlineParams = new ChainLightningParams();

        public bool playOnEnable = true;
        [Tooltip("재생 후 생성된 이펙트가 자동 삭제되기까지의 시간(초).")]
        public float autoDestroyDelay = 0.5f;

        void OnEnable()
        {
            if (playOnEnable) Play();
        }


        // 이 오브젝트 위치에서 체인 라이트닝을 1회 터뜨린다.
        public void Play()
        {
            var p = preset != null ? preset.param : inlineParams;
            ChainLightningVFX.Spawn(transform.position, p, autoDestroyDelay);
        }
    }
}
