using MLAgent.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MLAgent.Helpers
{

    public class CmdHelper
    {
        public static EventHandler<ConsoleOutArgs> ResultReceived;
        Process process { set; get; }
        CancellationTokenSource sourceToken = new CancellationTokenSource();
        //Thread th1 { set; get; }
        public bool IsRunning { get; set; }
        //import in the declaration for GenerateConsoleCtrlEvent
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, int dwProcessGroupId);
        public enum ConsoleCtrlEvent
        {
            CTRL_C = 0,
            CTRL_BREAK = 1,
            CTRL_CLOSE = 2,
            CTRL_LOGOFF = 5,
            CTRL_SHUTDOWN = 6
        }

        //set up the parents CtrlC event handler, so we can ignore the event while sending to the child
        public static volatile bool SENDING_CTRL_C_TO_CHILD = false;
        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = SENDING_CTRL_C_TO_CHILD;
        }
        public CmdHelper()
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

        }
        public class ConsoleOutArgs : EventArgs
        {
            public string Message { get; set; }
        }
        public void Stop()
        {
            if (IsRunning)
            {
                try
                {
                    SENDING_CTRL_C_TO_CHILD = true;
                    GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, process.SessionId);
                    //process.WaitForExit();
                    SENDING_CTRL_C_TO_CHILD = false;
                    process.Kill();
                    //process.Close();
                    process.CloseMainWindow();
                    process.Dispose();
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine("stop failed:" + ex);
                }
                IsRunning = false;
            }
        }
        public void StartCmd()
        {
            if (IsRunning) return;
            var token = sourceToken.Token;
            try
            {
                //th1 = new Thread(() =>
                //{
        var _myDocPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppConstants.FolderName);

        var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    WorkingDirectory = _myDocPath,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal,
                    FileName = "cmd",
                    //Arguments = "npm start",
                    RedirectStandardInput = true,
                    //UseShellExecute = true
                };
                // Configure the process using the StartInfo properties.
              
                process = Process.Start(startInfo);
                process.OutputDataReceived += (s, e) =>
                {
                    Console.WriteLine(e.Data);
                    ResultReceived?.Invoke(this, new ConsoleOutArgs() { Message = e.Data });
                };
                process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);

                //process.StandardInput.WriteLine("npm start");
                IsRunning = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"node execution error: {ex}");
                IsRunning = false;
            }
        }

        public bool ExecuteCommand(string Command)
        {
            try
            {
                process.StandardInput.WriteLine(Command);
                process.Start();
                //var res = process.StandardOutput.ReadToEnd();
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

    }
}
