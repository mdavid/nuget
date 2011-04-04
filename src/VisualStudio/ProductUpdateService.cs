﻿using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows.Automation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ExtensionManager;
using Microsoft.VisualStudio.ExtensionManager.UI;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;
using Thread = System.Threading.Thread;

namespace NuGet.VisualStudio {
    [Export(typeof(IProductUpdateService))]
    internal class ProductUpdateService : IProductUpdateService {
        private static readonly object _showUpdatesLock = new object();
        private const string NuGetVSIXId = "NuPackToolsVsix.Microsoft.67e54e40-0ae3-42c5-a949-fddf5739e7a5";
        private readonly IVsExtensionRepository _extensionRepository;
        private readonly IVsUIShell _vsUIShell;

        private bool _updateDeclined;
        private bool _updateAccepted;

        public ProductUpdateService() :
            this(ServiceLocator.GetGlobalService<SVsExtensionRepository, IVsExtensionRepository>(),
                 ServiceLocator.GetGlobalService<SVsUIShell, IVsUIShell>()) {
        }

        public ProductUpdateService(IVsExtensionRepository extensionRepository, IVsUIShell vsUIShell) {
            _extensionRepository = extensionRepository;
            _vsUIShell = vsUIShell;
        }

        public event EventHandler<ProductUpdateAvailableEventArgs> UpdateAvailable;

        public void CheckForAvailableUpdateAsync() {
            // If the user isn't admin then they can't perform update check.
            if (_updateDeclined || _updateAccepted) {
                return;
            }

            Task.Factory.StartNew(() => {
                try {
                    Thread.Sleep(2000);
                    RaiseUpdateEvent(new ProductUpdateAvailableEventArgs(new Version("1.3"), new Version("1.4")));

                    //// Find the vsix on the vs gallery
                    //VSGalleryEntry nugetVsix = _extensionRepository.CreateQuery<VSGalleryEntry>()
                    //                                          .Where(e => e.VsixID == NuGetVSIXId)
                    //                                          .AsEnumerable()
                    //                                          .FirstOrDefault();
                    //// Get the current NuGet version
                    //Version version = typeof(ProductUpdateService).Assembly.GetName().Version;

                    //// If we're running an older version then update
                    //if (nugetVsix != null && nugetVsix.NonNullVsixVersion > version) {
                    //    RaiseUpdateEvent(new ProductUpdateAvailableEventArgs(version, nugetVsix.NonNullVsixVersion));
                    //}
                }
                catch {
                    // Swallow all exceptions. We don't want to take down vs, if the VS extension
                    // gallery happens to be down.
                }
            });
        }

        private void RaiseUpdateEvent(ProductUpdateAvailableEventArgs args) {
            EventHandler<ProductUpdateAvailableEventArgs> handler = UpdateAvailable;
            if (handler != null) {
                handler(this, args);
            }
        }

        public void Update() {
            if (_updateDeclined) {
                return;
            }

            _updateAccepted = true;

            ShowUpdatesTabInExtensionManager();
        }

        public void DeclineUpdate() {
            _updateDeclined = true;
        }

        private void ShowUpdatesTabInExtensionManager() {
            Task.Factory.StartNew(delegate {
                lock (_showUpdatesLock) {
                    try {
                        AutomationElement extensionManagerWindow = GetExtensionManagerWindow();
                        if (extensionManagerWindow == null) {

                            Guid pguidCmdGroup = VSConstants.VsStd2010;
                            object pvaIn = null;
                            _vsUIShell.PostExecCommand(ref pguidCmdGroup, 0xbb8, 0, ref pvaIn);
                        }
                        while (extensionManagerWindow == null) {
                            extensionManagerWindow = GetExtensionManagerWindow();
                            Thread.Sleep(100);
                        }
                        AutomationElement element2 = FindUpdateProviderTab(extensionManagerWindow);
                        if (element2 != null) {
                            (element2.GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern).Select();
                        }
                    }
                    catch (Exception) {
                        if (System.Diagnostics.Debugger.IsAttached) {
                            System.Diagnostics.Debugger.Break();
                        }
                    }
                }
            });
        }

        private AutomationElement GetExtensionManagerWindow() {
            return AutomationElement.FromHandle(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle).
                FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "ExtensionManagerDialog"));
        }

        private AutomationElement FindUpdateProviderTab(AutomationElement extensionManagerWindow) {
            AutomationElement element = extensionManagerWindow.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ProvidersUid"));
            if (element == null) {
                return null;
            }

            return element.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).
                Cast<AutomationElement>().
                FirstOrDefault<AutomationElement>(delegate(AutomationElement x) {
                    return x.Current.AutomationId.StartsWith("Updates");
                });
        }
    }
}