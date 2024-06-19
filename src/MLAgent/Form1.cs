using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using MLAgent.Data;
using MLAgent.Helpers;
using MLAgent.Pages;
using MLAgent.Services;
using MudBlazor.Services;
using System.Net.NetworkInformation;
using WaterPositive.Kiosk.Helpers;

namespace MLAgent;

public partial class Form1 : Form
{
    AppState? state;
    System.Timers.Timer SyncTimer;
    public Form1()
    {
        InitializeComponent();
        this.Text = "Ml Builder Agent v1.0 - Gravicode";
        var services = new ServiceCollection();

        services.AddWindowsFormsBlazorWebView();
        services.AddMudServices();

        state = new AppState();
        services.AddSingleton<AppState>(state);
        services.AddSingleton<ChatService>();
        blazorWebView1.HostPage = "wwwroot\\index.html";
        blazorWebView1.Services = services.BuildServiceProvider();
        blazorWebView1.RootComponents.Add<App>("#app");
        CheckInternet();
        SyncTimer = new System.Timers.Timer(10000);
        SyncTimer.Elapsed += async (a, b) =>
        {
            var res = CheckInternet();
            await Console.Out.WriteLineAsync($"internet is {(res ? "on" : "off")}");
        };
        SyncTimer.Start();
        GoFullscreen(true);
    }
    bool CheckInternet()
    {
        var res = InternetHelper.IsConnectedToInternet();
        AppConstants.InternetOK = res;
        state?.RefreshInternet(res);
        return res;
    }
    private void GoFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            this.WindowState = FormWindowState.Normal;
            //this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Bounds = Screen.PrimaryScreen.Bounds;
        }
        else
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        }
    }
}