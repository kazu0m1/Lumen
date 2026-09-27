using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LumenApp
{
    internal sealed class AppSettings
    {
        private readonly string _path;

        public AppSettings(string baseDir)
        {
            DataDirectory = System.IO.Path.Combine(baseDir, "Data");
            _path = System.IO.Path.Combine(DataDirectory, "LumenSettings.ini");
            ThumbnailSize = 220;
            SidebarWidth = 250;
            SidebarVisible = true;
            WindowWidth = 1280;
            WindowHeight = 820;
            WindowLeft = double.NaN;
            WindowTop = double.NaN;
            WindowMaximized = false;
            VisibleExifKeys = "Camera,Lens,DateTaken,ExposureTime,FNumber,ISO,FocalLength,ExposureBias,ColorSpace,WhiteBalance,Saturation,DynamicRange,Quality,Sharpness,FilmMode";
            LastFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            JpegQuality = 100;
            ExportLongEdge = 3840;
            Load();
        }

        public string DataDirectory { get; private set; }
        public int ThumbnailSize { get; set; }
        public double SidebarWidth { get; set; }
        public bool SidebarVisible { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public bool WindowMaximized { get; set; }
        public string VisibleExifKeys { get; set; }
        public string LastFolder { get; set; }
        public int JpegQuality { get; set; }
        public int ExportLongEdge { get; set; }

        public HashSet<string> GetVisibleExifSet()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] parts = (VisibleExifKeys ?? string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int i;
            for (i = 0; i < parts.Length; i++)
                result.Add(parts[i].Trim());
            return result;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                StringBuilder sb = new StringBuilder();
                Append(sb, "ThumbnailSize", ThumbnailSize.ToString(CultureInfo.InvariantCulture));
                Append(sb, "SidebarWidth", SidebarWidth.ToString(CultureInfo.InvariantCulture));
                Append(sb, "SidebarVisible", SidebarVisible ? "true" : "false");
                Append(sb, "WindowWidth", WindowWidth.ToString(CultureInfo.InvariantCulture));
                Append(sb, "WindowHeight", WindowHeight.ToString(CultureInfo.InvariantCulture));
                Append(sb, "WindowLeft", double.IsNaN(WindowLeft) ? "NaN" : WindowLeft.ToString(CultureInfo.InvariantCulture));
                Append(sb, "WindowTop", double.IsNaN(WindowTop) ? "NaN" : WindowTop.ToString(CultureInfo.InvariantCulture));
                Append(sb, "WindowMaximized", WindowMaximized ? "true" : "false");
                Append(sb, "VisibleExifKeys", VisibleExifKeys ?? string.Empty);
                Append(sb, "LastFolder", Encode(LastFolder ?? string.Empty));
                Append(sb, "JpegQuality", JpegQuality.ToString(CultureInfo.InvariantCulture));
                Append(sb, "ExportLongEdge", ExportLongEdge.ToString(CultureInfo.InvariantCulture));

                string temp = _path + ".tmp";
                File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    string backup = _path + ".bak";
                    try { File.Replace(temp, _path, backup, true); }
                    catch
                    {
                        File.Copy(temp, _path, true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, _path);
                }
            }
            catch
            {
                // Preferences must never prevent the application from closing.
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return;
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                int i;
                for (i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int p = line.IndexOf('=');
                    if (p <= 0)
                        continue;
                    string key = line.Substring(0, p).Trim();
                    string value = line.Substring(p + 1);
                    Apply(key, value);
                }
            }
            catch
            {
                // Corrupt settings fall back to defaults.
            }
        }

        private void Apply(string key, string value)
        {
            int iv;
            double dv;
            bool bv;
            if (key == "ThumbnailSize" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv)) ThumbnailSize = Clamp(iv, 120, 360);
            else if (key == "SidebarWidth" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) SidebarWidth = Math.Max(180, Math.Min(500, dv));
            else if (key == "SidebarVisible" && bool.TryParse(value, out bv)) SidebarVisible = bv;
            else if (key == "WindowWidth" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) WindowWidth = Math.Max(800, dv);
            else if (key == "WindowHeight" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) WindowHeight = Math.Max(600, dv);
            else if (key == "WindowLeft") WindowLeft = ParseDoubleOrNaN(value);
            else if (key == "WindowTop") WindowTop = ParseDoubleOrNaN(value);
            else if (key == "WindowMaximized" && bool.TryParse(value, out bv)) WindowMaximized = bv;
            else if (key == "VisibleExifKeys") VisibleExifKeys = value;
            else if (key == "LastFolder") LastFolder = Decode(value);
            else if (key == "JpegQuality" && int.TryParse(value, out iv)) JpegQuality = Clamp(iv, 70, 100);
            else if (key == "ExportLongEdge" && int.TryParse(value, out iv)) ExportLongEdge = Clamp(iv, 320, 20000);
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static double ParseDoubleOrNaN(string value)
        {
            if (string.Equals(value, "NaN", StringComparison.OrdinalIgnoreCase)) return double.NaN;
            double d;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : double.NaN;
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append('=').Append(value ?? string.Empty).Append("\r\n");
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch { return value ?? string.Empty; }
        }
    }

    internal sealed class EditStore
    {
        private readonly string _path;
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();

        public EditStore(string dataDirectory)
        {
            _path = System.IO.Path.Combine(dataDirectory, "LumenEdits.tsv");
            Load();
        }

        public double GetExposure(string path)
        {
            lock (_sync)
            {
                double value;
                return _values.TryGetValue(path, out value) ? value : 0.0;
            }
        }

        public void SetExposure(string path, double value)
        {
            lock (_sync)
            {
                if (Math.Abs(value) < 0.00001)
                    _values.Remove(path);
                else
                    _values[path] = value;
            }
            Save();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                int i;
                for (i = 0; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(new char[] { '\t' }, 2);
                    if (parts.Length != 2) continue;
                    string path;
                    try { path = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0])); }
                    catch { continue; }
                    double value;
                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        _values[path] = value;
                }
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(_path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                lock (_sync)
                {
                    foreach (KeyValuePair<string, double> kv in _values)
                    {
                        sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Key)))
                          .Append('\t')
                          .Append(kv.Value.ToString("0.0", CultureInfo.InvariantCulture))
                          .Append("\r\n");
                    }
                }
                string temp = _path + ".tmp";
                File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Copy(temp, _path, true); else File.Move(temp, _path);
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch { }
        }
    }
}
