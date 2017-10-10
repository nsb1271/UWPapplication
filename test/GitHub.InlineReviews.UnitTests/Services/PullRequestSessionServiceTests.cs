﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitHub.Factories;
using GitHub.InlineReviews.Services;
using GitHub.InlineReviews.UnitTests.TestDoubles;
using GitHub.Models;
using GitHub.Services;
using NSubstitute;
using Xunit;

namespace GitHub.InlineReviews.UnitTests.Services
{
    public class PullRequestSessionServiceTests
    {
        const int PullRequestNumber = 5;
        const string RepoUrl = "https://foo.bar/owner/repo";
        const string FilePath = "test.cs";

        public class TheBuildCommentThreadsMethod
        {
            [Fact]
            public async Task MatchesReviewCommentOnOriginalLine()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService(FilePath, baseContents))
                {
                    var diff = await diffService.Diff(FilePath, headContents);
                    var pullRequest = CreatePullRequest(FilePath, comment);
                    var target = CreateTarget(diffService);

                    var result = target.BuildCommentThreads(
                        pullRequest,
                        FilePath,
                        diff);

                    var thread = result.Single();
                    Assert.Equal(2, thread.LineNumber);
                }
            }

            [Fact]
            public async Task MatchesReviewCommentOnDifferentLine()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"New Line 1
New Line 2
Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService(FilePath, baseContents))
                {
                    var diff = await diffService.Diff(FilePath, headContents);
                    var pullRequest = CreatePullRequest(FilePath, comment);
                    var target = CreateTarget(diffService);

                    var result = target.BuildCommentThreads(
                        pullRequest,
                        FilePath,
                        diff);

                    var thread = result.Single();
                    Assert.Equal(4, thread.LineNumber);
                }
            }

            [Fact]
            public async Task ReturnsLineNumberMinus1ForNonMatchingComment()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var comment1 = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", position: 1);

                var comment2 = CreateComment(@"@@ -1,4 +1,4 @@
-Line 1
 Line 2
-Line 3
+Line 3 with comment", position: 2);

                using (var diffService = new FakeDiffService(FilePath, baseContents))
                {
                    var diff = await diffService.Diff(FilePath, headContents);
                    var pullRequest = CreatePullRequest(FilePath, comment1, comment2);
                    var target = CreateTarget(diffService);

                    var result = target.BuildCommentThreads(
                        pullRequest,
                        FilePath,
                        diff);

                    Assert.Equal(2, result.Count);
                    Assert.Equal(-1, result[1].LineNumber);
                }
            }

            [Fact]
            public async Task HandlesDifferingPathSeparators()
            {
                var winFilePath = @"foo\test.cs";
                var gitHubFilePath = "foo/test.cs";

                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"New Line 1
New Line 2
Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", gitHubFilePath);

                using (var diffService = new FakeDiffService(winFilePath, baseContents))
                {
                    var diff = await diffService.Diff(winFilePath, headContents);
                    var pullRequest = CreatePullRequest(gitHubFilePath, comment);
                    var target = CreateTarget(diffService);

                    var result = target.BuildCommentThreads(
                        pullRequest,
                        winFilePath,
                        diff);

                    var thread = result.First();
                    Assert.Equal(4, thread.LineNumber);
                }
            }
        }

        public class TheUpdateCommentThreadsMethod
        {
            [Fact]
            public async Task UpdatesWithNewLineNumber()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";
                var newHeadContents = @"Inserted Line
Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService(FilePath, baseContents))
                {
                    var diff = await diffService.Diff(FilePath, headContents);
                    var pullRequest = CreatePullRequest(FilePath, comment);
                    var target = CreateTarget(diffService);

                    var threads = target.BuildCommentThreads(
                        pullRequest,
                        FilePath,
                        diff);

                    Assert.Equal(2, threads[0].LineNumber);

                    diff = await diffService.Diff(FilePath, newHeadContents);
                    var changedLines = target.UpdateCommentThreads(threads, diff);

                    Assert.Equal(3, threads[0].LineNumber);
                    Assert.Equal(new[] { 2, 3 }, changedLines.ToArray());
                }
            }

            [Fact]
            public async Task UnmarksStaleThreads()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService(FilePath, baseContents))
                {
                    var diff = await diffService.Diff(FilePath, headContents);
                    var pullRequest = CreatePullRequest(FilePath, comment);
                    var target = CreateTarget(diffService);

                    var threads = target.BuildCommentThreads(
                        pullRequest,
                        FilePath,
                        diff);

                    threads[0].IsStale = true;
                    var changedLines = target.UpdateCommentThreads(threads, diff);

                    Assert.False(threads[0].IsStale);
                    Assert.Equal(new[] { 2 }, changedLines.ToArray());
                }
            }
        }

        static PullRequestSessionService CreateTarget(IDiffService diffService)
        {
            return new PullRequestSessionService(
                Substitute.For<IGitService>(),
                Substitute.For<IGitClient>(),
                diffService,
                Substitute.For<IApiClientFactory>(),
                Substitute.For<IUsageTracker>());
        }

        static IPullRequestReviewCommentModel CreateComment(
            string diffHunk,
            string filePath = FilePath,
            string body = "Comment",
            int position = 1)
        {
            var result = Substitute.For<IPullRequestReviewCommentModel>();
            result.Body.Returns(body);
            result.DiffHunk.Returns(diffHunk);
            result.Path.Returns(filePath);
            result.OriginalCommitId.Returns("ORIG");
            result.OriginalPosition.Returns(position);
            return result;
        }

        static IPullRequestModel CreatePullRequest(
            string filePath,
            params IPullRequestReviewCommentModel[] comments)
        {
            var changedFile1 = Substitute.For<IPullRequestFileModel>();
            changedFile1.FileName.Returns(filePath);
            var changedFile2 = Substitute.For<IPullRequestFileModel>();
            changedFile2.FileName.Returns("other.cs");

            var result = Substitute.For<IPullRequestModel>();
            result.Number.Returns(PullRequestNumber);
            result.Base.Returns(new GitReferenceModel("BASE", "master", "BASE_SHA", RepoUrl));
            result.Head.Returns(new GitReferenceModel("HEAD", "pr", "HEAD_SHA", RepoUrl));
            result.ChangedFiles.Returns(new[] { changedFile1, changedFile2 });
            result.ReviewComments.Returns(comments);

            return result;
        }
    }
}
