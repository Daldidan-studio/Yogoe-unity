using System;
using KSpirits.Core;
using KSpirits.Model;

namespace KSpirits.Systems
{
    /// <summary>
    /// 디스크에 쓰는 세이브 스키마. GameState와 1:1에 가깝게 두되,
    /// 버전·시각·확장 필드(슬롯 목록·도감)를 여기에만 추가한다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAtIso;
        public string locale;

        public int tutorialStep;
        public bool tutorialFinished;
        public int lastEnding;
        public int unlockedSlots;
        public int scrollMode;

        public WalletSave wallet = new();
        public YokaiSave focusYokai;
        public CardSave oktoCard = new();

        // v1 예비: 본편 슬롯·도감이 붙을 자리 (지금은 비움)
        public YokaiSave[] slotYokai = Array.Empty<YokaiSave>();
        public CodexEntrySave[] codex = Array.Empty<CodexEntrySave>();
    }

    [Serializable]
    public class WalletSave
    {
        public int coins;
        public int hearts;
        public int purifiedWater;
        public int incense;
    }

    [Serializable]
    public class YokaiSave
    {
        public string id;
        public string displayName;
        public int stage;
        public int energy;
        public int intimacy;
        public bool isTutorialOkto;
        public bool occupiesSlot;
    }

    [Serializable]
    public class CardSave
    {
        public bool frontUnlocked;
        public bool backUnlocked;
        public bool preferBackView;
    }

    [Serializable]
    public class CodexEntrySave
    {
        public string yokaiId;
        public bool frontUnlocked;
        public bool backUnlocked;
    }

    public static class SaveMapper
    {
        public static SaveData FromState(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            return new SaveData
            {
                version = SaveData.CurrentVersion,
                savedAtIso = DateTime.UtcNow.ToString("o"),
                locale = GameLocale.Current,
                tutorialStep = (int)state.TutorialStep,
                tutorialFinished = state.TutorialFinished,
                lastEnding = (int)state.LastEnding,
                unlockedSlots = state.UnlockedSlots,
                scrollMode = (int)state.ScrollMode,
                wallet = new WalletSave
                {
                    coins = state.Wallet.Coins,
                    hearts = state.Wallet.Hearts,
                    purifiedWater = state.Wallet.PurifiedWater,
                    incense = state.Wallet.Incense
                },
                focusYokai = FromYokai(state.FocusYokai),
                oktoCard = new CardSave
                {
                    frontUnlocked = state.OktoCard.FrontUnlocked,
                    backUnlocked = state.OktoCard.BackUnlocked,
                    preferBackView = state.OktoCard.PreferBackView
                },
                slotYokai = Array.Empty<YokaiSave>(),
                codex = Array.Empty<CodexEntrySave>()
            };
        }

        public static GameState ToState(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!string.IsNullOrEmpty(data.locale))
                GameLocale.Current = data.locale;

            var state = new GameState
            {
                TutorialStep = (TutorialStepId)data.tutorialStep,
                TutorialFinished = data.tutorialFinished,
                LastEnding = (EndingType)data.lastEnding,
                UnlockedSlots = data.unlockedSlots > 0 ? data.unlockedSlots : GameConstants.BaseSlots,
                ScrollMode = (ScrollMode)data.scrollMode,
                Wallet = new PlayerWallet
                {
                    Coins = data.wallet?.coins ?? 0,
                    Hearts = data.wallet?.hearts ?? GameConstants.HeartMax,
                    PurifiedWater = data.wallet?.purifiedWater ?? 0,
                    Incense = data.wallet?.incense ?? 0
                },
                FocusYokai = ToYokai(data.focusYokai) ?? new YokaiInstance("okto", "옥토끼", isTutorialOkto: true),
                OktoCard = new CardFaceState
                {
                    FrontUnlocked = data.oktoCard?.frontUnlocked ?? false,
                    BackUnlocked = data.oktoCard?.backUnlocked ?? false,
                    PreferBackView = data.oktoCard?.preferBackView ?? false
                }
            };

            return state;
        }

        static YokaiSave FromYokai(YokaiInstance y)
        {
            if (y == null) return null;
            return new YokaiSave
            {
                id = y.Id,
                displayName = y.DisplayName,
                stage = (int)y.Stage,
                energy = y.Energy,
                intimacy = y.Intimacy,
                isTutorialOkto = y.IsTutorialOkto,
                occupiesSlot = y.OccupiesSlot
            };
        }

        static YokaiInstance ToYokai(YokaiSave s)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) return null;
            var y = new YokaiInstance(s.id, s.displayName ?? s.id, s.isTutorialOkto)
            {
                OccupiesSlot = s.occupiesSlot,
                Energy = s.energy,
                Intimacy = s.intimacy
            };
            y.SetStage((YokaiStage)s.stage);
            return y;
        }
    }
}
