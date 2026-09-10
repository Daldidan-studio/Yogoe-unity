using NUnit.Framework;
using Yoegoe.Save;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// GameSaveData.version 필드는 있었지만 아무 코드도 읽지 않아서, 스키마를 바꾸면
    /// 구세이브가 조용히 잘못 해석될 수 있었다. 이 테스트는 최소한 "버전이 실제로
    /// 검사·갱신된다"는 계약만 고정한다 — 실제 이관 단계는 스키마가 바뀔 때 추가한다.
    /// </summary>
    public class GameSaveMigrationTests
    {
        [Test]
        public void MigrateToCurrent_StampsCurrentVersion()
        {
            var data = new GameSaveData { version = 1 };
            GameSaveMigration.MigrateToCurrent(data);
            Assert.AreEqual(GameSaveMigration.CurrentVersion, data.version);
        }

        [Test]
        public void MigrateToCurrent_NullData_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => GameSaveMigration.MigrateToCurrent(null));
        }

        [Test]
        public void MigrateToCurrent_FutureVersion_LeavesDataUntouched()
        {
            var data = new GameSaveData { version = GameSaveMigration.CurrentVersion + 1 };
            GameSaveMigration.MigrateToCurrent(data);
            Assert.AreEqual(GameSaveMigration.CurrentVersion + 1, data.version);
        }
    }
}
