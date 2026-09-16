using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// ActionState → UI 표시(슬롯 딱지 / 상세 문구 / 기력바 색) 매핑.
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

        public static SlotBadge ForSlot(ActionState state) => state switch
        {
            ActionState.Staying => new SlotBadge(true, "일하는", new Color(0.2f, 0.45f, 0.85f, 0.95f)),
            ActionState.Fainted => new SlotBadge(true, "기절", new Color(0.55f, 0.2f, 0.2f, 0.95f)),
            ActionState.Playing => new SlotBadge(true, "놀기", new Color(0.85f, 0.35f, 0.75f, 0.95f)),
            ActionState.Slumped => new SlotBadge(true, "놀기", new Color(0.85f, 0.35f, 0.75f, 0.95f)), // 구세이브
            _ => SlotBadge.Hidden
        };

        public static string ForDetail(ActionState state) => state switch
        {
            ActionState.Walking => "돌아다니는 중",
            ActionState.Staying => "기물에서 일하는 중",
            ActionState.Fainted => "기절했다",
            ActionState.Playing => "놀고 있는 중",
            ActionState.Slumped => "놀고 있는 중",
            _ => ""
        };

        private static readonly Color StaminaHealthy = new Color(0.35f, 0.75f, 0.4f, 1f);
        private static readonly Color StaminaFlashRed = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color StaminaFlashGray = new Color(0.45f, 0.45f, 0.45f, 1f);

        /// <summary>기절만 빨강↔회색 번쩍.</summary>
        public static Color ForStaminaBar(ActionState state, float timeSeconds)
        {
            if (state == ActionState.Fainted)
            {
                float t = Mathf.PingPong(timeSeconds * 2.5f, 1f);
                return Color.Lerp(StaminaFlashRed, StaminaFlashGray, t);
            }
            return StaminaHealthy;
        }
    }
}
