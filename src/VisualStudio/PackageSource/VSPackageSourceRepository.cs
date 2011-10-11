using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.VisualStudio.Resources;

namespace NuGet.VisualStudio
{
    [Export(typeof(IPackageRepository))]
    public class VsPackageSourceRepository : IPackageRepository, ISearchableRepository, ICloneableRepository
    {
        private readonly IVsPackageSourceProvider _packageSourceProvider;
        private readonly IPackageRepositoryFactory _repositoryFactory;

        [ImportingConstructor]
        public VsPackageSourceRepository(IPackageRepositoryFactory repositoryFactory,
                                         IVsPackageSourceProvider packageSourceProvider)
        {
            if (repositoryFactory == null)
            {
                throw new ArgumentNullException("repositoryFactory");
            }

            if (packageSourceProvider == null)
            {
                throw new ArgumentNullException("packageSourceProvider");
            }
            _repositoryFactory = repositoryFactory;
            _packageSourceProvider = packageSourceProvider;
        }

        public string Source
        {
            get
            {
                return ActiveRepository.Source;
            }
        }

        public bool SupportsPrereleasePackages
        {
            get
            {
                return ActiveRepository.SupportsPrereleasePackages;
            }
        }

        internal IPackageRepository ActiveRepository
        {
            get
            {
                if (_packageSourceProvider.ActivePackageSource == null)
                {
                    throw new InvalidOperationException(VsResources.NoActivePackageSource);
                }
                return _repositoryFactory.CreateRepository(_packageSourceProvider.ActivePackageSource.Source);
            }
        }

        public IQueryable<IPackage> GetPackages()
        {
            return ActiveRepository.GetPackages();
        }

        public IPackage FindPackage(string packageId, SemanticVersion version)
        {
            return ActiveRepository.FindPackage(packageId, version);
        }

        public void AddPackage(IPackage package)
        {
            ActiveRepository.AddPackage(package);
        }

        public void RemovePackage(IPackage package)
        {
            ActiveRepository.RemovePackage(package);
        }

        public IQueryable<IPackage> Search(string searchTerm, IEnumerable<string> targetFrameworks, bool allowPrereleaseVersions)
        {
            return ActiveRepository.Search(searchTerm, targetFrameworks, allowPrereleaseVersions);
        }

        public IPackageRepository Clone()
        {
            return ActiveRepository.Clone();
        }
    }
}
