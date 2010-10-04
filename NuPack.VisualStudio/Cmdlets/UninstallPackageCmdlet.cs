﻿using System;
using System.Globalization;
using System.Management.Automation;

namespace NuPack.VisualStudio.Cmdlets {

    [Cmdlet(VerbsLifecycle.Uninstall, "Package")]
    public class UninstallPackageCmdlet : ProcessPackageBaseCmdlet {
        private const string ErrorId = "Uninstall-Package";

        #region Parameters

        [Parameter(Position = 2)]
        public SwitchParameter Force { get; set; }

        [Parameter(Position = 3)]
        public SwitchParameter RemoveDependencies { get; set; }

        #endregion

        protected override void ProcessRecordCore() {
            if (!IsSolutionOpen) {
                WriteError("There is no active solution.", ErrorId);
                return;
            }

            var packageManager = PackageManager;
            
            bool isSolutionLevel = IsSolutionOnlyPackage(packageManager.LocalRepository, Id);
            if (isSolutionLevel) {
                if (!String.IsNullOrEmpty(Project)) {
                    WriteError(
                        String.Format(CultureInfo.CurrentCulture, "The package '{0}' only applies to the solution and not to a project. Remove the -Project parameter.", Id),
                        "Remove-Package");
                }
                else {
                    packageManager.UninstallPackage(Id, null, Force.IsPresent, RemoveDependencies.IsPresent);
                }
            }
            else {
                var projectManager = ProjectManager;
                if (projectManager != null) {
                    projectManager.RemovePackageReference(Id, Force.IsPresent, RemoveDependencies.IsPresent);
                }
                else {
                    WriteError("Missing project parameter or invalid project name.", ErrorId);
                }
            }
        }
    }
}
