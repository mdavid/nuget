using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using NuGet.VisualStudio.Resources;

namespace NuGet.VisualStudio.Cmdlets {

    /// <summary>
    /// This command lists the available packages which are either from a package source or installed in the current solution.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Package", DefaultParameterSetName = "Default")]
    public class GetPackageCmdlet : NuGetBaseCmdlet {
        private readonly IPackageRepositoryFactory _repositoryFactory;
        private readonly IPackageSourceProvider _packageSourceProvider;
        private int _firstValue;
        private bool _firstValueSpecified;

        public GetPackageCmdlet()
            : this(ServiceLocator.GetInstance<IPackageRepositoryFactory>(),
                   ServiceLocator.GetInstance<IPackageSourceProvider>(),
                   ServiceLocator.GetInstance<ISolutionManager>(),
                   ServiceLocator.GetInstance<IVsPackageManagerFactory>()) {
        }

        public GetPackageCmdlet(IPackageRepositoryFactory repositoryFactory,
                                IPackageSourceProvider packageSourceProvider,
                                ISolutionManager solutionManager,
                                IVsPackageManagerFactory packageManagerFactory)
            : base(solutionManager, packageManagerFactory) {

            if (repositoryFactory == null) {
                throw new ArgumentNullException("repositoryFactory");
            }
            if (packageSourceProvider == null) {
                throw new ArgumentNullException("packageSourceProvider");
            }

            _repositoryFactory = repositoryFactory;
            _packageSourceProvider = packageSourceProvider;
        }

        [Parameter(Position = 0)]
        public string Filter { get; set; }

        [Parameter(Position = 1, ParameterSetName = "Remote")]
        [Alias("Online")]
        public SwitchParameter Remote { get; set; }

        [Parameter(Position = 1, ParameterSetName = "Updates")]
        public SwitchParameter Updates { get; set; }

        [Parameter(Position = 2)]
        public string Source { get; set; }

        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        public int First {
            get {
                return _firstValue;
            }
            set {
                _firstValue = value;
                _firstValueSpecified = true;
            }
        }

        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        public int Skip { get; set; }

        /// <summary>
        /// Determines if no local repositories are needed to process this command
        /// </summary>
        private bool UseRemoteSourceOnly {
            get {
                return Remote.IsPresent || (!String.IsNullOrEmpty(Source) && !Updates.IsPresent);
            }
        }

        /// <summary>
        /// Determines if a remote repository would be needed to process this command.
        /// </summary>
        private bool UseRemoteSource {
            get {
                return Remote.IsPresent || Updates.IsPresent || !String.IsNullOrEmpty(Source);
            }
        }

        protected override void ProcessRecordCore() {
            if (!UseRemoteSourceOnly && !SolutionManager.IsSolutionOpen) {
                WriteError(VsResources.Cmdlet_NoSolution);
                return;
            }

            IPackageRepository repository;
            if (UseRemoteSource) {
                repository = GetRemoteRepository();
            }
            else {
                repository = PackageManager.LocalRepository;
            }

            IEnumerable<IPackage> packages;
            if (Updates.IsPresent) {
                packages = FilterPackagesForUpdate(repository);
            }
            else {
                packages = FilterPackages(repository);
            }

            var filteredPackages = packages.AsQueryable().Skip(Skip);

            if (_firstValueSpecified) {
                filteredPackages = filteredPackages.Take(First);
            }

            WritePackages(filteredPackages);
        }

        /// <summary>
        /// Determines the remote repository to be used based on the state of the solution and the Source parameter
        /// </summary>
        private IPackageRepository GetRemoteRepository() {
            if (!String.IsNullOrEmpty(Source)) {
                // If a Source parameter is explicitly specified, use it
                return _repositoryFactory.CreateRepository(Source);
            }
            else if (SolutionManager.IsSolutionOpen) {
                // If the solution is open, retrieve the cached repository instance
                return PackageManager.SourceRepository;
            }
            else if (_packageSourceProvider.ActivePackageSource != null) {
                // No solution available. Use the repository Url to create a new repository
                return _repositoryFactory.CreateRepository(_packageSourceProvider.ActivePackageSource);
            }
            else {
                // No active source has been specified. 
                throw new InvalidOperationException(VsResources.NoActivePackageSource);
            }
        }

        protected virtual IEnumerable<IPackage> FilterPackages(IPackageRepository sourceRepository) {
            var packages = sourceRepository.GetPackages();
            if (!String.IsNullOrEmpty(Filter)) {
                packages = packages.Find(Filter.Split());
            }
            return packages.OrderBy(p => p.Id);
        }

        protected virtual IEnumerable<IPackage> FilterPackagesForUpdate(IPackageRepository sourceRepository) {
            IPackageRepository localRepository = PackageManager.LocalRepository;
            var packagesToUpdate = localRepository.GetPackages();
            if (!String.IsNullOrEmpty(Filter)) {
                packagesToUpdate = packagesToUpdate.Find(Filter.Split());
            }
            return sourceRepository.GetUpdates(packagesToUpdate);
        }

        private void WritePackages(IEnumerable<IPackage> packages) {
            if (!UseRemoteSource) {
                if (!packages.Any()) {
                    Log(MessageLevel.Info, VsResources.Cmdlet_NoPackagesInstalled);
                }
            }

            WriteObject(packages, enumerateCollection: true);
        }
    }
}