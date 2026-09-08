using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// ActionState → UI 표시(슬롯 딱지 / 상세 문구 / 디버그 점 색) 매핑.
    /// 원본 상태는 CharacterAgent.Stats.State, 문구·색만 여기서 관리한다.
    /// </summary>
    public static class CharacterStatusPresentation
    {
        public readonly struct SlotBadge
        {
            public readonly bool Visible;
            public readonly string Label;
            public readonly Color Color;

            public SlotBadge(bool visible, string label, Color color)
            {
                Visible = visible;
                Label = label;
                Color = color;
            }

            public static readonly SlotBadge Hidden = new SlotBadge(false, "", Color.clear);
        }

        /// <summary>하단 슬롯 위 딱지 (기획 6-2: 일하는 / 힘든 / 기절).</summary>
        public static SlotBadge ForSlot(ActionState state) => state switch
        {
            ActionState.Staying => new SlotBadge(true, "일하는", new Color(0.2f, 0.45f, 0.85f, 0.95f)),
            ActionState.Slumped => new SlotBadge(true, "힘든", new Color(0.9f, 0.55f, 0.15f, 0.95f)),
            ActionState.Fainted => new SlotBadge(true, "기절", new Color(0.55f, 0.2f, 0.2f, 0.95f)),
            _ => SlotBadge.Hidden
        };

        /// <summary>상세화면 상태 한 줄.</summary>
        public static string ForDetail(ActionState state) => state switch
        {
            ActionState.Walking => "돌아다니는 중",
            ActionState.Staying => "기물에서 일하는 중",
            ActionState.Slumped => "지쳐서 주저앉았다",
            ActionState.Fainted => "기절했다",
            ActionState.Playing => "놀고 있는 중",
            _ => ""
        };

        /// <summary>캐릭터 머리 위 디버그 점 색.</summary>
        public static Color ForDebugDot(ActionState state) => state switch
        {
            ActionState.Walking => Color.green,
            ActionState.Staying => new Color(0.25f, 0.55f, 1f),
            ActionState.Slumped => Color.yellow,
            ActionState.Fainted => Color.red,
            ActionState.Playing => new Color(1f, 0.45f, 0.85f),
            _ => Color.white
        };
    }
}
