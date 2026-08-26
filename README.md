# 다시...나의 요괴들 (My K-SPIRITS)

한국 설화 기반 **육성 시뮬레이션 + 카드 수집** 모바일 게임 프로토타입입니다.

나전칠기 선반장에서 탈출한 요괴를 족자에서 키우고, 선택에 따라 승천/악귀화시키며 양면 요괴패를 모읍니다.

## 현재 진행 (프로토타입)

- [x] 기획 정리 (`Docs/`)
- [x] 옥토끼 튜토리얼 STEP 1~14 **로직 골격**
- [x] 대사 JSON + 타이핑/스킵
- [x] UI 연출 클립 JSON
- [x] 세이브 (slot0 JSON, 원자적 쓰기 + 백업, 스텝/던지기 단위 재개)
- [x] 윷놀이 미니게임 분리 (`Minigames/Yut/`) — 29밭 보드·지름길 규칙·확률표, 본게임 수련장(`NurtureTrainingController`)에서 실제 RNG 루프로 연결됨
- [x] 요괴패 카드 뷰어 분리 (`Cards/`) — 확대·드래그 뒤집기·재생, 도감 화면 자체는 아직 없음
- [ ] 아트 스프라이트 적용
- [ ] 소환 화면 실구현
- [ ] 본편 요괴 1마리 완전 육성 (MVP)

## 요구 환경

- **Unity** `6000.3.11f1` (Unity 6)
- URP 모바일 템플릿 기반
- 플랫폼 목표: Android / iOS

## 웹 데모 (GitHub Pages)

`main` push 시 WebGL 자동 빌드·배포.

**플레이:** https://daldidan-studio.github.io/Yogoe-unity/

최초 1회 [CI Secrets·Pages 설정](Docs/04_CI_배포.md) 필요.

## 실행 방법 (로컬)

1. Unity Hub에서 이 폴더를 연다.
2. `Assets/Scenes/Boot`을 연다.
3. Play를 누른다.

씬에 오브젝트를 배치할 필요 없이, `GameBootstrap`이 런타임에 족자 UI와 튜토리얼을 띄웁니다.

### 튜토리얼 조작

| 조작 | 동작 |
|------|------|
| 요괴 영역 탭 | 건드리기 / 기력 부족 안내 |
| 대사 박스 탭 | 타이핑 중 → 전체 표시 / 완료 후 → 다음 대사 |
| 정화수 드래그 → 요괴 드롭 | 공양 |
| 수련장 → 윷 던지기 | 이무기와의 대결 각본(캡처·보너스·지름길·엽전 칸) → 이후 진짜 확률표 자유 던지기 |
| 선택지 | 흑화 / 소원 분기 |

정식 루트: 소원에서 **옥토끼를 구해주세요** → 카드 → 향 3개 → 소환 플레이스홀더

## 폴더 구조

```
Assets/
  Scripts/
    Bootstrap/       자동 기동 (GameBootstrap — 세이브 로드, 각 컨트롤러 Bind)
    Core/            상수·열거형 (GameConstants, GameEnums, GameLocale)
    Data/            대사 JSON 로더 (OktoDialogue, SummonCatalog)
    Animation/       연출 카탈로그·타이핑·플레이어
    Model/           게임 상태 (기력·친밀도·재화·카드)
    Systems/         세이브(JSON slot0), 소환 컨트롤러/서비스, 본게임 수련장 루프(NurtureTrainingController)
    Tutorial/        옥토끼 14스텝 머신 + 개발용 스텝 점프 메뉴
    UI/              족자 화면(ScrollScreenUI)·드래그 공양·폰트 공통 처리
    Minigames/Yut/   윷놀이 미니게임 (독립 모듈, YutMiniGame 아래 참고)
    Cards/           요괴패 카드 뷰어 (독립 모듈, CardViewer 아래 참고)
  Resources/
    Dialogue/okto_tutorial.ko.json      튜토리얼 대사·선택지 (언어별)
    Dialogue/gorani_wang_story.ko.json  고라니왕 혼1~3 스토리·선택지·엔딩 대사 (기획서에서 이관, 아직 로더/재생 미연결)
    Animation/ui_anims.json       shake / flash / typewriter 등
    Settings/UIFontSettings.asset 역할별(기본/대사/유저정보/HUD숫자) 폰트 지정
  Fonts/DOSGothic.ttf   전체 UI 기본 폰트
Docs/
  00_기획정리.md
  01_큰구조.md
  02_개발진행.md
  03_대사시트규약.md   Google Sheet id·탭·export 규약
```

### 윷놀이 미니게임 (`Minigames/Yut/`)

튜토리얼 전용이 아니라 본게임에서도 재사용하도록 분리한 독립 모듈. `ScrollScreenUI.YutGame`으로 접근.

| 파일 | 역할 |
|------|------|
| `YutMiniGame.cs` | 화면(전체화면 보드·윷가락 던지기 연출·하트 표시)과 입력 이벤트만 담당. 결과 판정은 모른다 |
| `YutBoardLayout.cs` | 전통 윷판 29발(바깥 둘레 20 + 대각선 지름길 8 + 중앙 방 1) 좌표 |
| `YutMoveResolver.cs` | 던지기 결과(도/개/걸/윷/모/빽도) → 실제 지나가는 노드 경로 계산 (지름길·빽도 규칙 포함) |
| `YutThrowRoller.cs` | 확률표(모1·빽도1·도3·개6·걸4·윷1, 16분의) 기반 RNG 판정 — `NurtureTrainingController`가 실제로 호출 |
| `YutBoardQuadrant.cs` | 두 대각선이 나누는 4구역(던진 윷 표시/특수능력/대기말/완주말+보물) enum — 첫 구역만 사용 중 |

### 본게임 수련장 (`Systems/NurtureTrainingController.cs`)

튜토리얼이 끝난 뒤(`TutorialStep.Done`) 수련장에서 실제로 도는 윷놀이 루프. 튜토리얼의 `StepTraining()`은 빽도→도 두 번만 재생하는 스크립트고, 이쪽은 `YutThrowRoller`로 진짜 확률표 기반 던지기를 돌려 `YutMoveResolver`로 이동한다. 참으로 돌아오면 완주(엽전 +1), 모/윷은 하트 소모 없이 한 번 더. 아직 말 1개짜리 최소 루프라 업기·잡기·상대 말은 다음 단계.

### 요괴패 카드 뷰어 (`Cards/`)

`CardViewer.cs` — `ScrollScreenUI.CardUI`로 접근. 확대 팝인, 좌우 드래그로 앞/뒷면 전환, X로 닫을 때 `CardFaceState.PreferBackView`에 마지막 본 면 저장, 재생 버튼으로 해당 면의 스토리 다시보기. 카드 내용은 `CardContent` 파라미터로 받아 특정 요괴에 종속되지 않음(도감 화면 생기면 재사용).

### 개발용 디버그 메뉴

화면 좌상단 **DEV** 버튼 → 튜토리얼 15스텝 목록에서 원하는 지점으로 바로 진입 (`TutorialController.DebugJumpToStep`). 이전 스텝들의 연출은 재생하지 않고 최소한의 전제 상태(카드 해금, 진화 단계 등)만 맞춰준다 — 매번 처음부터 플레이하지 않고 특정 단계를 바로 테스트할 때 사용. 옆의 **고라니왕 수련장** 버튼은 튜토리얼을 건너뛰고 고라니왕을 즉석 소환한 상태로 만든 뒤 본게임 수련장(`NurtureTrainingController`) 세션을 바로 시작한다. 별도 빌드 옵션 없이 항상 포함됨.

### ⚠️ UI 코드 수정 시 주의

`Assets/Prefabs/UI/ScrollScreenUI.prefab`은 씬과 연결되지 않은 **스냅샷**이다. 실제 게임은 `Assets/Scenes/Boot.unity`에 직접 구워진 계층을 쓴다 (`KSpirits → Setup Boot Scene UI` 메뉴가 씬에 만든 뒤 프리팹으로 내보내기만 하고, 씬 오브젝트를 그 프리팹의 인스턴스로 연결하진 않음). **정적 레이아웃/텍스트/색 같은 데이터성 변경은 `.prefab`이 아니라 `Boot.unity`를 직접 고쳐야 실제로 반영된다.** 반대로 순수 C# 코드 변경(런타임에 `transform.Find`로 자기 자식을 찾아 바인딩하는 방식)은 씬/프리팹 어느 쪽이 오래됐든 항상 정상 동작한다 — 새 UI 기능을 추가할 땐 이 패턴(`YutMiniGame`/`CardViewer`처럼 기존 GameObject에 런타임 `AddComponent`)을 따르는 걸 권장.

## 세이브

- 경로: 기기 `persistentDataPath/save_slot0.json` (+ `.bak` 백업, 쓰는 동안만 `.tmp`)
- **원자적 쓰기**: `.tmp`에 먼저 쓰고 → 기존 정식 파일을 `.bak`으로 복사 → `.tmp`를 정식 파일로 교체. 저장 도중 앱이 죽어도 정식 파일 또는 백업 중 하나는 항상 온전함
- **로드 시 백업 폴백**: 정식 파일 파싱이 실패하면 자동으로 `.bak`을 시도, 그것도 실패해야 새 게임으로 시작
- **WebGL IndexedDB sync**: WebGL의 `persistentDataPath`는 IndexedDB 위에 얹힌 가상 파일시스템(IDBFS)이라 `File.Write`만으로는 새로고침 시 사라질 수 있음 — 저장/삭제할 때마다 `Assets/Plugins/WebGL/YogoeSave.jslib`의 `FS.syncfs()`를 호출해 명시적으로 반영
- 저장 시점: 튜토리얼 **스텝 경계**, 본게임 수련장 **던지기 1회마다**(`NurtureTrainingController`), 앱 백그라운드/종료 시(`SaveHost`)
- 스키마 v1: 튜토리얼·지갑·포커스 요괴·옥토끼 카드 (+ 슬롯/도감 예비 배열)

## 데이터 수정

| 내용 | 파일 |
|------|------|
| 대사·선택지 | `Assets/Resources/Dialogue/okto_tutorial.{ko|en|zh}.json` |
| 연출 수치 (흔들림, 타이핑 속도 등) | `Assets/Resources/Animation/ui_anims.json` |

### 대사 시트 → JSON

Google Sheet에서 수정한 뒤 프로젝트로 반영:

```bash
npm run dialogue
```

- 설정: `Tools/dialogue_sheets.config.json` (`sheet_id`, `characters`, `locales`)
- 시트 탭 = 캐릭터명 (예: `okto`)
- 출력: `Assets/Resources/Dialogue/{character}_tutorial.{locale}.json`
- 시트 공유: **링크 있는 사용자 · 뷰어**
- id/탭 규약: `Docs/03_대사시트규약.md`
- 언어 전환: `GameLocale.Current = "en"` (없으면 `ko` 폴백)

시트 컬럼에 `text_en` / `text_zh`를 채운 뒤 `npm run dialogue` 하면 해당 언어 JSON이 추가로 생깁니다.

로컬 CSV만 쓸 때: `npm run dialogue:csv`

코드 재컴파일 없이 JSON만 고쳐도 내용/속도 조절이 가능합니다. (Play 모드 재시작 필요)

## 아트 붙이는 법 (다음 단계)

1. PNG를 `Assets/Art/` 또는 `Assets/Resources/Art/`에 넣는다.
2. Inspector에서 Texture Type = **Sprite (2D and UI)**
3. 요괴 `Image.sprite`에 단계별(넋/괴/혼/흑) 스프라이트를 연결한다.

자세한 현황은 `Docs/02_개발진행.md`를 참고하세요.

## 라이선스

프로토타입 / 비공개 개발용. (추후 명시)
