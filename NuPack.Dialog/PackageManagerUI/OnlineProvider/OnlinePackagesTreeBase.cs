using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.ExtensionsExplorer;

namespace NuPack.Dialog.Providers {
    internal abstract class OnlinePackagesTreeBase : IVsExtensionsTreeNode, IVsPageDataSource, IVsProgressPaneConsumer, INotifyPropertyChanged, IVsMessagePaneConsumer {

        private delegate void ExecuteDelegate(int pageNumber, int itemsPerPage, AsyncOperation async);

        // The number of extensions to show per page.
        private const int ItemsPerPage = 10;

        private IList<IVsExtension> _extensions;
        private IList<IVsExtensionsTreeNode> _nodes;
        private int _totalPages = 1, _currentPage = 1;
        private bool _progressPaneActive;
        private bool _isExpanded;
        private bool _isSelected;
        private bool _activeQueryStateCancelled;
        private bool _loadingInProgress;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<EventArgs> PageDataChanged;

        internal OnlinePackagesTreeBase(IVsExtensionsTreeNode parent, OnlinePackagesProvider provider) {
            Debug.Assert(provider != null);

            Parent = parent;
            Provider = provider;
        }

        protected OnlinePackagesProvider Provider {
            get;
            private set;
        }

        private IVsProgressPane ProgressPane {
            get;
            set;
        }

        private IVsMessagePane MessagePane {
            get;
            set;
        }

        /// <summary>
        /// Name of this node
        /// </summary>
        public abstract string Name {
            get;
        }

        public bool IsSearchResultsNode {
            get;
            set;
        }

        /// <summary>
        /// Select node (UI) property
        /// This property maps to TreeViewItem.IsSelected
        /// </summary>
        public bool IsSelected {
            get {
                return _isSelected;
            }
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    OnNotifyPropertyChanged("IsSelected");
                }
            }
        }

        /// <summary>
        /// Expand node (UI) property
        /// This property maps to TreeViewItem.IsExpanded
        /// </summary>
        public bool IsExpanded {
            get {
                return _isExpanded;
            }
            set {
                if (_isExpanded != value) {
                    _isExpanded = value;
                    OnNotifyPropertyChanged("IsExpanded");
                }
            }
        }

        /// <summary>
        /// List of templates at this node
        /// </summary>
        public IList<IVsExtension> Extensions {
            get {
                if (_extensions == null) {
                    _extensions = new ObservableCollection<IVsExtension>();
                    LoadPage(1);
                }
                else if (_activeQueryStateCancelled) {
                    LoadPage(CurrentPage);
                }
                return _extensions;
            }
        }

        /// <summary>
        /// Children at this node
        /// </summary>
        public IList<IVsExtensionsTreeNode> Nodes {
            get {
                if (_nodes == null) {
                    _nodes = new ObservableCollection<IVsExtensionsTreeNode>();
                    FillNodes(_nodes);
                }
                return _nodes;
            }
        }
        /// <summary>
        /// Parent of this node
        /// </summary>
        public IVsExtensionsTreeNode Parent {
            get;
            private set;
        }

        public int TotalPages {
            get {
                return _totalPages;
            }
            internal set {
                _totalPages = value;
                NotifyPropertyChanged();
            }
        }

        public int CurrentPage {
            get {
                return _currentPage;
            }
            internal set {
                _currentPage = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Refresh the list of packages belong to this node
        /// </summary>
        public void Refresh() {
            LoadPage(CurrentPage);
        }

        public override string ToString() {
            return Name;
        }

        /// <summary>
        /// Helper function to raise property changed events
        /// </summary>
        /// <param name="info"></param>
        private void NotifyPropertyChanged() {
            if (PageDataChanged != null) {
                PageDataChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Loads a specified page
        /// </summary>
        /// <param name="pageNumber"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Design", 
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification="We want to show the error message in the message pane rather than blowing up VS.")]
        public void LoadPage(int pageNumber) {

            if (pageNumber < 1) {
                throw new ArgumentOutOfRangeException("pageNumber");
            }

            Trace.WriteLine("Dialog loading page: " + pageNumber);
            if (_loadingInProgress) {
                return;
            }

            ShowProgressPane();

            // avoid more than one loading occurring at the same time
            _loadingInProgress = true;
            _activeQueryStateCancelled = false;

            AsyncOperation async = AsyncOperationManager.CreateOperation(null);
            ExecuteDelegate worker = new ExecuteDelegate(ExecuteAsync);
            worker.BeginInvoke(pageNumber, ItemsPerPage, async, null, null);
        }

        /// <summary>
        /// Called when user clicks on the Cancel button in the progress pane.
        /// </summary>
        private void CancelCurrentExtensionQuery() {
            Trace.WriteLine("Cancelling pending extensions query.");
            _activeQueryStateCancelled = true;
        }

        /// <summary>
        /// This method executes on background thread.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Design", 
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification="We want to show error message inside the dialog, rather than blowing up VS.")]
        private void ExecuteAsync(int pageNumber, int itemsPerPage, AsyncOperation async) {
            ExecuteCompletedEventArgs eventArgs = null;
            int totalCount = 0;

            try {
                // Getting the query (but not executing it yet)
                IQueryable<IPackage> query = GetQuery();

                // This should execute the query
                totalCount = query.Count();

                IQueryable<IPackage> pageQuery = query.Skip((pageNumber - 1) * itemsPerPage).Take(itemsPerPage);
                IEnumerable<IPackage> packages = pageQuery.ToList();

                eventArgs = new ExecuteCompletedEventArgs(null, false, null, packages, pageNumber, totalCount);
            }
            catch (Exception ex) {
                totalCount = 0;
                eventArgs = new ExecuteCompletedEventArgs(ex, false, null, null, pageNumber, totalCount);

                Debug.WriteLine(ex);
            }
            
            async.PostOperationCompleted(new SendOrPostCallback(QueryExecutionCompleted), eventArgs);
        }

        protected abstract IQueryable<IPackage> PreviewQuery(IQueryable<IPackage> query);

        protected abstract void FillNodes(IList<IVsExtensionsTreeNode> nodes);

        private IQueryable<IPackage> GetQuery() {
            var query = PreviewQuery(Provider.GetQuery());

            Trace.WriteLine("Query Created: " + query.ToString());
            return query;
        }

        private void QueryExecutionCompleted(object data) {
            _loadingInProgress = false;

            var args = (ExecuteCompletedEventArgs)data;

            if (_activeQueryStateCancelled) {
                _activeQueryStateCancelled = false;
                HideProgressPane();
            }
            else if (args.Error == null) {
                IEnumerable<IPackage> packages = args.Results;

                _extensions.Clear();
                foreach (IPackage package in packages) {
                    _extensions.Add(new OnlinePackagesItem(Provider, package, false, null, 0, null));
                }

                if (_extensions.Count > 0) {
                    _extensions[0].IsSelected = true;
                }

                int totalPages = (args.TotalCount + ItemsPerPage - 1) / ItemsPerPage;
                int pageNumber = args.PageNumber;

                TotalPages = Math.Max(1, totalPages);
                CurrentPage = Math.Max(1, pageNumber);

                HideProgressPane();
            }
            else {
                // show error message in the Message pane
                Exception exception = args.Error;
                ShowMessagePane((exception.InnerException ?? exception).Message);
            }
        }

        protected void OnNotifyPropertyChanged(string propertyName) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void SetProgressPane(IVsProgressPane progressPane) {
            ProgressPane = progressPane;
        }

        public void SetMessagePane(IVsMessagePane messagePane) {
            MessagePane = messagePane;
        }

        protected bool ShowProgressPane() {
            if (ProgressPane != null) {
                _progressPaneActive = true;
                return ProgressPane.Show(new CancelProgressCallback(CancelCurrentExtensionQuery), true);
            }
            else {
                return false;
            }
        }

        protected void HideProgressPane() {
            if (_progressPaneActive && ProgressPane != null) {
                ProgressPane.Close();
                _progressPaneActive = false;
            }
        }

        protected bool ShowMessagePane(string message) {
            if (MessagePane != null) {
                MessagePane.SetMessageThreadSafe(message);
                return MessagePane.Show();
            }
            else {
                return false;
            }
        }

        /// <summary>
        /// Called when this node is opened.
        /// </summary>
        internal void OnOpened() {
            if (Provider.RefreshOnNodeSelection && !this.IsSearchResultsNode) {
                Refresh();
            }
        }
    }
}