using System;

namespace FnirsExe.Snirf.Utils
{
    internal static class NaturalSort
    {
        public static int Compare(string a, string b)
        {
            int ai = ExtractTrailingInt(a);
            int bi = ExtractTrailingInt(b);

            if (ai >= 0 && bi >= 0)
            {
                string ap = a.Substring(0, a.Length - ai.ToString().Length);
                string bp = b.Substring(0, b.Length - bi.ToString().Length);

                int cmp = string.Compare(ap, bp, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                return ai.CompareTo(bi);
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static int ExtractTrailingInt(string s)
        {
            int i = s.Length - 1;
            while (i >= 0 && char.IsDigit(s[i])) i--;
            if (i == s.Length - 1) return -1;

            string num = s.Substring(i + 1);
            int v;
            return int.TryParse(num, out v) ? v : -1;
        }
    }
}
