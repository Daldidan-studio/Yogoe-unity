# 한 폭의 요괴 (Scroll of Yoegoe)

한국 설화 기반 **방치형(유휴) 육성 시뮬레이션** 모바일 게임 프로토타입입니다.
(기획문서 "한폭요괴 1.0" MVP 2차 기준)

족자 위에서 요괴들이 스스로 돌아다니며 기물을 사용해 공덕을 생산하고,
공양을 통해 친밀도·기력을 관리하며 성장(넋 → 혼)시킵니다.

## 현재 진행

- [x] BigNumber 무한 자릿수 재화 시스템 (ㄱㄴㄷ...ㅎ → ㄱㄱ,ㄴㄴ 순환 단위)
- [x] 캐릭터 행동 상태머신 (걷기 → 머무르기 → 늘어짐 → 기절), 기물 점유/생산 계산
- [x] 기물/공양/캐릭터 데이터 정의 (ScriptableObject)
- [x] Main 씬 진입점 (`Main` + `Assets/Scenes/Main.unity`)
- [x] 시작값 설정 (`Resources/StartingStateSettings.asset`)
- [x] 화면 크기 설정 (`Resources/ArtScaleSettings.asset`)
- [x] 윷놀이 보드 계산 · 확률표 · 화면(기존 프로젝트에서 재사용, 검증 완료)
- [ ] 윷놀이 승패 판정 / 상대 AI / 잡기 / 보상 지급 (새로 설계·구현 필요)
- [ ] 공양물 24종 에셋 채우기 (현재 일부만 StartingState에 연결)
- [ ] 소환/진화, 요구와 보상상자, 상점/업적/저장 시스템

자세한 현황은 [`Docs/02_개발진행.md`](Docs/02_개발진행.md), 기획 요약은
[`Docs/00_기획정리.md`](Docs/00_기획정리.md), 미확정 설계 이슈는
[`Docs/05_기획_미확정사항.md`](Docs/05_기획_미확정사항.md)을 참고하세요.

## 요구 환경

- **Unity** `6000.3.11f1` (Unity 6)
- 플랫폼 목표: Android / iOS

## 웹 데모 (GitHub Pages)

`main` push 시 WebGL 자동 빌드·배포.

**플레이:** https://daldidan-studio.github.io/Yogoe-unity/

최초 1회 [CI Secrets·Pages 설정](Docs/04_CI_배포.md) 필요. (기존 설계에서 이미
설정을 마쳤고 이번 교체로 영향받지 않으므로 재설정 불필요.)

## 실행 방법 (로컬)

1. Unity Hub에서 이 폴더를 연다.
2. `Assets/Scenes/Main.unity` 를 연다.
3. Play.

시작 재화·스탯: `Assets/Resources/StartingStateSettings.asset`  
화면 크기: `Assets/Resources/ArtScaleSettings.asset`

## 폴더 구조

```
Assets/
  Scripts/
    Core/            BigNumber
    Data/            Enums, Character/Prop/Offering Data, StartingStateSettings, ArtScaleSettings
    Characters/      CharacterAgent 등
    Economy/         GameEconomy
    Main.cs          Main 씬 진입점
    Debugging/       MapCameraDrag
    Minigames/Yut/   윷놀이
  Resources/         StartingStateSettings.asset, ArtScaleSettings.asset  ← 숫자 조절
  Data/              Characters/, Offerings/ (.asset)
  Scenes/Main.unity
Docs/
  00_기획정리.md
  02_개발진행.md
  04_CI_배포.md
  05_기획_미확정사항.md
  코드정리.md
```

### 윷놀이 미니게임 (`Minigames/Yut/`)

이전 프로젝트에서 검증 후 그대로 재사용 중인 모듈. 보드 좌표 계산, 이동 경로 판정,
던지기 확률표, 화면 표시(던지기 연출·말 표시·후보칸 강조)까지는 이미 되어 있지만,
**승패 판정·상대 AI·잡기·보상 지급 로직은 원래 프로젝트에도 없었고 지금도 없다.**
새 설계 기준 승리조건이 아직 기획 단계에서도 미정이라(`Docs/05_기획_미확정사항.md` 참고),
그 부분이 정해진 뒤 새로 구현해야 한다.

| 파일 | 역할 |
|------|------|
| `YutMiniGame.cs` | 화면(전체화면 보드·윷가락 던지기 연출·말 표시)과 입력 이벤트만 담당. 결과 판정은 모른다 |
| `YutBoardLayout.cs` | 전통 윷판 29발(바깥 둘레 20 + 대각선 지름길 8 + 중앙 방 1) 좌표 |
| `YutMoveResolver.cs` | 던지기 결과(도/개/걸/윷/모/빽도) → 실제 지나가는 노드 경로 계산 (지름길·빽도 규칙 포함) |
| `YutThrowRoller.cs` | 확률표(모1·빽도1·도3·개6·걸4·윷1, 16분의) 기반 RNG 판정 |
| `YutBoardQuadrant.cs` | 두 대각선이 나누는 4구역 enum (UI 레이아웃용) |

## 사용하지 않는 도구 (레거시)

`Tools/` (대사 시트 export 스크립트)와 루트 `package.json`의 `npm run dialogue`는
이전 비주얼노벨 설계의 대사 시스템 전용이었습니다. 현재 설계에는 대사 시트가
없어 사용하지 않지만, 삭제하지 않고 그대로 남겨두었습니다.

## 라이선스

프로토타입 / 비공개 개발용. (추후 명시)
