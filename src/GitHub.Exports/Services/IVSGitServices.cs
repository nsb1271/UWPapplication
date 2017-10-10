using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitHub.Models;

namespace GitHub.Services
{
    public interface IVSGitServices
    {
        string GetLocalClonePathFromGitProvider();

        /// <summary>
        /// Clones a repository via Team Explorer.
        /// </summary>
        /// <param name="cloneUrl">The URL of the repository to clone.</param>
        /// <param name="clonePath">The path to clone the repository to.</param>
        /// <param name="recurseSubmodules">Whether to recursively clone submodules.</param>
        /// <param name="progress">
        /// An object through which to report progress. This must be of type
        /// <see cref="System.IProgress{Microsoft.VisualStudio.Shell.ServiceProgressData}"/>, but
        /// as that type is only available in VS2017+ it is typed as <see cref="object"/> here.
        /// </param>
        Task Clone(
            string cloneUrl,
            string clonePath,
            bool recurseSubmodules,
            object progress = null);

        string GetActiveRepoPath();
        LibGit2Sharp.IRepository GetActiveRepo();
        IEnumerable<ILocalRepositoryModel> GetKnownRepositories();
        string SetDefaultProjectPath(string path);
    }
}