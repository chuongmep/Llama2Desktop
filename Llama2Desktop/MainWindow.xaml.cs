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

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputText))
                return;

            // Add user message to chat
            ChatMessages.Add(new ChatMessage
            {
                Content = InputText,
                IsFromUser = true,
                Timestamp = DateTime.Now
            });

            // Get response
            string response = GetBotResponse(InputText);
            
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
                    Code = extractedCode
                });
            }
            else
            {
                // Add regular bot message to chat
                ChatMessages.Add(new ChatMessage
                {
                    Content = response,
                    IsFromUser = false,
                    Timestamp = DateTime.Now
                });
            }

            // Clear input
            InputText = string.Empty;
            
            // Scroll to bottom of chat
            chatScrollViewer.ScrollToBottom();
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

        private string GetBotResponse(string userInput)
        {
            // Simple response logic - in a real app, this would connect to an AI service
            userInput = userInput.ToLower();
            
            if (userInput.Contains("hello") || userInput.Contains("hi"))
            {
                return "Hello! How can I help you today?";
            }
            else if (userInput.Contains("code") || userInput.Contains("example"))
            {
                return "Here's a simple example of a 'Hello World' program in C#:\n\n```csharp\nusing System;\n\nclass Program\n{\n    static void Main()\n    {\n        Console.WriteLine(\"Hello, World!\");\n    }\n}\n```";
            }
            else
            {
                return "I'm not sure how to respond to that. Can you try asking something else?";
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

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}