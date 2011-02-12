﻿using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shell;
using Microsoft.Win32;
using NuGet;
using PackageExplorer.Properties;
using PackageExplorerViewModel;
using StringResources = PackageExplorer.Resources.Resources;

namespace PackageExplorer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private readonly JumpList _jumpList = new JumpList() { ShowRecentCategory = true };

        public MainWindow() {
            InitializeComponent();

            try {
                LoadSettings();
            }
            catch (Exception) { }

            BuildStatusItem.Content = "Build " + typeof(MainWindow).Assembly.GetName().Version.ToString();
        }

        internal void LoadPackage(string packagePath) {
            ZipPackage package = null;
            try {
                package = new ZipPackage(packagePath);
            }
            catch (Exception ex) {
                MessageBox.Show(
                    ex.Message, 
                    StringResources.Dialog_Title, 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                return;
            }

            if (package != null) {
                DataContext = new PackageViewModel(package, packagePath);
                AddToRecentJumpList(packagePath);
            }
        }

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e) {
            bool canceled = AskToSaveCurrentFile();
            if (canceled) {
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog() {
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = Constants.PackageExtension,
                Multiselect = false,
                ValidateNames = true,
                Filter = StringResources.Dialog_OpenFileFilter
            };

            bool? result = dialog.ShowDialog();
            if (result ?? false) {
                LoadPackage(dialog.FileName);
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
                    if (firstFile.EndsWith(Constants.PackageExtension)) {
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

                    string firstFile = filenames.FirstOrDefault(
                        f => f.EndsWith(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase));

                    if (firstFile != null) {
                        e.Handled = true;

                        bool canceled = AskToSaveCurrentFile();
                        if (!canceled) {
                            LoadPackage(firstFile);
                        }
                    }
                }
            }
        }

        #endregion

        private void Hyperlink_Click(object sender, RoutedEventArgs e) {
            var link = (Hyperlink)sender;
            UriHelper.OpenExternalLink(link.NavigateUri);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(
                StringResources.Dialog_HelpAbout,
                StringResources.Dialog_Title, 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            bool isCanceled = AskToSaveCurrentFile();
            e.Cancel = isCanceled;
        }

        /// <summary>
        /// Asks user to save the current file before doing something (e.g. exit, open a new file)
        /// </summary>
        /// <returns>true if user cancels the impending action</returns>
        private bool AskToSaveCurrentFile() {
            
            if (HasUnsavedChanges) {

                var question = String.Format(
                    CultureInfo.CurrentCulture,
                    StringResources.Dialog_SaveQuestion,
                    System.IO.Path.GetFileName(PackageSourceItem.Content.ToString()));

                // if there is unsaved changes, ask user for confirmation
                var result = MessageBox.Show(
                    question,
                    PackageExplorer.Resources.Resources.Dialog_Title,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) {
                    return true;
                }

                if (result == MessageBoxResult.Yes) {
                    var saveCommand = SaveMenuItem.Command;
                    const string parameter = "Save";
                    if (saveCommand.CanExecute(parameter)) {
                        saveCommand.Execute(parameter);
                    }
                }
            }

            return false;
        }

        private bool HasUnsavedChanges {
            get {
                var viewModel = (PackageViewModel) DataContext;
                return (viewModel != null && viewModel.HasEdit);
            }
        }

        private void AddToRecentJumpList(string path) {
            JumpList.SetJumpList(Application.Current, _jumpList);

            var jumpPath = new JumpPath { Path = path };
            JumpList.AddToRecentCategory(jumpPath);

            _jumpList.Apply();
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
            this.FontSize = size;

            // check the corresponding font size menu item 
            foreach (MenuItem child in FontSizeMenuItem.Items) {
                int value = Convert.ToInt32(child.Tag);
                child.IsChecked = value == size;
            }
        }

        private void Window_Closed(object sender, EventArgs e) {
            try {
                SaveSettings();
            }
            catch (Exception) { }
        }

        private void SaveSettings() {
            Settings settings = Properties.Settings.Default;

            settings.FontSize = (int)Math.Round(this.FontSize);
            //settings.Left = this.Left;
            //settings.Top = this.Top;
            //settings.Width = this.Width;
            //settings.Height = this.Height;
            settings.WindowState = this.WindowState.ToString();
        }

        private void LoadSettings() {
            Settings settings = Properties.Settings.Default;
            
            SetFontSize(settings.FontSize);

            //double left = settings.Left;
            //double top = settings.Top;

            //if (left > 0 && top > 0) {
            //    this.Left = left;
            //    this.Top = top;
            //}

            //double width = settings.Width;
            //double height = settings.Height;

            //if (width > 0 && height > 0) {
            //    this.Width = width;
            //    this.Height = Height;
            //}

            string windowState = settings.WindowState;
            if (!String.IsNullOrEmpty(windowState)) {
                WindowState state;
                if (Enum.TryParse<WindowState>(windowState, out state) && state == WindowState.Maximized) {
                    this.WindowState = state;
                }
            }
        }

        private void ViewFileFormatItem_Click(object sender, RoutedEventArgs e) {
            Uri uri = new Uri("http://nuget.codeplex.com/documentation?title=Creating%20a%20Package");
            UriHelper.OpenExternalLink(uri);
        }
    }
}