﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet;
using PackageExplorerViewModel.Types;

namespace PackageExplorerViewModel {

    [Export(typeof(IPackageViewModelFactory))]
    public class PackageViewModelFactory : IPackageViewModelFactory {

        [Import]
        public IMruManager MruManager {
            get;
            set;
        }

        [Import]
        public IMruPackageSourceManager MruPackageSourceManager {
            get;
            set;
        }

        [Import]
        public IUIServices UIServices {
            get;
            set;
        }

        [Import]
        public Lazy<IPackageEditorService> EditorService {
            get;
            set;
        }

        [Import]
        public ISettingsManager SettingsManager {
            get;
            set;
        }

        [Import(typeof(IProxyService))]
        public Lazy<IProxyService> ProxyService {
            get;
            set;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        [ImportMany]
        public IEnumerable<Lazy<IPackageContentViewer, IPackageContentViewerMetadata>> ContentViewerMetadata { get; set; }

        public PackageViewModel CreateViewModel(NuGet.IPackage package, string packageSource) {
            return new PackageViewModel(
                package, 
                packageSource, 
                MruManager, 
                UIServices, 
                EditorService.Value, 
                SettingsManager, 
                ProxyService.Value,
                ContentViewerMetadata.ToList());
        }

        public PackageChooserViewModel CreatePackageChooserViewModel() {
            var model = new PackageChooserViewModel(MruPackageSourceManager, ProxyService.Value, SettingsManager.ShowLatestVersionOfPackage);
            model.PropertyChanged += OnPackageChooserViewModelPropertyChanged;
            return model;
        }

        private void OnPackageChooserViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "ShowLatestVersion") {
                var model = (PackageChooserViewModel)sender;
                SettingsManager.ShowLatestVersionOfPackage = model.ShowLatestVersion;
            }
        }
    }
}