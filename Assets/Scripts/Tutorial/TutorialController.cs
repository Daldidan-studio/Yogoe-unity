using System;
using System.Collections;
using System.Collections.Generic;
using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;
using KSpirits.UI;
using UnityEngine;

namespace KSpirits.Tutorial
{
    public class TutorialController : MonoBehaviour
    {
        GameState _state;
        ScrollScreenUI _ui;
        bool _waitingInput;
        string _lastChoiceId;

        public GameState State => _state;

        public void Bind(GameState state, ScrollScreenUI ui)
        {
            _state = state;
            _ui = ui;
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
                    yield return Advance(TutorialStepId.EnergyWarning);
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
                    yield return Advance(TutorialStepId.ImugiRestore);
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
                    _ui.ShowStatus("본편 진입 — 소환 화면(플레이스홀더)");
                    _ui.SetSummonPlaceholderVisible(true);
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

            yield return PlayLines(OktoDialogue.FirstMeeting);

            yield return Advance(TutorialStepId.FirstOffering);
        }

        IEnumerator StepFirstOffering()
        {
            _state.Wallet.PurifiedWater = 1;
            _ui.RefreshAll(_state);
            _ui.SetOfferingHighlight(true);
            _ui.ShowStatus("아래 정화수를 도깨비불 위로 드래그하세요");
            _ui.SetOfferButtonVisible(true);
            yield return WaitInput();

            _ui.SetOfferingHighlight(false);
            _ui.SetOfferButtonVisible(false);
            _ui.PlayShakeYokai();
            yield return PlayLines(OktoDialogue.AfterFirstOffering);
            yield return Advance(TutorialStepId.EvolveToApparition);
        }

        IEnumerator StepEvolveApparition()
        {
            _ui.ShowStatus("진화 중…");
            yield return _ui.PlayAnim("evolve_flash");
            _state.FocusYokai.SetStage(YokaiStage.Apparition);
            _state.FocusYokai.AddEnergy(40);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterApparitionEvolve);
            yield return Advance(TutorialStepId.Petting);
        }

        IEnumerator StepPetting()
        {
            _ui.ShowStatus("옥토끼를 쓰다듬어 보세요");
            _ui.SetYokaiInteractable(true);
            yield return WaitInput();

            _state.Wallet.PurifiedWater += 1;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.Petting);
            yield return Advance(TutorialStepId.Training);
        }

        IEnumerator StepTraining()
        {
            _ui.SetTrainingButtonVisible(true);
            _ui.ShowStatus("수련장을 눌러주세요");
            yield return WaitInput();

            _state.ScrollMode = ScrollMode.Training;
            _ui.SetTrainingButtonVisible(false);
            _ui.EnterTrainingMode(true);
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.TrainingIntro);

            _ui.SetThrowYutVisible(true);
            _ui.ShowStatus("윷을 던져보세요");
            yield return WaitInput();

            _state.Wallet.TrySpendHearts(1);
            _ui.ShowYutResult("빽도");
            _state.Wallet.Coins += 1;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterBaekdo);

            _ui.SetThrowYutVisible(true);
            _ui.ShowStatus("다시 윷을 던져주세요");
            yield return WaitInput();

            _state.Wallet.TrySpendHearts(1);
            _state.FocusYokai.AddEnergy(GameConstants.YutMoveEnergyGain);
            _state.FocusYokai.AddIntimacy(GameConstants.YutMoveIntimacyGain);
            _state.Wallet.PurifiedWater += 1;
            _ui.ShowYutResult("도 → 골인!");
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.AfterGoal);

            _ui.SetLeaveTrainingVisible(true);
            yield return WaitInput();

            _state.ScrollMode = ScrollMode.Nurture;
            _ui.EnterTrainingMode(false);
            _ui.SetLeaveTrainingVisible(false);
            _ui.SetThrowYutVisible(false);
            yield return Advance(TutorialStepId.ItemCollect);
        }

        IEnumerator StepEnergyWarning()
        {
            _state.FocusYokai.Energy = 50;
            _ui.RefreshAll(_state);
            _ui.PulseEnergyBar(true);
            yield return PlayLines(OktoDialogue.EnergyWarning);
            _ui.PulseEnergyBar(false);

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
            yield return PlayLines(OktoDialogue.AfterManifestEvolve);
            yield return Advance(TutorialStepId.MemoryView);
        }

        IEnumerator StepMemory()
        {
            _state.FocusYokai.Intimacy = GameConstants.OktoMemory1;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryMoon);

            _state.FocusYokai.Intimacy = GameConstants.OktoMemory2;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryEarth);

            _state.FocusYokai.Intimacy = GameConstants.OktoMemory3;
            _ui.RefreshAll(_state);
            yield return PlayLines(OktoDialogue.MemoryShop);

            yield return Advance(TutorialStepId.BlackeningChoice);
        }

        IEnumerator StepBlackening()
        {
            yield return PlayLines(OktoDialogue.BeforeBlackeningChoices);
            yield return WaitChoice(OktoDialogue.BlackeningChoices);

            _state.OktoCard.BackUnlocked = true;
            _ui.SetYokaiBlackened(true);
            yield return PlayLines(OktoDialogue.Blackening);
            yield return Advance(TutorialStepId.BlackRabbitFlee);
        }

        IEnumerator StepImugiRestore()
        {
            yield return PlayLines(OktoDialogue.ImugiRestore);
            _ui.SetYokaiBlackened(false);
            _ui.RefreshAll(_state);
            yield return Advance(TutorialStepId.WishBranch);
        }

        IEnumerator StepWish()
        {
            yield return PlayLines(OktoDialogue.WishPrompt);
            yield return WaitChoice(OktoDialogue.WishChoices);
            var choice = _lastChoiceId;

            if (choice == "cancel_contract")
            {
                yield return PlayLines(OktoDialogue.HiddenConfirm);
                yield return WaitChoice(OktoDialogue.HiddenConfirmChoices);

                if (_lastChoiceId == "confirm_cancel")
                {
                    _state.LastEnding = EndingType.HiddenEndingZero;
                    yield return PlayLines(OktoDialogue.HiddenEnding);
                    _ui.ShowStatus("히든 엔딩 0 — 타이틀로 복귀(세이브 유지)");
                    _state.TutorialFinished = true;
                    _state.TutorialStep = TutorialStepId.Done;
                    yield break;
                }
            }

            yield return PlayLines(OktoDialogue.Doppelganger);
            _state.OktoCard.FrontUnlocked = true;
            _state.LastEnding = EndingType.TutorialComplete;
            yield return Advance(TutorialStepId.CardComplete);
        }

        IEnumerator StepCardComplete()
        {
            _ui.ShowCardComplete(_state.OktoCard);
            yield return WaitInput();
            yield return PlayLines(OktoDialogue.CardAndIncense);
            _state.Wallet.Incense += GameConstants.IncensePerSummon;
            _state.TutorialFinished = true;
            _ui.RefreshAll(_state);
            yield return Advance(TutorialStepId.Done);
        }

        IEnumerator PlayLines(IReadOnlyList<DialogueLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                _ui.ShowDialogue(lines[i], i + 1, lines.Count);
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
                if (_waitingInput) _waitingInput = false;
            }
        }

        void HandleTrainingPressed()
        {
            if (_state.TutorialStep == TutorialStepId.Training && _waitingInput)
                _waitingInput = false;
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
