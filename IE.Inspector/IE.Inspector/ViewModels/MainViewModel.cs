using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Helpers;
using IE.Inspector.Models;
using IE.Inspector.Overlays;
using IE.Inspector.Services;

namespace IE.Inspector.ViewModels
{
    public sealed class MainViewModel : NotifyObject
    {
        private static readonly SolidColorBrush GrayBackground = new SolidColorBrush(Color.FromRgb(232, 232, 232));
        private static readonly SolidColorBrush GrayBorder = new SolidColorBrush(Color.FromRgb(189, 189, 189));
        private static readonly SolidColorBrush GrayForeground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        private static readonly SolidColorBrush GreenBackground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush GreenBorder = new SolidColorBrush(Color.FromRgb(56, 142, 60));
        private static readonly SolidColorBrush OrangeBackground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
        private static readonly SolidColorBrush OrangeBorder = new SolidColorBrush(Color.FromRgb(230, 126, 34));
        private static readonly SolidColorBrush RedBackground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        private static readonly SolidColorBrush RedBorder = new SolidColorBrush(Color.FromRgb(211, 47, 47));
        private static readonly SolidColorBrush WhiteForeground = new SolidColorBrush(Colors.White);

        private readonly IeBrowserContext _browserContext = new IeBrowserContext();
        private readonly SelectorResolver _selectorResolver;
        private IndicateService _indicateService;
        private DomTreeNode _domTreeRoot;
        private DomTreeNode _selectedDomNode;
        private SelectorLevel _selectedSelectorLevel;
        private IEWindowController.IEDomElement _targetElement;
        private IList<object> _framePath = new List<object>();
        private string _selectorXml = string.Empty;
        private string _targetElementDisplay = string.Empty;
        private ValidationState _validationState = ValidationState.None;
        private bool _hasIndicatedElement;
        private bool _isIndicating;
        private bool _isHighlighting;
        private bool _isValidating;
        private CancellationTokenSource _resolveRetryCts;
        private bool _suppressSelectorUpdate;
        private bool _suppressDomTreeSelectionApply;

        public MainViewModel()
        {
            _selectorResolver = new SelectorResolver(_browserContext);

            DomTree = new ObservableCollection<DomTreeNode>();
            SelectorLevels = new ObservableCollection<SelectorLevel>();
            PropertyExplorerItems = new ObservableCollection<ElementPropertyItem>();
            SelectedItemProperties = new ObservableCollection<ElementPropertyItem>();

            IndicateCommand = new RelayCommand(StartIndicate, () => !IsIndicating && !IsHighlighting && !IsValidating);
            ValidateCommand = new RelayCommand(() => _ = ValidateSelectorAsync(), () => _hasIndicatedElement && SelectorLevels.Count > 0 && !IsValidating && !IsHighlighting);
            HighlightCommand = new RelayCommand(() => _ = HighlightAsync(), () => !IsHighlighting && !IsIndicating && !IsValidating);
        }

        public ObservableCollection<DomTreeNode> DomTree { get; }
        public ObservableCollection<SelectorLevel> SelectorLevels { get; }
        public ObservableCollection<ElementPropertyItem> PropertyExplorerItems { get; }
        public ObservableCollection<ElementPropertyItem> SelectedItemProperties { get; }

        public RelayCommand IndicateCommand { get; }
        public RelayCommand ValidateCommand { get; }
        public RelayCommand HighlightCommand { get; }

        public string TargetElementDisplay
        {
            get => _targetElementDisplay;
            set => SetProperty(ref _targetElementDisplay, value);
        }

        public DomTreeNode SelectedDomNode
        {
            get => _selectedDomNode;
            set
            {
                if (!SetProperty(ref _selectedDomNode, value) || value == null)
                    return;

                if (!_suppressDomTreeSelectionApply)
                    LoadPropertyExplorer(value.Element);

                if (!_suppressDomTreeSelectionApply &&
                    !IsIndicating &&
                    value.Element != null &&
                    !ReferenceEquals(value.Element.raw, _targetElement?.raw))
                {
                    ApplyTargetElement(value.Element, keepDomSelection: true, autoValidate: false);
                }
            }
        }

        public SelectorLevel SelectedSelectorLevel
        {
            get => _selectedSelectorLevel;
            set
            {
                if (!SetProperty(ref _selectedSelectorLevel, value))
                    return;

                LoadSelectedItemProperties(value);
            }
        }

        public string SelectorXml
        {
            get => _selectorXml;
            set
            {
                if (!SetProperty(ref _selectorXml, value))
                    return;

                if (!_suppressSelectorUpdate)
                    MarkValidationStale();
            }
        }

        public ValidationState ValidationState
        {
            get => _validationState;
            private set
            {
                if (SetProperty(ref _validationState, value))
                    RaiseValidateButtonAppearanceChanged();
            }
        }

        public Brush ValidateButtonBackground
        {
            get
            {
                switch (ValidationState)
                {
                    case ValidationState.Valid: return GreenBackground;
                    case ValidationState.Ambiguous: return OrangeBackground;
                    case ValidationState.Invalid: return RedBackground;
                    default: return GrayBackground;
                }
            }
        }

        public Brush ValidateButtonBorderBrush
        {
            get
            {
                switch (ValidationState)
                {
                    case ValidationState.Valid: return GreenBorder;
                    case ValidationState.Ambiguous: return OrangeBorder;
                    case ValidationState.Invalid: return RedBorder;
                    default: return GrayBorder;
                }
            }
        }

        public Brush ValidateButtonForeground
        {
            get
            {
                switch (ValidationState)
                {
                    case ValidationState.Valid:
                    case ValidationState.Ambiguous:
                    case ValidationState.Invalid:
                        return WhiteForeground;
                    default:
                        return GrayForeground;
                }
            }
        }

        public bool IsIndicating
        {
            get => _isIndicating;
            private set
            {
                if (SetProperty(ref _isIndicating, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsHighlighting
        {
            get => _isHighlighting;
            private set
            {
                if (SetProperty(ref _isHighlighting, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsValidating
        {
            get => _isValidating;
            private set
            {
                if (SetProperty(ref _isValidating, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IgnoreDomTreeSelectionChanges => _suppressDomTreeSelectionApply;

        public void Initialize(Dispatcher dispatcher)
        {
            _indicateService = new IndicateService(dispatcher);
            _indicateService.ElementSelected += OnElementIndicated;
            _indicateService.IndicationCancelled += OnIndicationCancelled;
        }

        public void AttachSelectorHandlers()
        {
            foreach (var level in SelectorLevels)
            {
                level.PropertyChanged -= OnSelectorLevelChanged;
                level.PropertyChanged += OnSelectorLevelChanged;
                foreach (var property in level.Properties)
                {
                    property.PropertyChanged -= OnSelectorPropertyChanged;
                    property.PropertyChanged += OnSelectorPropertyChanged;
                }
            }
        }

        private void StartIndicate()
        {
            IsIndicating = true;

            var window = Application.Current.MainWindow;
            if (window != null)
                window.WindowState = WindowState.Minimized;

            _indicateService.Start();
        }

        private void OnElementIndicated(IndicateCaptureResult capture)
        {
            IsIndicating = false;
            _hasIndicatedElement = true;
            CommandManager.InvalidateRequerySuggested();
            ApplyIndicateCapture(capture, autoValidate: true);
            _indicateService.DismissProcessingOverlay();
            RestoreMainWindowAfterIndicate();
        }

        private void OnIndicationCancelled()
        {
            IsIndicating = false;
            CommandManager.InvalidateRequerySuggested();
            RestoreMainWindowAfterIndicate();
        }

        private static void RestoreMainWindowAfterIndicate()
        {
            var mainWindow = Application.Current.MainWindow;
            Application.Current.Dispatcher.BeginInvoke(
                new Action(() => WindowRestoreHelper.RestoreAfterIndicateComplete(mainWindow)),
                DispatcherPriority.ApplicationIdle);
        }

        private void ApplyIndicateCapture(IndicateCaptureResult capture, bool autoValidate = false)
        {
            if (capture == null)
                return;

            _suppressDomTreeSelectionApply = true;
            _suppressSelectorUpdate = true;

            try
            {
                _browserContext.Controller = capture.Controller;
                _targetElement = capture.TargetElement;
                _framePath = capture.FramePath ?? new List<object>();

                DomTree.Clear();
                _domTreeRoot = capture.DomTreeRoot;
                if (_domTreeRoot != null)
                    DomTree.Add(_domTreeRoot);

                foreach (var level in SelectorLevels)
                    level.PropertyChanged -= OnSelectorLevelChanged;
                SelectorLevels.Clear();

                foreach (var level in capture.SelectorLevels ?? new List<SelectorLevel>())
                    SelectorLevels.Add(level);

                AttachSelectorHandlers();
                foreach (var level in SelectorLevels)
                    level.RefreshTagLine();

                SelectedSelectorLevel = SelectorLevels.LastOrDefault();
                TargetElementDisplay = capture.TargetDisplay ?? string.Empty;

                PropertyExplorerItems.Clear();
                foreach (var item in capture.PropertyItems ?? new List<ElementPropertyItem>())
                    PropertyExplorerItems.Add(item);

                _suppressSelectorUpdate = false;
                if (!string.IsNullOrWhiteSpace(capture.SelectorXmlSnapshot))
                    SelectorXml = capture.SelectorXmlSnapshot;
                else
                    UpdateSelectorXml();
                _suppressSelectorUpdate = true;
                ApplyDomTreeSelection(capture.SelectedDomNode ?? _domTreeRoot);
                _suppressSelectorUpdate = false;

                if (autoValidate)
                    _ = ValidateSelectorAsync();
                else
                    MarkValidationStale();
            }
            finally
            {
                _suppressSelectorUpdate = false;
                Application.Current.Dispatcher.BeginInvoke(
                    new Action(() => _suppressDomTreeSelectionApply = false),
                    DispatcherPriority.ApplicationIdle);
            }
        }

        private void ApplyDomTreeSelection(DomTreeNode node)
        {
            if (_selectedDomNode != null && !ReferenceEquals(_selectedDomNode, node))
                _selectedDomNode.IsSelected = false;

            _selectedDomNode = node;
            if (_selectedDomNode != null)
                _selectedDomNode.IsSelected = true;

            RaisePropertyChanged(nameof(SelectedDomNode));
        }

        public void ApplyTargetElement(IEWindowController.IEDomElement element, bool keepDomSelection = false, bool autoValidate = false)
        {
            _targetElement = element;
            _suppressSelectorUpdate = true;

            if (!keepDomSelection)
            {
                DomTree.Clear();
                _domTreeRoot = DomTreeBuilder.BuildDocumentTree(element);
                if (_domTreeRoot != null)
                    DomTree.Add(_domTreeRoot);
            }

            foreach (var level in SelectorLevels)
                level.PropertyChanged -= OnSelectorLevelChanged;
            SelectorLevels.Clear();

            _framePath = element.build_frame_path() ?? new List<object>();
            foreach (var level in SelectorBuilder.BuildLevels(_browserContext.Controller, element, _framePath))
                SelectorLevels.Add(level);

            AttachSelectorHandlers();
            foreach (var level in SelectorLevels)
                level.RefreshTagLine();

            SelectedSelectorLevel = SelectorLevels.LastOrDefault();
            UpdateTargetElementDisplay(element);

            if (!keepDomSelection)
            {
                _suppressSelectorUpdate = false;
                UpdateSelectorXml();
                _suppressSelectorUpdate = true;
                SelectedDomNode = DomTreeBuilder.FindNode(_domTreeRoot, element) ?? _domTreeRoot;
            }
            else
            {
                LoadPropertyExplorer(element);
            }

            _suppressSelectorUpdate = false;
            UpdateSelectorXml();

            if (autoValidate)
                _ = ValidateSelectorAsync();
            else
                MarkValidationStale();
        }

        private void MarkValidationStale()
        {
            ValidationState = !_hasIndicatedElement ? ValidationState.None : ValidationState.Stale;
        }

        private void RaiseValidateButtonAppearanceChanged()
        {
            RaisePropertyChanged(nameof(ValidateButtonBackground));
            RaisePropertyChanged(nameof(ValidateButtonBorderBrush));
            RaisePropertyChanged(nameof(ValidateButtonForeground));
        }

        private void UpdateTargetElementDisplay(IEWindowController.IEDomElement element)
        {
            try
            {
                var tag = element.get_attribute("tagName")?.ToString() ?? "element";
                var id = element.get_attribute("id")?.ToString();
                var name = element.get_attribute("name")?.ToString();
                var label = !string.IsNullOrEmpty(id) ? id : name;
                TargetElementDisplay = string.IsNullOrEmpty(label) ? tag : tag + " '" + label + "'";
            }
            catch
            {
                TargetElementDisplay = "Unknown";
            }
        }

        private void LoadPropertyExplorer(IEWindowController.IEDomElement element)
        {
            PropertyExplorerItems.Clear();
            foreach (var item in DomPropertyCollector.CollectAll(element, _browserContext.Controller))
                PropertyExplorerItems.Add(item);
        }

        private void LoadSelectedItemProperties(SelectorLevel level)
        {
            SelectedItemProperties.Clear();
            if (level == null)
                return;

            foreach (var property in level.Properties)
            {
                property.PropertyChanged -= OnSelectorPropertyChanged;
                property.PropertyChanged += OnSelectorPropertyChanged;
                SelectedItemProperties.Add(property);
            }
        }

        private void OnSelectorLevelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectorLevel.IsEnabled))
            {
                if (sender is SelectorLevel level)
                    level.RefreshTagLine();
                UpdateSelectorXml();
            }
        }

        private void OnSelectorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ElementPropertyItem.IsSelected):
                case nameof(ElementPropertyItem.IsRegex):
                case nameof(ElementPropertyItem.Value):
                    break;
                default:
                    return;
            }

            SelectedSelectorLevel?.RefreshTagLine();
            UpdateSelectorXml();
        }

        private void UpdateSelectorXml()
        {
            if (_suppressSelectorUpdate)
                return;

            _suppressSelectorUpdate = true;
            SelectorXml = SelectorXmlSerializer.Serialize(SelectorLevels);
            _suppressSelectorUpdate = false;
            MarkValidationStale();
        }

        private CancellationToken BeginResolveRetry()
        {
            _resolveRetryCts?.Cancel();
            _resolveRetryCts?.Dispose();
            _resolveRetryCts = new CancellationTokenSource();
            return _resolveRetryCts.Token;
        }

        private async Task ValidateSelectorAsync()
        {
            if (!_hasIndicatedElement || SelectorLevels.Count == 0)
            {
                ValidationState = ValidationState.None;
                return;
            }

            ValidationState = ValidationState.Stale;
            IsValidating = true;
            IsHighlighting = false;

            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render).Task.ConfigureAwait(true);

            var cancellationToken = BeginResolveRetry();
            try
            {
                var results = await SelectorResolveRetry.FindElementsWithRetryAsync(
                    _selectorResolver,
                    GetResolvableLevels(),
                    maxResults: 2,
                    cancellationToken: cancellationToken).ConfigureAwait(true);

                if (cancellationToken.IsCancellationRequested)
                    return;

                if (results.Count == 0)
                    ValidationState = ValidationState.Invalid;
                else if (results.Count == 1)
                    ValidationState = ValidationState.Valid;
                else
                    ValidationState = ValidationState.Ambiguous;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                    ValidationState = ValidationState.Invalid;
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    IsValidating = false;
            }
        }

        private async Task HighlightAsync()
        {
            var cancellationToken = BeginResolveRetry();
            IsValidating = false;
            IsHighlighting = true;
            try
            {
                var results = await SelectorResolveRetry.FindElementsWithRetryAsync(
                    _selectorResolver,
                    GetResolvableLevels(),
                    maxResults: 2,
                    cancellationToken: cancellationToken).ConfigureAwait(true);

                if (cancellationToken.IsCancellationRequested || results.Count != 1)
                    return;

                var framePath = SelectorLocatorBuilder.BuildFramePathFromLevels(GetResolvableLevels());
                var bounds = IeElementDetector.GetBoundingRectangle(_browserContext.Controller, results[0], framePath);
                await HighlightOverlay.ShowAsync(bounds, 3000).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    IsHighlighting = false;
            }
        }

        public void Cleanup()
        {
            _resolveRetryCts?.Cancel();
            _resolveRetryCts?.Dispose();
            _resolveRetryCts = null;
            _indicateService?.Dispose();
        }

        private IList<SelectorLevel> GetResolvableLevels()
        {
            var parsed = SelectorXmlSerializer.Deserialize(SelectorXml);
            if (parsed.Count > 0)
            {
                TrySyncPropertiesFromXml(parsed);
                return parsed;
            }

            return SelectorLevels.Where(l => l.IsEnabled).ToList();
        }

        private void TrySyncPropertiesFromXml(IList<SelectorLevel> parsed)
        {
            var enabledLevels = SelectorLevels.Where(l => l.IsEnabled).ToList();
            if (parsed.Count != enabledLevels.Count)
                return;

            for (var i = 0; i < parsed.Count; i++)
                MergeLevelProperties(enabledLevels[i], parsed[i]);
        }

        private static void MergeLevelProperties(SelectorLevel target, SelectorLevel source)
        {
            foreach (var sourceProp in source.Properties)
            {
                var existing = target.Properties.FirstOrDefault(p =>
                    string.Equals(p.Name, sourceProp.Name, StringComparison.Ordinal));
                if (existing != null)
                {
                    existing.Value = sourceProp.Value;
                    existing.IsRegex = sourceProp.IsRegex;
                    existing.IsSelected = sourceProp.IsSelected;
                }
                else
                {
                    target.Properties.Add(new ElementPropertyItem
                    {
                        Name = sourceProp.Name,
                        Value = sourceProp.Value,
                        IsRegex = sourceProp.IsRegex,
                        IsSelected = sourceProp.IsSelected,
                        CanToggle = sourceProp.CanToggle
                    });
                }
            }

            foreach (var targetProp in target.Properties.ToList())
            {
                if (!source.Properties.Any(p => string.Equals(p.Name, targetProp.Name, StringComparison.Ordinal)))
                    targetProp.IsSelected = false;
            }

            target.RefreshTagLine();
        }
    }
}
