using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LumenApp
{
    internal sealed class ExifSettingsWindow : Window
    {
        private readonly Dictionary<string, CheckBox> _boxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        private readonly AppSettings _settings;

        public ExifSettingsWindow(Window owner, AppSettings settings, IList<ExifFieldDefinition> fields)
        {
            _settings = settings;
            Owner = owner;
            Title = "EXIF fields";
            Width = 360;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            DockPanel root = new DockPanel();
            Content = root;

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(10);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            Button cancel = new Button();
            cancel.Content = "Cancel";
            cancel.MinWidth = 80;
            cancel.Margin = new Thickness(6, 0, 0, 0);
            cancel.Click += delegate { DialogResult = false; };
            buttons.Children.Add(cancel);

            Button ok = new Button();
            ok.Content = "OK";
            ok.MinWidth = 80;
            ok.Margin = new Thickness(6, 0, 0, 0);
            ok.IsDefault = true;
            ok.Click += OnOk;
            buttons.Children.Add(ok);

            TextBlock help = new TextBlock();
            help.Text = "Choose the metadata shown in the left Information pane.";
            help.TextWrapping = TextWrapping.Wrap;
            help.Margin = new Thickness(16, 14, 16, 8);
            DockPanel.SetDock(help, Dock.Top);
            root.Children.Add(help);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            root.Children.Add(scroll);

            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(16, 4, 16, 8);
            scroll.Content = panel;

            HashSet<string> visible = settings.GetVisibleExifSet();
            int i;
            for (i = 0; i < fields.Count; i++)
            {
                ExifFieldDefinition field = fields[i];
                CheckBox cb = new CheckBox();
                cb.Content = field.Label;
                cb.IsChecked = visible.Contains(field.Key);
                cb.Margin = new Thickness(0, 5, 0, 5);
                panel.Children.Add(cb);
                _boxes[field.Key] = cb;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, CheckBox> kv in _boxes)
            {
                if (kv.Value.IsChecked == true) keys.Add(kv.Key);
            }
            _settings.VisibleExifKeys = string.Join(",", keys.ToArray());
            _settings.Save();
            DialogResult = true;
        }
    }
}
