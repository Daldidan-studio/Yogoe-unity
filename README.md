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

보드 좌표·이동 경로·화면 연출(홉 이동·말풍선·던지기 영역)까지 동작 중.
승패/보상 등 일부 규칙은 `Docs/05_기획_미확정사항.md`와 맞춰 계속 다듬는 중.

| 파일 | 역할 |
|------|------|
| `YutMiniGame.cs` | 보드 UI·윷 연출·말/후보 표시 |
| `YutMatch.cs` | 한 판 상태·이동 미리보기 |
| `YutBoardLayout.cs` | 전통 윷판 29발 좌표 |
| `YutMoveResolver.cs` | 도/개/걸/윷/모/빽도 → 경로 |
| `YutThrowRoller.cs` | 확률표 RNG |
| `YutBoardQuadrant.cs` | 4구역 enum (UI용) |

말풍선 문구: `Assets/Resources/Yut/yut_bubbles.{locale}.json`  
카탈로그: `Assets/Scripts/Data/YutBubbleCatalog.cs`

## 윷 말풍선 Google Sheets 동기화

시트 탭 `yut_bubbles` ↔ 로컬 JSON. 설정: `Tools/yut_bubbles_sheets.config.json`

| 명령 | 방향 |
|------|------|
| `npm run yut-bubbles` | 시트 → `Assets/Resources/Yut/yut_bubbles.*.json` |
| `npm run yut-bubbles:csv` | 로컬 CSV → JSON (오프라인) |
| `npm run yut-bubbles:to-csv` | JSON → `Tools/sheets/yut_bubbles.csv` |
| `npm run yut-bubbles:push` | JSON → 시트 (Apps Script 웹 앱) |

**가져오기**는 시트를 `링크 있는 모든 사용자: 뷰어`로 두면 됩니다 (공개 CSV).

**쓰기(`:push`)** 최초 1회:

1. 해당 스프레드시트 → 확장 프로그램 → Apps Script
2. `Tools/YutBubblesSheetsWrite.gs` 전체 붙여넣기 → 저장
3. 배포 → 웹 앱 / 실행: 나 / 액세스: **모든 사용자**
4. `/exec` URL을 config `write_url`에 저장 (코드 수정 후에는 **새 버전**으로 재배포)
5. `npm run yut-bubbles:push`

`write_token`은 선택. 쓸 때만 Apps Script 스크립트 속성 `WRITE_TOKEN`과 같은 임의 비밀을 넣습니다 (배포 URL의 `AKfycb…`가 아님).

## 레거시 도구

`npm run dialogue` / `Tools/export_dialogue.py` / `Tools/DialogueSheetsExport.gs`는
이전 비주얼노벨 대사 시트용이었고 현재 설계에서는 쓰지 않습니다.

## 라이선스

프로토타입 / 비공개 개발용. (추후 명시)
