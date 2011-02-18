﻿using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using NuGet;
using PackageExplorerViewModel;

namespace PackageExplorer {
    /// <summary>
    /// Interaction logic for PackageMetadataEditor.xaml
    /// </summary>
    public partial class PackageMetadataEditor : UserControl {

        private ObservableCollection<PackageDependency> _packageDependencies;
        private EditablePackageDependency _newPackageDependency;

        public PackageMetadataEditor() {
            InitializeComponent();
            PopulateLanguagesForLanguageBox();
        }

        private void PackageMetadataEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (this.Visibility == System.Windows.Visibility.Visible) {
                ClearDependencyTextBox();
                PrepareBindingForDependencyList();
            }
        }

        private void PrepareBindingForDependencyList() {
            var viewModel = (PackageViewModel)DataContext;
            _packageDependencies = new ObservableCollection<PackageDependency>(viewModel.PackageMetadata.Dependencies);
            DependencyList.ItemsSource = _packageDependencies;
        }

        private void ClearDependencyTextBox() {
            _newPackageDependency = new EditablePackageDependency();
            NewDependencyId.DataContext = NewDependencyVersion.DataContext = _newPackageDependency;
        }

        private void PopulateLanguagesForLanguageBox() {
            LanguageBox.ItemsSource = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(c => c.Name).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        }

        public BindingGroup MetadataBindingGroup {
            get { return PackageMetadataGroup; }
        }

        private void RemoveDependencyButtonClicked(object sender, System.Windows.RoutedEventArgs e) {
            var button = (Button)sender;
            var item = (PackageDependency)button.DataContext;

            _packageDependencies.Remove(item);
        }

        private void AddDependencyButtonClicked(object sender, System.Windows.RoutedEventArgs e) {

            var bindingExpression = NewDependencyId.GetBindingExpression(TextBox.TextProperty);
            if (bindingExpression.HasError) {
                return;
            }

            var bindingExpression2 = NewDependencyVersion.GetBindingExpression(TextBox.TextProperty);
            if (bindingExpression2.HasError) {
                return;
            }

            _packageDependencies.Add(_newPackageDependency.AsReadOnly());

            // after dependency is added, clear the textbox
            ClearDependencyTextBox();
        }

        private void SelectDependencyButtonClicked(object sender, System.Windows.RoutedEventArgs e) {
            var dialog = new PackageChooserDialog() { Owner = Window.GetWindow(this) };
            var result = dialog.ShowDialog();
            if (result ?? false) {
                var selectedPackage = dialog.SelectedPackage;
                if (selectedPackage != null) {

                    _newPackageDependency.Id = selectedPackage.Id;
                    _newPackageDependency.VersionSpec = VersionUtility.ParseVersionSpec(selectedPackage.Version.ToString());
                }
            }
        }

        private void OkButtonClicked(object sender, RoutedEventArgs e) {
            bool commited = PackageMetadataGroup.CommitEdit();
            if (commited) {
                var viewModel = (PackageViewModel)DataContext;
                _packageDependencies.CopyTo(viewModel.PackageMetadata.Dependencies);
            }
        }
    }
}