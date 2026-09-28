using System;
using NUnit.Framework;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>출석 윷점 (기획 18장): 새벽 4시 하루, 7일 순환, 걸러도 유지, 닫으면 그날 끝, 64괘.</summary>
    public class AttendanceTests
    {
        static readonly int[] Rewards = { 5, 5, 5, 7, 9, 10, 20 };

        [SetUp]
        public void SetUp() => Attendance.ResetFromSave(0, 0);

        [Test]
        public void DayKey_Before4am_IsPreviousDay()
        {
            Assert.AreEqual(20260927, Attendance.DayKey(new DateTime(2026, 9, 28, 3, 59, 0)));
            Assert.AreEqual(20260928, Attendance.DayKey(new DateTime(2026, 9, 28, 4, 0, 0)));
        }

        [Test]
        public void DayKey_IsKstFixed_RegardlessOfDeviceTimezone()
        {
            // UTC 19:00 = KST 다음날 04:00 → 새 하루
            Assert.AreEqual(20260928, Attendance.DayKeyFromUtc(new DateTime(2026, 9, 27, 19, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(20260927, Attendance.DayKeyFromUtc(new DateTime(2026, 9, 27, 18, 59, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void Claim_OncePerDay_AdvancesDay()
        {
            Assert.IsTrue(Attendance.ShouldOpen(20260928));
            Assert.AreEqual(5, Attendance.Claim(20260928, Rewards));
            Assert.IsFalse(Attendance.ShouldOpen(20260928));
            Assert.AreEqual(0, Attendance.Claim(20260928, Rewards));
            Assert.AreEqual(1, Attendance.NextDayIndex);
        }

        [Test]
        public void SeventhDay_Pays20_ThenWrapsToDay1()
        {
            int day = 20260901;
            int last = 0;
            for (int i = 0; i < 7; i++) last = Attendance.Claim(day + i, Rewards);
            Assert.AreEqual(20, last);
            Assert.AreEqual(0, Attendance.NextDayIndex);
            Assert.AreEqual(5, Attendance.Claim(day + 7, Rewards));
        }

        [Test]
        public void SkippedDays_DoNotReset()
        {
            Attendance.Claim(20260901, Rewards);
            Attendance.Claim(20260902, Rewards);
            Assert.AreEqual(5, Attendance.Claim(20260910, Rewards)); // 3일차
            Assert.AreEqual(7, Attendance.Claim(20260920, Rewards)); // 4일차
        }

        [Test]
        public void Dismiss_ClosesForTheDay_KeepsSlot()
        {
            Attendance.Dismiss(20260928);
            Assert.IsFalse(Attendance.ShouldOpen(20260928));
            Assert.AreEqual(0, Attendance.NextDayIndex);
            Assert.IsTrue(Attendance.ShouldOpen(20260929));
        }

        [Test]
        public void RollGua_MapsThreeThrowsTo64()
        {
            int n = 0;
            int[] seq = { 1, 2, 3 }; // 개·걸·윷
            int gua = Attendance.RollGua(_ => seq[n++], out int a, out int b, out int c);
            Assert.AreEqual(1 * 16 + 2 * 4 + 3, gua);
            Assert.AreEqual("개", Attendance.ThrowName(a));
            Assert.AreEqual("윷", Attendance.ThrowName(c));
        }

        [Test]
        public void Catalog_Has64Fortunes_InGuaOrder_And7Rewards()
        {
            CollectionAssert.AreEqual(Rewards, AttendanceCatalog.Rewards);
            Assert.AreEqual("도·도·도", AttendanceCatalog.Get(0).gua);
            Assert.AreEqual("개·걸·윷", AttendanceCatalog.Get(27).gua);
            Assert.AreEqual("활은 있는데 화살이 없는 격", AttendanceCatalog.Get(27).name);
            Assert.AreEqual("윷·윷·윷", AttendanceCatalog.Get(63).gua);
            Assert.AreEqual(3, AttendanceCatalog.Get(63).lines.Length);
            Assert.IsNull(AttendanceCatalog.Get(64));
        }
    }
}
