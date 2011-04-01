﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using NuGet;
using PackageExplorerViewModel.Types;

namespace PackageExplorerViewModel {
    internal class SavePackageCommand : CommandBase, ICommand {

        private const string SaveAction = "Save";
        private const string SaveAsAction = "SaveAs";
        private const string ForceSaveAction = "ForceSave";

        public SavePackageCommand(PackageViewModel model)
            : base(model) {
            model.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName.Equals("IsInEditMode")) {
                if (CanExecuteChanged != null) {
                    CanExecuteChanged(this, EventArgs.Empty);
                }
            }
        }

        public bool CanExecute(object parameter) {
            string action = parameter as string;
            if (action == ForceSaveAction) {
                return true;
            }
            else if (action == SaveAction) {
                return !ViewModel.IsInEditMode && 
                    Path.IsPathRooted(ViewModel.PackageSource) && 
                    Path.GetExtension(ViewModel.PackageSource).Equals(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase);
            }
            else if (action == SaveAsAction) {
                return !ViewModel.IsInEditMode;
            }
            else {
                return false;
            }
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter) {
            if (!ViewModel.IsValid) {
                ViewModel.MessageBox.Show(Resources.PackageHasNoFile, MessageLevel.Warning);
                return;
            }

            string action = parameter as string;
            if (action == SaveAction || action == ForceSaveAction) {
                if (String.IsNullOrEmpty(ViewModel.PackageSource)) {
                    SaveAs();
                }
                else {
                    Save();
                }
            }
            else if (action == SaveAsAction) {
                SaveAs();
            }
        }

        private void Save() {
            SavePackage(ViewModel.PackageSource);
            RaiseCanExecuteChangedEvent();
        }

        private void SaveAs() {
            string packageName = ViewModel.PackageMetadata.ToString() + Constants.PackageExtension;
            string selectedPackagePath;
            if (ViewModel.OpenSaveFileDialog(packageName, true, out selectedPackagePath)) {
                SavePackage(selectedPackagePath);
                ViewModel.PackageSource = selectedPackagePath;
            }
            RaiseCanExecuteChangedEvent();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void SavePackage(string fileName) {
            try {
                PackageHelper.SavePackage(ViewModel.PackageMetadata, ViewModel.GetFiles(), fileName, true);
                ViewModel.OnSaved(fileName);
            }
            catch (Exception ex) {
                ViewModel.MessageBox.Show(ex.Message, MessageLevel.Error);
            }
        }

        private void RaiseCanExecuteChangedEvent() {
            if (CanExecuteChanged != null) {
                CanExecuteChanged(this, EventArgs.Empty);
            }
        }
    }
}