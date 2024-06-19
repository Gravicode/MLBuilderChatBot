using System.Text.RegularExpressions;

namespace MLAgent.Helpers
{
    public class RegexHelper
    {
        public static (bool result, List<string> ImageUrls) ExtractImageUrl(string plainText)
        {
            try
            {
                //string plainText = "Your plain text with image URLs like https://example.com/image.jpg and other content.";

                // Define the regex pattern to match URLs
                Regex urlRegex = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.IgnoreCase);

                // Find all matches
                MatchCollection matches = urlRegex.Matches(plainText);
                List<string> urls = new List<string>();
                var res = matches.Any();
                foreach (Match match in matches)
                {
                    string imageUrl = match.Value;
                    Console.WriteLine($"Image URL: {imageUrl}");
                    urls.Add(imageUrl);
                }
                return (res, urls);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return (false,new());
            
        }
    }
}
