﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using GitHub.Extensions;
using GitHub.Helpers;
using GitHub.InlineReviews.Models;
using GitHub.Models;
using GitHub.Primitives;
using GitHub.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using ReactiveUI;

namespace GitHub.InlineReviews.Services
{
    /// <summary>
    /// Manages pull request sessions.
    /// </summary>
    [Export(typeof(IPullRequestSessionManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PullRequestSessionManager : ReactiveObject, IPullRequestSessionManager
    {
        readonly IPullRequestService service;
        readonly IPullRequestSessionService sessionService;
        readonly IRepositoryHosts hosts;
        readonly ITeamExplorerServiceHolder teamExplorerService;
        readonly Dictionary<Tuple<string, int>, WeakReference<PullRequestSession>> sessions =
            new Dictionary<Tuple<string, int>, WeakReference<PullRequestSession>>();
        IPullRequestSession currentSession;
        ILocalRepositoryModel repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="PullRequestSessionManager"/> class.
        /// </summary>
        /// <param name="gitService">The git service to use.</param>
        /// <param name="gitClient">The git client to use.</param>
        /// <param name="diffService">The diff service to use.</param>
        /// <param name="service">The pull request service to use.</param>
        /// <param name="hosts">The repository hosts.</param>
        /// <param name="teamExplorerService">The team explorer service to use.</param>
        [ImportingConstructor]
        public PullRequestSessionManager(
            IPullRequestService service,
            IPullRequestSessionService sessionService,
            IRepositoryHosts hosts,
            ITeamExplorerServiceHolder teamExplorerService)
        {
            Guard.ArgumentNotNull(service, nameof(service));
            Guard.ArgumentNotNull(sessionService, nameof(sessionService));
            Guard.ArgumentNotNull(hosts, nameof(hosts));
            Guard.ArgumentNotNull(teamExplorerService, nameof(teamExplorerService));

            this.service = service;
            this.sessionService = sessionService;
            this.hosts = hosts;
            this.teamExplorerService = teamExplorerService;
            teamExplorerService.Subscribe(this, x => RepoChanged(x).Forget());
        }

        /// <inheritdoc/>
        public IPullRequestSession CurrentSession
        {
            get { return currentSession; }
            private set { this.RaiseAndSetIfChanged(ref currentSession, value); }
        }

        public async Task<IPullRequestSessionLiveFile> GetLiveFile(
            string relativePath,
            ITextView textView,
            ITextBuffer textBuffer)
        {
            PullRequestSessionLiveFile result;

            if (!textBuffer.Properties.TryGetProperty(
                typeof(IPullRequestSessionLiveFile),
                out result))
            {
                var dispose = new CompositeDisposable();

                result = new PullRequestSessionLiveFile(
                    relativePath,
                    textBuffer,
                    sessionService.CreateRebuildSignal());

                textBuffer.Properties.AddProperty(
                    typeof(IPullRequestSessionLiveFile),
                    result);

                await UpdateLiveFile(result, true);

                textBuffer.Changed += TextBufferChanged;
                textView.Closed += TextViewClosed;

                dispose.Add(Disposable.Create(() =>
                {
                    textView.TextBuffer.Changed -= TextBufferChanged;
                    textView.Closed -= TextViewClosed;
                }));

                dispose.Add(result.Rebuild.Subscribe(x => UpdateLiveFile(result, x).Forget()));

                dispose.Add(this.WhenAnyValue(x => x.CurrentSession)
                    .Skip(1)
                    .Subscribe(_ => UpdateLiveFile(result, true).Forget()));
                dispose.Add(this.WhenAnyValue(x => x.CurrentSession.PullRequest)
                    .Skip(1)
                    .Subscribe(_ => UpdateLiveFile(result, true).Forget()));

                result.ToDispose = dispose;
            }

            return result;
        }

        /// <inheritdoc/>
        public string GetRelativePath(ITextBuffer buffer)
        {
            var document = sessionService.GetDocument(buffer);
            var path = document?.FilePath;

            if (!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && repository != null)
            {
                var basePath = repository.LocalPath;

                if (path.StartsWith(basePath) && path.Length > basePath.Length + 1)
                {
                    return path.Substring(basePath.Length + 1);
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IPullRequestSession> GetSession(IPullRequestModel pullRequest)
        {
            if (await service.EnsureLocalBranchesAreMarkedAsPullRequests(repository, pullRequest))
            {
                // The branch for the PR was not previously marked with the PR number in the git
                // config so we didn't pick up that the current branch is a PR branch. That has
                // now been corrected, so call RepoChanged to make sure everything is up-to-date.
                await RepoChanged(repository);
            }

            return await GetSessionInternal(pullRequest);
        }

        /// <inheritdoc/>
        public PullRequestTextBufferInfo GetTextBufferInfo(ITextBuffer buffer)
        {
            var projectionBuffer = buffer as IProjectionBuffer;
            PullRequestTextBufferInfo result;

            if (buffer.Properties.TryGetProperty(typeof(PullRequestTextBufferInfo), out result))
            {
                return result;
            }

            if (projectionBuffer != null)
            {
                foreach (var sourceBuffer in projectionBuffer.SourceBuffers)
                {
                    var sourceBufferInfo = GetTextBufferInfo(sourceBuffer);
                    if (sourceBufferInfo != null) return sourceBufferInfo;
                }
            }

            return null;
        }

        async Task RepoChanged(ILocalRepositoryModel repository)
        {
            try
            {
                await ThreadingHelper.SwitchToMainThreadAsync();
                await EnsureLoggedIn(repository);

                if (repository != this.repository)
                {
                    this.repository = repository;
                    CurrentSession = null;
                    sessions.Clear();
                }

                if (string.IsNullOrWhiteSpace(repository?.CloneUrl)) return;

                var modelService = hosts.LookupHost(HostAddress.Create(repository.CloneUrl))?.ModelService;
                var session = CurrentSession;

                if (modelService != null)
                {
                    var pr = await service.GetPullRequestForCurrentBranch(repository).FirstOrDefaultAsync();

                    if (pr?.Item1 != (CurrentSession?.PullRequest.Base.RepositoryCloneUrl.Owner) &&
                        pr?.Item2 != (CurrentSession?.PullRequest.Number))
                    {
                        var pullRequest = await GetPullRequestForTip(modelService, repository);

                        if (pullRequest != null)
                        {
                            var newSession = await GetSessionInternal(pullRequest);
                            if (newSession != null) newSession.IsCheckedOut = true;
                            session = newSession;
                        }
                    }
                }
                else
                {
                    session = null;
                }

                CurrentSession = session;
            }
            catch
            {
                // TODO: Log
            }
        }

        async Task<IPullRequestModel> GetPullRequestForTip(IModelService modelService, ILocalRepositoryModel repository)
        {
            if (modelService != null)
            {
                var pr = await service.GetPullRequestForCurrentBranch(repository);
                if (pr != null) return await modelService.GetPullRequest(pr.Item1, repository.Name, pr.Item2).ToTask();
            }

            return null;
        }

        async Task<PullRequestSession> GetSessionInternal(IPullRequestModel pullRequest)
        {
            PullRequestSession session = null;
            WeakReference<PullRequestSession> weakSession;
            var key = Tuple.Create(pullRequest.Base.RepositoryCloneUrl.Owner, pullRequest.Number);

            if (sessions.TryGetValue(key, out weakSession))
            {
                weakSession.TryGetTarget(out session);
            }

            if (session == null)
            {
                var modelService = hosts.LookupHost(HostAddress.Create(repository.CloneUrl))?.ModelService;

                if (modelService != null)
                {
                    session = new PullRequestSession(
                        sessionService,
                        await modelService.GetCurrentUser(),
                        pullRequest,
                        repository,
                        key.Item1,
                        false);
                    sessions[key] = new WeakReference<PullRequestSession>(session);
                }
            }
            else
            {
                await session.Update(pullRequest);
            }

            return session;
        }

        async Task EnsureLoggedIn(ILocalRepositoryModel repository)
        {
            if (!hosts.IsLoggedInToAnyHost && !string.IsNullOrWhiteSpace(repository?.CloneUrl))
            {
                var hostAddress = HostAddress.Create(repository.CloneUrl);
                await hosts.LogInFromCache(hostAddress);
            }
        }

        async Task UpdateLiveFile(PullRequestSessionLiveFile file, bool rebuildThreads)
        {
            var session = CurrentSession;

            if (session != null)
            {
                var mergeBase = await session.GetMergeBase();
                var contents = sessionService.GetContents(file.TextBuffer);
                file.BaseSha = session.PullRequest.Base.Sha;
                file.CommitSha = await CalculateCommitSha(session, file, contents);
                file.Diff = await sessionService.Diff(
                    session.LocalRepository,
                    mergeBase,
                    session.PullRequest.Head.Sha,
                    file.RelativePath,
                    contents);

                if (rebuildThreads)
                {
                    file.InlineCommentThreads = sessionService.BuildCommentThreads(
                        session.PullRequest,
                        file.RelativePath,
                        file.Diff);
                }
                else
                {
                    var changedLines = sessionService.UpdateCommentThreads(
                        file.InlineCommentThreads,
                        file.Diff);

                    if (changedLines.Count > 0)
                    {
                        file.NotifyLinesChanged(changedLines);
                    }
                }

                file.TrackingPoints = BuildTrackingPoints(
                    file.TextBuffer.CurrentSnapshot,
                    file.InlineCommentThreads);
            }
            else
            {
                file.BaseSha = null;
                file.CommitSha = null;
                file.Diff = null;
                file.InlineCommentThreads = null;
                file.TrackingPoints = null;
            }
        }

        async Task UpdateLiveFile(PullRequestSessionLiveFile file, ITextSnapshot snapshot)
        {
            if (file.TextBuffer.CurrentSnapshot == snapshot)
            {
                await UpdateLiveFile(file, false);
            }
        }

        void InvalidateLiveThreads(PullRequestSessionLiveFile file, ITextSnapshot snapshot)
        {
            if (file.TrackingPoints != null)
            {
                var linesChanged = new List<int>();

                foreach (var thread in file.InlineCommentThreads)
                {
                    ITrackingPoint trackingPoint;

                    if (file.TrackingPoints.TryGetValue(thread, out trackingPoint))
                    {
                        var position = trackingPoint.GetPosition(snapshot);
                        var lineNumber = snapshot.GetLineNumberFromPosition(position);

                        if (lineNumber != thread.LineNumber)
                        {
                            linesChanged.Add(lineNumber);
                            linesChanged.Add(thread.LineNumber);
                            thread.LineNumber = lineNumber;
                            thread.IsStale = true;
                        }
                    }
                }

                linesChanged = linesChanged
                    .Where(x => x >= 0)
                    .Distinct()
                    .ToList();

                if (linesChanged.Count > 0)
                {
                    file.NotifyLinesChanged(linesChanged);
                }
            }
        }

        private IDictionary<IInlineCommentThreadModel, ITrackingPoint> BuildTrackingPoints(
            ITextSnapshot snapshot,
            IReadOnlyList<IInlineCommentThreadModel> threads)
        {
            var result = new Dictionary<IInlineCommentThreadModel, ITrackingPoint>();

            foreach (var thread in threads)
            {
                if (thread.LineNumber >= 0)
                {
                    var line = snapshot.GetLineFromLineNumber(thread.LineNumber);
                    var p = snapshot.CreateTrackingPoint(line.Start, PointTrackingMode.Positive);
                    result.Add(thread, p);
                }
            }

            return result;
        }

        async Task<string> CalculateCommitSha(
            IPullRequestSession session,
            IPullRequestSessionFile file,
            byte[] content)
        {
            var repo = session.LocalRepository;
            return await sessionService.IsUnmodifiedAndPushed(repo, file.RelativePath, content) ?
                   await sessionService.GetTipSha(repo) : null;
        }

        private void CloseLiveFiles(ITextBuffer textBuffer)
        {
            PullRequestSessionLiveFile file;

            if (textBuffer.Properties.TryGetProperty(
                typeof(IPullRequestSessionLiveFile),
                out file))
            {
                file.Dispose();
            }

            var projection = textBuffer as IProjectionBuffer;

            if (projection != null)
            {
                foreach (var source in projection.SourceBuffers)
                {
                    CloseLiveFiles(source);
                }
            }
        }

        void TextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            var textBuffer = (ITextBuffer)sender;
            var file = textBuffer.Properties.GetProperty<PullRequestSessionLiveFile>(typeof(IPullRequestSessionLiveFile));
            InvalidateLiveThreads(file, e.After);
            file.Rebuild.OnNext(textBuffer.CurrentSnapshot);
        }

        void TextViewClosed(object sender, EventArgs e)
        {
            var textView = (ITextView)sender;
            CloseLiveFiles(textView.TextBuffer);
        }
    }
}
