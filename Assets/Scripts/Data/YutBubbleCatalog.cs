using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 윷놀이 말풍선 문구. 소스: Resources/Yut/yut_bubbles.{locale}.json
    /// 시트 → Tools/export_yut_bubbles.py 로 export. 조건/우선순위는 코드(YutScreen)에 둔다.
    /// </summary>
    public static class YutBubbleCatalog
    {
        public static class Ids
        {
            public const string RabbitYut = "rabbit.yut";
            public const string RabbitMo = "rabbit.mo";
            public const string RabbitBaekdo = "rabbit.baekdo";
            public const string RabbitSteps = "rabbit.steps";
            public const string CandidateTreasure = "candidate.treasure";
            public const string CandidateOffering = "candidate.offering";
            public const string CandidateCoin = "candidate.coin";
            public const string CandidateCapture = "candidate.capture";
            public const string CandidateStack = "candidate.stack";
            public const string CandidateFinish = "candidate.finish";
            public const string EventCaptured = "event.captured";
            public const string EventRevived = "event.revived";
            public const string EventOpponentCaught = "event.opponent_caught";
            public const string OpponentThrow = "opponent.throw";
        }

        [Serializable]
        public class Line
        {
            public string id;
            public string text;
        }

        [Serializable]
        class Root
        {
            public string version;
            public string locale;
            public Line[] lines;
        }

        static Dictionary<string, string> _byId;
        static string _loadedLocale;
        static bool _loggedMissing;

        /// <summary>로드할 locale. null이면 ko.</summary>
        public static string LocaleOverride { get; set; }

        public static void EnsureLoaded()
        {
            string locale = string.IsNullOrEmpty(LocaleOverride) ? "ko" : LocaleOverride;
            if (_byId != null && _loadedLocale == locale) return;

            _byId = new Dictionary<string, string>();
            _loadedLocale = locale;

            var text = Resources.Load<TextAsset>($"Yut/yut_bubbles.{locale}")
                       ?? Resources.Load<TextAsset>("Yut/yut_bubbles.ko")
                       ?? Resources.Load<TextAsset>("Yut/yut_bubbles");
            if (text == null)
            {
                if (!_loggedMissing)
                {
                    Debug.LogWarning("[YutBubbleCatalog] Resources/Yut/yut_bubbles*.json 없음 — 폴백 문구 사용.");
                    _loggedMissing = true;
                }
                return;
            }

            var root = JsonUtility.FromJson<Root>(text.text);
            if (root?.lines == null)
            {
                Debug.LogError("[YutBubbleCatalog] yut_bubbles JSON 파싱 실패.");
                return;
            }

            foreach (var line in root.lines)
            {
                if (line == null || string.IsNullOrEmpty(line.id)) continue;
                _byId[line.id] = line.text ?? "";
            }
        }

        /// <summary>테스트/에디터용 — 캐시 비우기.</summary>
        public static void ResetForTests()
        {
            _byId = null;
            _loadedLocale = null;
            _loggedMissing = false;
        }

        public static string Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            EnsureLoaded();
            if (_byId != null && _byId.TryGetValue(id, out var text) && !string.IsNullOrEmpty(text))
                return text;
            return Fallback(id);
        }

        public static string Format(string id, params (string key, string value)[] args)
        {
            string text = Get(id);
            if (args == null) return text;
            for (int i = 0; i < args.Length; i++)
            {
                var (key, value) = args[i];
                if (string.IsNullOrEmpty(key)) continue;
                text = text.Replace("{" + key + "}", value ?? "");
            }
            return text;
        }

        static string Fallback(string id) => id switch
        {
            Ids.RabbitYut => "윷이군. 다시.",
            Ids.RabbitMo => "모군. 다시.",
            Ids.RabbitBaekdo => "빽도. 뒤로 돌아가요!",
            Ids.RabbitSteps => "{result}. {steps}칸 이동할 수 있어요!",
            Ids.CandidateTreasure => "보물상자로 갈 수 있어.",
            Ids.CandidateOffering => "공양물을 얻을 수 있어.",
            Ids.CandidateCoin => "엽전을 얻을 수 있어.",
            Ids.CandidateCapture => "이무기 님을 잡을 수 있어.",
            Ids.CandidateStack => "{ally}와 업을 수 있어.",
            Ids.CandidateFinish => "완주할 수 있어.",
            Ids.EventCaptured => "으악!! 잡혀버렸어요!",
            Ids.EventRevived => "되살아났어요!",
            Ids.EventOpponentCaught => "크윽...! 방심했다, 한 번 더 던지거라!",
            Ids.OpponentThrow => "{result}!",
            _ => "",
        };
    }
}
