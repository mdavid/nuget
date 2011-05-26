﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EnvDTE;
using NuGet.VisualStudio;

namespace NuGet.Dialog.Providers {
    internal class SolutionOnlineProvider : OnlineProvider, IPackageOperationEventListener {
        private IVsPackageManager _activePackageManager;
        private readonly IProjectSelectorService _projectSelector;
        private readonly ISolutionManager _solutionManager;
        private static readonly Dictionary<string, bool> _checkStateCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public SolutionOnlineProvider(
            IPackageRepository localRepository,
            ResourceDictionary resources,
            IPackageRepositoryFactory packageRepositoryFactory,
            IPackageSourceProvider packageSourceProvider,
            IVsPackageManagerFactory packageManagerFactory,
            ProviderServices providerServices,
            IProgressProvider progressProvider,
            ISolutionManager solutionManager) :
            base(null,
                localRepository,
                resources,
                packageRepositoryFactory,
                packageSourceProvider,
                packageManagerFactory,
                providerServices,
                progressProvider,
                solutionManager) {
            _projectSelector = providerServices.ProjectSelector;
            _solutionManager = solutionManager;
        }

        protected override bool ExecuteAfterLicenseAgreement(
            PackageItem item,
            IVsPackageManager activePackageManager,
            IList<PackageOperation> operations) {

            _activePackageManager = activePackageManager;
            IList<Project> selectedProjectsList;

            if (activePackageManager.IsProjectLevel(item.PackageIdentity)) {
                // hide the progress window if we are going to show project selector window
                HideProgressWindow();
                var selectedProjects = _projectSelector.ShowProjectSelectorWindow(DetermineProjectCheckState, ignored => true);
                if (selectedProjects == null) {
                    // user presses Cancel button on the Solution dialog
                    return false;
                }

                selectedProjectsList = selectedProjects.ToList();
                if (selectedProjectsList.Count == 0) {
                    return false;
                }

                ShowProgressWindow();

                // save the checked state of projects so that we can restore them the next time
                SaveProjectCheckStates(selectedProjectsList);
            }
            else {
                // solution package. just install into the solution
                selectedProjectsList = new Project[0];
            }

            activePackageManager.InstallPackage(
                selectedProjectsList,
                item.PackageIdentity,
                operations,
                ignoreDependencies: false,
                logger: this,
                packageOperationEventListener: this);

            return true;
        }

        private void SaveProjectCheckStates(IList<Project> selectedProjects) {
            var selectedProjectSet = new HashSet<Project>(selectedProjects);

            foreach (Project project in _solutionManager.GetProjects()) {
                if (!String.IsNullOrEmpty(project.UniqueName)) {
                    bool checkState = selectedProjectSet.Contains(project);
                    _checkStateCache[project.UniqueName] = checkState;
                }
            }
        }

        private static bool DetermineProjectCheckState(Project project) {
            bool checkState;
            if (String.IsNullOrEmpty(project.UniqueName) ||
                !_checkStateCache.TryGetValue(project.UniqueName, out checkState)) {
                checkState = true;
            }
            return checkState;
        }

        public void OnBeforeAddPackageReference(Project project) {
            RegisterPackageOperationEvents(
                _activePackageManager,
                _activePackageManager.GetProjectManager(project));
        }

        public void OnAfterAddPackageReference(Project project) {
            UnregisterPackageOperationEvents(
                _activePackageManager,
                _activePackageManager.GetProjectManager(project));
        }

        public void OnAddPackageReferenceError(Project project, Exception exception) {
            AddFailedProject(project, exception);
        }
    }
}