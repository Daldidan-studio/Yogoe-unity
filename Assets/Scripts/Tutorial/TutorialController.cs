using System;
using System.Collections;
using System.Collections.Generic;
using KSpirits.Cards;
using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Minigames.Yut;
using KSpirits.Model;
using KSpirits.Systems;
using KSpirits.UI;
using UnityEngine;

namespace KSpirits.Tutorial
{
    /// <summary>
    /// 옥토끼(Okto) 튜토리얼 전체 진행 컨트롤러.
    ///
    /// 【전체 흐름】
    /// 1. FirstMeeting      도깨비불 탭 → 첫 만남 대사
    /// 2. FirstOffering     정화수 드래그 공양
    /// 3. EvolveToApparition 현상(Apparition) 진화
    /// 4. Petting           쓰다듬기 → 수련장 안내
    /// 5. Training          윷놀이(빽도 → 골인) → 나가기
    /// 6. ItemCollect       정화수 회수 안내
    /// 7. EnergyWarning     기력 경고 → 현현 진화 직전 세팅
    /// 8. EvolveToManifest  정화수로 기력 100 → 현현 진화
    /// 9. MemoryView        기억 3막(달/지구/고가구점)
    /// 10. BlackeningChoice 흑화 선택 → 카드 뒷면 해금
    /// 11. BlackRabbitFlee  흑토끼 도망 → 빈 족자
    /// 12. ImugiRestore     이무기 포획 → 요괴 복귀
    /// 13. WishBranch       소원 선택(히든엔딩 or 도플갱어)
    /// 14. CardComplete     카드 완성 → 향 지급
    /// 15. Done             튜토리얼 끝 → 소환/육성으로
    ///
    /// 【입력 대기 방식】
    /// yield return WaitInput() 하면 _waitingInput=true 로 멈춤.
    /// 플레이어가 탭/드래그/버튼/선택하면 Handle* 가 _waitingInput=false 로 풀어줌.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        // 세이브되는 게임 상태(기력, 아이템, 현재 스텝 등)
        GameState _state;
        // 화면에 그리는 UI (말풍선, 버튼, 연출)
        ScrollScreenUI _ui;
        // 튜토리얼 끝난 뒤 소환 UI 열 때 사용
        SummonController _summonController;
        // true면 WaitInput()이 플레이어 입력을 기다리는 중
        bool _waitingInput;
        // 선택지에서 고른 선택 id (예: "cancel_contract")
        string _lastChoiceId;
        // 쓰다듬기 대사 중에 이미 수련장 버튼을 눌렀는지
        bool _trainingButtonPressed;
        // 카드 뷰어에서 재생 버튼을 눌렀는지 (WaitInput 공유 플래그라 StepCardComplete가 직접 분기)
        bool _cardReplayRequested;
        bool _cardReplayShowingBack;

        // 외부에서 현재 상태 읽을 때 사용
        public GameState State => _state;

        /// <summary>
        /// GameBootstrap이 시작할 때 한 번 호출.
        /// UI 이벤트(탭/공양/수련/윷/선택/대사)를 이 컨트롤러에 연결한다.
        /// </summary>
        public void Bind(GameState state, ScrollScreenUI ui, SummonController summonController = null)
        {
            _state = state;
            _ui = ui;
            _summonController = summonController;
            // 요괴 탭
            _ui.OnYokaiTapped += HandleYokaiTap;
            // 정화수 공양(드래그)
            _ui.OnOfferPurifiedWater += HandleOffer;
            // 수련장 버튼
            _ui.OnTrainingPressed += HandleTrainingPressed;
            // 윷 던지기 버튼
            _ui.YutGame.OnThrowPressed += HandleThrowYut;
            // 수련장 나가기 버튼
            _ui.YutGame.OnLeavePressed += HandleLeaveTraining;
            // 요괴패 카드 뷰어(X로 닫기 / 재생 버튼)
            _ui.EnsureCardViewer().OnClosed += HandleCardClosed;
            _ui.CardUI.OnReplayRequested += HandleCardReplayRequested;
            // 선택지 클릭
            _ui.OnChoiceSelected += HandleChoice;
            // 대사 다음으로(탭/자동진행)
            _ui.OnDialogueContinue += HandleDialogueContinue;
        }

        /// <summary>
        /// 튜토리얼 시작. 세이브에 저장된 TutorialStep부터 이어서 실행.
        /// </summary>
        public void Begin()
        {
            // UI를 현재 세이브 상태로 맞춤
            _ui.RefreshAll(_state);
            // 코루틴으로 해당 스텝부터 진행 (한 프레임에 다 안 돌고 입력/연출 기다림)
            StartCoroutine(RunStep(_state.TutorialStep));
        }

        /// <summary>
        /// 개발용: 임의 스텝으로 바로 진입. 그 스텝이 전제하는 최소한의 상태(카드 해금·
        /// 진화 단계 등)만 대충 맞춰준다 — 이전 스텝들의 연출/대사/기력 누적을 전부
        /// 재현하진 않으므로, 에너지바 등 세부 수치는 실제 플레이와 다를 수 있다.
        /// </summary>
        public void DebugJumpToStep(TutorialStepId step)
        {
            StopAllCoroutines();
            _waitingInput = false;
            _cardReplayRequested = false;
            _trainingButtonPressed = false;

            if (step >= TutorialStepId.EvolveToApparition)
                _state.FocusYokai.SetStage(YokaiStage.Apparition);
            if (step >= TutorialStepId.EvolveToManifest)
                _state.FocusYokai.SetStage(YokaiStage.Manifest);
            if (step > TutorialStepId.BlackeningChoice)
                _state.FocusYokai.Card.BackUnlocked = true;
            _ui.SetYokaiBlackened(step > TutorialStepId.BlackeningChoice && step <= TutorialStepId.ImugiRestore);
            if (step > TutorialStepId.WishBranch)
                _state.FocusYokai.Card.FrontUnlocked = true;
            if (step >= TutorialStepId.Done)
                _state.TutorialFinished = true;

            _ui.RefreshAll(_state);
            StartCoroutine(RunStep(step));
        }

        /// <summary>
        /// 한 스텝 진입점. 스텝 저장 → UI 라벨 → 해당 Step* 코루틴 실행.
        /// </summary>
        IEnumerator RunStep(TutorialStepId step)
        {
            // 현재 스텝을 상태에 기록 + 세이브 (중간에 꺼도 여기부터 재개)
            _state.TutorialStep = step;
            SaveService.Save(_state);
            // 디버그/확인용 스텝 라벨
            _ui.SetStepLabel($"튜토리얼 STEP {(int)step}");
            _ui.RefreshAll(_state);

            // 스텝 id에 맞는 실제 진행 함수로 분기
            switch (step)
            {
                case TutorialStepId.FirstMeeting:
                    yield return StepFirstMeeting();
                    break;
                case TutorialStepId.FirstOffering:
                    yield return StepFirstOffering();
                    break;
                case TutorialStepId.EvolveToApparition:
                    yield return StepEvolveApparition();
                    break;
                case TutorialStepId.Petting:
                    yield return StepPetting();
                    break;
                case TutorialStepId.Training:
                    yield return StepTraining();
                    break;
                case TutorialStepId.ItemCollect:
                    yield return StepItemCollect();
                    break;
                case TutorialStepId.EnergyWarning:
                    yield return StepEnergyWarning();
                    break;
                case TutorialStepId.EvolveToManifest:
                    yield return StepEvolveManifest();
                    break;
                case TutorialStepId.MemoryView:
                    yield return StepMemory();
                    break;
                case TutorialStepId.BlackeningChoice:
                    yield return StepBlackening();
                    break;
                case TutorialStepId.BlackRabbitFlee:
                    yield return StepBlackRabbitFlee();
                    break;
                case TutorialStepId.ImugiRestore:
                    yield return StepImugiRestore();
                    break;
                case TutorialStepId.WishBranch:
                    yield return StepWish();
                    break;
                case TutorialStepId.CardComplete:
                    yield return StepCardComplete();
                    break;
                case TutorialStepId.Done:
                    // 튜토리얼 종료 후 상태별 안내
                    if (_state.TotalSummons == 0 && SummonService.CanSummon(_state))
                    {
                        // 향 있음 → 첫 소환 유도
                        _ui.ShowStatus("튜토리얼 클리어 — 향으로 첫 요괴를 소환하세요");
                        _summonController?.Open();
                    }
                    else if (_state.TotalSummons == 0)
                        // 향 부족
                        _ui.ShowStatus("향을 모으면 요괴를 소환할 수 있습니다");
                    else
                        // 이미 소환한 요괴 육성 중
                        _ui.ShowStatus($"{_state.FocusYokai?.DisplayName ?? "요괴"} 육성 중");
                    break;
            }
        }

        // 다음 스텝으로 넘어가기 (RunStep을 다시 호출)
        IEnumerator Advance(TutorialStepId next) => RunStep(next);

        // ─────────────────────────────────────────────
        // STEP 1: 첫 만남 — 도깨비불 탭
        // ─────────────────────────────────────────────
        IEnumerator StepFirstMeeting()
        {
            // 튜토리얼 시작값: 기력 0, 정화수 0
            _state.FocusYokai.Energy = 0;
            _state.Wallet.PurifiedWater = 0;
            _ui.RefreshAll(_state);
            // 요괴 탭 가능하게
            _ui.SetYokaiInteractable(true);
            _ui.ShowStatus("도깨비불을 탭해 보세요");
            // 탭할 때까지 대기 (HandleYokaiTap이 _waitingInput 해제)
            yield return WaitInput();

            // 첫 만남 대사 재생 (한 줄마다 탭해서 넘김)
            yield return PlayLines(OktoDialogue.FirstMeeting, OktoDialogueSection.FirstMeeting);

            // 다음 스텝 힌트: 기력바 깜빡 + 공양 하이라이트
            _ui.PulseEnergyBar(true);
            _ui.SetOfferingHighlight(true);
            yield return Advance(TutorialStepId.FirstOffering);
        }

        // ─────────────────────────────────────────────
        // STEP 2: 첫 공양 — 정화수 드래그
        // ─────────────────────────────────────────────
        IEnumerator StepFirstOffering()
        {
            // 공양용 정화수 1개 지급
            _state.Wallet.PurifiedWater = 1;
            _ui.RefreshAll(_state);
            _ui.PulseEnergyBar(true);
            _ui.SetOfferingHighlight(true);
            _ui.ShowStatus("아래 정화수를 도깨비불 위로 드래그하세요");
            // 공양 UI 표시
            _ui.SetOfferButtonVisible(true);
            // 공양 성공할 때까지 대기 (HandleOffer가 해제)
            yield return WaitInput();

            // 하이라이트/공양 UI 끄기
            _ui.PulseEnergyBar(false);
            _ui.SetOfferingHighlight(false);
            _ui.SetOfferButtonVisible(false);
            // 요괴 흔들림 연출
            _ui.PlayShakeYokai();
            // 공양 후 대사
            yield return PlayLines(OktoDialogue.AfterFirstOffering, OktoDialogueSection.AfterFirstOffering);
            yield return Advance(TutorialStepId.EvolveToApparition);
        }

        // ─────────────────────────────────────────────
        // STEP 3: 현상(Apparition) 진화
        // ─────────────────────────────────────────────
        IEnumerator StepEvolveApparition()
        {
            _ui.ShowStatus("진화 중…");
            // ui_anims.json 의 evolve_flash 재생
            yield return _ui.PlayAnim("evolve_flash");
            // 단계: 현상으로 변경 + 기력 +40
            _state.FocusYokai.SetStage(YokaiStage.Apparition);
            _state.FocusYokai.AddEnergy(40);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterApparitionEvolve, OktoDialogueSection.AfterApparitionEvolve);
            yield return Advance(TutorialStepId.Petting);
        }

        // ─────────────────────────────────────────────
        // STEP 4: 쓰다듬기 → 수련장으로 안내
        // ─────────────────────────────────────────────
        IEnumerator StepPetting()
        {
            _ui.ShowStatus("옥토끼를 쓰다듬어 보세요");
            _ui.SetYokaiInteractable(true);
            // 쓰다듬기(탭) 대기
            yield return WaitInput();

            _ui.PlayShakeYokai();
            // 보상: 정화수 +1
            _state.Wallet.PurifiedWater += 1;
            _ui.RefreshAll(_state);
            _ui.HighlightItemBar(true);
            _ui.ShowStatus("정화수 +1");

            // 쓰다듬기 대사. 마지막 줄에서 수련장 버튼을 보여줌
            var lines = OktoDialogue.Petting;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i == lines.Count - 1)
                {
                    // 마지막 대사와 함께 수련장 유도
                    _trainingButtonPressed = false;
                    _ui.HighlightItemBar(false);
                    _ui.SetTrainingButtonVisible(true);
                    _ui.SetTrainingHighlight(true);
                    _ui.ShowStatus("왼쪽 아래 수련장을 눌러주세요");
                }

                _ui.ShowDialogue(lines[i], i + 1, lines.Count, OktoDialogueSection.Petting);
                // 대사 탭 or 수련장 버튼으로 진행 가능
                yield return WaitInput();
            }

            _ui.HideDialogue();
            // 대사 중에 수련장을 안 눌렀으면 다시 강제 대기
            if (!_trainingButtonPressed)
            {
                _ui.SetTrainingButtonVisible(true);
                _ui.SetTrainingHighlight(true);
                _ui.ShowStatus("왼쪽 아래 수련장을 눌러주세요");
                yield return WaitInput();
            }

            yield return Advance(TutorialStepId.Training);
        }

        // ─────────────────────────────────────────────
        // STEP 5: 수련장 윷놀이 (스크립트된 연출)
        // ─────────────────────────────────────────────
        IEnumerator StepTraining()
        {
            _ui.SetTrainingHighlight(false);
            // 화면 모드를 수련장으로
            _state.ScrollMode = ScrollMode.Training;
            _ui.SetTrainingButtonVisible(false);
            // 윷판 표시, 말 위치 시작점
            _ui.YutGame.Show();
            _ui.YutGame.SetPieceIndex(0);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.TrainingIntro, OktoDialogueSection.TrainingIntro);

            // --- 1번째 던지기: 빽도 (고정 연출) ---
            _ui.YutGame.SetThrowVisible(true);
            _ui.ShowStatus("하트 1개를 쓰고 윷을 던져보세요");
            yield return WaitInput();

            _state.Wallet.TrySpendHearts(1);
            yield return _ui.YutGame.PlayThrowAnim(YutThrowResult.Baekdo);
            _ui.YutGame.ShowResult("빽도!");
            yield return MoveYutPiece(YutMoveResolver.GetPath(0, YutThrowResult.Baekdo), 0.35f);
            _state.Wallet.Coins += 1;
            _ui.RefreshAll(_state);
            _ui.ShowStatus("엽전 +1");
            yield return PlayLines(OktoDialogue.AfterBaekdo, OktoDialogueSection.AfterBaekdo);

            // --- 2번째 던지기: 도 → 골인 (고정 연출) ---
            _ui.YutGame.SetThrowVisible(true);
            _ui.ShowStatus("다시 윷을 던져주세요");
            yield return WaitInput();

            _state.Wallet.TrySpendHearts(1);
            _state.FocusYokai.AddEnergy(GameConstants.YutMoveEnergyGain);
            _state.FocusYokai.AddIntimacy(GameConstants.YutMoveIntimacyGain);
            _state.Wallet.PurifiedWater += 1;
            yield return _ui.YutGame.PlayThrowAnim(YutThrowResult.Do);
            _ui.YutGame.ShowResult("도 → 골인!");
            yield return MoveYutPiece(YutMoveResolver.GetPath(19, YutThrowResult.Do), 0.35f);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterGoal, OktoDialogueSection.AfterGoal);

            // 나가기 버튼
            _ui.YutGame.SetLeaveVisible(true);
            _ui.ShowStatus("수련장을 나가주세요");
            yield return WaitInput();

            // 육성 화면으로 복귀
            _state.ScrollMode = ScrollMode.Nurture;
            _ui.YutGame.Hide();
            _ui.YutGame.SetLeaveVisible(false);
            _ui.YutGame.SetThrowVisible(false);
            yield return Advance(TutorialStepId.ItemCollect);
        }

        // ─────────────────────────────────────────────
        // STEP 6: 아이템 회수 안내
        // ─────────────────────────────────────────────
        IEnumerator StepItemCollect()
        {
            _ui.HighlightItemBar(true);
            _ui.ShowStatus("정화수를 회수했습니다");
            yield return PlayLines(OktoDialogue.ItemCollect, OktoDialogueSection.ItemCollect);
            _ui.HighlightItemBar(false);
            yield return Advance(TutorialStepId.EnergyWarning);
        }

        // ─────────────────────────────────────────────
        // STEP 7: 기력 경고 → 현현 진화 직전 세팅
        // ─────────────────────────────────────────────
        IEnumerator StepEnergyWarning()
        {
            // 경고 연출용으로 기력 50
            _state.FocusYokai.Energy = 50;
            _ui.RefreshAll(_state);
            _ui.PulseEnergyBar(true);
            yield return PlayLines(OktoDialogue.EnergyWarning, OktoDialogueSection.EnergyWarning);
            _ui.PulseEnergyBar(false);

            // 기획: 진화 직전 기력 80, 정화수 1회로 100
            _state.FocusYokai.Energy = GameConstants.OktoPreEvolveEnergy;
            // 기억 해금에 필요한 친밀도 최소치 보장
            _state.FocusYokai.Intimacy = Math.Max(_state.FocusYokai.Intimacy, GameConstants.OktoMemoryStartIntimacy);
            // 정화수 최소 1개
            _state.Wallet.PurifiedWater = Math.Max(_state.Wallet.PurifiedWater, 1);
            // 글리치 오버레이 ON (불안한 분위기)
            _ui.SetGlitchVisible(true);
            _ui.RefreshAll(_state);
            yield return Advance(TutorialStepId.EvolveToManifest);
        }

        // ─────────────────────────────────────────────
        // STEP 8: 현현(Manifest) 진화 — 기력 100까지 공양
        // ─────────────────────────────────────────────
        IEnumerator StepEvolveManifest()
        {
            _ui.ShowStatus("정화수를 드래그해 기력을 100까지 채우세요");
            _ui.SetYokaiInteractable(true);
            _ui.SetOfferButtonVisible(true);
            _ui.SetOfferingHighlight(true);

            // 기력이 최대가 될 때까지 공양 반복 대기
            while (_state.FocusYokai.Energy < GameConstants.EnergyMax)
                yield return WaitInput();

            _ui.SetGlitchVisible(false);
            _ui.SetOfferButtonVisible(false);
            _ui.SetOfferingHighlight(false);
            yield return _ui.PlayAnim("evolve_flash");
            // 단계: 현현
            _state.FocusYokai.SetStage(YokaiStage.Manifest);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterManifestEvolve, OktoDialogueSection.AfterManifestEvolve);
            yield return Advance(TutorialStepId.MemoryView);
        }

        // ─────────────────────────────────────────────
        // STEP 9: 기억 3막 (달 → 지구 → 고가구점)
        // ─────────────────────────────────────────────
        IEnumerator StepMemory()
        {
            // 1막: 달
            _ui.ShowStoryOverlay("— 달 —", new Color(0.05f, 0.08f, 0.2f, 0.88f));
            _state.FocusYokai.Intimacy = GameConstants.OktoMemory1;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryMoon, OktoDialogueSection.MemoryMoon);

            // 2막: 지구
            _ui.ShowStoryOverlay("— 지구 —", new Color(0.02f, 0.02f, 0.02f, 0.94f));
            _state.FocusYokai.Intimacy = GameConstants.OktoMemory2;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryEarth, OktoDialogueSection.MemoryEarth);

            // 3막: 고가구점
            _ui.ShowStoryOverlay("— 고가구점 —", new Color(0.12f, 0.08f, 0.05f, 0.9f));
            _state.FocusYokai.Intimacy = GameConstants.OktoMemory3;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryShop, OktoDialogueSection.MemoryShop);

            _ui.HideStoryOverlay();
            yield return Advance(TutorialStepId.BlackeningChoice);
        }

        // ─────────────────────────────────────────────
        // STEP 10: 흑화 선택 → 요괴 검게 + 카드 뒷면
        // ─────────────────────────────────────────────
        IEnumerator StepBlackening()
        {
            yield return PlayLines(OktoDialogue.BeforeBlackeningChoices, OktoDialogueSection.BeforeBlackeningChoices);
            // 선택지 표시 후 고를 때까지 대기
            yield return WaitChoice(OktoDialogue.BlackeningChoices);

            // 카드 뒷면 해금 + 요괴 흑화 연출
            _state.FocusYokai.Card.BackUnlocked = true;
            _ui.SetYokaiBlackened(true);
            yield return PlayLines(OktoDialogue.Blackening, OktoDialogueSection.Blackening);
            yield return Advance(TutorialStepId.BlackRabbitFlee);
        }

        // ─────────────────────────────────────────────
        // STEP 11: 흑토끼 도망 → 빈 족자
        // ─────────────────────────────────────────────
        IEnumerator StepBlackRabbitFlee()
        {
            yield return PlayLines(OktoDialogue.BlackRabbitFlee, OktoDialogueSection.BlackRabbitFlee);
            // 요괴가 화면에서 도망가는 연출
            yield return _ui.PlayYokaiFlee();
            _ui.ShowStatus("빈 족자…");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Advance(TutorialStepId.ImugiRestore);
        }

        // ─────────────────────────────────────────────
        // STEP 12: 이무기가 요괴를 다시 잡아옴
        // ─────────────────────────────────────────────
        IEnumerator StepImugiRestore()
        {
            yield return _ui.PlayImugiCapture();
            // 족자에 요괴 다시 표시
            _ui.RestoreYokaiOnScroll();
            yield return PlayLines(OktoDialogue.ImugiRestore, OktoDialogueSection.ImugiRestore);
            // 흑화 해제
            _ui.SetYokaiBlackened(false);
            _ui.RefreshAll(_state);
            yield return Advance(TutorialStepId.WishBranch);
        }

        // ─────────────────────────────────────────────
        // STEP 13: 소원 분기 (히든엔딩 or 정상 엔딩)
        // ─────────────────────────────────────────────
        IEnumerator StepWish()
        {
            yield return PlayLines(OktoDialogue.WishPrompt, OktoDialogueSection.WishPrompt);
            yield return WaitChoice(OktoDialogue.WishChoices);
            var choice = _lastChoiceId;

            // "계약 취소" 선택 시 히든 루트
            if (choice == "cancel_contract")
            {
                yield return PlayLines(OktoDialogue.HiddenConfirm, OktoDialogueSection.HiddenConfirm);
                yield return WaitChoice(OktoDialogue.HiddenConfirmChoices);

                // 최종 확인까지 하면 히든 엔딩 0으로 종료 (카드 완성 스킵)
                if (_lastChoiceId == "confirm_cancel")
                {
                    _state.LastEnding = EndingType.HiddenEndingZero;
                    yield return PlayLines(OktoDialogue.HiddenEnding, OktoDialogueSection.HiddenEnding);
                    _ui.ShowStatus("히든 엔딩 0 — 타이틀로 복귀(세이브 유지)");
                    _state.TutorialFinished = true;
                    _state.TutorialStep = TutorialStepId.Done;
                    SaveService.Save(_state);
                    yield break; // CardComplete로 안 감
                }
            }

            // 일반 루트: 도플갱어 연출 → 카드 앞면 해금
            _ui.ShowDoppelganger(true);
            yield return PlayLines(OktoDialogue.Doppelganger, OktoDialogueSection.Doppelganger);
            _ui.ShowDoppelganger(false);
            _state.FocusYokai.Card.FrontUnlocked = true;
            _state.LastEnding = EndingType.TutorialComplete;
            yield return Advance(TutorialStepId.CardComplete);
        }

        // ─────────────────────────────────────────────
        // STEP 14: 카드 완성 + 향 지급
        // ─────────────────────────────────────────────
        IEnumerator StepCardComplete()
        {
            // 완성 카드 UI 표시 → X로 닫으면 계속, 재생 버튼이면 스토리 다시 보여주고 카드로 복귀
            _ui.CardUI.Show(_state.FocusYokai.Card, BuildOktoCardContent());
            while (true)
            {
                yield return WaitInput();
                if (!_cardReplayRequested) break;

                _cardReplayRequested = false;
                _ui.CardUI.Hide();
                yield return PlayLines(
                    _cardReplayShowingBack ? OktoDialogue.Blackening : OktoDialogue.Doppelganger,
                    _cardReplayShowingBack ? OktoDialogueSection.Blackening : OktoDialogueSection.Doppelganger);
                _ui.CardUI.Show(_state.FocusYokai.Card, BuildOktoCardContent());
            }
            yield return PlayLines(OktoDialogue.CardAndIncense, OktoDialogueSection.CardAndIncense);
            // 소환용 향 지급
            _state.Wallet.Incense += GameConstants.IncensePerSummon;
            _state.TutorialFinished = true;
            _ui.RefreshAll(_state);
            _ui.ShowStatus($"향 +{GameConstants.IncensePerSummon}");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Advance(TutorialStepId.Done);
        }

        // ─────────────────────────────────────────────
        // 유틸: 윷말 칸 이동 애니메이션
        // ─────────────────────────────────────────────
        // 29발 윷판 위 노드 id 경로를 따라 한 칸씩 이동 (경로는 호출부가 지정)
        IEnumerator MoveYutPiece(int[] path, float stepDuration)
        {
            if (path == null || path.Length == 0) yield break;

            _ui.YutGame.SetPieceIndex(path[0]);
            for (int i = 1; i < path.Length; i++)
            {
                yield return new WaitForSecondsRealtime(stepDuration);
                _ui.YutGame.SetPieceIndex(path[i]);
            }
        }

        // 대사 리스트를 한 줄씩 보여주고, 매번 입력 대기
        IEnumerator PlayLines(IReadOnlyList<DialogueLine> lines, string sectionId)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                // i+1 / 전체줄수 표시 + layout/fx 적용은 UI 쪽에서
                _ui.ShowDialogue(lines[i], i + 1, lines.Count, sectionId);
                yield return WaitInput();
            }
            _ui.HideDialogue();
        }

        // 플레이어 입력이 올 때까지 매 프레임 대기
        IEnumerator WaitInput()
        {
            _waitingInput = true;
            while (_waitingInput) yield return null; // Handle*가 false로 바꿀 때까지
        }

        // 선택지 띄우고, 고를 때까지 WaitInput과 동일하게 대기
        IEnumerator WaitChoice(IReadOnlyList<ChoiceOption> choices)
        {
            _lastChoiceId = null;
            _ui.ShowChoices(choices);
            yield return WaitInput();
        }

        // ─────────────────────────────────────────────
        // 입력 핸들러: WaitInput()을 풀어주는 쪽
        // ─────────────────────────────────────────────

        // 요괴 탭
        void HandleYokaiTap()
        {
            // 현현 진화 중인데 기력이 아직 부족하면 → 안내만 하고 진행은 안 함
            if (_state.TutorialStep == TutorialStepId.EvolveToManifest &&
                _state.FocusYokai.Energy < GameConstants.EnergyMax)
            {
                var lines = OktoDialogue.NeedMoreEnergy;
                if (lines.Count > 0)
                    _ui.ShowDialogue(lines[0], 1, 1, OktoDialogueSection.NeedMoreEnergy);
                else
                    _ui.ShowStatus("기력이 모자라요");
                return;
            }

            // 첫 만남 / 쓰다듬기 스텝에서만 탭으로 입력 완료
            if (_waitingInput &&
                (_state.TutorialStep == TutorialStepId.FirstMeeting ||
                 _state.TutorialStep == TutorialStepId.Petting))
            {
                _waitingInput = false;
            }
        }

        // 정화수 공양
        void HandleOffer()
        {
            if (_state.Wallet.PurifiedWater <= 0) return;

            // 첫 공양 / 현현 진화 스텝에서만 공양 처리
            if (_state.TutorialStep == TutorialStepId.FirstOffering ||
                _state.TutorialStep == TutorialStepId.EvolveToManifest)
            {
                _state.Wallet.PurifiedWater -= 1;
                _state.FocusYokai.AddEnergy(GameConstants.OfferingEnergyGain);
                _ui.RefreshAll(_state);
                _ui.PlayShakeYokai();
                if (_waitingInput) _waitingInput = false;
            }
        }

        // 수련장 버튼
        void HandleTrainingPressed()
        {
            if (!_waitingInput) return;
            // 쓰다듬기 안내 중 or 수련 스텝에서만
            if (_state.TutorialStep == TutorialStepId.Training ||
                _state.TutorialStep == TutorialStepId.Petting)
            {
                _trainingButtonPressed = true;
                _waitingInput = false;
            }
        }

        // 윷 던지기
        void HandleThrowYut()
        {
            if (_state.TutorialStep == TutorialStepId.Training && _waitingInput)
            {
                _ui.YutGame.SetThrowVisible(false);
                _waitingInput = false;
            }
        }

        // 수련장 나가기
        void HandleLeaveTraining()
        {
            if (_state.TutorialStep == TutorialStepId.Training && _waitingInput)
                _waitingInput = false;
        }

        static CardContent BuildOktoCardContent() => new("옥토끼 요괴패", "흑토끼", "백토끼 둘");

        // 카드 뷰어 X로 닫힘 → 대기 해제
        void HandleCardClosed()
        {
            if (_waitingInput) _waitingInput = false;
        }

        // 카드 뷰어 재생 버튼 → StepCardComplete의 대기 루프가 직접 재생을 이어서 처리
        // (WaitInput은 전역 플래그 하나뿐이라, 재생 중 별도 코루틴에서 또 WaitInput을 걸면
        // 바깥쪽 대기까지 같이 풀려버리는 경합이 생김 — 그래서 여기선 플래그만 세우고 넘김)
        void HandleCardReplayRequested(bool showingBack)
        {
            _cardReplayRequested = true;
            _cardReplayShowingBack = showingBack;
            if (_waitingInput) _waitingInput = false;
        }

        // 선택지 클릭 → id 저장 후 대기 해제
        void HandleChoice(string id)
        {
            _lastChoiceId = id;
            if (_waitingInput) _waitingInput = false;
        }

        // 대사 "다음" (탭/자동진행) → 대기 해제
        void HandleDialogueContinue()
        {
            if (_waitingInput) _waitingInput = false;
        }
    }
}
