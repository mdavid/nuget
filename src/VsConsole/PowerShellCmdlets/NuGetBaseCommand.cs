using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using EnvDTE;
using NuGet.VisualStudio;

namespace NuGet.PowerShell.Commands {
    /// <summary>
    /// This is the base class for all NuGet cmdlets.
    /// </summary>
    public abstract class NuGetBaseCommand : PSCmdlet, ILogger, IErrorHandler {
        private IVsPackageManager _packageManager;
        private readonly ISolutionManager _solutionManager;
        private readonly IVsPackageManagerFactory _vsPackageManagerFactory;
        private ProgressRecordCollection _progressRecordCache;
        private IVsProgressEvents _progressEvents;

        protected NuGetBaseCommand(ISolutionManager solutionManager, IVsPackageManagerFactory vsPackageManagerFactory, IVsProgressEvents progressEvents) {
            _solutionManager = solutionManager;
            _vsPackageManagerFactory = vsPackageManagerFactory;
            _progressEvents = progressEvents;
        }

        private ProgressRecordCollection ProgressRecordCache {
            get {
                if (_progressRecordCache == null) {
                    _progressRecordCache = new ProgressRecordCollection();
                }

                return _progressRecordCache;
            }
        }

        protected IErrorHandler ErrorHandler {
            get {
                return this;
            }
        }

        protected ISolutionManager SolutionManager {
            get {
                return _solutionManager;
            }
        }

        protected IVsPackageManagerFactory PackageManagerFactory {
            get {
                return _vsPackageManagerFactory;
            }
        }

        protected bool IsSyncMode {
            get {
                if (Host == null || Host.PrivateData == null) {
                    return false;
                }

                PSObject privateData = Host.PrivateData;
                var syncModeProp = privateData.Properties["IsSyncMode"];
                return syncModeProp != null && (bool)syncModeProp.Value;
            }
        }

        /// <summary>
        /// Gets an instance of VSPackageManager to be used throughout the execution of this command.
        /// </summary>
        /// <value>The package manager.</value>
        protected internal IVsPackageManager PackageManager {
            get {
                if (_packageManager == null) {
                    _packageManager = CreatePackageManager();
                }

                return _packageManager;
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to display friendly message to the console.")]
        protected sealed override void ProcessRecord() {
            try {
                ProcessRecordCore();
            }
            catch (Exception ex) {
                // unhandled exceptions should be terminating
                ErrorHandler.HandleException(ex, terminating: true);
            }
        }

        /// <summary>
        /// Derived classess must implement this method instead of ProcessRecord(), which is sealed by NuGetBaseCmdlet.
        /// </summary>
        protected abstract void ProcessRecordCore();

        void ILogger.Log(MessageLevel level, string message, params object[] args) {
            string formattedMessage = String.Format(CultureInfo.CurrentCulture, message, args);
            Log(level, formattedMessage);
        }

        internal void Execute() {
            BeginProcessing();
            ProcessRecord();
            EndProcessing();
        }

        protected override void BeginProcessing() {
            base.BeginProcessing();

            if (!IsSyncMode && _progressEvents != null) {
                // subscribe to the event so that VsProgressReporter doesn't automatically write progress to the status bar
                _progressEvents.ProgressAvailable += OnProgressAvailable;
            }
        }

        protected override void EndProcessing() {
            base.EndProcessing();

            if (_progressEvents != null) {
                _progressEvents.ProgressAvailable -= OnProgressAvailable;
            }
        }

        protected virtual void Log(MessageLevel level, string formattedMessage) {
            switch (level) {
                case MessageLevel.Debug:
                    WriteVerbose(formattedMessage);
                    break;

                case MessageLevel.Warning:
                    WriteWarning(formattedMessage);
                    break;

                case MessageLevel.Info:
                    WriteLine(formattedMessage);
                    break;
            }
        }

        protected virtual IVsPackageManager CreatePackageManager() {
            if (!SolutionManager.IsSolutionOpen) {
                return null;
            }

            return PackageManagerFactory.CreatePackageManager();
        }

        /// <summary>
        /// Return all projects in the solution matching the provided names. Wildcards are supported.
        /// This method will automatically generate error records for non-wildcarded project names that
        /// are not found.
        /// </summary>
        /// <param name="projectNames">An array of project names that may or may not include wildcards.</param>
        /// <returns>Projects matching the project name(s) provided.</returns>
        protected IEnumerable<Project> GetProjectsByName(string[] projectNames) {

            foreach (string projectName in projectNames) {
                // if ctrl+c hit, leave immediately
                if (Stopping) {
                    break;
                }

                // Treat every name as a wildcard; results in simpler code
                var pattern = new WildcardPattern(projectName, WildcardOptions.IgnoreCase);

                var matches =
                    (from project in _solutionManager.GetProjects()
                     where pattern.IsMatch(project.Name)
                     select project).ToList();

                // We only emit non-terminating error record if a non-wildcarded name was not found.
                // This is consistent with built-in cmdlets that support wildcarded search.
                // A search with a wildcard that returns nothing should not be considered an error.
                if ((matches.Count == 0) && !WildcardPattern.ContainsWildcardCharacters(projectName)) {
                    ErrorHandler.WriteProjectNotFoundError(projectName, terminating: false);
                }
                else {
                    foreach (Project project in matches) {
                        yield return project;
                    }
                }
            }
        }

        /// <summary>
        /// Translate a PSPath into a System.IO.* friendly Win32 path.
        /// Does not resolve/glob wildcards.
        /// </summary>                
        /// <param name="psPath">The PowerShell PSPath to translate which may reference PSDrives or have provider-qualified paths which are syntactically invalid for .NET APIs.</param>
        /// <param name="path">The translated PSPath in a format understandable to .NET APIs.</param>
        /// <param name="exists">Returns null if not tested, or a bool representing path existence.</param>
        /// <param name="errorMessage">If translation failed, contains the reason.</param>
        /// <returns>True if successfully translated, false if not.</returns>
        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "1#", Justification = "Following TryParse pattern in BCL", Target = "path")]
        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "2#", Justification = "Following TryParse pattern in BCL", Target = "exists")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "ps", Justification = "ps is a common powershell prefix")]
        protected bool TryTranslatePSPath(string psPath, out string path, out bool? exists, out string errorMessage) {
            return PSPathUtility.TryTranslatePSPath(SessionState, psPath, out path, out exists, out errorMessage);
        }

        [SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "This exception is passed to PowerShell. We really don't care about the type of exception here.")]
        protected void WriteError(string message) {
            if (!String.IsNullOrEmpty(message)) {
                WriteError(new Exception(message));
            }
        }

        protected void WriteError(Exception exception) {
            ErrorHandler.HandleException(exception, terminating: false);
        }

        void IErrorHandler.WriteProjectNotFoundError(string projectName, bool terminating) {
            var notFoundException =
                new ItemNotFoundException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        Resources.Cmdlet_ProjectNotFound, projectName));

            ErrorHandler.HandleError(
                new ErrorRecord(
                    notFoundException,
                    NuGetErrorId.ProjectNotFound, // This is your locale-agnostic error id.
                    ErrorCategory.ObjectNotFound,
                    projectName),
                    terminating: terminating);
        }

        void IErrorHandler.ThrowSolutionNotOpenTerminatingError() {
            ErrorHandler.HandleException(
                new InvalidOperationException(Resources.Cmdlet_NoSolution),
                terminating: true,
                errorId: NuGetErrorId.NoActiveSolution,
                category: ErrorCategory.InvalidOperation);
        }

        void IErrorHandler.ThrowNoCompatibleProjectsTerminatingError() {
            ErrorHandler.HandleException(
                new InvalidOperationException(Resources.Cmdlet_NoCompatibleProjects),
                terminating: true,
                errorId: NuGetErrorId.NoCompatibleProjects,
                category: ErrorCategory.InvalidOperation);
        }

        void IErrorHandler.HandleError(ErrorRecord errorRecord, bool terminating) {
            if (terminating) {
                ThrowTerminatingError(errorRecord);
            }
            else {
                WriteError(errorRecord);
            }
        }

        void IErrorHandler.HandleException(Exception exception, bool terminating,
            string errorId, ErrorCategory category, object target) {

            // Only unwrap target invocation exceptions
            if (exception is TargetInvocationException) {
                exception = exception.InnerException;
            }

            var error = new ErrorRecord(exception, errorId, category, target);

            ErrorHandler.HandleError(error, terminating: terminating);
        }

        protected void WriteLine(string message = null) {
            if (Host == null) {
                // Host is null when running unit tests. Simply return in this case
                return;
            }

            if (message == null) {
                Host.UI.WriteLine();
            }
            else {
                Host.UI.WriteLine(message);
            }
        }

        protected void WriteProgress(int activityId, string operation, int percentComplete) {
            if (IsSyncMode) {
                // don't bother to show progress if we are in synchronous mode
                return;
            }

            ProgressRecord progressRecord;

            // retrieve the ProgressRecord object for this particular activity id from the cache.
            if (ProgressRecordCache.Contains(activityId)) {
                progressRecord = ProgressRecordCache[activityId];
            }
            else {
                progressRecord = new ProgressRecord(activityId, operation, operation);
                ProgressRecordCache.Add(progressRecord);
            }

            progressRecord.CurrentOperation = operation;
            progressRecord.PercentComplete = percentComplete;

            WriteProgress(progressRecord);
        }

        private void OnProgressAvailable(object sender, ReportProgressEventArgs e) {
            WriteProgress(1, e.Operation, e.PercentComplete);
        }

        private class ProgressRecordCollection : KeyedCollection<int, ProgressRecord> {
            protected override int GetKeyForItem(ProgressRecord item) {
                return item.ActivityId;
            }
        }
    }
}