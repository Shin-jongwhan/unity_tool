using UnityEngine;

namespace VFX
{
    // 레이저 명중 VFX의 파라미터(레시피)를 저장하는 프리셋 에셋.
    // 에디터 툴에서 '내보내기'로 생성하고, 게임에서는 LaserImpactVFX.Spawn(pos, preset.param)으로 재현한다.
    [CreateAssetMenu(menuName = "VFX/Laser Impact Preset", fileName = "LaserImpactPreset")]
    public class LaserImpactPreset : ScriptableObject
    {
        public LaserImpactParams param = new LaserImpactParams();
    }
}
