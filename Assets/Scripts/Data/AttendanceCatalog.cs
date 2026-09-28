using System;
using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 출석 윷점 데이터. 소스: Resources/attendance.json
    /// JSON은 직접 고치지 말고 구글 시트(attendance / yut_fortune 탭)에서
    /// `npm run attendance` 로 생성한다 — Tools/export_attendance.py.
    /// </summary>
    public static class AttendanceCatalog
    {
        [Serializable]
        public class Fortune
        {
            /// <summary>"도·개·걸" 형식. 순서 = 도0 개1 걸2 윷3 의 3자리 4진수(0~63).</summary>
            public string gua;
            /// <summary>"반딧불이가 불 속으로 뛰어드는 격"</summary>
            public string name;
            /// <summary>[0]=일 [1]=사람 [2]=마음 — 태그는 플레이어에게 노출하지 않는다.</summary>
            public string[] lines;
        }

        [Serializable]
        class Root
        {
            public int[] rewards;
            public Fortune[] fortunes;
        }

        static readonly int[] DefaultRewards = { 5, 5, 5, 7, 9, 10, 20 };
        static Root root;

        static void EnsureLoaded()
        {
            if (root != null) return;
            var text = Resources.Load<TextAsset>("attendance");
            root = text != null ? JsonUtility.FromJson<Root>(text.text) : null;
            if (root == null)
            {
                Debug.LogError("[AttendanceCatalog] Resources/attendance.json 을 찾지 못했습니다.");
                root = new Root();
            }
            if (root.rewards == null || root.rewards.Length == 0) root.rewards = DefaultRewards;
            if (root.fortunes == null) root.fortunes = Array.Empty<Fortune>();
        }

        /// <summary>1~7일차 엽전 (길이 = 순환 일수).</summary>
        public static int[] Rewards
        {
            get { EnsureLoaded(); return root.rewards; }
        }

        /// <summary>괘 번호(0~63)의 본문. 없으면 null.</summary>
        public static Fortune Get(int index)
        {
            EnsureLoaded();
            return index >= 0 && index < root.fortunes.Length ? root.fortunes[index] : null;
        }
    }
}
