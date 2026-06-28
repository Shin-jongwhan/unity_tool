using UnityEngine;

namespace VFX
{
    // 씬 오브젝트에 붙여 프리셋(또는 직접 설정한 파라미터)으로 라이트닝 구체를 켜는 컴포넌트.
    // 구체는 명중 이펙트와 달리 '지속형'이라 켜지면 계속 깜빡이고, 꺼지면 사라진다.
    // - playOnEnable: 켜질 때 자동 생성
    // - Play()/Stop(): 코드/이벤트에서 켜고 끄기
    public class LightningOrbPlayer : MonoBehaviour
    {
        [Tooltip("연결하면 이 프리셋의 파라미터로 재생. 비우면 아래 inlineParams 사용.")]
        public LightningOrbPreset preset;
        public LightningOrbParams inlineParams = new LightningOrbParams();

        public bool playOnEnable = true;

        GameObject instance;

        void OnEnable()
        {
            if (playOnEnable) Play();
        }


        void OnDisable()
        {
            Stop();
        }


        // 이 오브젝트 위치에 구체를 켠다(이미 켜져 있으면 다시 만든다).
        public void Play()
        {
            Stop();
            var p = preset != null ? preset.param : inlineParams;
            instance = LightningOrbVFX.Build(transform.position, p);
            instance.transform.SetParent(transform, true);
        }


        // 켜져 있던 구체를 제거한다.
        public void Stop()
        {
            if (instance == null) return;
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
            instance = null;
        }
    }
}
