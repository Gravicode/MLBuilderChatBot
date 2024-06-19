namespace MLAgent.Data
{
    public class AppConstants
    {
        public static string EmptyMessageResponse = "Maaf tidak dapat memproses pesan kosong";
        public static string FolderName { get; set; } = "MLData";
        public static string OpenAIKey { get; set; }
        public static string OpenAIOrg { get; set; }
        public static bool InternetOK { set; get; }
        public static List<string> ModelOpenAIs = new List<string> { "gpt-3.5-turbo", "gpt-4o", "gpt-3.5-turbo-0125", "gpt-4-0125-preview", "gpt-4-vision-preview", "gemini-1.0-pro", "gemini-1.5-pro-latest" };        //"chat-bison-001"
    
    }
}
