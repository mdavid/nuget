﻿using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NuGet;
using PackageExplorer.Properties;
using PackageExplorerViewModel;
using PackageExplorerViewModel.Types;
using StringResources = PackageExplorer.Resources.Resources;

namespace PackageExplorer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [Export]
    public partial class MainWindow : Window {

        private readonly IMruManager _mruManager;
        private readonly IPackageViewModelFactory _packageViewModelFactory;

        [Import]
        public IMessageBox MessageBoxServices {
            get;
            set;
        }

        [Import]
        public ISettingsManager SettingsManager {
            get;
            set;
        }

        [Import]
        public IMruPackageSourceManager PackageSourceManager { get; set; }

        [Import]
        public IUIServices UIServices { get; set; }

        [ImportingConstructor]
        public MainWindow(IMruManager mruManager, IPackageViewModelFactory packageViewModelFactory) {
            InitializeComponent();
            
            RecentFilesMenuItem.DataContext = _mruManager = mruManager;
            RecentFilesContainer.Collection = _mruManager.Files;

            _packageViewModelFactory = packageViewModelFactory;
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);

            try {
                LoadSettings();
            }
            catch (Exception) { }
        }

        internal void OpenLocalPackage(string packagePath) {
            if (!File.Exists(packagePath)) {
                MessageBoxServices.Show("File not found at " + packagePath, MessageLevel.Error);
                return;
            }
            PackageSourceItem.SetCurrentValue(ContentControl.ContentProperty, "Loading " + packagePath + "...");
            Dispatcher.BeginInvoke(new Action<string>(OpenLocalPackageCore), DispatcherPriority.Loaded, packagePath);
        }

        private void OpenLocalPackageCore(string packagePath) {
            IPackage package = null;

            try {
                string extension = Path.GetExtension(packagePath);
                if (extension.Equals(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase)) {
                    package = new ZipPackage(packagePath);
                }
                else if (extension.Equals(Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase)) {
                    PackageBuilder builder = new PackageBuilder(packagePath);
                    package = builder.Build();
                }
            }
            catch (Exception ex) {
                MessageBoxServices.Show(ex.Message, MessageLevel.Error);
                return;
            }

            if (package != null) {
                LoadPackage(package, packagePath, PackageType.LocalPackage);
            }
        }

        private void LoadPackage(IPackage package, string packagePath, PackageType packageType) {
            if (package != null) {

                if (!RootLayout.LastChildFill) {
                    var packageViewer = new PackageViewer(MessageBoxServices, _packageViewModelFactory);
                    Grid.SetRow(packageViewer, 1);
                    RootLayout.Children.Add(packageViewer);
                    RootLayout.LastChildFill = true;
                }

                DataContext = _packageViewModelFactory.CreateViewModel(package, packagePath);
                if (!String.IsNullOrEmpty(packagePath)) {
                    _mruManager.NotifyFileAdded(package, packagePath, packageType);
                }
            }
        }

        private void NewMenuItem_Click(object sender, RoutedEventArgs e) {
            bool canceled = AskToSaveCurrentFile();
            if (canceled) {
                return;
            }

            LoadPackage(new EmptyPackage(), String.Empty, PackageType.LocalPackage);
        }

        private void OpenMenuItem_Click(object sender, ExecutedRoutedEventArgs e) {
            string parameter = (string)e.Parameter;
            if (parameter == "Feed") {
                OpenPackageFromNuGetFeed();
            }
            else {
                OpenPackageFromLocal();
            }
        }

        private void OpenPackageFromLocal() {
            bool canceled = AskToSaveCurrentFile();
            if (canceled) {
                return;
            }

            string selectedFile;
            bool result = UIServices.OpenFileDialog(
                "Select File",
                StringResources.Dialog_OpenFileFilter,
                out selectedFile);

            if (result) {
                OpenLocalPackage(selectedFile);
            }
        }

        private void OpenPackageFromNuGetFeed() {
            if (!NetworkInterface.GetIsNetworkAvailable()) {
                MessageBoxServices.Show(
                    PackageExplorer.Resources.Resources.NoNetworkConnection,
                    MessageLevel.Warning);
                return;
            }

            bool canceled = AskToSaveCurrentFile();
            if (canceled) {
                return;
            }

            var dialog = new PackageChooserDialog() {
                Owner = this,
                DataContext = _packageViewModelFactory.CreatePackageChooserViewModel()
            };

            bool? result = dialog.ShowDialog();
            if (result ?? false) {
                var selectedPackage = dialog.SelectedPackage;
                if (selectedPackage != null) {
                    IPackage cachePackage = MachineCache.Default.FindPackage(selectedPackage.Id, ((IPackage)selectedPackage).Version); ;
                    if (cachePackage == null || cachePackage.GetHash() != selectedPackage.PackageHash) {
                        // if not in the cache, or if the cache package's hash is different from the feed hash, (re)download it
                        var progressWindow = new DownloadProgressWindow(
                            selectedPackage.DownloadUrl,
                            selectedPackage.Id,
                            ((IPackage)selectedPackage).Version) {
                                Owner = this
                            };

                        result = progressWindow.ShowDialog();
                        if (result ?? false) {
                            cachePackage = progressWindow.DownloadedPackage;
                        }
                    }

                    if (cachePackage != null) {
                        selectedPackage.CorePackage = cachePackage;
                        LoadPackage(selectedPackage, selectedPackage.DownloadUrl.ToString(), PackageType.DataServicePackage);
                    }
                }
            }
        }

        #region Drag & drop

        private void Window_DragOver(object sender, DragEventArgs e) {
            var data = e.Data;
            if (data.GetDataPresent(DataFormats.FileDrop)) {
                object value = data.GetData(DataFormats.FileDrop);
                string[] filenames = value as string[];
                if (filenames != null && filenames.Length > 0) {
                    string firstFile = filenames[0];
                    if (FileUtility.IsSupportedFile(firstFile)) {
                        e.Effects = DragDropEffects.Copy;
                        e.Handled = true;
                        return;
                    }
                }
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            var data = e.Data;
            if (data.GetDataPresent(DataFormats.FileDrop)) {
                object value = data.GetData(DataFormats.FileDrop);
                string[] filenames = value as string[];
                if (filenames != null && filenames.Length > 0) {
                    string firstFile = filenames.FirstOrDefault(f => FileUtility.IsSupportedFile(f));
                    if (firstFile != null) {
                        e.Handled = true;

                        bool canceled = AskToSaveCurrentFile();
                        if (!canceled) {
                            OpenLocalPackage(firstFile);
                        }
                    }
                }
            }
        }

        #endregion

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e) {
            var dialog = new AboutWindow() { Owner = this };
            dialog.ShowDialog();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            bool isCanceled = AskToSaveCurrentFile();
            e.Cancel = isCanceled;

            if (!isCanceled) {
                try {
                    SaveSettings();
                    _mruManager.OnApplicationExit();
                    PackageSourceManager.OnApplicationExit();
                }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Asks user to save the current file before doing something (e.g. exit, open a new file)
        /// </summary>
        /// <returns>true if user cancels the impending action</returns>
        private bool AskToSaveCurrentFile() {
            if (HasUnsavedChanges) {

                // if there is unsaved changes, ask user for confirmation
                var result = MessageBoxServices.ConfirmWithCancel(StringResources.Dialog_SaveQuestion);
                if (result == null) {
                    return true;
                }

                if (result == true) {
                    var saveCommand = SaveMenuItem.Command;
                    const string parameter = "ForceSave";
                    saveCommand.Execute(parameter);
                }
            }

            return false;
        }

        private bool HasUnsavedChanges {
            get {
                var viewModel = (PackageViewModel)DataContext;
                return (viewModel != null && viewModel.HasEdit);
            }
        }

        private void OnFontSizeItem_Click(object sender, RoutedEventArgs e) {
            var item = (MenuItem)sender;
            int size = Convert.ToInt32(item.Tag);
            SetFontSize(size);
        }

        private void SetFontSize(int size) {
            if (size <= 8 || size >= 50) {
                size = 12;
            }
            Properties.Settings.Default.FontSize = size;

            // check the corresponding font size menu item 
            foreach (MenuItem child in FontSizeMenuItem.Items) {
                int value = Convert.ToInt32(child.Tag);
                child.IsChecked = value == size;
            }
        }

        private void LoadSettings() {
            Settings settings = Properties.Settings.Default;
            SetFontSize(settings.FontSize);
            this.LoadWindowPlacementFromSettings(settings.WindowPlacement);
        }

        private void SaveSettings() {
            Settings settings = Properties.Settings.Default;
            settings.WindowPlacement = this.SaveWindowPlacementToSettings();
        }

        private void OpenExternalLink(object sender, ExecutedRoutedEventArgs e) {
            if (e.Command != NavigationCommands.GoToPage) {
                return;
            }

            Uri uri = e.Parameter as Uri;
            if (uri == null) {
                string url = (string)e.Parameter;
                Uri.TryCreate(url, UriKind.Absolute, out uri);
            }

            if (uri != null) {
                UriHelper.OpenExternalLink(uri);
            }
        }

        private void OnPublishButtonClick(object sender, RoutedEventArgs e) {
            if (!NetworkInterface.GetIsNetworkAvailable()) {
                MessageBoxServices.Show(
                    PackageExplorer.Resources.Resources.NoNetworkConnection,
                    MessageLevel.Warning);
                return;
            }

            var viewModel = (PackageViewModel)DataContext;

            if (!viewModel.IsValid) {
                MessageBoxServices.Show(
                    PackageExplorer.Resources.Resources.PackageHasNoFile,
                    MessageLevel.Warning);
                return;
            }

            string storedKey = SettingsManager.ReadApiKeyFromSettingFile();
            var publishPackageViewModel = new PublishPackageViewModel(viewModel) {
                PublishKey = storedKey
            };

            var dialog = new PublishPackageWindow { Owner = this };
            dialog.DataContext = publishPackageViewModel;
            dialog.ShowDialog();

            string newKey = publishPackageViewModel.PublishKey;
            if (!String.IsNullOrEmpty(newKey)) {
                SettingsManager.WriteApiKeyToSettingFile(newKey);
            }
        }

        private void CanPublishToFeedCommand(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e) {
            var model = DataContext as PackageViewModel;
            bool canExecute = model == null ? false : !model.IsInEditMode;

            e.CanExecute = canExecute;
            e.Handled = true;
        }

        private void CloseMenuItem_Click(object sender, ExecutedRoutedEventArgs e) {
            bool isCanceled = AskToSaveCurrentFile();
            if (isCanceled) {
                return;
            }

            DataContext = null;
        }

        private void CanExecuteCloseCommand(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = DataContext != null;
            e.Handled = true;
        }

        private string _folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        private void OnExportMenuItem_Click(object sender, RoutedEventArgs e) {
            BrowseForFolder dialog = new BrowseForFolder();
            string rootPath = dialog.SelectFolder(
                "Choose a folder to export package to:",
                _folderPath,
                new System.Windows.Interop.WindowInteropHelper(this).Handle);

            if (!String.IsNullOrEmpty(rootPath)) {
                var model = (PackageViewModel)DataContext;
                if (model != null) {
                    try {
                        model.Export(rootPath);
                        MessageBoxServices.Show("The package has been exported successfully.", MessageLevel.Information);
                    }
                    catch (Exception ex) {
                        MessageBoxServices.Show(ex.Message, MessageLevel.Error);
                    }
                }

                _folderPath = rootPath;
            }

            e.Handled = true;
        }

        private void RecentFileMenuItem_Click(object sender, RoutedEventArgs e) {
            bool canceled = AskToSaveCurrentFile();
            if (canceled) {
                return;
            }

            MenuItem menuItem = (MenuItem)sender;
            var mruItem = menuItem.DataContext as MruItem;
            if (mruItem == null) {
                _mruManager.Clear();
            }
            else {
                if (mruItem.PackageType == PackageType.LocalPackage) {
                    OpenLocalPackage(mruItem.Path);
                }
                else {
                    DownloadAndOpenDataServicePackage(mruItem);
                }
            }
        }

        private void DownloadAndOpenDataServicePackage(MruItem item) {
            if (!NetworkInterface.GetIsNetworkAvailable()) {
                MessageBoxServices.Show(
                    PackageExplorer.Resources.Resources.NoNetworkConnection,
                    MessageLevel.Warning);
                return;
            }

            Uri downloadUrl;
            if (Uri.TryCreate(item.Path, UriKind.Absolute, out downloadUrl)) {
                var progressWindow = new DownloadProgressWindow(downloadUrl, item.Id, item.Version) {
                    Owner = this
                };
                var result = progressWindow.ShowDialog();
                if (result ?? false) {
                    LoadPackage(progressWindow.DownloadedPackage, item.Path, PackageType.DataServicePackage);
                }
            }
        }
    }
}