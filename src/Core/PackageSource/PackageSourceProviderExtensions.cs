﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet {
    public static class PackageSourceProviderExtensions {
        public static AggregateRepository GetAggregate(this IPackageSourceProvider provider, IPackageRepositoryFactory factory) {
            return GetAggregate(provider, factory, ignoreFailingRepositories: false);
        }

        public static AggregateRepository GetAggregate(this IPackageSourceProvider provider, IPackageRepositoryFactory factory, bool ignoreFailingRepositories) {
            return new AggregateRepository(factory, provider.LoadPackageSources().Select(s => s.Source), ignoreFailingRepositories);
        }

        public static IPackageRepository GetAggregate(this IPackageSourceProvider provider, IPackageRepositoryFactory factory, bool ignoreFailingRepositories, IEnumerable<string> feeds) {
            Func<string, IPackageRepository> createRepository = factory.CreateRepository;

            if (ignoreFailingRepositories) {
                createRepository = (source) => {
                    try {
                        return factory.CreateRepository(source);
                    }
                    catch (InvalidOperationException) {
                        return null;
                    }
                };
            }

            var repositories = (from item in feeds 
                                let repository = createRepository(provider.ResolveSource(item))
                                where repository != null
                                select repository).ToArray();
            return new AggregateRepository(repositories) { IgnoreFailingRepositories = ignoreFailingRepositories };
        }

        /// <summary>
        /// Resolves a package source by either Name or Source.
        /// </summary>
        public static string ResolveSource(this IPackageSourceProvider provider, string value) {
            var resolvedSource = (from source in provider.LoadPackageSources()
                                  where source.Name.Equals(value, StringComparison.CurrentCultureIgnoreCase) || source.Source.Equals(value, StringComparison.OrdinalIgnoreCase)
                                  select source.Source
                                   ).FirstOrDefault();

            return resolvedSource ?? value;
        }

        public static string GetSourceDisplayName(this IPackageSourceProvider sourceProvider, string source) {
            return (from item in sourceProvider.LoadPackageSources()
                    where source.Equals(item.Source, StringComparison.OrdinalIgnoreCase)
                    select item.Name).DefaultIfEmpty(source).FirstOrDefault();
        }
    }
}