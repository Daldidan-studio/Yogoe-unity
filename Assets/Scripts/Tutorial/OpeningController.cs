using System;
using System.Collections;
using System.Collections.Generic;
using KSpirits.Data;
using KSpirits.Model;
using KSpirits.UI;
using UnityEngine;

namespace KSpirits.Tutorial
{
    /// <summary>
    /// 튜토리얼보다 먼저, 게임을 처음 켰을 때 딱 한 번만 재생되는 오프닝 컷씬.
    /// GameState.OpeningSeen이 false일 때 GameBootstrap이 호출하고, 끝나면
    /// TutorialController.Begin()으로 넘어간다.
    ///
    /// 대사/선택지 표시는 TutorialController와 동일하게 ScrollScreenUI의 단일 대사창
    /// (ShowDialogue/ShowChoices)을 그대로 재사용한다 — 화자가 둘뿐이고 겹쳐 말하지 않아서
    /// TwoSpeakerDialogueBox까지는 필요 없다.
    ///
    /// 지금은 배경/이미지 전환 시스템이 없다. speaker/text가 둘 다 빈 연출 전용 행(시트의
    /// fx/note만 채워진 행)은 화면에 아무것도 띄우지 않고 건너뛴다 — 나중에 배경 전환
    /// 시스템이 생기면 이 스킵 지점이 그 훅 포인트가 된다.
    /// </summary>
    public class OpeningController : MonoBehaviour
    {
        GameState _state;
        ScrollScreenUI _ui;
        bool _waitingInput;
        string _lastChoiceId;

        public void Bind(GameState state, ScrollScreenUI ui)
        {
            _state = state;
            _ui = ui;
        }

        public void Play(Action onComplete)
        {
            StartCoroutine(Run(onComplete));
        }

        IEnumerator Run(Action onComplete)
        {
            _ui.OnDialogueContinue += HandleContinue;
            _ui.OnChoiceSelected += HandleChoice;

            yield return PlayLines(OpeningDialogue.Intro);
            yield return WaitChoice(OpeningDialogue.ChoiceOptions);

            yield return PlayLines(_lastChoiceId == "flee" ? OpeningDialogue.Flee : OpeningDialogue.Accept);
            yield return PlayLines(OpeningDialogue.Reveal);

            _ui.OnDialogueContinue -= HandleContinue;
            _ui.OnChoiceSelected -= HandleChoice;

            _state.OpeningSeen = true;
            onComplete?.Invoke();
        }

        IEnumerator PlayLines(IReadOnlyList<DialogueLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line.Text)) continue; // 연출 전용 행 — 아직 재생할 게 없음

                _ui.ShowDialogue(line, i + 1, lines.Count, "opening");
                yield return WaitInput();
            }
            _ui.HideDialogue();
        }

        IEnumerator WaitChoice(IReadOnlyList<ChoiceOption> choices)
        {
            _lastChoiceId = null;
            _ui.ShowChoices(choices);
            yield return WaitInput();
        }

        IEnumerator WaitInput()
        {
            _waitingInput = true;
            while (_waitingInput) yield return null;
        }

        void HandleContinue() => _waitingInput = false;

        void HandleChoice(string id)
        {
            _lastChoiceId = id;
            _waitingInput = false;
        }
    }
}
