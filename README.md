# Sky Tools

Unity 에디터 툴 모음. **Claude Code + Unity MCP**로 제작하며 계속 툴을 추가해 나가는 레포입니다.

UPM 패키지 구조라 다른 Unity 프로젝트에도 그대로 가져다 쓸 수 있습니다.

## 포함된 툴

### 1. ProceduralTree (런타임)
코드로 생성하는 로우폴리 침엽수(뾰족한 전나무). 참고 이미지의 실루엣을 정점 단위로 재현 → **약 62 트라이앵글**.
- 갈색 줄기 + 원뿔 스커트 N단(층층 침엽수) + 코드 생성 세로 그라데이션 텍스처(아래 진녹→위 연녹).
- 메시/머티리얼을 정적 캐시로 공유 → 수백 그루 배치해도 가벼움.
- 사용: `SkyTools.ProceduralTree.Build(위치, 스케일)` → 나무 GameObject 반환.

### 2. Tree Placement Tool (에디터)
파라미터를 조절하며 절차 나무 + 임의 프리팹 나무를 씬에 흩뿌려 배치하는 에디터 윈도우.
- 메뉴: **Tools > Sky Tools > Tree Placement Tool**
- 개수 / X·Z 범위 / 절차·프리팹 혼합 비율 / 스케일 / 시드 / 지면 스냅(레이캐스트) 조절.
- 절차 나무 메시·텍스처·머티리얼은 `Assets/SkyTools/Generated/Tree/`에 에셋으로 저장 → 씬 저장 후에도 유지.
- 모든 배치는 Undo 지원.

## 설치 (UPM, git URL)
Unity > `Window > Package Manager` > `+` > **Add package from git URL...** 에 아래 입력:
```
https://github.com/<USER>/<REPO>.git
```
또는 로컬 경로로 `Add package from disk...` 에서 `package.json` 선택.

요구사항: URP(Universal Render Pipeline). 셰이더는 `Universal Render Pipeline/Lit`을 이름으로 참조합니다.

## 로드맵 (계속 추가)
- [ ] 절차 나무 변형(둥근 활엽수 / 설원 눈 쌓인 버전 / 단풍)
- [ ] 바위·풀 등 추가 프롭 절차 생성기
- [ ] 배치 브러시(씬뷰에서 드래그로 칠하기)
- [ ] 보물상자 등 절차 프롭 생성기

## 라이선스
MIT (LICENSE 참고).
