using System;
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
        public YokaiInstance FocusYokai;
        public CardFaceState OktoCard = new();
        public int UnlockedSlots = GameConstants.BaseSlots;
        public int TotalSummons;
        public ScrollMode ScrollMode = ScrollMode.Nurture;

        public static GameState CreateNewGame()
        {
            var state = new GameState
            {
                FocusYokai = new YokaiInstance("okto", "옥토끼", isTutorialOkto: true),
                TutorialStep = TutorialStepId.FirstMeeting
            };
            state.Wallet.PurifiedWater = 0;
            state.Wallet.Hearts = GameConstants.HeartMax;
            return state;
        }
    }
}
