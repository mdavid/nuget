﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;

namespace NuGet.VisualStudio.Cmdlets {
    [Cmdlet(VerbsCommon.Find, "Package", DefaultParameterSetName = "Default")]
    public class FindPackage : GetPackageCmdlet {

        public FindPackage()
            : this(ServiceLocator.GetInstance<IPackageRepositoryFactory>(),
                   ServiceLocator.GetInstance<IPackageSourceProvider>(),
                   ServiceLocator.GetInstance<ISolutionManager>(),
                   ServiceLocator.GetInstance<IVsPackageManagerFactory>()) {
        }

        public FindPackage(IPackageRepositoryFactory repositoryFactory,
                                IPackageSourceProvider packageSourceProvider,
                                ISolutionManager solutionManager,
                                IVsPackageManagerFactory packageManagerFactory)
            : base(repositoryFactory, packageSourceProvider, solutionManager, packageManagerFactory) {
        }

        protected override IEnumerable<IPackage> FilterPackages(IPackageRepository sourceRepository) {
            var packages = sourceRepository.GetPackages();
            if (!String.IsNullOrEmpty(Filter)) {
                packages = packages.Where(p => p.Id.ToLower().StartsWith(Filter.ToLower()));
            }
            return packages.OrderBy(p => p.Id);
        }

        protected override IEnumerable<IPackage> FilterPackagesForUpdate(IPackageRepository sourceRepository) {
            IPackageRepository localRepository = PackageManager.LocalRepository;
            var packagesToUpdate = localRepository.GetPackages();
            if (!String.IsNullOrEmpty(Filter)) {
                packagesToUpdate = packagesToUpdate.Where(p => p.Id.ToLower().StartsWith(Filter.ToLower()));
            }
            return localRepository.GetUpdates(sourceRepository, packagesToUpdate);
        }
    }
}
