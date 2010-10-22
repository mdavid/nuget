﻿using System;
using System.Collections.Generic;

namespace NuPack {
    public sealed class DependencyHelper : PackageWalker {
        private HashSet<IPackage> _dependencies;
        private readonly IPackageRepository _sourceRepository;

        public DependencyHelper(IPackageRepository sourceRepository) {
            if (sourceRepository == null) {
                throw new ArgumentNullException("sourceRepository");
            }
            _sourceRepository = sourceRepository;
        }

        public IEnumerable<IPackage> GetDependencies(IPackage package) {
            _dependencies = new HashSet<IPackage>();
            _dependencies.Add(package);

            Walk(package);
            
            return _dependencies;
        }

        protected override IPackage ResolveDependency(PackageDependency dependency) {
            return _sourceRepository.FindPackage(dependency);
        }

        protected override bool OnAfterResolveDependency(IPackage package, IPackage dependency) {
            _dependencies.Add(dependency);
            return base.OnAfterResolveDependency(package, dependency);
        }
    }
}