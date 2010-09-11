﻿using System;
using System.Linq;
using System.Windows;
using NuPack.Dialog.ToolsOptionsUI;
using NuPack.VisualStudio;

namespace NuPack.Dialog.Providers {
    /// <summary>
    /// IVsExtensionsProvider implementation responsible for gathering
    /// a list of extensions from the extension repository
    /// which will be shown in the Add NuPack dialog.
    /// </summary>
    internal class InstalledPackagesProvider : OnlinePackagesProvider {
        public InstalledPackagesProvider(ResourceDictionary resources)
            : base(resources, false) {
        }

        public override string Name {
            get {
                return "Installed";
            }
        }

        protected override IPackageRepository PackagesRepository {
            get {
                if (_packagesRepository == null) {
                    _feed = Settings.RepositoryServiceUri;

                    if (String.IsNullOrEmpty(_feed)) {
                        return EmptyPackageRepository.Default;
                    }

                    _dte = NuPackPackage.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                    _project = GetActiveProject(_dte);

                    _vsPackageManager = VSPackageManager.GetPackageManager(_feed, _dte);
                    _vsProjectManager = _vsPackageManager.GetProjectManager(_project);

                    _packagesRepository = _vsPackageManager.ExternalRepository;
                }
                return _packagesRepository;
            }
        }

        public override IQueryable<Package> GetQuery() {
            if (String.IsNullOrEmpty(_feed)) {
                return Enumerable.Empty<Package>().AsQueryable();
            }
            return _vsProjectManager.GetPackageReferences().AsQueryable();
        }

    }
}
