using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public sealed class AvaloniaUser : ReactiveObject, IUser
    {
        private string lastMessage = "";
        private string lastError   = "";
        private int    progressPercent;
        private bool   isBusy;

        public bool Headless => false;

        public string LastMessage
        {
            get => lastMessage;
            private set => this.RaiseAndSetIfChanged(ref lastMessage, value);
        }

        public string LastError
        {
            get => lastError;
            private set => this.RaiseAndSetIfChanged(ref lastError, value);
        }

        public int ProgressPercent
        {
            get => progressPercent;
            private set => this.RaiseAndSetIfChanged(ref progressPercent, value);
        }

        public bool IsBusy
        {
            get => isBusy;
            private set => this.RaiseAndSetIfChanged(ref isBusy, value);
        }

        public bool RaiseYesNoDialog(string question)
            => RunDialog(() => new SimplePromptWindow(question, new[] { "Yes", "No" })
                              .ShowDialog<int>(OwnerWindow())) == 0;

        public int RaiseSelectionDialog(string message, params object[] args)
        {
            var prompt  = args.Length > 0 && args[0] is string first ? first : message;
            var options = new List<string>();
            foreach (var arg in args)
            {
                if (arg is string option)
                {
                    options.Add(option);
                }
                else if (arg is IEnumerable<string> many)
                {
                    options.AddRange(many);
                }
            }
            return RunDialog(() => new SimplePromptWindow(prompt, options)
                                  .ShowDialog<int>(OwnerWindow()));
        }

        public void RaiseError(string message, params object[] args)
        {
            LastError = string.Format(message, args);
            LastMessage = LastError;
            IsBusy = false;
        }

        public void RaiseProgress(string message, int percent)
        {
            ProgressPercent = percent;
            LastMessage     = message;
            IsBusy          = percent < 100;
        }

        public void RaiseProgress(ByteRateCounter rateCounter)
        {
            ProgressPercent = Math.Max(0, Math.Min(100, rateCounter.Percent));
            LastMessage     = rateCounter.Summary;
            IsBusy          = ProgressPercent < 100;
        }

        public void RaiseMessage(string message, params object[] args)
        {
            LastMessage = string.Format(message, args);
        }

        private static Window OwnerWindow()
        {
            var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner == null)
            {
                throw new InvalidOperationException("No main window is available for dialogs.");
            }
            return owner;
        }

        private static T RunDialog<T>(Func<Task<T>> dialogFunc)
        {
            var tcs = new TaskCompletionSource<T>();
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    tcs.SetResult(await dialogFunc());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task.GetAwaiter().GetResult();
        }
    }
}
