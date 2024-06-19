using HtmlAgilityPack;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace MLAgent.Plugins
{
    public class WebPlugin
    {
      
        int MaxTokens = 3000; double Temperature = 0.0; double TopP = 1; double FrequencyPenalty = 0.0f; double PresencePenalty = 0.0f;

        HttpClient client;
        public WebPlugin()
        {
            client = new HttpClient();
          
        }
        [KernelFunction, Description("scrapping web page to text")]

        public async Task<string> ScrappingWebContent([Description("website url")] string WebUrl)
        {
            try
            {
                var html = await client.GetStringAsync(WebUrl);
                if (string.IsNullOrEmpty(html))
                    return "";

                var web = new HtmlWeb();
                var doc = web.Load(WebUrl);
               
                var plainText = doc.DocumentNode.InnerText;
                plainText = CleanUp(plainText);
                //string plainText = sb.ToString();
                return plainText;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return "fail to scrap web content";
        }
       
        static string CleanUp(string text)
        {
            //string cleaned = Regex.Replace(text, @"\s", "");
            string cleanedText = string.Join(" ", text.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            //string cleanedSentence = Regex.Replace(cleanedText.Trim(), " +", " ");
            return cleanedText;
        }
    }
}
