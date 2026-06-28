using UnityEngine;

namespace VFX
{
    // 씬 오브젝트에 붙여 프리셋(또는 직접 설정한 파라미터)으로 레이저 명중 VFX를 재생하는 컴포넌트.
    // 이 컴포넌트가 달린 GameObject를 프리팹으로 저장하면 드래그&드롭 완성품이 된다.
    // - playOnEnable: 켜질 때 자동 1회 재생
    // - Play(): 코드/이벤트에서 호출(예: 총알 명중 시)
    public class LaserImpactPlayer : MonoBehaviour
    {
        [Tooltip("연결하면 이 프리셋의 파라미터로 재생. 비우면 아래 inlineParams 사용.")]
        public LaserImpactPreset preset;
        public LaserImpactParams inlineParams = new LaserImpactParams();

        public bool playOnEnable = true;
        [Tooltip("재생 후 생성된 이펙트가 자동 삭제되기까지의 시간(초).")]
        public float autoDestroyDelay = 2f;

        void OnEnable()
        {
            if (playOnEnable) Play();
        }


        // 이 오브젝트 위치에 명중 VFX를 1회 터뜨린다.
        public void Play()
        {
            var p = preset != null ? preset.param : inlineParams;
            LaserImpactVFX.Spawn(transform.position, p, autoDestroyDelay);
        }
    }
}
