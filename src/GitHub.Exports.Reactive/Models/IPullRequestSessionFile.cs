﻿using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace GitHub.Models
{
    /// <summary>
    /// Represents a file in a pull request.
    /// </summary>
    /// <remarks>
    /// A <see cref="IPullRequestSessionFile"/> holds the review comments for a file in a pull
    /// request together with associated information such as the commit SHA of the file and the
    /// diff with the file's merge base.
    /// </remarks>
    /// <seealso cref="IPullRequestSession"/>
    /// <seealso cref="IPullRequestSessionManager"/>
    public interface IPullRequestSessionFile : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets the SHA of the base commit of the file in the pull request.
        /// </summary>
        string BaseSha { get; }

        /// <summary>
        /// Gets the SHA of the current commit of the file, or null if the file has uncommitted
        /// changes.
        /// </summary>
        string CommitSha { get; }

        /// <summary>
        /// Gets the path to the file relative to the repository.
        /// </summary>
        string RelativePath { get; }

        /// <summary>
        /// Gets the diff between the PR merge base and the current state of the file.
        /// </summary>
        IReadOnlyList<DiffChunk> Diff { get; }

        /// <summary>
        /// Gets the inline comments threads for the file.
        /// </summary>
        IReadOnlyList<IInlineCommentThreadModel> InlineCommentThreads { get; }
    }
}
