using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NuGet {
    public static class PackageRepositoryExtensions {
        public static bool Exists(this IPackageRepository repository, IPackageMetadata package) {
            return repository.Exists(package.Id, package.Version);
        }

        public static bool Exists(this IPackageRepository repository, string packageId) {
            return Exists(repository, packageId, version: null);
        }

        public static bool Exists(this IPackageRepository repository, string packageId, Version version) {
            return repository.FindPackage(packageId, version) != null;
        }

        public static bool TryFindPackage(this IPackageRepository repository, string packageId, Version version, out IPackage package) {
            package = repository.FindPackage(packageId, version);
            return package != null;
        }

        public static IPackage FindPackage(this IPackageRepository repository, string packageId) {
            return repository.FindPackage(packageId, version: null);
        }

        public static IPackage FindPackage(this IPackageRepository repository, string packageId, string versionSpec) {
            if (versionSpec == null) {
                throw new ArgumentNullException("versionSpec");
            }
            return repository.FindPackage(packageId, VersionUtility.ParseVersionSpec(versionSpec));
        }

        public static IPackage FindPackage(this IPackageRepository repository, string packageId, Version version) {
            if (repository == null) {
                throw new ArgumentNullException("repository");
            }

            if (packageId == null) {
                throw new ArgumentNullException("packageId");
            }

            IEnumerable<IPackage> packages = repository.FindPackagesById(packageId)
                                                       .AsEnumerable()
                                                       .OrderByDescending(p => p.Version);

            if (version != null) {
                packages = packages.Where(p => p.Version == version);
            }

            return packages.FirstOrDefault();
        }

        public static IPackage FindPackage(this IPackageRepository repository, string packageId, IVersionSpec versionInfo) {
            if (repository == null) {
                throw new ArgumentNullException("repository");
            }

            if (packageId == null) {
                throw new ArgumentNullException("packageId");
            }

            IEnumerable<IPackage> packages = repository.FindPackagesById(packageId)
                                                       .AsEnumerable()
                                                       .OrderByDescending(p => p.Version);

            if (versionInfo != null) {
                packages = packages.FindByVersion(versionInfo);
            }

            return packages.FirstOrDefault();
        }

        public static IPackage FindDependency(this IPackageRepository repository, PackageDependency dependency) {
            if (repository == null) {
                throw new ArgumentNullException("repository");
            }

            if (dependency == null) {
                throw new ArgumentNullException("dependency");
            }

            // When looking for dependencies, order by lowest version
            IEnumerable<IPackage> packages = repository.FindPackagesById(dependency.Id)
                                                       .AsEnumerable()
                                                       .OrderBy(p => p.Version);

            // If version info was specified then use it
            if (dependency.VersionSpec != null) {
                packages = packages.FindByVersion(dependency.VersionSpec);
            }

            return packages.FirstOrDefault();
        }

        /// <summary>
        /// Returns packages from the source repository that are later versions to any package in the current repository.
        /// </summary>
        public static IEnumerable<IPackage> GetUpdates(this IPackageRepository repository, IPackageRepository sourceRepository) {
            return GetUpdates(repository, sourceRepository, repository.GetPackages());
        }

        public static IEnumerable<IPackage> GetUpdates(this IPackageRepository repository, IPackageRepository sourceRepository, IEnumerable<IPackage> packages) {
            List<IPackage> packageList = packages.ToList();

            if (!packageList.Any()) {
                yield break;
            }

            // Filter packages by what we currently have installed
            ParameterExpression parameterExpression = Expression.Parameter(typeof(IPackageMetadata));
            Expression expressionBody = packageList.Select(package => GetCompareExpression(parameterExpression, package))
                                                .Aggregate(Expression.OrElse);

            var filterExpression = Expression.Lambda<Func<IPackage, bool>>(expressionBody, parameterExpression);

            // These are the packages that we need to look at for potential updates.
            IDictionary<string, IPackage> sourcePackages = sourceRepository.GetPackages()
                                                                           .Where(filterExpression)
                                                                           .OrderBy(p => p.Id)
                                                                           .AsEnumerable()
                                                                           .GroupBy(package => package.Id)
                                                                           .ToDictionary(package => package.Key,
                                                                                         package => package.OrderByDescending(p => p.Version).First());

            foreach (IPackage package in packageList) {
                IPackage newestAvailablePackage;
                if (sourcePackages.TryGetValue(package.Id, out newestAvailablePackage) &&
                    newestAvailablePackage.Version > package.Version) {
                    yield return newestAvailablePackage;
                }
            }
        }

        /// <summary>
        /// Builds the expression: package.Id.ToLower() == "somepackageid"
        /// </summary>
        [SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.ToLower", Justification = "This is for a linq query")]
        private static Expression GetCompareExpression(Expression parameterExpression, IPackage package) {
            // package.Id
            Expression propertyExpression = Expression.Property(parameterExpression, "Id");
            // .ToLower()
            Expression toLowerExpression = Expression.Call(propertyExpression, typeof(string).GetMethod("ToLower", Type.EmptyTypes));
            // == localPackage.Id
            return Expression.Equal(toLowerExpression, Expression.Constant(package.Id.ToLower()));
        }

        private static IQueryable<IPackage> FindPackagesById(this IPackageRepository repository, string packageId) {
            return from p in repository.GetPackages()
                   where p.Id.ToLower() == packageId.ToLower()
                   orderby p.Id
                   select p;
        }

        // HACK: We need this to avoid a partial trust issue. We need to be able to evaluate closures
        // within this class
        internal static object Eval(FieldInfo fieldInfo, object obj) {
            return fieldInfo.GetValue(obj);
        }
    }
}
