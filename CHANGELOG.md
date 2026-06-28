# Changelog

## [0.2.0] - 2026-06-28
### Changed
- 패키지 리네임: **Sky Tools → Unity Tool** (`com.shinejh.skytools` → `com.shinejh.unitytool`,
  어셈블리 `SkyTools.*` → `UnityTool.*`). 카테고리별 정리 구조로 전환.
- 기존 나무 툴을 `Procedural` 카테고리로 이동 (메뉴 `Tools > Procedural > Tree Placement`).

### Added
- **VFX / 2D / Laser Impact** — 2D 레이저 명중 이펙트 툴.
  - `LaserImpactVFX` (Runtime): 섬광+불똥+잔광 3겹 파티클을 파라미터로 생성하는 팩토리.
    2D용 `Sprites/Default` 셰이더로 알파 보존(URP 파티클 '흰 네모' 회피).
  - `LaserImpactPreset` (ScriptableObject): 색·파라미터 레시피 저장.
  - `LaserImpactPlayer` (Component): 씬 오브젝트에서 프리셋 재생(playOnEnable / Play()).
  - 에디터 툴 `Tools > VFX > 2D > Laser Impact`: 독립 미리보기 화면(루프 재생), 랜덤 파라미터,
    프리셋 내보내기/불러오기.

## [0.1.2] - 2026-06-21
### Changed
- `ProceduralTree` 2차 개선: 꼭대기 침(spike) 제거(위 단 굵고 짧게, 전체 높이↓), 림을 스캘럽(부채꼴 물결)
  +불규칙 드룹으로 또렷하게 들쭉날쭉, 단별 진녹(그늘)→연녹 그라데이션 강화. tree.png에 더 근접.

## [0.1.1] - 2026-06-21
### Changed
- `ProceduralTree` 개선: 더 풍성하고 덜 뾰족한 실루엣(밑단 넓힘·위 단 뭉툭), 단 가장자리(림)를
  정점별로 불규칙하게(반경 ±·아래 드룹) → 들쭉날쭉한 나뭇가지 느낌. UV를 나무 전체 높이 기준으로
  매핑해 아래 진녹→위 연녹 그라데이션이 또렷하게 보이도록.

## [0.1.0] - 2026-06-21
### Added
- `ProceduralTree` — 코드 생성 로우폴리 침엽수(원뿔 스커트 N단 + 세로 그라데이션 텍스처, ~62 tris).
- `Tree Placement Tool` — 절차/프리팹 나무를 파라미터 기반으로 씬에 배치하는 에디터 윈도우.
