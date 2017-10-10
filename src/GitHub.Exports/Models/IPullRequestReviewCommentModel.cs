﻿using System;

namespace GitHub.Models
{
    /// <summary>
    /// Represents a comment on a changed file in a pull request.
    /// </summary>
    public interface IPullRequestReviewCommentModel : ICommentModel
    {
        /// <summary>
        /// The relative path to the file that the comment was made on.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// The line number in the diff between <see cref="IPullRequestModel.Base"/> and 
        /// <see cref="CommitId"/> that the comment appears on.
        /// </summary>
        int? Position { get; }

        /// <summary>
        /// The line number in the diff between <see cref="IPullRequestModel.Base"/> and 
        /// <see cref="OriginalCommitId"/> that the comment was originally left on.
        /// </summary>
        int? OriginalPosition { get; }

        /// <summary>
        /// The commit that the comment appears on.
        /// </summary>
        string CommitId { get; }

        /// <summary>
        /// The commit that the comment was originally left on.
        /// </summary>
        string OriginalCommitId { get; }

        /// <summary>
        /// The diff hunk used to match the pull request.
        /// </summary>
        string DiffHunk { get; }
    }
}
