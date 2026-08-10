# 다시...나의 요괴들 (My K-SPIRITS)

한국 설화 기반 **육성 시뮬레이션 + 카드 수집** 모바일 게임 프로토타입입니다.

나전칠기 선반장에서 탈출한 요괴를 족자에서 키우고, 선택에 따라 승천/악귀화시키며 양면 요괴패를 모읍니다.

## 현재 진행 (프로토타입)

- [x] 기획 정리 (`Docs/`)
- [x] 옥토끼 튜토리얼 STEP 1~14 **로직 골격**
- [x] 대사 JSON + 타이핑/스킵
- [x] UI 연출 클립 JSON
- [ ] 아트 스프라이트 적용
- [ ] 윷판 29밭 실구현
- [ ] 소환 화면 실구현
- [ ] 본편 요괴 1마리 완전 육성 (MVP)

## 요구 환경

- **Unity** `6000.3.11f1` (Unity 6)
- URP 모바일 템플릿 기반
- 플랫폼 목표: Android / iOS

## 실행 방법

1. Unity Hub에서 이 폴더를 연다.
2. `Assets/Scenes/SampleScene`을 연다.
3. Play를 누른다.

씬에 오브젝트를 배치할 필요 없이, `GameBootstrap`이 런타임에 족자 UI와 튜토리얼을 띄웁니다.

### 튜토리얼 조작

| 조작 | 동작 |
|------|------|
| 요괴 영역 탭 | 건드리기 / 기력 부족 안내 |
| 대사 박스 탭 | 타이핑 중 → 전체 표시 / 완료 후 → 다음 대사 |
| 정화수 드래그 → 요괴 드롭 | 공양 |
| 수련장 → 윷 던지기 | 튜토리얼 강제 빽도 → 도 |
| 선택지 | 흑화 / 소원 분기 |

정식 루트: 소원에서 **옥토끼를 구해주세요** → 카드 → 향 3개 → 소환 플레이스홀더

## 폴더 구조

```
Assets/
  Scripts/
    Bootstrap/     자동 기동
    Core/          상수·열거형
    Data/          대사 JSON 로더
    Animation/     연출 카탈로그·타이핑·플레이어
    Model/         게임 상태 (기력·친밀도·재화)
    Tutorial/      옥토끼 14스텝 머신
    UI/            족자 화면·드래그 공양
  Resources/
    Dialogue/okto_tutorial.json   튜토리얼 대사·선택지
    Animation/ui_anims.json       shake / flash / typewriter 등
Docs/
  00_기획정리.md
  01_큰구조.md
  02_개발진행.md
```

## 데이터 수정

| 내용 | 파일 |
|------|------|
| 대사·선택지 | `Assets/Resources/Dialogue/okto_tutorial.json` |
| 연출 수치 (흔들림, 타이핑 속도 등) | `Assets/Resources/Animation/ui_anims.json` |

코드 재컴파일 없이 JSON만 고쳐도 내용/속도 조절이 가능합니다. (Play 모드 재시작 필요)

## 아트 붙이는 법 (다음 단계)

1. PNG를 `Assets/Art/` 또는 `Assets/Resources/Art/`에 넣는다.
2. Inspector에서 Texture Type = **Sprite (2D and UI)**
3. 요괴 `Image.sprite`에 단계별(넋/괴/혼/흑) 스프라이트를 연결한다.

자세한 현황은 `Docs/02_개발진행.md`를 참고하세요.

## 라이선스

프로토타입 / 비공개 개발용. (추후 명시)
