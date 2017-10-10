﻿using System.Reactive.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using UnitTests;
using GitHub.Models;
using System;
using GitHub.Services;
using GitHub.ViewModels;
using Octokit;
using GitHub.Api;
using System.ComponentModel;
using Rothko;

/// <summary>
/// All the tests in this class are split in subclasses so that when they run
/// in parallel the temp dir is set up uniquely for each test
/// </summary>

public class PullRequestCreationViewModelTests : TestBaseClass
{
    static LibGit2Sharp.IRepository SetupLocalRepoMock(IGitClient gitClient, IGitService gitService, string remote, string head, bool isTracking)
    {
        var l2remote = Substitute.For<LibGit2Sharp.Remote>();
        l2remote.Name.Returns(remote);
        gitClient.GetHttpRemote(Args.LibGit2Repo, Args.String).Returns(Task.FromResult(l2remote));

        var l2repo = Substitute.For<LibGit2Sharp.IRepository>();
        var l2branchcol = Substitute.For<LibGit2Sharp.BranchCollection>();
        var l2branch = Substitute.For<LibGit2Sharp.Branch>();
        l2branch.FriendlyName.Returns(head);
        l2branch.IsTracking.Returns(isTracking);
        l2branchcol[Args.String].Returns(l2branch);
        l2repo.Branches.Returns(l2branchcol);
        l2repo.Head.Returns(l2branch);
        gitService.GetRepository(Args.String).Returns(l2repo);
        return l2repo;
    }

    struct TestData
    {
        public IServiceProvider ServiceProvider;
        public ILocalRepositoryModel ActiveRepo;
        public LibGit2Sharp.IRepository L2Repo;
        public IRepositoryModel SourceRepo;
        public IRepositoryModel TargetRepo;
        public IBranch SourceBranch;
        public IBranch TargetBranch;
        public IGitClient GitClient;
        public IGitService GitService;
        public INotificationService NotificationService;
        public IRepositoryHost RepositoryHost;
        public IApiClient ApiClient;
        public IModelService ModelService;
    }

    static TestData PrepareTestData(
        string repoName, string sourceRepoOwner, string sourceBranchName,
        string targetRepoOwner, string targetBranchName,
        string remote,
        bool repoIsFork, bool sourceBranchIsTracking)
    {
        var serviceProvider = Substitutes.ServiceProvider;
        var gitService = serviceProvider.GetGitService();
        var gitClient = Substitute.For<IGitClient>();
        var notifications = Substitute.For<INotificationService>();
        var host = Substitute.For<IRepositoryHost>();
        var api = Substitute.For<IApiClient>();
        var ms = Substitute.For<IModelService>();

        // this is the local repo instance that is available via TeamExplorerServiceHolder and friends
        var activeRepo = Substitute.For<ILocalRepositoryModel>();
        activeRepo.LocalPath.Returns("");
        activeRepo.Name.Returns(repoName);
        activeRepo.CloneUrl.Returns(new GitHub.Primitives.UriString("http://github.com/" + sourceRepoOwner + "/" + repoName));
        activeRepo.Owner.Returns(sourceRepoOwner);

        Repository githubRepoParent = null;
        if (repoIsFork)
            githubRepoParent = CreateRepository(targetRepoOwner, repoName, id: 1);
        var githubRepo = CreateRepository(sourceRepoOwner, repoName, id: 2, parent: githubRepoParent);
        var sourceBranch = new BranchModel(sourceBranchName, activeRepo);
        var sourceRepo = new RemoteRepositoryModel(githubRepo);
        var targetRepo = targetRepoOwner == sourceRepoOwner ? sourceRepo : sourceRepo.Parent;
        var targetBranch = targetBranchName != targetRepo.DefaultBranch.Name ? new BranchModel(targetBranchName, targetRepo) : targetRepo.DefaultBranch;

        activeRepo.CurrentBranch.Returns(sourceBranch);
        serviceProvider.GetRepositoryHosts().GitHubHost.Returns(host);
        host.ApiClient.Returns(api);
        host.ModelService.Returns(ms);
        api.GetRepository(Args.String, Args.String).Returns(Observable.Return(githubRepo));

        // sets up the libgit2sharp repo and branch objects
        var l2repo = SetupLocalRepoMock(gitClient, gitService, remote, sourceBranchName, sourceBranchIsTracking);

        return new TestData
        {
            ServiceProvider = serviceProvider,
            ActiveRepo = activeRepo,
            L2Repo = l2repo,
            SourceRepo = sourceRepo,
            SourceBranch = sourceBranch,
            TargetRepo = targetRepo,
            TargetBranch = targetBranch,
            GitClient = gitClient,
            GitService = gitService,
            NotificationService = notifications,
            RepositoryHost = host,
            ApiClient = api,
            ModelService = ms
        };
    }

    [Fact]
    public void TargetBranchDisplayNameIncludesRepoOwnerWhenFork()
    {
        var data = PrepareTestData("octokit.net", "shana", "master", "octokit", "master", "origin", true, true);
        var prservice = new PullRequestService(data.GitClient, data.GitService, data.ServiceProvider.GetOperatingSystem(), Substitute.For<IUsageTracker>());
        prservice.GetPullRequestTemplate(data.ActiveRepo).Returns(Observable.Empty<string>());
        var vm = new PullRequestCreationViewModel(data.RepositoryHost, data.ActiveRepo, prservice, data.NotificationService);
        Assert.Equal("octokit/master", vm.TargetBranch.DisplayName);
    }

    [Theory]
    [InlineData(1, "repo-name", "source-repo-owner", "source-branch", true, true, "target-repo-owner", "target-branch", "title", null)]
    [InlineData(2, "repo-name", "source-repo-owner", "source-branch", true, true, "target-repo-owner", "master", "title", "description")]
    [InlineData(3, "repo-name", "source-repo-owner", "master", true, true, "target-repo-owner", "master", "title", "description")]
    [InlineData(4, "repo-name", "source-repo-owner", "source-branch", false, true, "source-repo-owner", "target-branch", "title", null)]
    [InlineData(5, "repo-name", "source-repo-owner", "source-branch", false, true, "source-repo-owner", "master", "title", "description")]
    [InlineData(6, "repo-name", "source-repo-owner", "source-branch", true, false, "target-repo-owner", "target-branch", "title", null)]
    [InlineData(7, "repo-name", "source-repo-owner", "source-branch", true, false, "target-repo-owner", "master", "title", "description")]
    [InlineData(8, "repo-name", "source-repo-owner", "master", true, false, "target-repo-owner", "master", "title", "description")]
    [InlineData(9, "repo-name", "source-repo-owner", "source-branch", false, false, "source-repo-owner", "target-branch", "title", null)]
    [InlineData(10, "repo-name", "source-repo-owner", "source-branch", false, false, "source-repo-owner", "master", "title", "description")]
    public async Task CreatingPRs(int testId,
        string repoName, string sourceRepoOwner, string sourceBranchName,
        bool repoIsFork, bool sourceBranchIsTracking,
        string targetRepoOwner, string targetBranchName,
        string title, string body)
    {
        var remote = "origin";
        var data = PrepareTestData(repoName, sourceRepoOwner, sourceBranchName, targetRepoOwner, targetBranchName, "origin",
            repoIsFork, sourceBranchIsTracking);

        var targetRepo = data.TargetRepo;
        var gitClient = data.GitClient;
        var l2repo = data.L2Repo;
        var activeRepo = data.ActiveRepo;
        var sourceBranch = data.SourceBranch;
        var targetBranch = data.TargetBranch;
        var ms = data.ModelService;

        var prservice = new PullRequestService(data.GitClient, data.GitService, data.ServiceProvider.GetOperatingSystem(), Substitute.For<IUsageTracker>());
        var vm = new PullRequestCreationViewModel(data.RepositoryHost, data.ActiveRepo, prservice, data.NotificationService);

        vm.Initialize();

        // the user has to input this
        vm.PRTitle = title;

        // this is optional
        if (body != null)
            vm.Description = body;

        // the TargetBranch property gets set to whatever the repo default is (we assume master here),
        // so we only set it manually to emulate the user selecting a different target branch
        if (targetBranchName != "master")
            vm.TargetBranch = new BranchModel(targetBranchName, targetRepo);

        await vm.CreatePullRequest.ExecuteAsync();

        var unused2 = gitClient.Received().Push(l2repo, sourceBranchName, remote);
        if (!sourceBranchIsTracking)
            unused2 = gitClient.Received().SetTrackingBranch(l2repo, sourceBranchName, remote);
        else
            unused2 = gitClient.DidNotReceiveWithAnyArgs().SetTrackingBranch(Args.LibGit2Repo, Args.String, Args.String);
        var unused = ms.Received().CreatePullRequest(activeRepo, targetRepo, sourceBranch, targetBranch, title, body ?? String.Empty);
    }

    [Fact]
    public void TemplateIsUsedIfPresent()
    {
        var data = PrepareTestData("stuff", "owner", "master", "owner", "master",
            "origin", false, true);

        var prservice = Substitute.For<IPullRequestService>();
        prservice.GetPullRequestTemplate(data.ActiveRepo).Returns(Observable.Return("Test PR template"));

        var vm = new PullRequestCreationViewModel(data.RepositoryHost, data.ActiveRepo, prservice, data.NotificationService);
        vm.Initialize();

        Assert.Equal("Test PR template", vm.Description);
    }
}
