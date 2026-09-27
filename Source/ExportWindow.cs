using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace LumenApp
{
    internal sealed class ExportWindow : Window
    {
        private readonly TextBox _folder;
        private readonly TextBox _longEdge;
        private readonly TextBox _quality;
        private readonly CheckBox _neverUpscale;
        private readonly AppSettings _settings;

        public ExportWindow(Window owner, AppSettings settings, string currentFolder, int photoCount)
        {
            _settings = settings;
            Owner = owner;
            Title = "Export " + photoCount.ToString() + (photoCount == 1 ? " photo" : " photos");
            Width = 520;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            Grid root = new Grid();
            root.Margin = new Thickness(18);
            Content = root;
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int r;
            for (r = 0; r < 6; r++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            AddLabel(root, "Output folder", 0);
            _folder = new TextBox();
            _folder.Text = Path.Combine(currentFolder, "Lumen Export");
            _folder.Margin = new Thickness(0, 4, 8, 8);
            Grid.SetRow(_folder, 0); Grid.SetColumn(_folder, 1);
            root.Children.Add(_folder);

            Button browse = new Button();
            browse.Content = "Browse...";
            browse.Padding = new Thickness(12, 4, 12, 4);
            browse.Margin = new Thickness(0, 4, 0, 8);
            browse.Click += OnBrowse;
            Grid.SetRow(browse, 0); Grid.SetColumn(browse, 2);
            root.Children.Add(browse);

            AddLabel(root, "Long edge (px)", 1);
            _longEdge = new TextBox();
            _longEdge.Text = settings.ExportLongEdge.ToString();
            _longEdge.Width = 100;
            _longEdge.HorizontalAlignment = HorizontalAlignment.Left;
            _longEdge.Margin = new Thickness(0, 4, 0, 8);
            Grid.SetRow(_longEdge, 1); Grid.SetColumn(_longEdge, 1);
            root.Children.Add(_longEdge);

            AddLabel(root, "JPEG quality", 2);
            _quality = new TextBox();
            _quality.Text = settings.JpegQuality.ToString();
            _quality.Width = 100;
            _quality.HorizontalAlignment = HorizontalAlignment.Left;
            _quality.Margin = new Thickness(0, 4, 0, 8);
            Grid.SetRow(_quality, 2); Grid.SetColumn(_quality, 1);
            root.Children.Add(_quality);

            _neverUpscale = new CheckBox();
            _neverUpscale.Content = "Never enlarge smaller images";
            _neverUpscale.IsChecked = true;
            _neverUpscale.Margin = new Thickness(0, 6, 0, 10);
            Grid.SetRow(_neverUpscale, 3); Grid.SetColumn(_neverUpscale, 1); Grid.SetColumnSpan(_neverUpscale, 2);
            root.Children.Add(_neverUpscale);

            TextBlock note = new TextBlock();
            note.Text = "Originals are never modified. Exposure + resize are rendered from the original and encoded once. Resizing itself is not mathematically lossless; JPEG quality 100 minimizes added JPEG loss but increases file size.";
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 8, 0, 8);
            Grid.SetRow(note, 4); Grid.SetColumn(note, 0); Grid.SetColumnSpan(note, 3);
            root.Children.Add(note);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(actions, 7); Grid.SetColumn(actions, 0); Grid.SetColumnSpan(actions, 3);
            root.Children.Add(actions);

            Button cancel = new Button();
            cancel.Content = "Cancel";
            cancel.MinWidth = 86;
            cancel.Margin = new Thickness(6, 0, 0, 0);
            cancel.Click += delegate { DialogResult = false; };
            actions.Children.Add(cancel);

            Button export = new Button();
            export.Content = "Export";
            export.MinWidth = 86;
            export.Margin = new Thickness(6, 0, 0, 0);
            export.IsDefault = true;
            export.Click += OnExport;
            actions.Children.Add(export);
        }

        public ExportOptions Options { get; private set; }

        private static void AddLabel(Grid root, string text, int row)
        {
            TextBlock label = new TextBlock();
            label.Text = text;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 4, 12, 8);
            Grid.SetRow(label, row); Grid.SetColumn(label, 0);
            root.Children.Add(label);
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            using (Forms.FolderBrowserDialog dlg = new Forms.FolderBrowserDialog())
            {
                dlg.Description = "Choose export folder";
                dlg.SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : Path.GetDirectoryName(_folder.Text);
                if (dlg.ShowDialog() == Forms.DialogResult.OK)
                    _folder.Text = dlg.SelectedPath;
            }
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            int edge;
            int quality;
            if (!int.TryParse(_longEdge.Text, out edge) || edge < 320 || edge > 20000)
            {
                MessageBox.Show(this, "Long edge must be between 320 and 20000 pixels.", "Lumen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(_quality.Text, out quality) || quality < 70 || quality > 100)
            {
                MessageBox.Show(this, "JPEG quality must be between 70 and 100.", "Lumen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_folder.Text))
            {
                MessageBox.Show(this, "Choose an output folder.", "Lumen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Options = new ExportOptions();
            Options.OutputFolder = _folder.Text.Trim();
            Options.LongEdge = edge;
            Options.JpegQuality = quality;
            Options.NeverUpscale = _neverUpscale.IsChecked == true;
            _settings.ExportLongEdge = edge;
            _settings.JpegQuality = quality;
            _settings.Save();
            DialogResult = true;
        }
    }
}
