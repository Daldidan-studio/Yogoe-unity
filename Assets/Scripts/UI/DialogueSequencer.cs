using System.Collections;
using System.Collections.Generic;
using KSpirits.Data;

namespace KSpirits.UI
{
    /// <summary>
    /// "대사를 한 줄씩 보여주고 탭할 때까지 대기 → (있으면) 선택지 보여주고 고를 때까지 대기"
    /// 패턴을 한 곳에 모은 것. TutorialController/OpeningController처럼 대사를 진행하는
    /// 컨트롤러마다 이 로직을 따로 들고 있지 않게, 각자 자기 인스턴스를 하나씩 만들어 쓴다.
    ///
    /// PlayLines/WaitChoice를 실행하는 동안만 ScrollScreenUI의 대사 입력 소유권을 가져갔다가
    /// 끝나면 그 전 소유자에게 돌려준다(SetDialogueInputOwner) — 그래서 서로 다른 시퀀서가
    /// (오프닝 다시보기처럼) 겹쳐 실행돼도 서로의 대기를 건드리지 않는다.
    /// </summary>
    public class DialogueSequencer
    {
        readonly ScrollScreenUI _ui;
        bool _waitingInput;
        string _lastChoiceId;

        public string LastChoiceId => _lastChoiceId;

        public DialogueSequencer(ScrollScreenUI ui)
        {
            _ui = ui;
        }

        /// <summary>lines를 한 줄씩, 탭할 때마다 다음으로 넘기며 보여준다. 다 끝나면 대사창을 숨긴다.</summary>
        public IEnumerator PlayLines(IReadOnlyList<DialogueLine> lines, string sectionId = null)
        {
            var previousOwner = _ui.SetDialogueInputOwner(HandleContinue, HandleChoice);
            for (int i = 0; i < lines.Count; i++)
            {
                _ui.ShowDialogue(lines[i], i + 1, lines.Count, sectionId);
                yield return WaitForInput();
            }
            _ui.HideDialogue();
            _ui.SetDialogueInputOwner(previousOwner.onContinue, previousOwner.onChoice);
        }

        /// <summary>선택지를 보여주고 고를 때까지 대기한다. 고른 id는 LastChoiceId로 읽는다.</summary>
        public IEnumerator WaitChoice(IReadOnlyList<ChoiceOption> choices)
        {
            var previousOwner = _ui.SetDialogueInputOwner(HandleContinue, HandleChoice);
            _lastChoiceId = null;
            _ui.ShowChoices(choices);
            yield return WaitForInput();
            _ui.SetDialogueInputOwner(previousOwner.onContinue, previousOwner.onChoice);
        }

        IEnumerator WaitForInput()
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
