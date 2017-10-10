﻿using GitHub.Extensions;
using GitHub.Services;
using GitHub.Models;
using Microsoft.VisualStudio.Text.Tagging;

namespace GitHub.InlineReviews.Tags
{
    /// <summary>
    /// Base class for inline comment tags.
    /// </summary>
    /// <seealso cref="AddInlineCommentTag"/>
    /// <seealso cref="ShowInlineCommentTag"/>
    public abstract class InlineCommentTag : ITag
    {
        public InlineCommentTag(
            IPullRequestSession session,
            int lineNumber,
            DiffChangeType diffChangeType)
        {
            Guard.ArgumentNotNull(session, nameof(session));

            LineNumber = lineNumber;
            Session = session;
            DiffChangeType = diffChangeType;
        }

        public int LineNumber { get; }
        public IPullRequestSession Session { get; }
        public DiffChangeType DiffChangeType { get; }
    }
}
