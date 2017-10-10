﻿using GitHub.Extensions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace GitHub.Services
{
    [Export(typeof(IStatusBarNotificationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class StatusBarNotificationService : IStatusBarNotificationService
    {
        readonly IServiceProvider serviceProvider;

        [ImportingConstructor]
        public StatusBarNotificationService([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public void HideNotification(Guid guid)
        {
            // status bar only shows text, this is a noop
        }

        public bool IsNotificationVisible(Guid guid)
        {
            // it's only text, there's no way of checking
            return false;
        }

        public void ShowError(string message)
        {
            ShowText(message);
        }

        public void ShowMessage(string message)
        {
            ShowText(message);
        }

        public void ShowMessage(string message, ICommand command, bool showToolTips = true, Guid guid = default(Guid))
        {
            ShowText(message);
        }

        public void ShowWarning(string message)
        {
            ShowText(message);
        }

        void ShowText(string text)
        {
            var statusBar = serviceProvider.GetServiceSafe<IVsStatusbar>();
            int frozen;
            if (!ErrorHandler.Succeeded(statusBar.IsFrozen(out frozen)))
                return;
            // If it's frozen, someone else is grabbing the status bar and we
            // can't show the message until they release it
            // so might as well not show it. 
            if (frozen == 0)
                ErrorHandler.Succeeded(statusBar.SetText(text));
        }
    }
}
