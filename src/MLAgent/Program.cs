using MLAgent.Data;
using ConfigurationManager = System.Configuration.ConfigurationManager;
using MLAgent.Helpers;

namespace MLAgent
{

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Setup();
            Application.Run(new Form1());
            

        }
        static async void Setup()
        {
            AppConstants.OpenAIKey = ConfigurationManager.AppSettings["OpenAIKey"];
            AppConstants.OpenAIOrg = ConfigurationManager.AppSettings["OpenAIOrg"];
        }

       
    }
}