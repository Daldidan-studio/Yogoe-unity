using System;
using System.Collections.Generic;
using KSpirits.Core;

namespace KSpirits.Model
{
    [Serializable]
    public class YokaiInstance
    {
        public string Id;
        public string DisplayName;
        public YokaiStage Stage;
        public int Energy;
        public int Intimacy;
        public bool IsTutorialOkto;
        public bool OccupiesSlot = true;
        // 이 요괴 개체 하나의 양면 카드 진행도. 이전엔 GameState.OktoCard로 하나만 있었지만,
        // 요괴를 여러 마리 소환해도 각자 카드가 따로 남아야 해서 개체별로 들고 있는다.
        public CardFaceState Card = new();

        public YokaiInstance(string id, string displayName, bool isTutorialOkto = false)
        {
            Id = id;
            DisplayName = displayName;
            IsTutorialOkto = isTutorialOkto;
            OccupiesSlot = !isTutorialOkto;
            Stage = YokaiStage.Spirit;
            Energy = 0;
            Intimacy = isTutorialOkto ? GameConstants.OktoMemoryStartIntimacy : 0;
        }

        public void AddEnergy(int amount)
        {
            Energy = Math.Clamp(Energy + amount, 0, GameConstants.EnergyMax);
        }

        public void AddIntimacy(int amount)
        {
            Intimacy = Math.Clamp(Intimacy + amount, 0, GameConstants.IntimacyMax);
        }

        public void SetStage(YokaiStage stage) => Stage = stage;
    }

    [Serializable]
    public class PlayerWallet
    {
        public int Coins;
        public int Hearts = GameConstants.HeartMax;
        public int PurifiedWater;
        public int Incense;

        public bool TrySpendHearts(int amount)
        {
            if (Hearts < amount) return false;
            Hearts -= amount;
            return true;
        }

        public void AddHearts(int amount)
        {
            Hearts = Math.Clamp(Hearts + amount, 0, GameConstants.HeartMax);
        }
    }

    [Serializable]
    public class CardFaceState
    {
        public bool FrontUnlocked;
        public bool BackUnlocked;
        public bool PreferBackView;

        public bool IsComplete => FrontUnlocked && BackUnlocked;
    }

    [Serializable]
    public class GameState
    {
        public TutorialStepId TutorialStep = TutorialStepId.FirstMeeting;
        public bool TutorialFinished;
        public EndingType LastEnding = EndingType.None;
        public PlayerWallet Wallet = new();

        // 지금까지 소환해서 보유 중인 요괴 전체(각자 카드 포함). 소환할 때마다 여기 추가된다.
        public List<YokaiInstance> OwnedYokai = new();
        // OwnedYokai 중 지금 화면에 표시/육성 중인 한 마리 — 반드시 OwnedYokai 안의 참조여야 한다.
        public YokaiInstance FocusYokai;

        public int UnlockedSlots = GameConstants.BaseSlots;
        public int TotalSummons;
        public ScrollMode ScrollMode = ScrollMode.Nurture;

        public static GameState CreateNewGame()
        {
            var okto = new YokaiInstance("okto", "옥토끼", isTutorialOkto: true);
            var state = new GameState
            {
                OwnedYokai = new List<YokaiInstance> { okto },
                FocusYokai = okto,
                TutorialStep = TutorialStepId.FirstMeeting
            };
            state.Wallet.PurifiedWater = 0;
            state.Wallet.Hearts = GameConstants.HeartMax;
            return state;
        }
    }
}
