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
using FlaUI.Core.AutomationElements;
using FlaUI.Inspector.Helpers;
using FlaUI.Inspector.Models;
using FlaUI.Inspector.Overlays;
using FlaUI.Inspector.Services;

namespace FlaUI.Inspector.ViewModels
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

        private readonly SelectorResolver _selectorResolver = new SelectorResolver();
        private IndicateService _indicateService;
        private VisualTreeNode _visualTreeRoot;
        private VisualTreeNode _selectedVisualNode;
        private SelectorLevel _selectedSelectorLevel;
        private AutomationElement _targetElement;
        private string _selectorXml = string.Empty;
        private string _targetElementDisplay = string.Empty;
        private ValidationState _validationState = ValidationState.None;
        private bool _hasIndicatedElement;
        private bool _isIndicating;
        private bool _isHighlighting;
        private bool _isValidating;
        private CancellationTokenSource _resolveRetryCts;
        private bool _suppressSelectorUpdate;
        private bool _suppressVisualTreeSelectionApply;

        public MainViewModel()
        {
            VisualTree = new ObservableCollection<VisualTreeNode>();
            SelectorLevels = new ObservableCollection<SelectorLevel>();
            PropertyExplorerItems = new ObservableCollection<ElementPropertyItem>();
            SelectedItemProperties = new ObservableCollection<ElementPropertyItem>();

            IndicateCommand = new RelayCommand(StartIndicate, () => !IsIndicating && !IsHighlighting && !IsValidating);
            ValidateCommand = new RelayCommand(() => _ = ValidateSelectorAsync(), () => _hasIndicatedElement && SelectorLevels.Count > 0 && !IsValidating && !IsHighlighting);
            HighlightCommand = new RelayCommand(() => _ = HighlightAsync(), () => !IsHighlighting && !IsIndicating && !IsValidating);
        }

        public ObservableCollection<VisualTreeNode> VisualTree { get; }
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

        public VisualTreeNode SelectedVisualNode
        {
            get => _selectedVisualNode;
            set
            {
                if (!SetProperty(ref _selectedVisualNode, value) || value == null)
                    return;

                if (!_suppressVisualTreeSelectionApply)
                    LoadPropertyExplorer(value.Element);

                if (!_suppressVisualTreeSelectionApply &&
                    !IsIndicating &&
                    value.Element != null &&
                    !ReferenceEquals(value.Element, _targetElement))
                {
                    ApplyTargetElement(value.Element, keepVisualSelection: true, autoValidate: false);
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
                    case ValidationState.Valid:
                        return GreenBackground;
                    case ValidationState.Ambiguous:
                        return OrangeBackground;
                    case ValidationState.Invalid:
                        return RedBackground;
                    default:
                        return GrayBackground;
                }
            }
        }

        public Brush ValidateButtonBorderBrush
        {
            get
            {
                switch (ValidationState)
                {
                    case ValidationState.Valid:
                        return GreenBorder;
                    case ValidationState.Ambiguous:
                        return OrangeBorder;
                    case ValidationState.Invalid:
                        return RedBorder;
                    default:
                        return GrayBorder;
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

        public bool IgnoreVisualTreeSelectionChanges => _suppressVisualTreeSelectionApply;

        public void Initialize(Dispatcher dispatcher)
        {
            _indicateService = new IndicateService(dispatcher);
            _indicateService.ElementSelected += OnElementIndicated;
            _indicateService.Cancelled += OnIndicateCancelled;
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

            var mainWindow = Application.Current.MainWindow;
            Application.Current.Dispatcher.BeginInvoke(
                new Action(() => WindowRestoreHelper.RestoreAfterIndicateComplete(mainWindow)),
                DispatcherPriority.ApplicationIdle);
        }

        private void OnIndicateCancelled()
        {
            IsIndicating = false;
            CommandManager.InvalidateRequerySuggested();

            var mainWindow = Application.Current.MainWindow;
            Application.Current.Dispatcher.BeginInvoke(
                new Action(() => WindowRestoreHelper.RestoreAfterIndicateComplete(mainWindow)),
                DispatcherPriority.ApplicationIdle);
        }

        private void ApplyIndicateCapture(IndicateCaptureResult capture, bool autoValidate = false)
        {
            if (capture == null)
                return;

            _suppressVisualTreeSelectionApply = true;
            _suppressSelectorUpdate = true;

            try
            {
                _targetElement = capture.TargetElement;

                VisualTree.Clear();
                _visualTreeRoot = capture.VisualTreeRoot;
                if (_visualTreeRoot != null)
                    VisualTree.Add(_visualTreeRoot);

                foreach (var level in SelectorLevels)
                    level.PropertyChanged -= OnSelectorLevelChanged;
                SelectorLevels.Clear();

                var levels = capture.SelectorLevels ?? new List<SelectorLevel>();
                if (levels.Count <= 1 && capture.PathDepth > 1 && !string.IsNullOrWhiteSpace(capture.SelectorXmlSnapshot))
                {
                    var parsed = SelectorXmlSerializer.Deserialize(capture.SelectorXmlSnapshot);
                    if (parsed.Count > 1)
                        levels = parsed.ToList();
                }

                foreach (var level in levels)
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

                ApplyVisualTreeSelection(capture.SelectedVisualNode ?? _visualTreeRoot);

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
                    new Action(() => _suppressVisualTreeSelectionApply = false),
                    DispatcherPriority.ApplicationIdle);
            }
        }

        private void ApplyVisualTreeSelection(VisualTreeNode node)
        {
            if (_selectedVisualNode != null && !ReferenceEquals(_selectedVisualNode, node))
                _selectedVisualNode.IsSelected = false;

            _selectedVisualNode = node;
            if (_selectedVisualNode != null)
                _selectedVisualNode.IsSelected = true;

            RaisePropertyChanged(nameof(SelectedVisualNode));
        }

        public void ApplyTargetElement(AutomationElement element, bool keepVisualSelection = false, bool autoValidate = false)
        {
            _targetElement = element;
            _suppressSelectorUpdate = true;

            if (!keepVisualSelection)
            {
                VisualTree.Clear();
                _visualTreeRoot = VisualTreeBuilder.BuildWindowTree(element);
                if (_visualTreeRoot != null)
                    VisualTree.Add(_visualTreeRoot);
            }

            foreach (var level in SelectorLevels)
                level.PropertyChanged -= OnSelectorLevelChanged;
            SelectorLevels.Clear();

            foreach (var level in SelectorBuilder.BuildLevels(element))
                SelectorLevels.Add(level);

            AttachSelectorHandlers();
            foreach (var level in SelectorLevels)
                level.RefreshTagLine();

            SelectedSelectorLevel = SelectorLevels.LastOrDefault();
            UpdateTargetElementDisplay(element);

            if (!keepVisualSelection)
            {
                _suppressSelectorUpdate = false;
                UpdateSelectorXml();
                _suppressSelectorUpdate = true;
                SelectedVisualNode = VisualTreeBuilder.FindNode(_visualTreeRoot, element) ?? _visualTreeRoot;
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
            if (!_hasIndicatedElement)
            {
                ValidationState = ValidationState.None;
                return;
            }

            ValidationState = ValidationState.Stale;
        }

        private void RaiseValidateButtonAppearanceChanged()
        {
            RaisePropertyChanged(nameof(ValidateButtonBackground));
            RaisePropertyChanged(nameof(ValidateButtonBorderBrush));
            RaisePropertyChanged(nameof(ValidateButtonForeground));
        }

        private void UpdateTargetElementDisplay(AutomationElement element)
        {
            try
            {
                var role = element.Properties.ControlType.IsSupported
                    ? element.Properties.ControlType.Value.ToString()
                    : "element";
                var name = element.Properties.Name.IsSupported ? element.Properties.Name.ValueOrDefault : string.Empty;
                TargetElementDisplay = string.IsNullOrEmpty(name)
                    ? role
                    : role + " '" + name + "'";
            }
            catch
            {
                TargetElementDisplay = "Unknown";
            }
        }

        private void LoadPropertyExplorer(AutomationElement element)
        {
            PropertyExplorerItems.Clear();
            foreach (var item in PropertyCollector.CollectAll(element))
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

            if (SelectedSelectorLevel != null)
                SelectedSelectorLevel.RefreshTagLine();

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

            await Application.Current.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render).Task.ConfigureAwait(true);

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

                var bounds = ElementDetector.GetBoundingRectangle(results[0]);
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
                var existing = target.Properties.FirstOrDefault(p => string.Equals(p.Name, sourceProp.Name, StringComparison.Ordinal));
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

        public void Cleanup()
        {
            _resolveRetryCts?.Cancel();
            _resolveRetryCts?.Dispose();
            _resolveRetryCts = null;
            _indicateService?.Dispose();
        }
    }
}
