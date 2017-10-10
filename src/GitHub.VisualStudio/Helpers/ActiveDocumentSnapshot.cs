﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;

namespace GitHub.VisualStudio
{
    [Export(typeof(IActiveDocumentSnapshot))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class ActiveDocumentSnapshot : IActiveDocumentSnapshot
    {
        public string Name { get; private set; }
        public int StartLine { get; private set; }
        public int EndLine { get; private set; }

        [ImportingConstructor]
        public ActiveDocumentSnapshot([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            StartLine = EndLine = -1;
            var document = Services.Dte2?.ActiveDocument;
            Name = document.FullName.Equals(document.ProjectItem.FileNames[1], StringComparison.OrdinalIgnoreCase) ? document.ProjectItem.FileNames[1] : document.FullName;

            var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            Debug.Assert(textManager != null, "No SVsTextManager service available");
            if (textManager == null)
                return;
            IVsTextView view;
            int anchorLine, anchorCol, endLine, endCol;
            if (ErrorHandler.Succeeded(textManager.GetActiveView(0, null, out view)) &&
                ErrorHandler.Succeeded(view.GetSelection(out anchorLine, out anchorCol, out endLine, out endCol)))
            {
                StartLine = anchorLine + 1;
                EndLine = endLine + 1;
            }
        }
    }
}
