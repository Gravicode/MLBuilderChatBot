namespace MLAgent.Helpers
{
    public class TokenHelper
    {
        public static bool CheckIfMaxTokenOver(int MaxToken, string Content)
        {
            var count = CountWords(Content);
            var token = 4 * count / 3;
            if (token > MaxToken) {
                return true;
            }
            return false;
            //throw new Exception("Please reduce text, already exceeded the limit.");
        }
        public static unsafe int CountWords(string s)
        {
            int count = 0;
            fixed (char* ps = s)
            {
                int len = s.Length;
                bool inWord = false;
                char* pc = ps;
                while (len-- > 0)
                {
                    if (char.IsWhiteSpace(*pc++))
                    {
                        if (!inWord)
                        {
                            inWord = true;
                        }
                    }
                    else
                    {
                        if (inWord)
                        {
                            inWord = false;
                            count++;
                        }
                    }
                    if (len == 0)
                    {
                        if (inWord)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }
    }
}
