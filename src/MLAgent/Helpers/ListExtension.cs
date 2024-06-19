namespace MLAgent.Helpers
{
    public static class ListExtension
    {
       
        public static List<T> GetSnapshot<T>(this List<T> source, int Limit)
        {
            var list = source.Count >= Limit ? source.TakeLast(source.Count - 1).ToList():source;
            return list;
        }
    }
}
