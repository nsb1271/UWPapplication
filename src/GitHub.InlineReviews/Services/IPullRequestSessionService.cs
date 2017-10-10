﻿using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using GitHub.Models;
using Microsoft.VisualStudio.Text;

namespace GitHub.InlineReviews.Services
{
    /// <summary>
    /// Provides a common interface for services required by <see cref="PullRequestSession"/>.
    /// </summary>
    public interface IPullRequestSessionService
    {
        /// <summary>
        /// Carries out a diff of a file between two commits.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="baseSha">The commit to use as the base.</param>
        /// <param name="headSha">The commit to use as the head.</param>
        /// <param name="relativePath">The relative path to the file.</param>
        /// <returns></returns>
        Task<IReadOnlyList<DiffChunk>> Diff(
            ILocalRepositoryModel repository,
            string baseSha,
            string headSha,
            string relativePath);

        /// <summary>
        /// Carries out a diff between a file at a commit and the current file contents.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="baseSha">The commit to use as the base.</param>
        /// <param name="headSha">The commit to use as the head.</param>
        /// <param name="relativePath">The relative path to the file.</param>
        /// <param name="contents">The contents of the file.</param>
        /// <returns></returns>
        Task<IReadOnlyList<DiffChunk>> Diff(
            ILocalRepositoryModel repository,
            string baseSha,
            string headSha,
            string relativePath,
            byte[] contents);

        /// <summary>
        /// Builds a set of comment thread models for a file based on a pull request model and a diff.
        /// </summary>
        /// <param name="pullRequest">The pull request session.</param>
        /// <param name="relativePath">The relative path to the file.</param>
        /// <param name="diff">The diff.</param>
        /// <returns>
        /// A collection of <see cref="IInlineCommentThreadModel"/> objects with updated line numbers.
        /// </returns>
        IReadOnlyList<IInlineCommentThreadModel> BuildCommentThreads(
            IPullRequestModel pullRequest,
            string relativePath,
            IReadOnlyList<DiffChunk> diff);

        /// <summary>
        /// Updates a set of comment thread models for a file based on a new diff.
        /// </summary>
        /// <param name="threads">The theads to update.</param>
        /// <param name="diff">The diff.</param>
        /// <returns>
        /// A collection of updated line numbers.
        /// </returns>
        IReadOnlyList<int> UpdateCommentThreads(
            IReadOnlyList<IInlineCommentThreadModel> threads,
            IReadOnlyList<DiffChunk> diff);

        /// <summary>
        /// Tests whether the contents of a file represent a commit that is pushed to origin.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="relativePath">The relative path to the file.</param>
        /// <param name="contents">The contents of the file.</param>
        /// <returns>
        /// A task returning true if the file is unmodified with respect to the latest commit
        /// pushed to origin; otherwise false.
        /// </returns>
        Task<bool> IsUnmodifiedAndPushed(
            ILocalRepositoryModel repository,
            string relativePath,
            byte[] contents);

        /// <summary>
        /// Extracts a file at a specified commit from the repository.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="commitSha">The SHA of the commit.</param>
        /// <param name="relativePath">The path to the file, relative to the repository.</param>
        /// <returns>
        /// The contents of the file, or null if the file was not found at the specified commit.
        /// </returns>
        Task<byte[]> ExtractFileFromGit(
            ILocalRepositoryModel repository,
            int pullRequestNumber,
            string sha,
            string relativePath);

        /// <summary>
        /// Gets the associated <see cref="ITextDocument"/> for an <see cref="ITextBuffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>
        /// The associated document, or null if not found.
        /// </returns>
        ITextDocument GetDocument(ITextBuffer buffer);

        /// <summary>
        /// Gets the contents of an <see cref="ITextBuffer"/> using the buffer's current encoding.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The contents of the buffer.</returns>
        byte[] GetContents(ITextBuffer buffer);

        /// <summary>
        /// Gets the SHA of the tip of the current branch.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <returns>The tip SHA.</returns>
        Task<string> GetTipSha(ILocalRepositoryModel repository);

        /// <summary>
        /// Asynchronously reads the contents of a file.
        /// </summary>
        /// <param name="path">The full path to the file.</param>
        /// <returns>
        /// A task returning the contents of the file, or null if the file was not found.
        /// </returns>
        Task<byte[]> ReadFileAsync(string path);

        /// <summary>
        /// Find the merge base for a pull request.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="pullRequest">The pull request.</param>
        /// <returns>
        /// The merge base SHA for the PR.
        /// </returns>
        Task<string> GetPullRequestMergeBase(ILocalRepositoryModel repository, IPullRequestModel pullRequest);

        /// <summary>
        /// Creates a rebuild signal subject for a <see cref="IPullRequestSessionLiveFile"/>.
        /// </summary>
        /// <returns>
        /// A subject which is used to signal a rebuild of a live file.
        /// </returns>
        /// <remarks>
        /// The creation of the rebuild signal for a <see cref="IPullRequestSessionLiveFile"/> is
        /// abstracted out into this service for unit testing. The default behavior of this subject
        /// is to throttle the signal for 500ms so that the live file is not updated continuously
        /// while the user is typing.
        /// </remarks>
        ISubject<ITextSnapshot, ITextSnapshot> CreateRebuildSignal();

        /// <summary>
        /// Posts a new PR review comment.
        /// </summary>
        /// <param name="localRepository">The local repository.</param>
        /// <param name="remoteRepositoryOwner">The owner of the repository fork to post to.</param>
        /// <param name="user">The user posting the comment.</param>
        /// <param name="number">The pull request number.</param>
        /// <param name="body">The comment body.</param>
        /// <param name="commitId">THe SHA of the commit to comment on.</param>
        /// <param name="path">The relative path of the file to comment on.</param>
        /// <param name="position">The line index in the diff to comment on.</param>
        /// <returns>A model representing the posted comment.</returns>
        Task<IPullRequestReviewCommentModel> PostReviewComment(
            ILocalRepositoryModel localRepository,
            string remoteRepositoryOwner,
            IAccount user,
            int number,
            string body,
            string commitId,
            string path,
            int position);

        /// <summary>
        /// Posts a PR review comment reply.
        /// </summary>
        /// <param name="localRepository">The local repository.</param>
        /// <param name="remoteRepositoryOwner">The owner of the repository fork to post to.</param>
        /// <param name="user">The user posting the comment.</param>
        /// <param name="number">The pull request number.</param>
        /// <param name="body">The comment body.</param>
        /// <param name="inReplyTo">The comment ID to reply to.</param>
        /// <returns>A model representing the posted comment.</returns>
        Task<IPullRequestReviewCommentModel> PostReviewComment(
            ILocalRepositoryModel localRepository,
            string remoteRepositoryOwner,
            IAccount user,
            int number,
            string body,
            int inReplyTo);
    }
}
