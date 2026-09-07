# 한 폭의 요괴 (Scroll of Yoegoe)

한국 설화 기반 **방치형(유휴) 육성 시뮬레이션** 모바일 게임 프로토타입입니다.
(기획문서 "한폭요괴 1.0" MVP 2차 기준)

족자 위에서 요괴들이 스스로 돌아다니며 기물을 사용해 공덕을 생산하고,
공양을 통해 친밀도·기력을 관리하며 성장(넋 → 혼)시킵니다.

> 이 저장소는 이전에 "다시...나의 요괴들(My K-SPIRITS)" — 카드 수집 + 비주얼노벨
> 육성 게임 — 을 개발하던 프로젝트였습니다. 해당 설계는 폐기되었고, 유니티 프로젝트
> 셸(ProjectSettings/Packages)과 CI/CD 파이프라인만 유지한 채 게임 로직/기획을
> "한 폭의 요괴" 설계로 전면 교체했습니다. 이전 설계의 코드/문서는
> `Assets/_Legacy_KSpirits/`, `Docs/_legacy_kspirits/`에 참고용으로 보관되어 있습니다.

## 현재 진행

- [x] BigNumber 무한 자릿수 재화 시스템 (ㄱㄴㄷ...ㅎ → ㄱㄱ,ㄴㄴ 순환 단위)
- [x] 캐릭터 행동 상태머신 (걷기 → 머무르기 → 늘어짐 → 기절), 기물 점유/생산 계산
- [x] 기물/공양/캐릭터 데이터 정의 (ScriptableObject)
- [x] 아트 없이도 실제 빌드에서 확인 가능한 테스트 부트스트랩 (`TestSceneBootstrap`)
- [x] 윷놀이 보드 계산 · 확률표 · 화면(기존 프로젝트에서 재사용, 검증 완료)
- [ ] 윷놀이 승패 판정 / 상대 AI / 잡기 / 보상 지급 (새로 설계·구현 필요)
- [ ] 활성 테스트 씬 (Boot.unity가 레거시로 이동되어 현재 없음 — 아래 "실행 방법" 참고)
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

`Assets/Scenes/Boot.unity`가 레거시로 이동되면서 현재 활성 씬이 없습니다.

1. Unity Hub에서 이 폴더를 연다.
2. 새 씬을 만든다 (예: `Assets/Scenes/Main.unity`).
3. 빈 GameObject를 만들고 `TestSceneBootstrap` 컴포넌트를 붙인다.
4. File → Build Profiles(또는 Build Settings) → 방금 만든 씬을 Scenes In Build에 추가한다.
5. Play를 누른다 — 아트 없이 캡슐/큐브로 캐릭터·기물이 움직이는 것을 확인할 수 있다.

다음 CI 빌드(WebGL)도 여기서 등록한 씬을 기준으로 빌드됩니다.

## 폴더 구조

```
Assets/
  Scripts/
    Core/            BigNumber (무한 자릿수 재화 표기)
    Data/            Enums, CharacterData / PropData / OfferingData (ScriptableObject)
    Characters/      CharacterRuntimeStats, PropSlot, PropManager, CharacterAgent(행동 상태머신)
    Economy/         GameEconomy (공덕 누적)
    Debugging/       TestSceneBootstrap (아트 없이 프리미티브로 씬 구성)
    Minigames/Yut/   윷놀이 보드·이동·확률·화면 (기존 프로젝트에서 재사용)
  _Legacy_KSpirits/  이전 설계("다시...나의 요괴들") 코드/에셋 보관 (활성 트리 아님)
Docs/
  00_기획정리.md           현재 설계("한 폭의 요괴") 요약
  02_개발진행.md           실제 개발 현황 (유지/신규/레거시/남은 작업)
  04_CI_배포.md            CI/배포 방법
  05_기획_미확정사항.md    기획 리뷰에서 발견된 미확정/모순 사항
  _legacy_kspirits/        이전 설계 문서 보관
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
