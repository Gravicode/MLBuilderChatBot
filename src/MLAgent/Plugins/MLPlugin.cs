using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Wordprocessing;
using GemBox.Spreadsheet;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MLAgent.Data;
using MLAgent.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MLAgent.Plugins
{
    public class MLPlugin
    {
        public MLPlugin()
        {
            if (!Directory.Exists(_myDocPath))
            {
                Directory.CreateDirectory(_myDocPath);
            }
            Cmd = new();
        }
        private static readonly string _myDocPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppConstants.FolderName);
        public string FileCsvName { get; set; }
        public string Input { get; set; }
        public System.Data.DataTable DataRaw { get; set; }
        CmdHelper Cmd { get; set; }
        public List<string> Columns { get; set; } = new();
        System.Data.DataTable LoadExcel(byte[] FileExcel)
        {

            var ms = new MemoryStream(FileExcel);
            var workbook = ExcelFile.Load(ms);
            DataTable dt = new DataTable("table");
            var sb = new System.Text.StringBuilder();
            var rowindex = 0;
            // Iterate through all worksheets in an Excel workbook.
            foreach (var worksheet in workbook.Worksheets)
            {
                sb.AppendLine();
                sb.AppendFormat("{0} {1} {0}", new string('-', 25), worksheet.Name);

                // Iterate through all rows in an Excel worksheet.
                foreach (var row in worksheet.Rows)
                {
                    sb.AppendLine();
                    var cols = new List<object>();
                    // Iterate through all allocated cells in an Excel row.
                    foreach (var cell in row.AllocatedCells)
                    {
                        if (cell.ValueType != CellValueType.Null)
                            cols.Add(string.Format("{0}", cell.Value));
                        else
                            cols.Add(" ");
                    }
                    if (rowindex == 0)
                    {
                        foreach (var col in cols)
                            dt.Columns.Add(col.ToString());
                        dt.AcceptChanges();
                    }
                    else
                    {
                        var newRow = dt.NewRow();
                        var colidx = 0;
                        foreach (var col in cols)
                        {
                            newRow[colidx++] = col;
                        }
                        dt.Rows.Add(newRow);
                    }
                    rowindex++;
                }
                break;
            }
            return dt;
        }
        public async Task LoadFile(string FileName)
        {
            if (!File.Exists(FileName)) return;
            var FileData = File.ReadAllBytes(FileName);
            var ext = Path.GetExtension(FileName);
            if (ext == ".xlsx")
            {
                DataRaw = LoadExcel(FileData);
                Input = DataRaw.ToCsv();
            }
            else
            {
                Input = System.Text.Encoding.UTF8.GetString(FileData);
                DataRaw = await CsvHelpers.CSVToDataTable(Input);
            }
            Columns.Clear();
            foreach (DataColumn dc in DataRaw.Columns)
            {
                Columns.Add(dc.ColumnName);
            }
            this.FileCsvName = Path.GetFileName(FileName);
            //FileCsvName = Guid.NewGuid().ToString().Replace("-", "_") + ".csv";
            var pathToSave = Path.Combine(_myDocPath, FileCsvName);
            File.WriteAllText(pathToSave, Input);
        }

        bool IsFileAlreadyLoaded(string FileName)
        {
            var fName = Path.GetFileName(FileName);
            if(this.FileCsvName == fName) return true;
            return false;
        }

        [KernelFunction,
   Description("Detect Machine Learning Task")]
        public async Task<string> DetectMLTask(Kernel kernel, [Description("csv or excel file path")] string filePath)
        {
            try
            {
                ReportPluginInUse();

                if (filePath is null or "")
                {
                    return $"Error load file {filePath} Please provide a correct file path.";
                }
                var content = string.Empty;
                var count = 0;
                var maxLines = 10;
                foreach (var line in File.ReadLines(filePath))
                {
                    content += line + "\n";
                    count++;
                    if (count > maxLines) break;
                }

                if (string.IsNullOrEmpty(content))
                {
                    return "File content is empty, cannot detect machine learning task.";
                }
                else
                {
                    string skPrompt = """
{{$input}}

analyze csv above, what is the best machine learning task for it: Classification, Regression, or Recommendation.
""";

                    PromptExecutionSettings setting = new OpenAIPromptExecutionSettings() { MaxTokens = 2000, Temperature = 0.3f, FrequencyPenalty = 0, PresencePenalty = 0 };
                    var detectLabelFunction = kernel.CreateFunctionFromPrompt(skPrompt, executionSettings: setting, functionName: "DetectLabel");
                    var result = await kernel.InvokeAsync(detectLabelFunction, new KernelArguments() { ["input"] = content });
                    var selectedTask = result.GetValue<string>();
                    return selectedTask ?? "cannot detect machine learning task";
                }

            }
            catch (Exception ex)
            {
                return $"cannot detect machine learning task: {ex}";
            }
        }

        [KernelFunction,
   Description("Detect label Column for training dataset from csv/excel file")]
        public async Task<string> DetectLabelColumn(Kernel kernel, [Description("csv or excel file path")] string filePath)
        {
            try
            {
                ReportPluginInUse();

                if (filePath is null or "")
                {
                    return $"Error load file {filePath} Please provide a correct file path.";
                }
                var content = string.Empty;
                var count = 0;
                var maxLines = 10;
                foreach (var line in File.ReadLines(filePath))
                {
                    content += line + "\n";
                    count++;
                    if (count > maxLines) break;
                }
               
                if (string.IsNullOrEmpty(content))
                {
                    return "File content is empty, cannot detect label column.";
                }
                else
                {
                    string skPrompt = """
{{$input}}

Select 1 label column from csv above for training machine learning model. Write only column name.
""";

                    PromptExecutionSettings setting = new OpenAIPromptExecutionSettings() { MaxTokens = 2000, Temperature = 0.3f, FrequencyPenalty = 0, PresencePenalty = 0 };
                    var detectLabelFunction = kernel.CreateFunctionFromPrompt(skPrompt, executionSettings: setting, functionName: "DetectLabel");
                    var result = await kernel.InvokeAsync(detectLabelFunction, new KernelArguments() { ["input"] = content });
                    var selectedColumn = result.GetValue<string>();
                    return selectedColumn ?? "cannot detect label column";
                }

            }
            catch (Exception ex)
            {
                return $"cannot detect label column: {ex}";
            }
        }
        private static void ReportPluginInUse([CallerMemberName] string? functionName = null)
        {
            Console.WriteLine($"Plugins: MLPlugin; functionName => {functionName ?? "Unknown"}");

        }

        [KernelFunction,
   Description("Install Ml.Net CLI")]
        public async Task<string> InstallMLNetCLI()
        {
            try
            {
                ReportPluginInUse();

                
                if (!Cmd.IsRunning)
                {
                    Cmd.StartCmd();
                }
                while (!Cmd.IsRunning)
                {
                    Thread.Sleep(500);
                }
                var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
                bool IsArm = false;
                switch (arch)
                {
                    case System.Runtime.InteropServices.Architecture.Arm64:
                    case System.Runtime.InteropServices.Architecture.Armv6:
                    case System.Runtime.InteropServices.Architecture.Arm:
                        IsArm = true; 
                        break;
                    default:
                        IsArm = false;
                        break;
                }
                var res = Cmd.ExecuteCommand(IsArm ? $"dotnet tool install --global mlnet-win-arm64" : "dotnet tool install --global mlnet-win-x64");
                if (res)
                    return "ML.Net CLI has been installed.";
                else
                    return "Please install dotnet SDK first from this url: https://dotnet.microsoft.com/en-us/download";
            }
            catch (Exception ex)
            {
                return $"cannot install ML.Net CLI because {ex}";
            }
        }

        [KernelFunction,
 Description("Create Machine Learning Model")]
        public async Task<string> CreateMLModel([Description("csv or excel file path")] string filePath, [Description("Label Column")] string LabelColumn, [Description("ML Task Type")] MLTaskTypes TaskType, [Description("training time in seconds")]int TrainTime = 60)
        {
            try
            {
                ReportPluginInUse();


                if (!Cmd.IsRunning)
                {
                    Cmd.StartCmd();
                }
                while (!Cmd.IsRunning)
                {
                    Thread.Sleep(500);
                }
                if (!IsFileAlreadyLoaded(filePath))
                {
                    await LoadFile(filePath);
                }
                var CmdStr = string.Empty;
                switch (TaskType)
                {
                    case MLTaskTypes.Classification:
                        CmdStr = $@"mlnet classification --dataset ""{this.FileCsvName}"" --label-col ""{LabelColumn}"" --train-time {TrainTime}";
                        break;
                    case MLTaskTypes.Regression:
                        CmdStr = $@"mlnet regression --dataset ""{this.FileCsvName}"" --label-col ""{LabelColumn}"" --train-time {TrainTime}";
                        break;
                    case MLTaskTypes.Recommendation:
                        CmdStr = $@"mlnet recommendation --dataset ""{this.FileCsvName}"" --label-col ""{LabelColumn}"" --train-time {TrainTime}";
                        break;
                }
                var targetFolder = Path.Combine(_myDocPath, $"EXP-{DateTime.Now.ToString("yyyy_MMM_dd_HH_mm_ss")}");
                var dirInfo = new DirectoryInfo(targetFolder);
                CmdStr += $" --output \"{dirInfo.Name}\"";
                if(!dirInfo.Exists)
                {
                    dirInfo.Create();
                }
                System.Diagnostics.Process.Start("explorer.exe", dirInfo.FullName);
                var res = Cmd.ExecuteCommand(CmdStr);
                if (res)
                    return $"Creating ML Model, check the result from this folder: {targetFolder}";
                else
                    return "Please install dotnet SDK first from this url: https://dotnet.microsoft.com/en-us/download";
            }
            catch (Exception ex)
            {
                return $"cannot install ML.Net CLI because {ex}";
            }
        }
       
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum MLTaskTypes
        {
            [Description("Model for Classification")]
            Classification,

            [Description("Model for Regression (predict number)")]
            Regression,

            [Description("Model for recommendation")]
            Recommendation
        }
    }

}
