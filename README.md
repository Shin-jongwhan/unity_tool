# Unity Tool

Unity 에디터/런타임 툴 모음. **Claude Code + Unity MCP**로 제작하며 카테고리별로 정리해 나가는 레포입니다.
UPM 패키지라 다른 Unity 프로젝트에도 그대로 가져다 쓸 수 있습니다.

## 카테고리

### VFX / 2D / Laser Impact
2D 레이저 명중 이펙트. 섬광(Flash) + 불똥(Sparks) + 잔광(Embers) 3겹 파티클을 코드로 생성.
- 메뉴: **Tools > VFX > 2D > Laser Impact** (색·각종 파라미터 조절 + 씬 미리보기)
- 런타임 API: `VFX.LaserImpactVFX.Spawn(위치, LaserImpactParams, 수명)` → 이펙트 GameObject 반환.
- 2D(직교 카메라)용. `Sprites/Default` 셰이더로 알파 falloff를 보존 → URP 파티클 셰이더의 '흰 네모' 함정 회피.

### Procedural / Tree (예시)
코드로 생성하는 로우폴리 침엽수 + 파라미터 기반 배치 툴. (Sky 게임 제작 때 만든 예시 툴)
- 런타임 API: `Procedural.ProceduralTree.Build(위치, 스케일)` → 나무 GameObject 반환.
- 배치 툴 메뉴: **Tools > Procedural > Tree Placement**

## 설치 (UPM)

git URL:
```
Window > Package Manager > + > Add package from git URL...
https://github.com/Shin-jongwhan/unity_tool.git
```

또는 로컬 참조(여러 프로젝트가 한 클론을 공유) — 각 프로젝트 `Packages/manifest.json`에:
```json
"com.shinejh.unitytool": "file:<상대경로>/unity_tools"
```

요구사항: URP(Universal Render Pipeline).

## 라이선스
MIT (LICENSE 참고).
