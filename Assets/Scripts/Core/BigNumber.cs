using System;
using System.Text;

namespace Yoegoe.Core
{
    /// <summary>
    /// 무한 확장 숫자 표현 (기획서 3장 "숫자 단위계").
    /// mantissa * 10^exponent 형태로 저장한다. System.Numerics.BigInteger를 매 프레임
    /// 연산에 쓰면 GC 부담이 커서, idle 게임에서 흔히 쓰는 mantissa/exponent 방식을 사용.
    ///
    /// 표시 단위: 1,000 미만은 숫자 그대로, 1,000부터 ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎ(14자) 순서로
    /// 3자리씩 올라가고, ㅎ을 넘어가면 ㄱㄱ, ㄴㄴ ... 식으로 같은 순서를 겹쳐서 확장한다.
    /// </summary>
    [Serializable]
    public struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>
    {
        private static readonly string[] Consonants =
        {
            "ㄱ", "ㄴ", "ㄷ", "ㄹ", "ㅁ", "ㅂ", "ㅅ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"
        };

        public double Mantissa; // 0 이거나 1 <= |Mantissa| < 10
        public int Exponent;

        public static readonly BigNumber Zero = new BigNumber(0, 0);

        public BigNumber(double mantissa, int exponent)
        {
            Mantissa = mantissa;
            Exponent = exponent;
            Normalize(ref Mantissa, ref Exponent);
        }

        public static implicit operator BigNumber(double value) => FromDouble(value);
        public static implicit operator BigNumber(long value) => FromDouble(value);
        public static implicit operator BigNumber(int value) => FromDouble(value);

        public static BigNumber FromDouble(double value)
        {
            if (value == 0) return Zero;
            double sign = value < 0 ? -1 : 1;
            double abs = Math.Abs(value);
            int exp = (int)Math.Floor(Math.Log10(abs));
            double mant = abs / Math.Pow(10, exp);
            return new BigNumber(sign * mant, exp);
        }

        private static void Normalize(ref double mantissa, ref int exponent)
        {
            if (mantissa == 0) { exponent = 0; return; }

            double abs = Math.Abs(mantissa);
            while (abs >= 10) { mantissa /= 10; abs /= 10; exponent++; }
            while (abs > 0 && abs < 1) { mantissa *= 10; abs *= 10; exponent--; }
        }

        public double ToDouble() => Mantissa * Math.Pow(10, Exponent);

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.Mantissa == 0) return b;
            if (b.Mantissa == 0) return a;

            int diff = a.Exponent - b.Exponent;
            // 지수 차이가 크면 작은 쪽은 double 정밀도상 더해봐야 사라지므로 무시
            if (diff > 15) return a;
            if (diff < -15) return b;

            if (diff >= 0)
                return new BigNumber(a.Mantissa + b.Mantissa / Math.Pow(10, diff), a.Exponent);
            return new BigNumber(a.Mantissa / Math.Pow(10, -diff) + b.Mantissa, b.Exponent);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b) => a + new BigNumber(-b.Mantissa, b.Exponent);
        public static BigNumber operator *(BigNumber a, double scalar) => new BigNumber(a.Mantissa * scalar, a.Exponent);
        public static BigNumber operator *(BigNumber a, BigNumber b) => new BigNumber(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        public static BigNumber operator /(BigNumber a, double scalar) => new BigNumber(a.Mantissa / scalar, a.Exponent);

        public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
        public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
        public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;
        public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;

        public int CompareTo(BigNumber other)
        {
            if (Mantissa == 0 && other.Mantissa == 0) return 0;
            if (Mantissa == 0) return other.Mantissa > 0 ? -1 : 1;
            if (other.Mantissa == 0) return Mantissa > 0 ? 1 : -1;

            bool aNeg = Mantissa < 0, bNeg = other.Mantissa < 0;
            if (aNeg != bNeg) return aNeg ? -1 : 1;

            int cmp = Exponent.CompareTo(other.Exponent);
            if (cmp != 0) return aNeg ? -cmp : cmp;
            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(BigNumber other) => Mantissa.Equals(other.Mantissa) && Exponent == other.Exponent;
        public override bool Equals(object obj) => obj is BigNumber other && Equals(other);
        public override int GetHashCode() => (Mantissa, Exponent).GetHashCode();

        /// <summary>기획서 3장 단위계 표기 문자열로 변환.</summary>
        public string ToDisplayString(int decimals = 1)
        {
            if (Mantissa == 0) return "0";

            string sign = Mantissa < 0 ? "-" : "";
            double absMant = Math.Abs(Mantissa);

            if (Exponent < 3)
            {
                double raw = absMant * Math.Pow(10, Exponent);
                return sign + Math.Round(raw).ToString("N0");
            }

            int unitLevel = Exponent / 3;
            int remExp = Exponent % 3;
            double displayValue = absMant * Math.Pow(10, remExp); // 1 ~ 999.999 범위

            // 초반(ㄱ 구간, 1,000대)은 분당 생산이 작아 F1이면 수십 초간 "1.0ㄱ"에 고정된 것처럼 보인다.
            // 값이 작을수록 자릿수를 더 보여 체감 증가가 나게 한다.
            int places = decimals;
            if (displayValue < 10) places = Math.Max(places, 2);
            else if (displayValue < 100) places = Math.Max(places, 1);

            return sign + displayValue.ToString("F" + places) + GetUnitLabel(unitLevel);
        }

        /// <summary>level 1=ㄱ(1,000) ... level 14=ㅎ, level 15=ㄱㄱ, level 16=ㄴㄴ ... (기획서 3장 확장 규칙)</summary>
        private static string GetUnitLabel(int level)
        {
            int idx = level - 1;
            int repeat = idx / Consonants.Length + 1;
            int letterIndex = idx % Consonants.Length;
            string letter = Consonants[letterIndex];

            var sb = new StringBuilder();
            for (int i = 0; i < repeat; i++) sb.Append(letter);
            return sb.ToString();
        }

        public override string ToString() => ToDisplayString();
    }
}
