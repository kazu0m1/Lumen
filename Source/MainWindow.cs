using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LumenApp
{
    internal sealed class MainWindow : Window
    {
        private readonly ObservableCollection<PhotoItem> _photos = new ObservableCollection<PhotoItem>();
        private readonly AppSettings _settings;
        private readonly EditStore _editStore;
        private readonly string[] _startupArgs;
        private readonly List<ExifFieldDefinition> _exifFields;

        private ListBox _grid;
        private ListBox _filmstrip;
        private Grid _browseView;
        private Grid _viewRoot;
        private Grid _mainArea;
        private ColumnDefinition _sidebarColumn;
        private ColumnDefinition _splitterColumn;
        private Border _sidebarBorder;
        private GridSplitter _sidebarSplitter;
        private StackPanel _fileInfoPanel;
        private StackPanel _exifPanel;
        private StackPanel _adjustmentPanel;
        private Image _viewerImage;
        private ScrollViewer _viewerScroll;
        private Slider _exposureSlider;
        private TextBlock _exposureText;
        private TextBlock _statusText;
        private TextBlock _folderText;
        private Slider _thumbnailSlider;
        private ToolBar _toolbar;
        private Border _filmstripBorder;
        private Border _statusBorder;
        private Button _browseModeButton;
        private Button _viewModeButton;

        private string _currentFolder;
        private int _currentIndex = -1;
        private bool _viewMode;
        private bool _updatingSelection;
        private bool _updatingExposureUi;
        private bool _fitMode = true;
        private double _zoom = 1.0;
        private bool _fullScreen;
        private WindowStyle _savedWindowStyle;
        private ResizeMode _savedResizeMode;
        private WindowState _savedWindowState;
        private GridLength _savedSidebarWidth;
        private int _folderGeneration;
        private int _imageGeneration;
        private CancellationTokenSource _folderCts;
        private DeleteBatch _lastDeleteBatch;
        private readonly DispatcherTimer _exposureDebounce;
        private readonly object _cacheSync = new object();
        private readonly SemaphoreSlim _currentDecodeGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _preloadDecodeGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _metadataGate = new SemaphoreSlim(1, 1);
        private int _metadataGeneration;
        private readonly Dictionary<string, BitmapSource> _fullCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> _fullCacheLru = new LinkedList<string>();
        private const int FullCacheLimit = 5;

        public MainWindow(string[] args)
        {
            _startupArgs = args ?? new string[0];
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _settings = new AppSettings(baseDir);
            _editStore = new EditStore(_settings.DataDirectory);
            _exifFields = CreateExifFields();

            Title = "Lumen";
            Width = _settings.WindowWidth;
            Height = _settings.WindowHeight;
            MinWidth = 850;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
            {
                Left = _settings.WindowLeft;
                Top = _settings.WindowTop;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            AllowDrop = true;
            Drop += OnDrop;
            DragOver += OnDragOver;
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += OnClosing;
            Loaded += OnLoaded;

            _exposureDebounce = new DispatcherTimer();
            _exposureDebounce.Interval = TimeSpan.FromMilliseconds(130);
            _exposureDebounce.Tick += delegate
            {
                _exposureDebounce.Stop();
                if (_viewMode) LoadCurrentImage(true);
            };

            BuildUi();
            if (_settings.WindowMaximized) WindowState = WindowState.Maximized;
        }

        private void BuildUi()
        {
            DockPanel root = new DockPanel();
            Content = root;

            _toolbar = BuildToolbar();
            DockPanel.SetDock(_toolbar, Dock.Top);
            root.Children.Add(_toolbar);

            _statusBorder = new Border();
            _statusBorder.BorderThickness = new Thickness(0, 1, 0, 0);
            _statusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 210));
            _statusBorder.Padding = new Thickness(10, 5, 10, 5);
            DockPanel.SetDock(_statusBorder, Dock.Bottom);
            _statusText = new TextBlock();
            _statusText.Text = "Ready";
            _statusBorder.Child = _statusText;
            root.Children.Add(_statusBorder);

            _mainArea = new Grid();
            root.Children.Add(_mainArea);
            _sidebarColumn = new ColumnDefinition { Width = _settings.SidebarVisible ? new GridLength(_settings.SidebarWidth) : new GridLength(0) };
            _mainArea.ColumnDefinitions.Add(_sidebarColumn);
            _splitterColumn = new ColumnDefinition { Width = _settings.SidebarVisible ? new GridLength(5) : new GridLength(0) };
            _mainArea.ColumnDefinitions.Add(_splitterColumn);
            _mainArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _sidebarSplitter = new GridSplitter();
            _sidebarSplitter.Width = 5;
            _sidebarSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            _sidebarSplitter.VerticalAlignment = VerticalAlignment.Stretch;
            _sidebarSplitter.ResizeDirection = GridResizeDirection.Columns;
            _sidebarSplitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
            _sidebarSplitter.Background = new SolidColorBrush(Color.FromRgb(225, 225, 225));
            _sidebarSplitter.Visibility = _settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(_sidebarSplitter, 1);
            _mainArea.Children.Add(_sidebarSplitter);

            Grid content = new Grid();
            Grid.SetColumn(content, 2);
            _mainArea.Children.Add(content);

            _browseView = BuildBrowseView();
            content.Children.Add(_browseView);

            _viewRoot = BuildViewer();
            _viewRoot.Visibility = Visibility.Collapsed;
            content.Children.Add(_viewRoot);

            _sidebarBorder = BuildSidebar();
            _sidebarBorder.Visibility = _settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(_sidebarBorder, 0);
            _mainArea.Children.Add(_sidebarBorder);
        }

        private ToolBar BuildToolbar()
        {
            ToolBar bar = new ToolBar();

            Button open = MakeButton("Open Folder", "Open a photo folder");
            open.Click += delegate { ChooseFolder(); };
            bar.Items.Add(open);

            Separator s1 = new Separator(); bar.Items.Add(s1);

            _browseModeButton = MakeButton("Browse", "Thumbnail overview (Esc)");
            _browseModeButton.Click += delegate { SetViewMode(false); };
            bar.Items.Add(_browseModeButton);

            _viewModeButton = MakeButton("View", "Single photo view (Enter)");
            _viewModeButton.Click += delegate { if (_currentIndex >= 0) SetViewMode(true); };
            bar.Items.Add(_viewModeButton);

            Separator s2 = new Separator(); bar.Items.Add(s2);

            Button fit = MakeButton("Fit", "Fit image to window");
            fit.Click += delegate { _fitMode = true; ApplyViewerSizing(); };
            bar.Items.Add(fit);

            Button actual = MakeButton("100%", "Show image at 100%");
            actual.Click += delegate { _fitMode = false; _zoom = 1.0; ApplyViewerSizing(); };
            bar.Items.Add(actual);

            Separator s3 = new Separator(); bar.Items.Add(s3);

            Button sidebar = MakeButton("Info", "Show/hide Information pane");
            sidebar.Click += delegate { ToggleSidebar(); };
            bar.Items.Add(sidebar);

            Button export = MakeButton("Export", "Resize/export selected photos (Ctrl+E)");
            export.Click += delegate { ExportSelected(); };
            bar.Items.Add(export);

            Separator s4 = new Separator(); bar.Items.Add(s4);

            TextBlock t = new TextBlock();
            t.Text = "Thumbnail";
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(6, 0, 5, 0);
            bar.Items.Add(t);

            _thumbnailSlider = new Slider();
            _thumbnailSlider.Minimum = 120;
            _thumbnailSlider.Maximum = 340;
            _thumbnailSlider.Value = _settings.ThumbnailSize;
            _thumbnailSlider.Width = 110;
            _thumbnailSlider.TickFrequency = 20;
            _thumbnailSlider.IsSnapToTickEnabled = false;
            _thumbnailSlider.ValueChanged += OnThumbnailSizeChanged;
            bar.Items.Add(_thumbnailSlider);

            _folderText = new TextBlock();
            _folderText.Text = string.Empty;
            _folderText.VerticalAlignment = VerticalAlignment.Center;
            _folderText.Margin = new Thickness(16, 0, 8, 0);
            bar.Items.Add(_folderText);

            return bar;
        }

        private Grid BuildBrowseView()
        {
            Grid root = new Grid();
            _grid = new ListBox();
            _grid.ItemsSource = _photos;
            _grid.SelectionMode = SelectionMode.Extended;
            _grid.Background = Brushes.White;
            _grid.BorderThickness = new Thickness(0);
            _grid.SelectionChanged += OnGridSelectionChanged;
            _grid.MouseDoubleClick += delegate { if (_grid.SelectedItem != null) SetViewMode(true); };
            _grid.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            _grid.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            _grid.SetValue(VirtualizingPanel.IsVirtualizingProperty, false);

            FrameworkElementFactory panelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            panelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            ItemsPanelTemplate panelTemplate = new ItemsPanelTemplate(panelFactory);
            _grid.ItemsPanel = panelTemplate;
            ApplyThumbnailTemplate(_settings.ThumbnailSize);
            root.Children.Add(_grid);
            return root;
        }

        private Grid BuildViewer()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Background = new SolidColorBrush(Color.FromRgb(28, 28, 28));

            _viewerScroll = new ScrollViewer();
            _viewerScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _viewerScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _viewerScroll.Background = root.Background;
            _viewerScroll.PreviewMouseWheel += OnViewerMouseWheel;
            _viewerScroll.SizeChanged += delegate { if (_fitMode) ApplyViewerSizing(); };
            Grid.SetRow(_viewerScroll, 0);
            root.Children.Add(_viewerScroll);

            Grid imageHost = new Grid();
            imageHost.Background = root.Background;
            _viewerScroll.Content = imageHost;

            _viewerImage = new Image();
            _viewerImage.Stretch = Stretch.Uniform;
            _viewerImage.HorizontalAlignment = HorizontalAlignment.Center;
            _viewerImage.VerticalAlignment = VerticalAlignment.Center;
            imageHost.Children.Add(_viewerImage);

            _filmstripBorder = new Border();
            _filmstripBorder.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
            _filmstripBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 65));
            _filmstripBorder.BorderThickness = new Thickness(0, 1, 0, 0);
            _filmstripBorder.Padding = new Thickness(5);
            Grid.SetRow(_filmstripBorder, 1);
            root.Children.Add(_filmstripBorder);

            _filmstrip = new ListBox();
            _filmstrip.ItemsSource = _photos;
            _filmstrip.SelectionMode = SelectionMode.Single;
            _filmstrip.Height = 104;
            _filmstrip.Background = Brushes.Transparent;
            _filmstrip.BorderThickness = new Thickness(0);
            _filmstrip.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            _filmstrip.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            _filmstrip.SelectionChanged += OnFilmstripSelectionChanged;

            FrameworkElementFactory filmPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            filmPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            _filmstrip.ItemsPanel = new ItemsPanelTemplate(filmPanelFactory);
            _filmstrip.ItemTemplate = MakeFilmstripTemplate();
            _filmstripBorder.Child = _filmstrip;
            return root;
        }

        private Border BuildSidebar()
        {
            Border border = new Border();
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(205, 205, 205));
            border.BorderThickness = new Thickness(0, 0, 1, 0);
            border.Background = new SolidColorBrush(Color.FromRgb(247, 247, 247));

            DockPanel root = new DockPanel();
            border.Child = root;

            StackPanel top = new StackPanel();
            top.Margin = new Thickness(14, 12, 14, 10);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            TextBlock title = new TextBlock();
            title.Text = "Information";
            title.FontSize = 17;
            title.FontWeight = FontWeights.SemiBold;
            top.Children.Add(title);

            Button fields = new Button();
            fields.Content = "Choose EXIF fields...";
            fields.Margin = new Thickness(0, 9, 0, 0);
            fields.HorizontalAlignment = HorizontalAlignment.Left;
            fields.Padding = new Thickness(8, 3, 8, 3);
            fields.Click += OnChooseExifFields;
            top.Children.Add(fields);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            root.Children.Add(scroll);

            StackPanel sections = new StackPanel();
            sections.Margin = new Thickness(14, 0, 14, 14);
            scroll.Content = sections;

            sections.Children.Add(MakeSectionHeader("FILE"));
            _fileInfoPanel = new StackPanel();
            sections.Children.Add(_fileInfoPanel);

            sections.Children.Add(MakeSectionHeader("EXIF"));
            _exifPanel = new StackPanel();
            sections.Children.Add(_exifPanel);

            sections.Children.Add(MakeSectionHeader("ADJUSTMENTS"));
            _adjustmentPanel = new StackPanel();
            sections.Children.Add(_adjustmentPanel);

            TextBlock expLabel = new TextBlock();
            expLabel.Text = "Exposure";
            expLabel.Margin = new Thickness(0, 2, 0, 3);
            _adjustmentPanel.Children.Add(expLabel);

            _exposureSlider = new Slider();
            _exposureSlider.Minimum = -3.0;
            _exposureSlider.Maximum = 3.0;
            _exposureSlider.TickFrequency = 0.1;
            _exposureSlider.IsSnapToTickEnabled = true;
            _exposureSlider.ValueChanged += OnExposureChanged;
            _adjustmentPanel.Children.Add(_exposureSlider);

            _exposureText = new TextBlock();
            _exposureText.Text = "0.0 EV";
            _exposureText.HorizontalAlignment = HorizontalAlignment.Center;
            _exposureText.Margin = new Thickness(0, 1, 0, 5);
            _adjustmentPanel.Children.Add(_exposureText);

            Button reset = new Button();
            reset.Content = "Reset exposure";
            reset.Padding = new Thickness(8, 3, 8, 3);
            reset.HorizontalAlignment = HorizontalAlignment.Left;
            reset.Click += delegate { if (_currentIndex >= 0) _exposureSlider.Value = 0.0; };
            _adjustmentPanel.Children.Add(reset);

            return border;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            string folder = null;
            string select = null;
            bool openView = false;
            if (_startupArgs.Length > 0)
            {
                string p = _startupArgs[0].Trim('"');
                if (Directory.Exists(p)) folder = p;
                else if (File.Exists(p) && Imaging.IsSupportedImage(p))
                {
                    folder = Path.GetDirectoryName(p);
                    select = p;
                    openView = true;
                }
            }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = _settings.LastFolder;
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            LoadFolder(folder, select, openView);
        }

        private async void LoadFolder(string folder, string selectPath, bool openView)
        {
            int generation = ++_folderGeneration;
            ++_imageGeneration;
            ++_metadataGeneration;
            if (_folderCts != null) _folderCts.Cancel();
            _folderCts = new CancellationTokenSource();
            CancellationToken token = _folderCts.Token;

            _statusText.Text = "Loading " + folder + "...";
            _folderText.Text = folder;
            _currentFolder = folder;
            _settings.LastFolder = folder;
            _settings.Save();

            List<string> paths = null;
            try
            {
                paths = await Task.Run(delegate
                {
                    List<string> result = new List<string>();
                    IEnumerable<string> files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        if (token.IsCancellationRequested) return result;
                        if (Imaging.IsSupportedImage(file)) result.Add(file);
                    }
                    result.Sort(new LogicalPathComparer());
                    return result;
                }, token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _statusText.Text = "Could not open folder: " + ex.Message;
                return;
            }

            if (generation != _folderGeneration || token.IsCancellationRequested) return;

            _photos.Clear();
            ClearFullCache();
            int i;
            for (i = 0; i < paths.Count; i++)
                _photos.Add(new PhotoItem(paths[i], _editStore.GetExposure(paths[i])));

            int index = 0;
            if (!string.IsNullOrEmpty(selectPath))
            {
                for (i = 0; i < _photos.Count; i++)
                {
                    if (string.Equals(_photos[i].Path, selectPath, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (_photos.Count > 0)
            {
                SetCurrentIndex(index, false);
                if (openView) SetViewMode(true); else SetViewMode(false);
            }
            else
            {
                _currentIndex = -1;
                _viewerImage.Source = null;
                ClearInfo();
                SetViewMode(false);
            }

            _statusText.Text = _photos.Count.ToString() + " photos";
            StartThumbnailWorkers(generation, token);
        }

        private void StartThumbnailWorkers(int generation, CancellationToken token)
        {
            PhotoItem[] items = _photos.ToArray();
            int next = -1;
            int worker;
            for (worker = 0; worker < 4; worker++)
            {
                Task.Run(delegate
                {
                    while (!token.IsCancellationRequested)
                    {
                        int index = Interlocked.Increment(ref next);
                        if (index >= items.Length) break;
                        PhotoItem item = items[index];
                        BitmapSource thumb = null;
                        try { thumb = Imaging.LoadBitmap(item.Path, 360); }
                        catch { }
                        if (thumb != null && generation == _folderGeneration && !token.IsCancellationRequested)
                        {
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                if (generation == _folderGeneration && _photos.Contains(item))
                                    item.Thumbnail = thumb;
                            }), DispatcherPriority.Background);
                        }
                    }
                }, token);
            }
        }

        private void ChooseFolder()
        {
            using (Forms.FolderBrowserDialog dlg = new Forms.FolderBrowserDialog())
            {
                dlg.Description = "Choose a photo folder";
                if (!string.IsNullOrEmpty(_currentFolder) && Directory.Exists(_currentFolder)) dlg.SelectedPath = _currentFolder;
                if (dlg.ShowDialog() == Forms.DialogResult.OK)
                    LoadFolder(dlg.SelectedPath, null, false);
            }
        }

        private void SetViewMode(bool view)
        {
            _viewMode = view && _currentIndex >= 0 && _currentIndex < _photos.Count;
            _browseView.Visibility = _viewMode ? Visibility.Collapsed : Visibility.Visible;
            _viewRoot.Visibility = _viewMode ? Visibility.Visible : Visibility.Collapsed;
            _browseModeButton.FontWeight = _viewMode ? FontWeights.Normal : FontWeights.Bold;
            _viewModeButton.FontWeight = _viewMode ? FontWeights.Bold : FontWeights.Normal;

            if (_viewMode)
            {
                SetCurrentIndex(_currentIndex, true);
                _viewerImage.Focus();
            }
            else
            {
                if (_currentIndex >= 0 && _currentIndex < _photos.Count)
                {
                    _updatingSelection = true;
                    _grid.SelectedItem = _photos[_currentIndex];
                    _grid.ScrollIntoView(_photos[_currentIndex]);
                    _updatingSelection = false;
                }
                _grid.Focus();
            }
        }

        private void SetCurrentIndex(int index, bool loadViewer)
        {
            if (_photos.Count == 0) { _currentIndex = -1; return; }
            index = Math.Max(0, Math.Min(_photos.Count - 1, index));
            _currentIndex = index;
            PhotoItem current = _photos[index];

            _updatingSelection = true;
            _grid.SelectedItem = current;
            _filmstrip.SelectedItem = current;
            _updatingSelection = false;
            if (_viewMode) _filmstrip.ScrollIntoView(current);

            _updatingExposureUi = true;
            _exposureSlider.Value = current.Exposure;
            _exposureText.Text = current.Exposure.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " EV";
            _updatingExposureUi = false;

            RefreshInfo(current);
            if (loadViewer || _viewMode) LoadCurrentImage(false);
        }

        private void Navigate(int delta)
        {
            if (_photos.Count == 0) return;
            int next = Math.Max(0, Math.Min(_photos.Count - 1, _currentIndex + delta));
            if (next != _currentIndex) SetCurrentIndex(next, true);
        }

        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelection) return;
            PhotoItem item = _grid.SelectedItem as PhotoItem;
            if (item == null) return;
            int index = _photos.IndexOf(item);
            if (index >= 0)
            {
                _currentIndex = index;
                _updatingSelection = true;
                _filmstrip.SelectedItem = item;
                _updatingSelection = false;
                _updatingExposureUi = true;
                _exposureSlider.Value = item.Exposure;
                _exposureText.Text = item.Exposure.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " EV";
                _updatingExposureUi = false;
                RefreshInfo(item);
            }
        }

        private void OnFilmstripSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelection) return;
            PhotoItem item = _filmstrip.SelectedItem as PhotoItem;
            if (item == null) return;
            int index = _photos.IndexOf(item);
            if (index >= 0) SetCurrentIndex(index, true);
        }

        private async void LoadCurrentImage(bool forceExposureRender)
        {
            if (_currentIndex < 0 || _currentIndex >= _photos.Count) return;
            PhotoItem item = _photos[_currentIndex];
            int generation = ++_imageGeneration;
            string path = item.Path;
            double exposure = item.Exposure;
            _statusText.Text = Path.GetFileName(path);

            BitmapSource bitmap = null;
            if (Math.Abs(exposure) < 0.00001 && !forceExposureRender)
                bitmap = TryGetCached(path);

            if (bitmap == null)
            {
                await _currentDecodeGate.WaitAsync();
                try
                {
                    if (generation != _imageGeneration) return;
                    try
                    {
                        bitmap = await Task.Run(delegate
                        {
                            if (Math.Abs(exposure) < 0.00001)
                                return Imaging.LoadBitmap(path, 0);
                            return Imaging.LoadPreviewWithExposure(path, exposure, 4096);
                        });
                    }
                    catch (Exception ex)
                    {
                        if (generation == _imageGeneration) _statusText.Text = "Cannot display image: " + ex.Message;
                        return;
                    }

                    if (Math.Abs(exposure) < 0.00001) PutCached(path, bitmap);
                }
                finally { _currentDecodeGate.Release(); }
            }

            if (generation != _imageGeneration || _currentIndex < 0 || _currentIndex >= _photos.Count || !string.Equals(_photos[_currentIndex].Path, path, StringComparison.OrdinalIgnoreCase))
                return;

            _viewerImage.Source = bitmap;
            _fitMode = true;
            _zoom = 1.0;
            ApplyViewerSizing();
            UpdateDimensions(bitmap);
            PreloadNeighbors();
        }

        private void PreloadNeighbors()
        {
            int folderGeneration = _folderGeneration;
            int[] offsets = new int[] { -2, -1, 1, 2 };
            int i;
            for (i = 0; i < offsets.Length; i++)
            {
                int idx = _currentIndex + offsets[i];
                if (idx < 0 || idx >= _photos.Count) continue;
                PhotoItem item = _photos[idx];
                if (Math.Abs(item.Exposure) > 0.00001 || TryGetCached(item.Path) != null) continue;
                string path = item.Path;
                Task.Run(delegate
                {
                    _preloadDecodeGate.Wait();
                    try
                    {
                        if (folderGeneration != _folderGeneration) return;
                        if (TryGetCached(path) != null) return;
                        BitmapSource b = Imaging.LoadBitmap(path, 0);
                        if (folderGeneration == _folderGeneration) PutCached(path, b);
                    }
                    catch { }
                    finally { _preloadDecodeGate.Release(); }
                });
            }
        }

        private BitmapSource TryGetCached(string path)
        {
            lock (_cacheSync)
            {
                BitmapSource value;
                if (!_fullCache.TryGetValue(path, out value)) return null;
                LinkedListNode<string> node = _fullCacheLru.Find(path);
                if (node != null) { _fullCacheLru.Remove(node); _fullCacheLru.AddFirst(node); }
                return value;
            }
        }

        private void PutCached(string path, BitmapSource value)
        {
            if (value == null) return;
            lock (_cacheSync)
            {
                _fullCache[path] = value;
                LinkedListNode<string> node = _fullCacheLru.Find(path);
                if (node != null) _fullCacheLru.Remove(node);
                _fullCacheLru.AddFirst(path);
                while (_fullCacheLru.Count > FullCacheLimit)
                {
                    string last = _fullCacheLru.Last.Value;
                    _fullCacheLru.RemoveLast();
                    _fullCache.Remove(last);
                }
            }
        }

        private void RemoveFromCache(string path)
        {
            lock (_cacheSync)
            {
                _fullCache.Remove(path);
                LinkedListNode<string> node = _fullCacheLru.Find(path);
                if (node != null) _fullCacheLru.Remove(node);
            }
        }

        private void ClearFullCache()
        {
            lock (_cacheSync)
            {
                _fullCache.Clear();
                _fullCacheLru.Clear();
            }
        }

        private void RefreshInfo(PhotoItem item)
        {
            _fileInfoPanel.Children.Clear();
            _exifPanel.Children.Clear();
            if (item == null) return;
            try
            {
                FileInfo fi = new FileInfo(item.Path);
                AddInfo(_fileInfoPanel, "Name", fi.Name);
                AddInfo(_fileInfoPanel, "Size", FormatBytes(fi.Length));
                AddInfo(_fileInfoPanel, "Modified", fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch { }

            string path = item.Path;
            int metadataGeneration = ++_metadataGeneration;
            HashSet<string> visible = _settings.GetVisibleExifSet();
            Task.Run(delegate
            {
                _metadataGate.Wait();
                try
                {
                    if (metadataGeneration != _metadataGeneration) return;
                    int pixelWidth;
                    int pixelHeight;
                    bool hasDimensions = Imaging.TryReadPixelDimensions(path, out pixelWidth, out pixelHeight);
                    List<ExifValue> values = Imaging.ReadExif(path);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (metadataGeneration != _metadataGeneration) return;
                        if (_currentIndex < 0 || _currentIndex >= _photos.Count || !string.Equals(_photos[_currentIndex].Path, path, StringComparison.OrdinalIgnoreCase)) return;
                        if (hasDimensions) AddOrReplaceFileInfo("Dimensions", pixelWidth.ToString() + " × " + pixelHeight.ToString());
                        _exifPanel.Children.Clear();
                        int i;
                        for (i = 0; i < _exifFields.Count; i++)
                        {
                            if (!visible.Contains(_exifFields[i].Key)) continue;
                            int j;
                            for (j = 0; j < values.Count; j++)
                            {
                                if (string.Equals(values[j].Key, _exifFields[i].Key, StringComparison.OrdinalIgnoreCase))
                                {
                                    AddInfo(_exifPanel, values[j].Label, values[j].Value);
                                    break;
                                }
                            }
                        }
                        if (_exifPanel.Children.Count == 0)
                        {
                            TextBlock none = new TextBlock();
                            none.Text = "No selected EXIF fields available";
                            none.Foreground = Brushes.Gray;
                            none.TextWrapping = TextWrapping.Wrap;
                            _exifPanel.Children.Add(none);
                        }
                    }), DispatcherPriority.Background);
                }
                finally { _metadataGate.Release(); }
            });
        }

        private void UpdateDimensions(BitmapSource bitmap)
        {
            if (bitmap == null) return;
            AddOrReplaceFileInfo("Dimensions", bitmap.PixelWidth.ToString() + " × " + bitmap.PixelHeight.ToString());
        }

        private void AddOrReplaceFileInfo(string label, string value)
        {
            int i;
            for (i = 0; i < _fileInfoPanel.Children.Count; i++)
            {
                FrameworkElement element = _fileInfoPanel.Children[i] as FrameworkElement;
                if (element != null && Convert.ToString(element.Tag) == label)
                {
                    _fileInfoPanel.Children.RemoveAt(i);
                    _fileInfoPanel.Children.Insert(i, MakeInfoRow(label, value));
                    return;
                }
            }
            _fileInfoPanel.Children.Add(MakeInfoRow(label, value));
        }

        private void ClearInfo()
        {
            _fileInfoPanel.Children.Clear();
            _exifPanel.Children.Clear();
            _updatingExposureUi = true;
            _exposureSlider.Value = 0;
            _exposureText.Text = "0.0 EV";
            _updatingExposureUi = false;
        }

        private void OnExposureChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingExposureUi || _currentIndex < 0 || _currentIndex >= _photos.Count) return;
            double value = Math.Round(_exposureSlider.Value * 10.0) / 10.0;
            PhotoItem item = _photos[_currentIndex];
            item.Exposure = value;
            _exposureText.Text = value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " EV";
            _editStore.SetExposure(item.Path, value);
            RemoveFromCache(item.Path);
            _exposureDebounce.Stop();
            _exposureDebounce.Start();
        }

        private void OnChooseExifFields(object sender, RoutedEventArgs e)
        {
            ExifSettingsWindow w = new ExifSettingsWindow(this, _settings, _exifFields);
            if (w.ShowDialog() == true && _currentIndex >= 0 && _currentIndex < _photos.Count)
                RefreshInfo(_photos[_currentIndex]);
        }

        private void DeleteSelection()
        {
            List<PhotoItem> targets = new List<PhotoItem>();
            if (_viewMode)
            {
                if (_currentIndex >= 0 && _currentIndex < _photos.Count) targets.Add(_photos[_currentIndex]);
            }
            else
            {
                foreach (object selected in _grid.SelectedItems)
                {
                    PhotoItem p = selected as PhotoItem;
                    if (p != null) targets.Add(p);
                }
                if (targets.Count == 0 && _currentIndex >= 0 && _currentIndex < _photos.Count) targets.Add(_photos[_currentIndex]);
            }
            if (targets.Count == 0) return;

            int anchor = _currentIndex;
            DeleteBatch batch = new DeleteBatch();
            int success = 0;
            int i;
            for (i = 0; i < targets.Count; i++)
            {
                PhotoItem item = targets[i];
                if (ShellHelpers.SendToRecycleBin(item.Path))
                {
                    batch.Paths.Add(item.Path);
                    success++;
                    RemoveFromCache(item.Path);
                }
            }
            if (success == 0)
            {
                _statusText.Text = "Delete failed.";
                return;
            }

            for (i = 0; i < targets.Count; i++)
            {
                if (batch.Paths.Contains(targets[i].Path)) _photos.Remove(targets[i]);
            }
            _lastDeleteBatch = batch;

            if (_photos.Count == 0)
            {
                _currentIndex = -1;
                _viewerImage.Source = null;
                ClearInfo();
                SetViewMode(false);
            }
            else
            {
                anchor = Math.Max(0, Math.Min(anchor, _photos.Count - 1));
                SetCurrentIndex(anchor, _viewMode);
            }
            _statusText.Text = success.ToString() + (success == 1 ? " photo moved" : " photos moved") + " to Recycle Bin. Ctrl+Z restores the last batch.";
        }

        private void UndoDelete()
        {
            DeleteBatch batch = _lastDeleteBatch;
            if (batch == null || batch.Paths.Count == 0)
            {
                _statusText.Text = "Nothing to undo.";
                return;
            }
            _lastDeleteBatch = null;
            _statusText.Text = "Restoring from Recycle Bin...";

            Thread t = new Thread(new ThreadStart(delegate
            {
                int restored = ShellHelpers.RestoreBatch(batch);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _statusText.Text = restored.ToString() + (restored == 1 ? " photo restored." : " photos restored.");
                    if (!string.IsNullOrEmpty(_currentFolder) && Directory.Exists(_currentFolder)) LoadFolder(_currentFolder, restored > 0 ? batch.Paths[0] : null, _viewMode);
                }));
            }));
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void ExportSelected()
        {
            if (string.IsNullOrEmpty(_currentFolder) || _photos.Count == 0) return;
            List<PhotoItem> targets = new List<PhotoItem>();
            if (!_viewMode && _grid.SelectedItems.Count > 0)
            {
                foreach (object o in _grid.SelectedItems)
                {
                    PhotoItem p = o as PhotoItem;
                    if (p != null) targets.Add(p);
                }
            }
            else if (_currentIndex >= 0 && _currentIndex < _photos.Count)
            {
                targets.Add(_photos[_currentIndex]);
            }
            if (targets.Count == 0) return;

            ExportWindow dialog = new ExportWindow(this, _settings, _currentFolder, targets.Count);
            if (dialog.ShowDialog() != true || dialog.Options == null) return;
            ExportOptions options = dialog.Options;

            try { Directory.CreateDirectory(options.OutputFolder); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Cannot create output folder.\n\n" + ex.Message, "Lumen", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _statusText.Text = "Exporting 0 / " + targets.Count.ToString() + "...";
            Task.Run(delegate
            {
                int success = 0;
                List<string> failures = new List<string>();
                int i;
                for (i = 0; i < targets.Count; i++)
                {
                    PhotoItem item = targets[i];
                    try
                    {
                        string output = Imaging.GetUniqueOutputPath(options.OutputFolder, item.Path);
                        Imaging.ExportImage(item.Path, output, item.Exposure, options);
                        success++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add(item.FileName + ": " + ex.Message);
                    }
                    int done = i + 1;
                    Dispatcher.BeginInvoke(new Action(delegate { _statusText.Text = "Exporting " + done.ToString() + " / " + targets.Count.ToString() + "..."; }));
                }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _statusText.Text = "Export complete: " + success.ToString() + " / " + targets.Count.ToString() + ".";
                    if (failures.Count > 0)
                    {
                        string text = string.Join("\n", failures.Take(8).ToArray());
                        if (failures.Count > 8) text += "\n...and " + (failures.Count - 8).ToString() + " more.";
                        MessageBox.Show(this, "Some files could not be exported:\n\n" + text, "Lumen", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (e.Key == Key.F11)
            {
                ToggleFullScreen(); e.Handled = true; return;
            }
            if (ctrl && e.Key == Key.E)
            {
                ExportSelected(); e.Handled = true; return;
            }
            if (ctrl && e.Key == Key.Z)
            {
                UndoDelete(); e.Handled = true; return;
            }
            if (ctrl && e.Key == Key.A && !_viewMode)
            {
                _grid.SelectAll(); e.Handled = true; return;
            }
            if (e.Key == Key.Delete)
            {
                DeleteSelection(); e.Handled = true; return;
            }
            if (e.Key == Key.Escape)
            {
                if (_fullScreen) ToggleFullScreen();
                else if (_viewMode) SetViewMode(false);
                else _grid.UnselectAll();
                e.Handled = true; return;
            }
            if (e.Key == Key.Enter && !_viewMode && _currentIndex >= 0)
            {
                SetViewMode(true); e.Handled = true; return;
            }
            if (e.Key == Key.Space)
            {
                SetViewMode(!_viewMode); e.Handled = true; return;
            }
            if (_viewMode && e.Key == Key.Right)
            {
                Navigate(1); e.Handled = true; return;
            }
            if (_viewMode && e.Key == Key.Left)
            {
                Navigate(-1); e.Handled = true; return;
            }
            if (_viewMode && e.Key == Key.OemOpenBrackets)
            {
                _exposureSlider.Value = Math.Max(-3.0, Math.Round((_exposureSlider.Value - 0.1) * 10.0) / 10.0); e.Handled = true; return;
            }
            if (_viewMode && e.Key == Key.OemCloseBrackets)
            {
                _exposureSlider.Value = Math.Min(3.0, Math.Round((_exposureSlider.Value + 0.1) * 10.0) / 10.0); e.Handled = true; return;
            }
        }

        private void OnViewerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            _fitMode = false;
            double factor = e.Delta > 0 ? 1.12 : 1.0 / 1.12;
            _zoom = Math.Max(0.05, Math.Min(8.0, _zoom * factor));
            ApplyViewerSizing();
            e.Handled = true;
        }

        private void ApplyViewerSizing()
        {
            BitmapSource bitmap = _viewerImage.Source as BitmapSource;
            if (bitmap == null) return;
            if (_fitMode)
            {
                double width = Math.Max(10, _viewerScroll.ViewportWidth - 4);
                double height = Math.Max(10, _viewerScroll.ViewportHeight - 4);
                if (double.IsNaN(width) || double.IsInfinity(width) || width <= 10) width = Math.Max(10, ActualWidth - 320);
                if (double.IsNaN(height) || double.IsInfinity(height) || height <= 10) height = Math.Max(10, ActualHeight - 200);
                _viewerImage.Width = width;
                _viewerImage.Height = height;
                _viewerImage.Stretch = Stretch.Uniform;
            }
            else
            {
                double dpiX = bitmap.DpiX > 1 ? bitmap.DpiX : 96.0;
                double dpiY = bitmap.DpiY > 1 ? bitmap.DpiY : 96.0;
                _viewerImage.Stretch = Stretch.Fill;
                _viewerImage.Width = bitmap.PixelWidth * 96.0 / dpiX * _zoom;
                _viewerImage.Height = bitmap.PixelHeight * 96.0 / dpiY * _zoom;
            }
        }

        private void ToggleSidebar()
        {
            bool visible = _sidebarBorder.Visibility == Visibility.Visible;
            if (visible)
            {
                _settings.SidebarWidth = _sidebarColumn.ActualWidth > 20 ? _sidebarColumn.ActualWidth : _settings.SidebarWidth;
                _sidebarBorder.Visibility = Visibility.Collapsed;
                _sidebarSplitter.Visibility = Visibility.Collapsed;
                _sidebarColumn.Width = new GridLength(0);
                _splitterColumn.Width = new GridLength(0);
                _settings.SidebarVisible = false;
            }
            else
            {
                _sidebarBorder.Visibility = Visibility.Visible;
                _sidebarSplitter.Visibility = Visibility.Visible;
                _sidebarColumn.Width = new GridLength(_settings.SidebarWidth);
                _splitterColumn.Width = new GridLength(5);
                _settings.SidebarVisible = true;
            }
            _settings.Save();
        }

        private void ToggleFullScreen()
        {
            if (!_fullScreen)
            {
                _savedWindowStyle = WindowStyle;
                _savedResizeMode = ResizeMode;
                _savedWindowState = WindowState;
                _savedSidebarWidth = _sidebarColumn.Width;
                _fullScreen = true;
                _toolbar.Visibility = Visibility.Collapsed;
                _statusBorder.Visibility = Visibility.Collapsed;
                _sidebarBorder.Visibility = Visibility.Collapsed;
                _sidebarSplitter.Visibility = Visibility.Collapsed;
                _sidebarColumn.Width = new GridLength(0);
                _splitterColumn.Width = new GridLength(0);
                if (_viewMode) _filmstripBorder.Visibility = Visibility.Collapsed;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
            else
            {
                _fullScreen = false;
                WindowStyle = _savedWindowStyle;
                ResizeMode = _savedResizeMode;
                WindowState = _savedWindowState;
                _toolbar.Visibility = Visibility.Visible;
                _statusBorder.Visibility = Visibility.Visible;
                if (_settings.SidebarVisible)
                {
                    _sidebarBorder.Visibility = Visibility.Visible;
                    _sidebarSplitter.Visibility = Visibility.Visible;
                    _sidebarColumn.Width = _savedSidebarWidth.Value > 0 ? _savedSidebarWidth : new GridLength(_settings.SidebarWidth);
                    _splitterColumn.Width = new GridLength(5);
                }
                if (_viewMode) _filmstripBorder.Visibility = Visibility.Visible;
            }
            if (_viewMode) Dispatcher.BeginInvoke(new Action(ApplyViewerSizing), DispatcherPriority.Loaded);
        }

        private void OnThumbnailSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_grid == null) return;
            int size = (int)Math.Round(_thumbnailSlider.Value);
            ApplyThumbnailTemplate(size);
            _settings.ThumbnailSize = size;
            _settings.Save();
        }

        private void ApplyThumbnailTemplate(int size)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, (double)size + 24.0);
            border.SetValue(Border.HeightProperty, (double)size + 52.0);
            border.SetValue(Border.MarginProperty, new Thickness(5));
            border.SetValue(Border.PaddingProperty, new Thickness(5));

            FrameworkElementFactory panel = new FrameworkElementFactory(typeof(StackPanel));
            border.AppendChild(panel);

            FrameworkElementFactory image = new FrameworkElementFactory(typeof(Image));
            image.SetValue(Image.WidthProperty, (double)size);
            image.SetValue(Image.HeightProperty, (double)size);
            image.SetValue(Image.StretchProperty, Stretch.Uniform);
            image.SetValue(Image.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            image.SetBinding(Image.SourceProperty, new Binding("Thumbnail"));
            panel.AppendChild(image);

            FrameworkElementFactory name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            name.SetValue(TextBlock.MarginProperty, new Thickness(2, 6, 2, 0));
            name.SetBinding(TextBlock.TextProperty, new Binding("FileName"));
            panel.AppendChild(name);

            DataTemplate template = new DataTemplate(typeof(PhotoItem));
            template.VisualTree = border;
            _grid.ItemTemplate = template;
        }

        private static DataTemplate MakeFilmstripTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, 104.0);
            border.SetValue(Border.HeightProperty, 86.0);
            border.SetValue(Border.MarginProperty, new Thickness(2));
            border.SetValue(Border.PaddingProperty, new Thickness(3));

            FrameworkElementFactory image = new FrameworkElementFactory(typeof(Image));
            image.SetValue(Image.WidthProperty, 96.0);
            image.SetValue(Image.HeightProperty, 78.0);
            image.SetValue(Image.StretchProperty, Stretch.Uniform);
            image.SetBinding(Image.SourceProperty, new Binding("Thumbnail"));
            border.AppendChild(image);

            DataTemplate template = new DataTemplate(typeof(PhotoItem));
            template.VisualTree = border;
            return template;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;
            string p = paths[0];
            if (Directory.Exists(p)) LoadFolder(p, null, false);
            else if (File.Exists(p) && Imaging.IsSupportedImage(p)) LoadFolder(Path.GetDirectoryName(p), p, true);
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_folderCts != null) _folderCts.Cancel();
            if (WindowState == WindowState.Normal)
            {
                _settings.WindowWidth = ActualWidth;
                _settings.WindowHeight = ActualHeight;
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
            _settings.WindowMaximized = WindowState == WindowState.Maximized;
            if (_sidebarBorder.Visibility == Visibility.Visible && _sidebarColumn.ActualWidth > 20)
                _settings.SidebarWidth = _sidebarColumn.ActualWidth;
            _settings.Save();
        }

        private static Button MakeButton(string text, string toolTip)
        {
            Button b = new Button();
            b.Content = text;
            b.ToolTip = toolTip;
            b.Padding = new Thickness(8, 3, 8, 3);
            b.Margin = new Thickness(1, 0, 1, 0);
            return b;
        }

        private static TextBlock MakeSectionHeader(string text)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = 11;
            t.FontWeight = FontWeights.Bold;
            t.Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90));
            t.Margin = new Thickness(0, 12, 0, 6);
            return t;
        }

        private static void AddInfo(StackPanel panel, string label, string value)
        {
            panel.Children.Add(MakeInfoRow(label, value));
        }

        private static FrameworkElement MakeInfoRow(string label, string value)
        {
            StackPanel row = new StackPanel();
            row.Margin = new Thickness(0, 0, 0, 7);
            row.Tag = label;

            TextBlock l = new TextBlock();
            l.Text = label;
            l.FontSize = 11;
            l.Foreground = Brushes.Gray;
            row.Children.Add(l);

            TextBlock v = new TextBlock();
            v.Text = value ?? string.Empty;
            v.TextWrapping = TextWrapping.Wrap;
            row.Children.Add(v);
            return row;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") + " GB";
            if (bytes >= 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("0.0") + " KB";
            return bytes.ToString() + " B";
        }

        private static List<ExifFieldDefinition> CreateExifFields()
        {
            List<ExifFieldDefinition> result = new List<ExifFieldDefinition>();
            result.Add(new ExifFieldDefinition("Camera", "Camera"));
            result.Add(new ExifFieldDefinition("Lens", "Lens"));
            result.Add(new ExifFieldDefinition("DateTaken", "Date taken"));
            result.Add(new ExifFieldDefinition("ExposureTime", "Shutter speed"));
            result.Add(new ExifFieldDefinition("FNumber", "Aperture"));
            result.Add(new ExifFieldDefinition("ISO", "ISO"));
            result.Add(new ExifFieldDefinition("FocalLength", "Focal length"));
            result.Add(new ExifFieldDefinition("ExposureBias", "Exposure bias"));
            result.Add(new ExifFieldDefinition("ColorSpace", "Color space"));
            result.Add(new ExifFieldDefinition("WhiteBalance", "White balance"));
            result.Add(new ExifFieldDefinition("Saturation", "Saturation"));
            result.Add(new ExifFieldDefinition("DynamicRange", "Dynamic range"));
            result.Add(new ExifFieldDefinition("Quality", "Quality"));
            result.Add(new ExifFieldDefinition("Sharpness", "Sharpness"));
            result.Add(new ExifFieldDefinition("FilmMode", "Film mode"));
            result.Add(new ExifFieldDefinition("Flash", "Flash"));
            result.Add(new ExifFieldDefinition("Software", "Software"));
            return result;
        }

        private sealed class LogicalPathComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(Path.GetFileName(x), Path.GetFileName(y));
            }

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string psz1, string psz2);
        }
    }
}
