using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Boots.Agents;
using GPTEngine;
using GPTEngine.Text.WPFCommand;
using GPTNet.Events;
using Microsoft.Extensions.Configuration;

namespace Boots.ViewModels
{
    internal class MainViewModel : ViewModel
    {
        private readonly GPT _gpt;

        public ICommand SendToGPT { get; set; }


        public MainViewModel()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            _gpt = new GPT(configuration["OpenApiKey"], configuration["Model"]);
            
            _history = new ObservableCollection<string>();

            History.Add("Loading ... please wait");

            ActivateAsync();

            History.Clear();

            SendToGPT = new AsyncRelayCommand(SendToGPTHandlerAsync);
        }

        private async Task SendToGPTHandlerAsync(object arg)
        {
            string input = Input;
            Input = string.Empty;
            ShowInput = false;

            await Run(input);

            ShowInput = true;
        }

        private async Task Run(string input)
        {
            bool loop = true;
            Supervisor supervisor = new Supervisor(input);
            Developer developer = new Developer(input);

            developer.OnMessageAdded += (sender, args) => { Application.Current.Dispatcher.Invoke(() => { History.Add(BuildGPTMessageFromEvent(args)); }); };
            supervisor.OnMessageAdded += (sender, args) => { Application.Current.Dispatcher.Invoke(() => { History.Add(BuildGPTMessageFromEvent(args)); }); };

            Application.Current.Dispatcher.Invoke(async () => { History.Add("Sending to developer ..."); });

            while (loop)
            {
                var developerResponse = await _gpt.Call(developer);
                
                if (IsError(developerResponse)) return;

                string developerResponseText = developerResponse.Response;

                supervisor.AddMessage($"The developer has returned: {developerResponseText}. Analyse this and return only DONE if you determine this is the best output, otherwise critique and the developer will have another go.");

                var supervisorResponse = await _gpt.Call(supervisor);

                if (IsError(supervisorResponse)) return;

                string supervisorResponseText = supervisorResponse.Response;

                loop = !(supervisorResponseText.ToLowerInvariant().Contains("done"));

                developer.AddMessage(supervisorResponseText);
            }
            
        }

        private string BuildGPTMessageFromEvent(GPTMessageEventArgs args)
        {
            return $"{args.Direction} ({args.Name}): {args.Message}";
        }

        private bool IsError(GPTResponse response)
        {
            if (response.IsError)
            {
                History.Add($"Error: {response.Error}");
                return true;
            }

            return false;
        }

        private async Task ActivateAsync()
        {
            ShowInput = false;

            //Activatey stuff

            ShowInput = true;
        }

        private ObservableCollection<string> _history;
        public ObservableCollection<string> History
        {
            get { return _history; }
            set
            {
                _history = value;
                OnPropertyChanged(nameof(History));
            }
        }


        private bool _showInput;

        public bool ShowInput
        {
            get { return _showInput; }
            set
            {
                SetField(ref _showInput, value, nameof(ShowInput));
            }
        }

        private string _input = string.Empty;
        public string Input
        {
            get { return _input; }
            set
            {
                _input = value;
                OnPropertyChanged(nameof(Input));
            }
        }
    }
}
