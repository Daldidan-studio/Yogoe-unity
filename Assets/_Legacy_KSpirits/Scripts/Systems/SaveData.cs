using System;
using System.Collections.Generic;
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
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public string savedAtIso;
        public string locale;

        public int tutorialStep;
        public bool tutorialFinished;
        public bool openingSeen;
        public int lastEnding;
        public int unlockedSlots;
        public int totalSummons;
        public int scrollMode;

        public WalletSave wallet = new();

        /// <summary>보유한 요괴 전체(각자 카드 포함). 소환할 때마다 추가된다.</summary>
        public YokaiSave[] ownedYokai = Array.Empty<YokaiSave>();
        /// <summary>ownedYokai 중 지금 화면에 표시/육성 중인 인덱스.</summary>
        public int focusIndex;

        public CodexEntrySave[] codex = Array.Empty<CodexEntrySave>();

        // v1 호환 전용 필드 — v2 마이그레이션에서 한 번 읽히고 나면 더 이상 쓰이지 않는다.
        // (요괴 1마리 + 카드 1장만 있던 시절의 스키마. 이름을 바꾸면 JsonUtility가
        // 옛 세이브 파일의 "focusYokai"/"oktoCard" 키를 못 읽으므로 그대로 둔다.)
        public YokaiSave focusYokai;
        public CardSave oktoCard;
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
        public int boardNode;
        public CardSave card = new();
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

            var ownedYokai = new YokaiSave[state.OwnedYokai.Count];
            int focusIndex = 0;
            for (int i = 0; i < state.OwnedYokai.Count; i++)
            {
                ownedYokai[i] = FromYokai(state.OwnedYokai[i]);
                if (ReferenceEquals(state.OwnedYokai[i], state.FocusYokai))
                    focusIndex = i;
            }

            return new SaveData
            {
                version = SaveData.CurrentVersion,
                savedAtIso = DateTime.UtcNow.ToString("o"),
                locale = GameLocale.Current,
                tutorialStep = (int)state.TutorialStep,
                tutorialFinished = state.TutorialFinished,
                openingSeen = state.OpeningSeen,
                lastEnding = (int)state.LastEnding,
                unlockedSlots = state.UnlockedSlots,
                totalSummons = state.TotalSummons,
                scrollMode = (int)state.ScrollMode,
                wallet = new WalletSave
                {
                    coins = state.Wallet.Coins,
                    hearts = state.Wallet.Hearts,
                    purifiedWater = state.Wallet.PurifiedWater,
                    incense = state.Wallet.Incense
                },
                ownedYokai = ownedYokai,
                focusIndex = focusIndex,
                codex = Array.Empty<CodexEntrySave>()
            };
        }

        public static GameState ToState(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!string.IsNullOrEmpty(data.locale))
                GameLocale.Current = data.locale;

            var owned = new List<YokaiInstance>();
            if (data.ownedYokai != null)
            {
                foreach (var y in data.ownedYokai)
                {
                    var instance = ToYokai(y);
                    if (instance != null) owned.Add(instance);
                }
            }

            YokaiInstance focus;
            if (owned.Count > 0)
            {
                int idx = data.focusIndex >= 0 && data.focusIndex < owned.Count ? data.focusIndex : 0;
                focus = owned[idx];
            }
            else
            {
                // 보유 요괴가 하나도 없는 파일(손상/구버전 마이그레이션 실패 등) → 튜토리얼 옥토끼로 시작
                focus = new YokaiInstance("okto", "옥토끼", isTutorialOkto: true);
                owned.Add(focus);
            }

            var state = new GameState
            {
                TutorialStep = (TutorialStepId)data.tutorialStep,
                TutorialFinished = data.tutorialFinished,
                OpeningSeen = data.openingSeen,
                LastEnding = (EndingType)data.lastEnding,
                UnlockedSlots = data.unlockedSlots > 0 ? data.unlockedSlots : GameConstants.BaseSlots,
                TotalSummons = data.totalSummons,
                ScrollMode = (ScrollMode)data.scrollMode,
                Wallet = new PlayerWallet
                {
                    Coins = data.wallet?.coins ?? 0,
                    Hearts = data.wallet?.hearts ?? GameConstants.HeartMax,
                    PurifiedWater = data.wallet?.purifiedWater ?? 0,
                    Incense = data.wallet?.incense ?? 0
                },
                OwnedYokai = owned,
                FocusYokai = focus
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
                occupiesSlot = y.OccupiesSlot,
                boardNode = y.BoardNode,
                card = new CardSave
                {
                    frontUnlocked = y.Card.FrontUnlocked,
                    backUnlocked = y.Card.BackUnlocked,
                    preferBackView = y.Card.PreferBackView
                }
            };
        }

        static YokaiInstance ToYokai(YokaiSave s)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) return null;
            var y = new YokaiInstance(s.id, s.displayName ?? s.id, s.isTutorialOkto)
            {
                OccupiesSlot = s.occupiesSlot,
                Energy = s.energy,
                Intimacy = s.intimacy,
                BoardNode = s.boardNode,
                Card = new CardFaceState
                {
                    FrontUnlocked = s.card?.frontUnlocked ?? false,
                    BackUnlocked = s.card?.backUnlocked ?? false,
                    PreferBackView = s.card?.preferBackView ?? false
                }
            };
            y.SetStage((YokaiStage)s.stage);
            return y;
        }
    }
}
