using UnityEngine;

namespace VFX
{
    // 라이트닝 구체 VFX의 파라미터(레시피)를 저장하는 프리셋 에셋.
    // 에디터 툴에서 '내보내기'로 생성하고, 게임에서는 LightningOrbVFX.Build(pos, preset.param)으로 재현한다.
    [CreateAssetMenu(menuName = "VFX/Lightning Orb Preset", fileName = "LightningOrbPreset")]
    public class LightningOrbPreset : ScriptableObject
    {
        public LightningOrbParams param = new LightningOrbParams();
    }
}
