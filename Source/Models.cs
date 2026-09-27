using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace LumenApp
{
    internal sealed class PhotoItem : INotifyPropertyChanged
    {
        private BitmapSource _thumbnail;
        private double _exposure;
        private bool _isDeleted;

        public PhotoItem(string path, double exposure)
        {
            Path = path;
            _exposure = exposure;
        }

        public string Path { get; private set; }
        public string FileName { get { return System.IO.Path.GetFileName(Path); } }
        public string Extension { get { return System.IO.Path.GetExtension(Path); } }

        public BitmapSource Thumbnail
        {
            get { return _thumbnail; }
            set
            {
                if (!object.ReferenceEquals(_thumbnail, value))
                {
                    _thumbnail = value;
                    OnPropertyChanged("Thumbnail");
                }
            }
        }

        public double Exposure
        {
            get { return _exposure; }
            set
            {
                if (Math.Abs(_exposure - value) > 0.00001)
                {
                    _exposure = value;
                    OnPropertyChanged("Exposure");
                }
            }
        }

        public bool IsDeleted
        {
            get { return _isDeleted; }
            set
            {
                if (_isDeleted != value)
                {
                    _isDeleted = value;
                    OnPropertyChanged("IsDeleted");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }

    internal sealed class DeleteBatch
    {
        public DeleteBatch()
        {
            Paths = new System.Collections.Generic.List<string>();
            DeletedAtUtc = DateTime.UtcNow;
        }

        public System.Collections.Generic.List<string> Paths { get; private set; }
        public DateTime DeletedAtUtc { get; private set; }
    }

    internal sealed class ExifFieldDefinition
    {
        public ExifFieldDefinition(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; private set; }
        public string Label { get; private set; }
    }

    internal sealed class ExifValue
    {
        public ExifValue(string key, string label, string value)
        {
            Key = key;
            Label = label;
            Value = value;
        }

        public string Key { get; private set; }
        public string Label { get; private set; }
        public string Value { get; private set; }
    }

    internal sealed class ExportOptions
    {
        public string OutputFolder { get; set; }
        public int LongEdge { get; set; }
        public int JpegQuality { get; set; }
        public bool NeverUpscale { get; set; }
    }
}
