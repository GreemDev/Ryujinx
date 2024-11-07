using Avalonia.Collections;
using DynamicData;
using Gommon;
using Avalonia.Threading;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Ryujinx.Common.Utilities.XCIFileTrimmer;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class XCITrimmerViewModel : BaseModel
    {
        private const long _bytesPerMB = 1024 * 1024;
        private enum ProcessingMode
        {
            Trimming,
            Untrimming
        }

        public enum SortField
        {
            Name,
            Saved
        }

        private const string _FileExtXCI = "XCI";

        private readonly Ryujinx.Common.Logging.XCIFileTrimmerLog _logger;
        private readonly ApplicationLibrary _applicationLibrary;
        private Optional<XCITrimmerFileModel> _processingApplication = null;
        private AvaloniaList<XCITrimmerFileModel> _allXCIFiles = new();
        private AvaloniaList<XCITrimmerFileModel> _selectedXCIFiles = new();
        private AvaloniaList<XCITrimmerFileModel> _displayedXCIFiles = new();
        private MainWindowViewModel _mainWindowViewModel;
        private CancellationTokenSource _cancellationTokenSource;
        private string _search;
        private ProcessingMode _processingMode;
        private SortField _sortField = SortField.Name;
        private bool _sortAscending = true;

        public XCITrimmerViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _logger = new XCIFileTrimmerWindowLog(this);
            _mainWindowViewModel = mainWindowViewModel;
            _applicationLibrary = _mainWindowViewModel.ApplicationLibrary;
            LoadXCIApplications();
        }

        private void LoadXCIApplications()
        {
            var apps = _applicationLibrary.Applications.Items
                .Where(app => app.FileExtension == _FileExtXCI);

            foreach (var xciApp in apps)
                AddOrUpdateXCITrimmerFile(CreateXCITrimmerFile(xciApp.Path));

            ApplicationsChanged();
        }

        private XCITrimmerFileModel CreateXCITrimmerFile(
            string path,
            OperationOutcome operationOutcome = OperationOutcome.Undetermined)
        {
            var xciApp = _applicationLibrary.Applications.Items.First(app => app.FileExtension == _FileExtXCI && app.Path == path);
            return XCITrimmerFileModel.FromApplicationData(xciApp, _logger) with { ProcessingOutcome = operationOutcome };
        }

        private bool AddOrUpdateXCITrimmerFile(XCITrimmerFileModel xci, bool suppressChanged = true, bool autoSelect = true)
        {
            bool replaced = _allXCIFiles.ReplaceWith(xci);
            _displayedXCIFiles.ReplaceWith(xci, Filter(xci));
            _selectedXCIFiles.ReplaceWith(xci, xci.Trimmable && autoSelect);

            if (!suppressChanged)
                ApplicationsChanged();

            return replaced;
        }

        private void FilteringChanged()
        {
            OnPropertyChanged(nameof(Search));
            SortAndFilter();
        }

        private void SortingChanged()
        {
            OnPropertyChanged(nameof(IsSortedByName));
            OnPropertyChanged(nameof(IsSortedBySaved));
            OnPropertyChanged(nameof(SortingAscending));
            OnPropertyChanged(nameof(SortingField));
            OnPropertyChanged(nameof(SortingFieldName));
            SortAndFilter();
        }

        private void DisplayedChanged()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(DisplayedXCIFiles));
            OnPropertyChanged(nameof(SelectedDisplayedXCIFiles));
        }

        private void ApplicationsChanged()
        {
            OnPropertyChanged(nameof(AllXCIFiles));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(PotentialSavings));
            OnPropertyChanged(nameof(ActualSavings));
            OnPropertyChanged(nameof(CanTrim));
            OnPropertyChanged(nameof(CanUntrim));
            DisplayedChanged();
            SortAndFilter();
        }

        private void SelectionChanged(bool displayedChanged = true)
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(CanTrim));
            OnPropertyChanged(nameof(CanUntrim));
            OnPropertyChanged(nameof(SelectedXCIFiles));

            if (displayedChanged)
                OnPropertyChanged(nameof(SelectedDisplayedXCIFiles));
        }

        private void ProcessingChanged()
        {
            OnPropertyChanged(nameof(Processing));
            OnPropertyChanged(nameof(Cancel));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(CanTrim));
            OnPropertyChanged(nameof(CanUntrim));
        }

        private IEnumerable<XCITrimmerFileModel> GetSelectedDisplayedXCIFiles()
        {
            return _displayedXCIFiles.Where(xci => _selectedXCIFiles.Contains(xci));
        }

        private void PerformOperation(ProcessingMode processingMode)
        {
            if (Processing)
            {
                return;
            }

            _processingMode = processingMode;
            Processing = true;
            var cancellationToken = _cancellationTokenSource.Token;

            Thread XCIFileTrimThread = new(() =>
            {
                var toProcess = Sort(SelectedXCIFiles
                    .Where(xci =>
                        (processingMode == ProcessingMode.Untrimming && xci.Untrimmable) ||
                        (processingMode == ProcessingMode.Trimming && xci.Trimmable)
                    )).ToList();

                var viewsSaved = DisplayedXCIFiles.ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    _selectedXCIFiles.Clear();
                    _displayedXCIFiles.Clear();
                    _displayedXCIFiles.AddRange(toProcess);
                });

                try
                {
                    foreach (var xciApp in toProcess)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var trimmer = new XCIFileTrimmer(xciApp.Path, _logger);

                        Dispatcher.UIThread.Post(() =>
                        {
                            ProcessingApplication = xciApp;
                        });

                        var outcome = OperationOutcome.Undetermined;

                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            switch (processingMode)
                            {
                                case ProcessingMode.Trimming:
                                    outcome = trimmer.Trim(cancellationToken);
                                    break;
                                case ProcessingMode.Untrimming:
                                    outcome = trimmer.Untrim(cancellationToken);
                                    break;
                            }

                            if (outcome == OperationOutcome.Cancelled)
                                outcome = OperationOutcome.Undetermined;
                        }
                        finally
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                ProcessingApplication = CreateXCITrimmerFile(xciApp.Path);
                                AddOrUpdateXCITrimmerFile(ProcessingApplication, false, false);
                                ProcessingApplication = null;
                            });
                        }
                    }
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _displayedXCIFiles.AddOrReplaceMatching(_allXCIFiles, viewsSaved);
                        _selectedXCIFiles.AddOrReplaceMatching(_allXCIFiles, toProcess);
                        Processing = false;
                        ApplicationsChanged();
                    });
                }
            })
            {
                Name = "GUI.XCIFilesTrimmerThread",
                IsBackground = true,
            };

            XCIFileTrimThread.Start();
        }

        private bool Filter<T>(T arg)
        {
            if (arg is XCITrimmerFileModel content)
            {
                return string.IsNullOrWhiteSpace(_search)
                    || content.Name.ToLower().Contains(_search.ToLower())
                    || content.Path.ToLower().Contains(_search.ToLower());
            }

            return false;
        }

        private class CompareXCITrimmerFiles : IComparer<XCITrimmerFileModel>
        {
            private XCITrimmerViewModel _viewModel;

            public CompareXCITrimmerFiles(XCITrimmerViewModel ViewModel)
            {
                _viewModel = ViewModel;
            }

            public int Compare(XCITrimmerFileModel x, XCITrimmerFileModel y)
            {
                int result = 0;

                switch (_viewModel.SortingField)
                {
                    case SortField.Name:
                        result = x.Name.CompareTo(y.Name);
                        break;
                    case SortField.Saved:
                        result = x.PotentialSavingsB.CompareTo(y.PotentialSavingsB);
                        break;
                }

                if (!_viewModel.SortingAscending)
                    result = -result;

                if (result == 0)
                    result = x.Path.CompareTo(y.Path);

                return result;
            }
        }

        private IOrderedEnumerable<XCITrimmerFileModel> Sort(IEnumerable<XCITrimmerFileModel> list)
        {
            return list
                .OrderBy(xci => xci, new CompareXCITrimmerFiles(this))
                .ThenBy(it => it.Path);
        }

        public void TrimSelected()
        {
            PerformOperation(ProcessingMode.Trimming);
        }

        public void UntrimSelected()
        {
            PerformOperation(ProcessingMode.Untrimming);
        }

        public void SetProgress(int current, int maximum)
        {
            if (_processingApplication != null)
            {
                int percentageProgress = 100 * current / maximum;
                if (!ProcessingApplication.HasValue || (ProcessingApplication.Value.PercentageProgress != percentageProgress))
                    ProcessingApplication = ProcessingApplication.Value with { PercentageProgress = percentageProgress };
            }
        }

        public void SelectDisplayed()
        {
            SelectedXCIFiles.AddRange(DisplayedXCIFiles);
            SelectionChanged();
        }

        public void DeselectDisplayed()
        {
            SelectedXCIFiles.RemoveMany(DisplayedXCIFiles);
            SelectionChanged();
        }

        public void Select(XCITrimmerFileModel model)
        {
            bool selectionChanged = !SelectedXCIFiles.Contains(model);
            bool displayedSelectionChanged = !SelectedDisplayedXCIFiles.Contains(model);
            SelectedXCIFiles.ReplaceOrAdd(model, model);
            if (selectionChanged)
                SelectionChanged(displayedSelectionChanged);
        }

        public void Deselect(XCITrimmerFileModel model)
        {
            bool displayedSelectionChanged = !SelectedDisplayedXCIFiles.Contains(model);
            if (SelectedXCIFiles.Remove(model))
                SelectionChanged(displayedSelectionChanged);
        }

        public void SortAndFilter()
        {
            if (Processing)
                return;

            Sort(AllXCIFiles)
                .AsObservableChangeSet()
                .Filter(Filter)
                .Bind(out var view).AsObservableList();

            _displayedXCIFiles.Clear();
            _displayedXCIFiles.AddRange(view);

            DisplayedChanged();
        }

        public Optional<XCITrimmerFileModel> ProcessingApplication
        {
            get => _processingApplication;
            set
            {
                if (!value.HasValue && _processingApplication.HasValue)
                    value = _processingApplication.Value with { PercentageProgress = null };

                if (value.HasValue)
                    _displayedXCIFiles.ReplaceWith(value.Value);

                _processingApplication = value;
                OnPropertyChanged();
            }
        }

        public bool Processing
        {
            get => _cancellationTokenSource != null;
            private set
            {
                if (value && !Processing)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }
                else if (!value && Processing)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                ProcessingChanged();
            }
        }

        public bool Cancel
        {
            get => _cancellationTokenSource != null && _cancellationTokenSource.IsCancellationRequested;
            set
            {
                if (value)
                {
                    if (!Processing)
                        return;

                    _cancellationTokenSource.Cancel();
                }

                ProcessingChanged();
            }
        }

        public string Status
        {
            get
            {
                if (Processing)
                {
                    return _processingMode switch
                    {
                        ProcessingMode.Trimming => string.Format(LocaleManager.Instance[LocaleKeys.XCITrimmerTitleStatusTrimming], DisplayedXCIFiles.Count),
                        ProcessingMode.Untrimming => string.Format(LocaleManager.Instance[LocaleKeys.XCITrimmerTitleStatusUntrimming], DisplayedXCIFiles.Count),
                        _ => string.Empty
                    };
                }
                else
                {
                    return string.IsNullOrEmpty(Search) ?
                        string.Format(LocaleManager.Instance[LocaleKeys.XCITrimmerTitleStatusCount], SelectedXCIFiles.Count, AllXCIFiles.Count) :
                        string.Format(LocaleManager.Instance[LocaleKeys.XCITrimmerTitleStatusCountWithFilter], SelectedXCIFiles.Count, AllXCIFiles.Count, DisplayedXCIFiles.Count);
                }
            }
        }

        public string Search
        {
            get => _search;
            set
            {
                _search = value;
                FilteringChanged();
            }
        }

        public SortField SortingField
        {
            get => _sortField;
            set
            {
                _sortField = value;
                SortingChanged();
            }
        }

        public string SortingFieldName
        {
            get
            {
                return SortingField switch
                {
                    SortField.Name => LocaleManager.Instance[LocaleKeys.XCITrimmerSortName],
                    SortField.Saved => LocaleManager.Instance[LocaleKeys.XCITrimmerSortSaved],
                    _ => string.Empty,
                };
            }
        }
        public bool SortingAscending
        {
            get => _sortAscending;
            set
            {
                _sortAscending = value;
                SortingChanged();
            }
        }

        public bool IsSortedByName
        {
            get => _sortField == SortField.Name;
        }

        public bool IsSortedBySaved
        {
            get => _sortField == SortField.Saved;
        }

        public AvaloniaList<XCITrimmerFileModel> SelectedXCIFiles
        {
            get => _selectedXCIFiles;
            set
            {
                _selectedXCIFiles = value;
                SelectionChanged();
            }
        }

        public AvaloniaList<XCITrimmerFileModel> AllXCIFiles
        {
            get => _allXCIFiles;
        }

        public AvaloniaList<XCITrimmerFileModel> DisplayedXCIFiles
        {
            get => _displayedXCIFiles;
        }

        public string PotentialSavings
        {
            get
            {
                return string.Format(LocaleManager.Instance[LocaleKeys.XCITrimmerSavingsMb], AllXCIFiles.Sum(xci => xci.PotentialSavingsB / _bytesPerMB));
            }
        }

        public string ActualSavings
        {
            get
            {
                return string.Format(LocaleManager.Instance[LocaleKeys.XCITrimmerSavingsMb], AllXCIFiles.Sum(xci => xci.CurrentSavingsB / _bytesPerMB));
            }
        }

        public IEnumerable<XCITrimmerFileModel> SelectedDisplayedXCIFiles
        {
            get
            {
                return GetSelectedDisplayedXCIFiles().ToList();
            }
        }

        public bool CanTrim
        {
            get
            {
                return !Processing && _selectedXCIFiles.Any(xci => xci.Trimmable);
            }
        }

        public bool CanUntrim
        {
            get
            {
                return !Processing && _selectedXCIFiles.Any(xci => xci.Untrimmable);
            }
        }
    }
}