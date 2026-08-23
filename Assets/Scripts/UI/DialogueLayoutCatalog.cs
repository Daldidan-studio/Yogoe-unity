using System;
using System.Collections.Generic;
using KSpirits.Core;
using KSpirits.Data;
using UnityEngine;

namespace KSpirits.UI
{
    [CreateAssetMenu(fileName = "DialogueLayoutCatalog", menuName = "KSpirits/Dialogue Layout Catalog")]
    public class DialogueLayoutCatalog : ScriptableObject
    {
        [Serializable]
        public struct SectionRule
        {
            public string sectionId;
            public DialogueLayoutId layout;
        }

        public DialogueLayoutId defaultLayout = DialogueLayoutId.BottomWide;
        public DialogueLayoutId narrationLayout = DialogueLayoutId.TopNarration;
        public DialogueLayoutId oktoSpeakerLayout = DialogueLayoutId.NearYokai;
        public DialogueLayoutId imugiSpeakerLayout = DialogueLayoutId.AboveMortar;
        public SectionRule[] sectionRules;

        public DialogueLayoutId Resolve(DialogueLine line, string sectionId)
        {
            if (line != null && line.TryGetLayout(out var fromLine))
                return fromLine;

            if (!string.IsNullOrEmpty(sectionId) && TryGetSectionLayout(sectionId, out var fromSection))
                return fromSection;

            if (line != null && line.IsNarration)
                return narrationLayout;

            if (line != null && line.Speaker == "옥토끼")
                return oktoSpeakerLayout;

            if (line != null && line.Speaker == "이무기")
                return imugiSpeakerLayout;

            return defaultLayout;
        }

        public bool TryGetSectionLayout(string sectionId, out DialogueLayoutId layout)
        {
            if (sectionRules != null)
            {
                foreach (var rule in sectionRules)
                {
                    if (rule.sectionId == sectionId)
                    {
                        layout = rule.layout;
                        return true;
                    }
                }
            }

            layout = defaultLayout;
            return false;
        }
    }
}
