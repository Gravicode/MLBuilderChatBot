namespace MLAgent.Helpers
{
    public static class StringExtensions
    {
        public static string RemovePrefix(this string s, int prefixLen)
        {
            if (s.Length < prefixLen)
            {
                return string.Empty;
            }
            return s.Substring(prefixLen);
        }
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }
        public static bool Contains(this string source, string[] toChecks, StringComparison comp)
        {
            foreach (var toCheck in toChecks)
            {
                bool res = (source?.IndexOf(toCheck, comp) >= 0);
                if (res) return res;
            }
            return false;
        }
       
    }
}
