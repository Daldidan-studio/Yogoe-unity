using System;
using System.Collections;
using System.Collections.Generic;
using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;
using KSpirits.Systems;
using KSpirits.UI;
using UnityEngine;

namespace KSpirits.Tutorial
{
    public class TutorialController : MonoBehaviour
    {
        GameState _state;
        ScrollScreenUI _ui;
        SummonController _summonController;
        bool _waitingInput;
        string _lastChoiceId;
        bool _trainingButtonPressed;

        public GameState State => _state;

        public void Bind(GameState state, ScrollScreenUI ui, SummonController summonController = null)
        {
            _state = state;
            _ui = ui;
            _summonController = summonController;
            _ui.OnYokaiTapped += HandleYokaiTap;
            _ui.OnOfferPurifiedWater += HandleOffer;
            _ui.OnTrainingPressed += HandleTrainingPressed;
            _ui.OnThrowYutPressed += HandleThrowYut;
            _ui.OnLeaveTrainingPressed += HandleLeaveTraining;
            _ui.OnChoiceSelected += HandleChoice;
            _ui.OnDialogueContinue += HandleDialogueContinue;
        }

        public void Begin()
        {
            _ui.RefreshAll(_state);
            StartCoroutine(RunStep(_state.TutorialStep));
        }

        IEnumerator RunStep(TutorialStepId step)
        {
            _state.TutorialStep = step;
            SaveService.Save(_state);
            _ui.SetStepLabel($"튜토리얼 STEP {(int)step}");
            _ui.RefreshAll(_state);

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
                    if (_state.TotalSummons == 0 && SummonService.CanSummon(_state))
                    {
                        _ui.ShowStatus("튜토리얼 클리어 — 향으로 첫 요괴를 소환하세요");
                        _summonController?.Open();
                    }
                    else if (_state.TotalSummons == 0)
                        _ui.ShowStatus("향을 모으면 요괴를 소환할 수 있습니다");
                    else
                        _ui.ShowStatus($"{_state.FocusYokai?.DisplayName ?? "요괴"} 육성 중");
                    break;
            }
        }

        IEnumerator Advance(TutorialStepId next) => RunStep(next);

        IEnumerator StepFirstMeeting()
        {
            _state.FocusYokai.Energy = 0;
            _state.Wallet.PurifiedWater = 0;
            _ui.RefreshAll(_state);
            _ui.SetYokaiInteractable(true);
            _ui.ShowStatus("도깨비불을 탭해 보세요");
            yield return WaitInput();

            yield return PlayLines(OktoDialogue.FirstMeeting, OktoDialogueSection.FirstMeeting);

            _ui.PulseEnergyBar(true);
            _ui.SetOfferingHighlight(true);
            yield return Advance(TutorialStepId.FirstOffering);
        }

        IEnumerator StepFirstOffering()
        {
            _state.Wallet.PurifiedWater = 1;
            _ui.RefreshAll(_state);
            _ui.PulseEnergyBar(true);
            _ui.SetOfferingHighlight(true);
            _ui.ShowStatus("아래 정화수를 도깨비불 위로 드래그하세요");
            _ui.SetOfferButtonVisible(true);
            yield return WaitInput();

            _ui.PulseEnergyBar(false);
            _ui.SetOfferingHighlight(false);
            _ui.SetOfferButtonVisible(false);
            _ui.PlayShakeYokai();
            yield return PlayLines(OktoDialogue.AfterFirstOffering, OktoDialogueSection.AfterFirstOffering);
            yield return Advance(TutorialStepId.EvolveToApparition);
        }

        IEnumerator StepEvolveApparition()
        {
            _ui.ShowStatus("진화 중…");
            yield return _ui.PlayAnim("evolve_flash");
            _state.FocusYokai.SetStage(YokaiStage.Apparition);
            _state.FocusYokai.AddEnergy(40);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterApparitionEvolve, OktoDialogueSection.AfterApparitionEvolve);
            yield return Advance(TutorialStepId.Petting);
        }

        IEnumerator StepPetting()
        {
            _ui.ShowStatus("옥토끼를 쓰다듬어 보세요");
            _ui.SetYokaiInteractable(true);
            yield return WaitInput();

            _ui.PlayShakeYokai();
            _state.Wallet.PurifiedWater += 1;
            _ui.RefreshAll(_state);
            _ui.HighlightItemBar(true);
            _ui.ShowStatus("정화수 +1");

            var lines = OktoDialogue.Petting;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i == lines.Count - 1)
                {
                    _trainingButtonPressed = false;
                    _ui.HighlightItemBar(false);
                    _ui.SetTrainingButtonVisible(true);
                    _ui.SetTrainingHighlight(true);
                    _ui.ShowStatus("왼쪽 아래 수련장을 눌러주세요");
                }

                _ui.ShowDialogue(lines[i], i + 1, lines.Count, OktoDialogueSection.Petting);
                yield return WaitInput();
            }

            _ui.HideDialogue();
            if (!_trainingButtonPressed)
            {
                _ui.SetTrainingButtonVisible(true);
                _ui.SetTrainingHighlight(true);
                _ui.ShowStatus("왼쪽 아래 수련장을 눌러주세요");
                yield return WaitInput();
            }

            yield return Advance(TutorialStepId.Training);
        }

        IEnumerator StepTraining()
        {
            _ui.SetTrainingHighlight(false);
            _state.ScrollMode = ScrollMode.Training;
            _ui.SetTrainingButtonVisible(false);
            _ui.EnterTrainingMode(true);
            _ui.SetYutPieceIndex(0);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.TrainingIntro, OktoDialogueSection.TrainingIntro);

            _ui.SetThrowYutVisible(true);
            _ui.ShowStatus("하트 1개를 쓰고 윷을 던져보세요");
            yield return WaitInput();

            _state.Wallet.TrySpendHearts(1);
            yield return _ui.PlayYutThrowAnim();
            _ui.ShowYutResult("빽도!");
            yield return MoveYutPiece(0, 1, 0.35f);
            _state.Wallet.Coins += 1;
            _ui.RefreshAll(_state);
            _ui.ShowStatus("엽전 +1");
            yield return PlayLines(OktoDialogue.AfterBaekdo, OktoDialogueSection.AfterBaekdo);

            _ui.SetThrowYutVisible(true);
            _ui.ShowStatus("다시 윷을 던져주세요");
            yield return WaitInput();

            _state.Wallet.TrySpendHearts(1);
            _state.FocusYokai.AddEnergy(GameConstants.YutMoveEnergyGain);
            _state.FocusYokai.AddIntimacy(GameConstants.YutMoveIntimacyGain);
            _state.Wallet.PurifiedWater += 1;
            yield return _ui.PlayYutThrowAnim();
            _ui.ShowYutResult("도 → 골인!");
            yield return MoveYutPiece(1, 7, 0.55f);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterGoal, OktoDialogueSection.AfterGoal);

            _ui.SetLeaveTrainingVisible(true);
            _ui.ShowStatus("수련장을 나가주세요");
            yield return WaitInput();

            _state.ScrollMode = ScrollMode.Nurture;
            _ui.EnterTrainingMode(false);
            _ui.SetLeaveTrainingVisible(false);
            _ui.SetThrowYutVisible(false);
            yield return Advance(TutorialStepId.ItemCollect);
        }

        IEnumerator StepItemCollect()
        {
            _ui.HighlightItemBar(true);
            _ui.ShowStatus("정화수를 회수했습니다");
            yield return PlayLines(OktoDialogue.ItemCollect, OktoDialogueSection.ItemCollect);
            _ui.HighlightItemBar(false);
            yield return Advance(TutorialStepId.EnergyWarning);
        }

        IEnumerator StepEnergyWarning()
        {
            _state.FocusYokai.Energy = 50;
            _ui.RefreshAll(_state);
            _ui.PulseEnergyBar(true);
            yield return PlayLines(OktoDialogue.EnergyWarning, OktoDialogueSection.EnergyWarning);
            _ui.PulseEnergyBar(false);

            // 기획: 진화 직전 기력 80, 정화수 1회로 100
            _state.FocusYokai.Energy = GameConstants.OktoPreEvolveEnergy;
            _state.FocusYokai.Intimacy = Math.Max(_state.FocusYokai.Intimacy, GameConstants.OktoMemoryStartIntimacy);
            _state.Wallet.PurifiedWater = Math.Max(_state.Wallet.PurifiedWater, 1);
            _ui.SetGlitchVisible(true);
            _ui.RefreshAll(_state);
            yield return Advance(TutorialStepId.EvolveToManifest);
        }

        IEnumerator StepEvolveManifest()
        {
            _ui.ShowStatus("정화수를 드래그해 기력을 100까지 채우세요");
            _ui.SetYokaiInteractable(true);
            _ui.SetOfferButtonVisible(true);
            _ui.SetOfferingHighlight(true);

            while (_state.FocusYokai.Energy < GameConstants.EnergyMax)
                yield return WaitInput();

            _ui.SetGlitchVisible(false);
            _ui.SetOfferButtonVisible(false);
            _ui.SetOfferingHighlight(false);
            yield return _ui.PlayAnim("evolve_flash");
            _state.FocusYokai.SetStage(YokaiStage.Manifest);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterManifestEvolve, OktoDialogueSection.AfterManifestEvolve);
            yield return Advance(TutorialStepId.MemoryView);
        }

        IEnumerator StepMemory()
        {
            _ui.ShowStoryOverlay("— 달 —", new Color(0.05f, 0.08f, 0.2f, 0.88f));
            _state.FocusYokai.Intimacy = GameConstants.OktoMemory1;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryMoon, OktoDialogueSection.MemoryMoon);

            _ui.ShowStoryOverlay("— 지구 —", new Color(0.02f, 0.02f, 0.02f, 0.94f));
            _state.FocusYokai.Intimacy = GameConstants.OktoMemory2;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryEarth, OktoDialogueSection.MemoryEarth);

            _ui.ShowStoryOverlay("— 고가구점 —", new Color(0.12f, 0.08f, 0.05f, 0.9f));
            _state.FocusYokai.Intimacy = GameConstants.OktoMemory3;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryShop, OktoDialogueSection.MemoryShop);

            _ui.HideStoryOverlay();
            yield return Advance(TutorialStepId.BlackeningChoice);
        }

        IEnumerator StepBlackening()
        {
            yield return PlayLines(OktoDialogue.BeforeBlackeningChoices, OktoDialogueSection.BeforeBlackeningChoices);
            yield return WaitChoice(OktoDialogue.BlackeningChoices);

            _state.OktoCard.BackUnlocked = true;
            _ui.SetYokaiBlackened(true);
            yield return PlayLines(OktoDialogue.Blackening, OktoDialogueSection.Blackening);
            yield return Advance(TutorialStepId.BlackRabbitFlee);
        }

        IEnumerator StepBlackRabbitFlee()
        {
            yield return PlayLines(OktoDialogue.BlackRabbitFlee, OktoDialogueSection.BlackRabbitFlee);
            yield return _ui.PlayYokaiFlee();
            _ui.ShowStatus("빈 족자…");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Advance(TutorialStepId.ImugiRestore);
        }

        IEnumerator StepImugiRestore()
        {
            yield return _ui.PlayImugiCapture();
            _ui.RestoreYokaiOnScroll();
            yield return PlayLines(OktoDialogue.ImugiRestore, OktoDialogueSection.ImugiRestore);
            _ui.SetYokaiBlackened(false);
            _ui.RefreshAll(_state);
            yield return Advance(TutorialStepId.WishBranch);
        }

        IEnumerator StepWish()
        {
            yield return PlayLines(OktoDialogue.WishPrompt, OktoDialogueSection.WishPrompt);
            yield return WaitChoice(OktoDialogue.WishChoices);
            var choice = _lastChoiceId;

            if (choice == "cancel_contract")
            {
                yield return PlayLines(OktoDialogue.HiddenConfirm, OktoDialogueSection.HiddenConfirm);
                yield return WaitChoice(OktoDialogue.HiddenConfirmChoices);

                if (_lastChoiceId == "confirm_cancel")
                {
                    _state.LastEnding = EndingType.HiddenEndingZero;
                    yield return PlayLines(OktoDialogue.HiddenEnding, OktoDialogueSection.HiddenEnding);
                    _ui.ShowStatus("히든 엔딩 0 — 타이틀로 복귀(세이브 유지)");
                    _state.TutorialFinished = true;
                    _state.TutorialStep = TutorialStepId.Done;
                    SaveService.Save(_state);
                    yield break;
                }
            }

            _ui.ShowDoppelganger(true);
            yield return PlayLines(OktoDialogue.Doppelganger, OktoDialogueSection.Doppelganger);
            _ui.ShowDoppelganger(false);
            _state.OktoCard.FrontUnlocked = true;
            _state.LastEnding = EndingType.TutorialComplete;
            yield return Advance(TutorialStepId.CardComplete);
        }

        IEnumerator StepCardComplete()
        {
            _ui.ShowCardComplete(_state.OktoCard);
            yield return WaitInput();
            yield return PlayLines(OktoDialogue.CardAndIncense, OktoDialogueSection.CardAndIncense);
            _state.Wallet.Incense += GameConstants.IncensePerSummon;
            _state.TutorialFinished = true;
            _ui.RefreshAll(_state);
            _ui.ShowStatus($"향 +{GameConstants.IncensePerSummon}");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Advance(TutorialStepId.Done);
        }

        IEnumerator MoveYutPiece(int from, int to, float duration)
        {
            if (from == to)
            {
                _ui.SetYutPieceIndex(to);
                yield break;
            }

            int step = to > from ? 1 : -1;
            int cur = from;
            float per = duration / Mathf.Max(1, Mathf.Abs(to - from));
            while (cur != to)
            {
                cur += step;
                _ui.SetYutPieceIndex(cur);
                yield return new WaitForSecondsRealtime(per);
            }
        }

        IEnumerator PlayLines(IReadOnlyList<DialogueLine> lines, string sectionId)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                _ui.ShowDialogue(lines[i], i + 1, lines.Count, sectionId);
                yield return WaitInput();
            }
            _ui.HideDialogue();
        }

        IEnumerator WaitInput()
        {
            _waitingInput = true;
            while (_waitingInput) yield return null;
        }

        IEnumerator WaitChoice(IReadOnlyList<ChoiceOption> choices)
        {
            _lastChoiceId = null;
            _ui.ShowChoices(choices);
            yield return WaitInput();
        }

        void HandleYokaiTap()
        {
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

            if (_waitingInput &&
                (_state.TutorialStep == TutorialStepId.FirstMeeting ||
                 _state.TutorialStep == TutorialStepId.Petting))
            {
                _waitingInput = false;
            }
        }

        void HandleOffer()
        {
            if (_state.Wallet.PurifiedWater <= 0) return;

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

        void HandleTrainingPressed()
        {
            if (!_waitingInput) return;
            if (_state.TutorialStep == TutorialStepId.Training ||
                _state.TutorialStep == TutorialStepId.Petting)
            {
                _trainingButtonPressed = true;
                _waitingInput = false;
            }
        }

        void HandleThrowYut()
        {
            if (_state.TutorialStep == TutorialStepId.Training && _waitingInput)
            {
                _ui.SetThrowYutVisible(false);
                _waitingInput = false;
            }
        }

        void HandleLeaveTraining()
        {
            if (_state.TutorialStep == TutorialStepId.Training && _waitingInput)
                _waitingInput = false;
        }

        void HandleChoice(string id)
        {
            _lastChoiceId = id;
            if (_waitingInput) _waitingInput = false;
        }

        void HandleDialogueContinue()
        {
            if (_waitingInput) _waitingInput = false;
        }
    }
}
