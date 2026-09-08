using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// ActionState → UI 표시(슬롯 딱지 / 상세 문구 / 기력바 색) 매핑.
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

        /// <summary>하단 슬롯 위 딱지 (기획 6-2: 일하는 / 힘든 / 기절). 놀기는 디버그용.</summary>
        public static SlotBadge ForSlot(ActionState state) => state switch
        {
            ActionState.Staying => new SlotBadge(true, "일하는", new Color(0.2f, 0.45f, 0.85f, 0.95f)),
            ActionState.Slumped => new SlotBadge(true, "힘든", new Color(0.9f, 0.55f, 0.15f, 0.95f)),
            ActionState.Fainted => new SlotBadge(true, "기절", new Color(0.55f, 0.2f, 0.2f, 0.95f)),
            // 디버그: 놀기 확인용 (기획 슬롯 딱지에는 원래 없음)
            ActionState.Playing => new SlotBadge(true, "놀기", new Color(0.85f, 0.35f, 0.75f, 0.95f)),
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

        private static readonly Color StaminaHealthy = new Color(0.35f, 0.75f, 0.4f, 1f);
        private static readonly Color StaminaFlashRed = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color StaminaFlashGray = new Color(0.45f, 0.45f, 0.45f, 1f);

        /// <summary>
        /// 슬롯 기력바 색. 주저앉기·기절은 빨강↔회색 번쩍 (기획 6-2).
        /// </summary>
        public static Color ForStaminaBar(ActionState state, float timeSeconds)
        {
            if (state == ActionState.Slumped || state == ActionState.Fainted)
            {
                float t = Mathf.PingPong(timeSeconds * 2.5f, 1f);
                return Color.Lerp(StaminaFlashRed, StaminaFlashGray, t);
            }
            return StaminaHealthy;
        }
    }
}
