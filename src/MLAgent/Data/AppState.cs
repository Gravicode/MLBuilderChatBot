using Newtonsoft.Json;

namespace MLAgent.Data
{
    public class AppState 
    {
        public event Action<bool> OnInternetChange;
        //public UserProfile CurrentUser { get; set; }
        public void RefreshInternet(bool State)
        {
            InternetStateChanged(State);
        }
        private void InternetStateChanged(bool state) => OnInternetChange?.Invoke(state);

    }
}
