using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using RestSharp;

namespace WpfChatBot
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<ChatMessage> _chatMessages;
        private string _inputText;
        private bool _isBusy;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ChatMessage> ChatMessages
        {
            get { return _chatMessages; }
            set
            {
                _chatMessages = value;
                OnPropertyChanged("ChatMessages");
            }
        }

        public string InputText
        {
            get { return _inputText; }
            set
            {
                _inputText = value;
                OnPropertyChanged("InputText");
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                OnPropertyChanged("IsBusy");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            ChatMessages = new ObservableCollection<ChatMessage>();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputText))
                return;

            // Store user input and clear the input field
            string userInput = InputText;
            InputText = string.Empty;

            // Add user message to chat
            ChatMessages.Add(new ChatMessage
            {
                Content = userInput,
                IsFromUser = true,
                Timestamp = DateTime.Now,
                Role = "user"
            });

            // Show loading indicator
            IsBusy = true;

            try
            {
                // Get response asynchronously
                string response = await GetBotResponse(userInput);

                // Check if response contains code
                if (ContainsCode(response))
                {
                    // Extract code
                    string extractedCode = ExtractCode(response);

                    // Add bot message to chat with code component
                    ChatMessages.Add(new ChatMessage
                    {
                        Content = response.Replace(extractedCode, ""),
                        IsFromUser = false,
                        Timestamp = DateTime.Now,
                        ContainsCode = true,
                        Code = extractedCode,
                        Role = "assistant"
                    });
                }
                else
                {
                    // Add regular bot message to chat
                    ChatMessages.Add(new ChatMessage
                    {
                        Content = response,
                        IsFromUser = false,
                        Timestamp = DateTime.Now,
                        Role = "assistant"
                    });
                }
            }
            catch (Exception ex)
            {
                // Add error message to chat
                ChatMessages.Add(new ChatMessage
                {
                    Content = $"Error: {ex.Message}",
                    IsFromUser = false,
                    Timestamp = DateTime.Now,
                    IsError = true,
                    Role = "system"
                });
            }
            finally
            {
                // Hide loading indicator
                IsBusy = false;

                // Scroll to bottom of chat
                chatScrollViewer.ScrollToBottom();
            }
        }

        private bool ContainsCode(string text)
        {
            // Simple check for code block markers
            return text.Contains("```") || text.Contains("```csharp") || text.Contains("```cs");
        }

        private string ExtractCode(string text)
        {
            // Extract code between markers
            var regex = new Regex(@"```(?:csharp|cs)?\s*([\s\S]*?)\s*```");
            var match = regex.Match(text);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return string.Empty;
        }

        private async Task<string> GetBotResponse(string userInput)
        {
            try
            {
                // Create chat history including current user input
                var messages = new List<ChatMessage>();

                // Add previous context if needed (optional - can be expanded)
                // You could add the last few messages from ChatMessages collection here

                // Add current user input
                messages.Add(new ChatMessage { Role = "user", Content = userInput });

                // Connect to Ollama API
                var options = new RestClientOptions("http://localhost:11434")
                {
                    // Increase timeout for longer responses
                    Timeout = TimeSpan.FromMinutes(2)
                };

                var client = new RestClient(options);
                var request = new RestRequest("/api/chat", Method.Post);
                request.AddHeader("Content-Type", "application/json");

                // Create request body with formatted messages
                var requestBody = new OllamaRequest
                {
                    Model = "llama3.2",
                    Messages = messages.Select(m => new MessageRequest
                    {
                        Role = m.Role,
                        Content = m.Content
                    }).ToList(),
                    // Add stream flag to properly handle streaming responses
                    Stream = true
                };

                // Serialize request body
                request.AddJsonBody(requestBody);

                // Execute request but handle the streaming response properly
                var fullResponse = new StringBuilder();

                // Use stream handling for proper token accumulation
                using (var response = await client.DownloadStreamAsync(request))
                {
                    using (var reader = new StreamReader(response))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            // Skip empty lines
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            try
                            {
                                // Parse each chunk as a separate JSON object
                                var chunk = JsonSerializer.Deserialize<OllamaResponse>(line);

                                if (chunk != null && chunk.message != null)
                                {
                                    // Append this chunk to our full response
                                    fullResponse.Append(chunk.message.content);

                                    // If this is the final chunk, we're done
                                    if (chunk.done)
                                        break;
                                }
                            }
                            catch (JsonException)
                            {
                                // If we got invalid JSON, just skip this chunk
                                continue;
                            }
                        }
                    }
                }

                return fullResponse.ToString();
            }
            catch (Exception ex)
            {
                return $"Error connecting to the LLM service: {ex.Message}";
            }
        }

        private void EditCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var chatMessage = button.DataContext as ChatMessage;

            if (chatMessage != null)
            {
                chatMessage.IsEditingCode = true;
            }
        }

        private void SaveCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var chatMessage = button.DataContext as ChatMessage;

            if (chatMessage != null)
            {
                chatMessage.IsEditingCode = false;
            }
        }

        private void ExecuteCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var chatMessage = button.DataContext as ChatMessage;

            if (chatMessage != null && !string.IsNullOrEmpty(chatMessage.Code))
            {
                try
                {
                    // Execute C# code
                    string result = CompileAndExecuteCode(chatMessage.Code);

                    // Display result
                    ChatMessages.Add(new ChatMessage
                    {
                        Content = "Code Execution Result:\n" + result,
                        IsFromUser = false,
                        Timestamp = DateTime.Now,
                        IsExecutionResult = true
                    });

                    // Scroll to bottom of chat
                    chatScrollViewer.ScrollToBottom();
                }
                catch (Exception ex)
                {
                    // Display error
                    ChatMessages.Add(new ChatMessage
                    {
                        Content = "Error executing code:\n" + ex.Message,
                        IsFromUser = false,
                        Timestamp = DateTime.Now,
                        IsExecutionResult = true,
                        IsError = true
                    });

                    // Scroll to bottom of chat
                    chatScrollViewer.ScrollToBottom();
                }
            }
        }

        private string CompileAndExecuteCode(string code)
        {
            // Simple code compilation and execution
            var codeProvider = new CSharpCodeProvider();
            var parameters = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false
            };

            // Add basic references
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");

            // Wrap the code in a class with a Main method if it doesn't already have one
            if (!code.Contains("static void Main"))
            {
                code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class CodeExecutor
{
    public static void Main()
    {
" + code + @"
    }
}";
            }

            // Compile
            var results = codeProvider.CompileAssemblyFromSource(parameters, code);

            if (results.Errors.Count > 0)
            {
                // Join error messages
                return string.Join("\n", results.Errors.Cast<CompilerError>().Select(error => error.ErrorText));
            }

            // Create a new instance of StringWriter to capture the console output
            var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            try
            {
                // Execute the compiled assembly
                var assembly = results.CompiledAssembly;
                var entryPoint = assembly.EntryPoint;
                entryPoint.Invoke(null, null);

                // Get the output
                return consoleOutput.ToString();
            }
            finally
            {
                // Reset console output
                var standardOutput = new StreamWriter(Console.OpenStandardOutput());
                standardOutput.AutoFlush = true;
                Console.SetOut(standardOutput);
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
            }
        }
    }

    // Class for chat messages in UI
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content;
        private bool _isFromUser;
        private DateTime _timestamp;
        private bool _containsCode;
        private string _code;
        private bool _isEditingCode;
        private bool _isExecutionResult;
        private bool _isError;
        private string _role;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
                OnPropertyChanged("Content");
            }
        }

        public bool IsFromUser
        {
            get { return _isFromUser; }
            set
            {
                _isFromUser = value;
                OnPropertyChanged("IsFromUser");
            }
        }

        public DateTime Timestamp
        {
            get { return _timestamp; }
            set
            {
                _timestamp = value;
                OnPropertyChanged("Timestamp");
            }
        }

        public bool ContainsCode
        {
            get { return _containsCode; }
            set
            {
                _containsCode = value;
                OnPropertyChanged("ContainsCode");
            }
        }

        public string Code
        {
            get { return _code; }
            set
            {
                _code = value;
                OnPropertyChanged("Code");
            }
        }

        public bool IsEditingCode
        {
            get { return _isEditingCode; }
            set
            {
                _isEditingCode = value;
                OnPropertyChanged("IsEditingCode");
            }
        }

        public bool IsExecutionResult
        {
            get { return _isExecutionResult; }
            set
            {
                _isExecutionResult = value;
                OnPropertyChanged("IsExecutionResult");
            }
        }

        public bool IsError
        {
            get { return _isError; }
            set
            {
                _isError = value;
                OnPropertyChanged("IsError");
            }
        }

        public string Role
        {
            get { return _role; }
            set
            {
                _role = value;
                OnPropertyChanged("Role");
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Classes for Llama API communication
    public class MessageRequest
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class OllamaRequest
    {
        public string Model { get; set; }
        public List<MessageRequest> Messages { get; set; }
        public bool Stream { get; set; } = true;
    }

    public class MessageResponse
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class OllamaResponse
    {
        public MessageResponse message { get; set; }
        public string model { get; set; }
        public DateTime created_at { get; set; }
        public bool done { get; set; }
    }
}