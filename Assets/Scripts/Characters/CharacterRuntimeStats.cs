using System;
using System.Collections.Generic;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 런타임 상태값만 담는 클래스. CharacterData(기획 고정값)와 분리해서
    /// 나중에 세이브 파일에는 이 클래스만 직렬화하면 되게 구성.
    /// </summary>
    [Serializable]
    public class CharacterRuntimeStats
    {
        public float Intimacy;   // 0~100
        public float Stamina;    // 0~(25+친밀도)
        public ActionState State = ActionState.Walking;
        public float StateTimer; // 놀기 5분 / 기력0 놀기 18시간→기절

        /// <summary>선호 공양물 중 실제로 먹여서 공개된 offeringId들. 한 번 공개되면 영구(세이브에 유지).</summary>
        public List<string> RevealedPreferredOfferingIds = new List<string>();

        public bool IsPreferenceRevealed(string offeringId)
        {
            if (string.IsNullOrEmpty(offeringId)) return false;
            foreach (var id in RevealedPreferredOfferingIds)
                if (string.Equals(id, offeringId, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>새로 공개됐으면 true.</summary>
        public bool RevealPreference(string offeringId)
        {
            if (string.IsNullOrEmpty(offeringId) || IsPreferenceRevealed(offeringId)) return false;
            RevealedPreferredOfferingIds.Add(offeringId);
            return true;
        }

        public void SetRevealedPreferences(string[] offeringIds)
        {
            RevealedPreferredOfferingIds.Clear();
            if (offeringIds == null) return;
            foreach (var id in offeringIds)
                RevealPreference(id);
        }
    }
}
