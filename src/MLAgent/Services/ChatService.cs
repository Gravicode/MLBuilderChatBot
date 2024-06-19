using MLAgent.Data;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using System.Text;
using MLAgent.Helpers;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.Agents;
using MLAgent.Plugins;
using MLAgent.Models;

namespace MLAgent.Services
{
    public class ChatService
    {
        public string GroupId { get; set; }
        DataAgent dataAgent { set; get; }
        static string DefaultContextMessage = """
            You are data scientist agent, you can help people to create ML Model with functions that you have. You can explain data with details.
            """;
        public string SkillName { get; set; } = "MLBotSkill";
        public string FunctionName { set; get; } = "MLBot";
        public int MaxTokens { set; get; } = 4096;
        public double Temperature { set; get; }
        public double TopP { set; get; }
        //public bool IsProcessing { get; set; } = false;
        public string systemMessage { set; get; }
        Dictionary<string, KernelFunction> ListFunctions = new Dictionary<string, KernelFunction>();

        IKernelBuilder kernelBuilder { set; get; }
        Kernel kernel { set; get; }
        ChatHistory chat;
        IChatCompletionService chatGPT;
        public bool IsConfigured { get; set; } = false;
        public bool IsProcessing { get; set; } = false;
       
        HttpClient client;
        public void Load(RagData data)
        {
            this.Temperature = data.Setting.Temperature;
            this.TopP = data.Setting.TopP;
            this.MaxTokens = (int)data.Setting.MaxToken;
            this.systemMessage = data.SystemMessage;
            var ModelId = data.ModelId;

            chat = new ChatHistory(this.systemMessage);
            foreach (var item in data.Items)
            {
                if (string.IsNullOrEmpty(item.ImageUrl))
                {
                    chat.AddUserMessage(item.Question);
                    chat.AddAssistantMessage(item.Answer);

                }
                else
                {
                    chat.AddUserMessage(new ChatMessageContentItemCollection
        {
            new TextContent(item.Question),
            new ImageContent(new Uri(item.ImageUrl))
        });
                    chat.AddAssistantMessage(item.Answer);
                }

            }

        }
        public ChatService()
        {
            client = new();
            kernelBuilder = Kernel.CreateBuilder();
            //kernelBuilder.Services.ConfigureHttpClientDefaults(x => { x.AddStandardResilienceHandler(); });
                    kernel = kernelBuilder
                       .AddOpenAIChatCompletion(modelId: AppConstants.ModelOpenAIs.First(), apiKey: AppConstants.OpenAIKey, orgId: AppConstants.OpenAIOrg, serviceId: "chat-gpt")
                       .Build();
            var timefun = kernel.CreateFunctionFromMethod(() => DateTime.UtcNow.ToString("R"), "GetCurrentUtcTime", "Retrieves the current time in UTC.");
            kernel.ImportPluginFromFunctions("Helpers", new[] { timefun });
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            kernel.ImportPluginFromType<FileManagerPlugin>();
            kernel.ImportPluginFromType<WebPlugin>();
            kernel.ImportPluginFromType<MLPlugin>();
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            SetupSkill();
        }


        public void SetupSkill(string Context = "")
        {
            // Get AI service instance used to manage the user chat
            chatGPT = kernel.GetRequiredService<IChatCompletionService>();
            systemMessage = string.IsNullOrEmpty(Context) ? DefaultContextMessage : Context;
            chat = new ChatHistory(systemMessage);
            IsConfigured = true;
        }

        public void Reset(string newPersona = "")
        {
            if (!string.IsNullOrEmpty(newPersona))
            {
                chat = new ChatHistory(newPersona);
            }
            else
            {
                chat = new ChatHistory(systemMessage);
            }
        }

        void TruncateHistoryIfTooLong()
        {
            if (chat.Count > 10)
            {
                var allText = new StringBuilder();
                foreach (Microsoft.SemanticKernel.TextContent item in chat.Where(x => x.InnerContent is Microsoft.SemanticKernel.TextContent).Select(x => x.InnerContent))
                {
                    allText.Append(item?.Text);
                }
                var over = TokenHelper.CheckIfMaxTokenOver(MaxTokens, allText.ToString());
                if (over)
                {
                    //take last 10 messages
                    var newChat = new ChatHistory(systemMessage);
                    foreach (ChatMessageContent item in chat.TakeLast(10))
                    {
                        newChat.Add(item);
                    }
                    chat = newChat;
                }
            }
        }
        public async Task<AgentDataResponse> Chat(string userMessage, string FilePath = "")
        {
            string PdfUrl = string.Empty;
            string CSVUrl = string.Empty;
            if (!IsConfigured) SetupSkill();

            string Result = string.Empty;
            if (IsProcessing) return new(Result);
            if (string.IsNullOrEmpty(userMessage))
            {
                Result = AppConstants.EmptyMessageResponse;
                return new AgentDataResponse(Result);
            }
            TruncateHistoryIfTooLong();
            try
            {
                if (!string.IsNullOrEmpty(FilePath))
                {
                    var fileType = Path.GetExtension(FilePath).Replace(".","") == "Csv" ? "CSV":"Excel";
                    userMessage += $",{fileType} filepath: '{FilePath}'";
                }
                var checkImg = RegexHelper.ExtractImageUrl(userMessage);
                if (checkImg.result)
                {
                    var ext = Path.GetExtension(checkImg.ImageUrls?.FirstOrDefault());
                    if (ext.Contains("csv", StringComparison.InvariantCultureIgnoreCase))
                    {
                        CSVUrl = checkImg.ImageUrls?.FirstOrDefault();
                    }
                    else if (ext.Contains("pdf", StringComparison.InvariantCultureIgnoreCase))
                    {
                        PdfUrl = checkImg.ImageUrls?.FirstOrDefault();
                    }
                }
                //1.Ask the user for a message. The user enters a message.Add the user message into the Chat History object.
                Console.WriteLine($"User: {userMessage}");
                if (!string.IsNullOrEmpty(CSVUrl))
                {
                    if (dataAgent is null)
                    {
                        dataAgent = new();
                    }
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    var response = await dataAgent.Answer(userMessage, CSVUrl);
                    chat.AddUserMessage(userMessage);
                    chat.AddAssistantMessage(response.Message);
                    return response;
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                }
                else if (!string.IsNullOrEmpty(PdfUrl))
                {
                    if (dataAgent is null)
                    {
                        dataAgent = new();
                    }
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    var response = await dataAgent.Answer(userMessage, PdfUrl);
                    chat.AddUserMessage(userMessage);
                    chat.AddAssistantMessage(response.Message);
                    return response;
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                }
                else
                {
                    chat.AddUserMessage(userMessage);
                }


                // 2. Send the chat object to AI asking to generate a response. Add the bot message into the Chat History object.
                PromptExecutionSettings setting = new OpenAIPromptExecutionSettings()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    MaxTokens = MaxTokens,
                    Temperature = 1,
                    TopP = 1
                };


                var assistantReply = await chatGPT.GetChatMessageContentAsync(chat, setting, kernel);
                chat.AddAssistantMessage(assistantReply.Content);
                Console.WriteLine(assistantReply);
                Result = assistantReply.Content;
            }
            catch (Exception ex)
            {
                Reset();
                Result = "terjadi kesalahan saat memproses pertanyaan kamu, chat di reset.";
                Console.WriteLine(ex);
            }
            finally
            {
                IsProcessing = false;
            }
            return new(Result);
        }

    }
    public class AgentDataResponse
    {
        public AgentDataResponse()
        {

        }
        public AgentDataResponse(string pesan)
        {
            this.Message = pesan;
        }
        public string Message { get; set; }
        public List<(string mime, byte[] data)> BinaryData { get; set; } = new();
    }
    public class DataAgent
    {
        public string FileUrl { get; set; }
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        public OpenAIAssistantAgent agent { get; set; }
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        HttpClient client = new();
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        AgentGroupChat chat { set; get; }
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        OpenAIFileService fileService { set; get; }
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        public DataAgent()
        {

        }

        public async Task<AgentDataResponse> Answer(string Message, string FileUrl)
        {

            try
            {
                if (agent is null)
                    await SetupAgent(FileUrl);
                else if (this.FileUrl != FileUrl)
                {
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    await agent.DeleteAsync();
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    await SetupAgent(FileUrl);
                }


                // Create a chat for agent interaction.
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                chat = new AgentGroupChat();
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                // Respond to user input

                var msg = await InvokeAgentAsync(Message);
                return msg;
            }
            catch (Exception ex)
            {
                return new AgentDataResponse($"agent error: {ex}");
            }
            finally
            {
            }

            // Local function to invoke agent and display the conversation messages.
            async Task<AgentDataResponse> InvokeAgentAsync(string input)
            {
                var resp = new AgentDataResponse();
                input = input.Replace(this.FileUrl, string.Empty);
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                Console.WriteLine($"# {AuthorRole.User}: '{input}'");
                resp.Message = string.Empty;
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                await foreach (var content in chat.InvokeAsync(agent))
                {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    if (content.Role == AuthorRole.Assistant || content.Role == AuthorRole.Tool)
                    {

                        resp.Message += $"{content.Content}\n";
                        foreach (var fileReference in content.Items.OfType<FileReferenceContent>())
                        {
                            try
                            {
                                Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: #{fileReference.FileId}");
                                var mime = fileReference.MimeType ?? "";
                                var filecontent = fileService.GetFileContent(fileReference.FileId);
                                var outputStream = new MemoryStream();
                                await using var inputStream = await filecontent.GetStreamAsync();
                                await inputStream.CopyToAsync(outputStream);
                                var bytes = outputStream.ToArray();
                                resp.BinaryData.Add((mime, bytes));
                                //var imageUrl = ImageHelper.ConvertToBase64Image(outputStream.ToArray());
                            }
                            catch (Exception xx)
                            {
                                Console.WriteLine(xx);
                            }

                        }
                    }
                    Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                }
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                return resp;

            }
            return default;
        }
        async Task SetupAgent(string fileurl)
        {
            this.FileUrl = fileurl;
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            fileService = new(AppConstants.OpenAIKey);
            var bytes = await client.GetByteArrayAsync(FileUrl);
            var fName = Path.GetFileName(FileUrl);
            OpenAIFileReference uploadFile =
                await fileService.UploadContentAsync(
                    new BinaryContent(bytes),
                    new OpenAIFileUploadExecutionSettings(fName, OpenAIFilePurpose.Assistants));
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            agent = await OpenAIAssistantAgent.CreateAsync(
                            kernel: new(),
                            config: new(AppConstants.OpenAIKey),
                            new()
                            {
                                Description = "Senior Data Analyst",
                                Instructions = "Anda ahli analisa data, Anda dapat menjelaskan analisis Anda dengan detail dengan memberikan beberapa referensi, Anda dapat menemukan informasi penting, anomali data, membuat beberapa prediksi, memberikan saran kepada pengguna untuk analisis lebih lanjut",
                                Name = "Data Analyst Agent",
                                EnableCodeInterpreter = true, // Enable code-interpreter
                                ModelId = AppConstants.ModelOpenAIs[2],
                                FileIds = [uploadFile.Id],
                                EnableRetrieval = true
                            });

#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        }
    }
}
