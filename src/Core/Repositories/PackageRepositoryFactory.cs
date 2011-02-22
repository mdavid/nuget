using System;
using System.Globalization;
using NuGet.Resources;

namespace NuGet {
    public class PackageRepositoryFactory : IPackageRepositoryFactory {
        private static readonly PackageRepositoryFactory _default = new PackageRepositoryFactory();
        private IHttpClient _httpClient;
        private readonly IProgressReporter _progressReporter;

        public PackageRepositoryFactory() : this(new HttpClient(), NullProgressReporter.Instance) { }

        public PackageRepositoryFactory(IHttpClient httpClient, IProgressReporter progressReporter) {
            _httpClient = httpClient;
            _progressReporter = progressReporter;
        }

        public static PackageRepositoryFactory Default {
            get {
                return _default;
            }
        }

        public virtual IPackageRepository CreateRepository(PackageSource packageSource) {
            if (packageSource == null) {
                throw new ArgumentNullException("packageSource");
            }

            if (packageSource.IsAggregate) {
                throw new NotSupportedException();
            }

            Uri uri = new Uri(packageSource.Source);
            if (uri.IsFile) {
                return new LocalPackageRepository(uri.LocalPath);
            }

            try {
                uri = _httpClient.GetRedirectedUri(uri);
            }
            catch (Exception exception) {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                    NuGetResources.UnavailablePackageSource, packageSource), 
                    exception);
            }

            // Make sure we get resolve any fwlinks before creating the repository
            return new DataServicePackageRepository(uri, _httpClient, _progressReporter);
        }
    }
}
