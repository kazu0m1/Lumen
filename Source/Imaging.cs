using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LumenApp
{
    internal static class Imaging
    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static readonly string[] SupportedExtensions = new string[]
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif"
        };

        public static bool IsSupportedImage(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            int i;
            for (i = 0; i < SupportedExtensions.Length; i++)
                if (ext == SupportedExtensions[i]) return true;
            return false;
        }

        public static BitmapSource LoadBitmap(string path, int decodePixelWidth)
        {
            ushort orientation = 1;
            try
            {
                using (FileStream metaStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    BitmapDecoder metaDecoder = BitmapDecoder.Create(metaStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    orientation = GetOrientation(metaDecoder.Frames[0].Metadata as BitmapMetadata);
                }
            }
            catch { }

            BitmapImage image = new BitmapImage();
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                if (decodePixelWidth > 0) image.DecodePixelWidth = decodePixelWidth;
                image.StreamSource = fs;
                image.EndInit();
            }
            image.Freeze();
            BitmapSource source = ApplyOrientation(image, orientation);
            source.Freeze();
            return source;
        }

        public static BitmapSource LoadPreviewWithExposure(string path, double exposure, int maxLongEdge)
        {
            using (System.Drawing.Image source = System.Drawing.Image.FromFile(path))
            {
                ApplyOrientation(source);
                int width = source.Width;
                int height = source.Height;
                if (maxLongEdge > 0 && Math.Max(width, height) > maxLongEdge)
                {
                    double scale = (double)maxLongEdge / (double)Math.Max(width, height);
                    width = Math.Max(1, (int)Math.Round(width * scale));
                    height = Math.Max(1, (int)Math.Round(height * scale));
                }

                using (Bitmap rendered = Render(source, width, height, exposure))
                {
                    return ToBitmapSource(rendered);
                }
            }
        }

        public static bool TryReadPixelDimensions(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    BitmapFrame frame = decoder.Frames[0];
                    width = frame.PixelWidth;
                    height = frame.PixelHeight;
                    ushort orientation = GetOrientation(frame.Metadata as BitmapMetadata);
                    if (orientation == 5 || orientation == 6 || orientation == 7 || orientation == 8)
                    {
                        int swap = width;
                        width = height;
                        height = swap;
                    }
                    return width > 0 && height > 0;
                }
            }
            catch { return false; }
        }

        public static List<ExifValue> ReadExif(string path)
        {
            List<ExifValue> values = new List<ExifValue>();

            // WPF metadata is fast and preserves the existing Lumen behavior.  The
            // System.Drawing pass below fills gaps seen with some camera JPEGs.
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    BitmapFrame frame = decoder.Frames[0];
                    BitmapMetadata md = frame.Metadata as BitmapMetadata;
                    if (md != null)
                    {
                        string make = QueryString(md, new string[] { "/app1/ifd/{ushort=271}", "/ifd/{ushort=271}" });
                        string model = QueryString(md, new string[] { "/app1/ifd/{ushort=272}", "/ifd/{ushort=272}" });
                        string camera = ((make ?? string.Empty) + " " + (model ?? string.Empty)).Trim();
                        Add(values, "Camera", "Camera", camera);
                        Add(values, "Lens", "Lens", QueryString(md, new string[] { "/app1/ifd/exif/{ushort=42036}" }));
                        Add(values, "DateTaken", "Date taken", QueryString(md, new string[] { "/app1/ifd/exif/{ushort=36867}", "/app1/ifd/{ushort=306}", "/ifd/{ushort=306}" }));

                        object exposure = Query(md, new string[] { "/app1/ifd/exif/{ushort=33434}" });
                        AddExposure(values, AsRational(exposure));

                        object fnumber = Query(md, new string[] { "/app1/ifd/exif/{ushort=33437}" });
                        double? f = AsRational(fnumber);
                        if (f.HasValue) Add(values, "FNumber", "Aperture", "f/" + f.Value.ToString("0.0#"));

                        object iso = Query(md, new string[] { "/app1/ifd/exif/{ushort=34855}" });
                        if (iso != null) Add(values, "ISO", "ISO", Convert.ToString(iso, System.Globalization.CultureInfo.InvariantCulture));

                        object focal = Query(md, new string[] { "/app1/ifd/exif/{ushort=37386}" });
                        double? fl = AsRational(focal);
                        if (fl.HasValue) Add(values, "FocalLength", "Focal length", fl.Value.ToString("0.#") + " mm");

                        object bias = Query(md, new string[] { "/app1/ifd/exif/{ushort=37380}" });
                        double? bv = AsSignedRational(bias);
                        if (bv.HasValue) Add(values, "ExposureBias", "Exposure bias", bv.Value.ToString("+0.0;-0.0;0.0") + " EV");

                        object flash = Query(md, new string[] { "/app1/ifd/exif/{ushort=37385}" });
                        if (flash != null)
                        {
                            int fv;
                            if (int.TryParse(Convert.ToString(flash), out fv))
                                Add(values, "Flash", "Flash", (fv & 1) == 1 ? "Fired" : "Did not fire");
                        }

                        Add(values, "Software", "Software", QueryString(md, new string[] { "/app1/ifd/{ushort=305}", "/ifd/{ushort=305}" }));

                        object cs = Query(md, new string[] { "/app1/ifd/exif/{ushort=40961}" });
                        if (cs != null) Add(values, "ColorSpace", "Color space", DecodeExifColorSpace(ToUInt16(cs)));

                        object wb = Query(md, new string[] { "/app1/ifd/exif/{ushort=41987}" });
                        if (wb != null) Add(values, "WhiteBalance", "White balance", DecodeStandardWhiteBalance(ToUInt16(wb)));

                        object sat = Query(md, new string[] { "/app1/ifd/exif/{ushort=41993}" });
                        if (sat != null) Add(values, "Saturation", "Saturation", DecodeStandardSaturation(ToUInt16(sat)));

                        object sharp = Query(md, new string[] { "/app1/ifd/exif/{ushort=41994}" });
                        if (sharp != null) Add(values, "Sharpness", "Sharpness", DecodeStandardSharpness(ToUInt16(sharp)));
                    }
                }
            }
            catch { }

            try
            {
                using (System.Drawing.Image image = System.Drawing.Image.FromFile(path))
                {
                    string make = ReadAsciiProperty(image, 0x010F);
                    string model = ReadAsciiProperty(image, 0x0110);
                    Add(values, "Camera", "Camera", ((make ?? string.Empty) + " " + (model ?? string.Empty)).Trim());
                    Add(values, "Lens", "Lens", ReadAsciiProperty(image, 0xA434));
                    Add(values, "DateTaken", "Date taken", FirstNonEmpty(ReadAsciiProperty(image, 0x9003), ReadAsciiProperty(image, 0x0132)));

                    AddExposure(values, ReadUnsignedRationalProperty(image, 0x829A));

                    double? f = ReadUnsignedRationalProperty(image, 0x829D);
                    if (!f.HasValue)
                    {
                        double? apertureValue = ReadUnsignedRationalProperty(image, 0x9202);
                        if (apertureValue.HasValue) f = Math.Pow(2.0, apertureValue.Value / 2.0);
                    }
                    if (f.HasValue) Add(values, "FNumber", "Aperture", "f/" + f.Value.ToString("0.0#"));

                    ushort? iso = ReadUShortProperty(image, 0x8827);
                    if (iso.HasValue) Add(values, "ISO", "ISO", iso.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

                    double? fl = ReadUnsignedRationalProperty(image, 0x920A);
                    if (fl.HasValue) Add(values, "FocalLength", "Focal length", fl.Value.ToString("0.#") + " mm");

                    double? bias = ReadSignedRationalProperty(image, 0x9204);
                    if (bias.HasValue) Add(values, "ExposureBias", "Exposure bias", bias.Value.ToString("+0.0;-0.0;0.0") + " EV");

                    ushort? flash = ReadUShortProperty(image, 0x9209);
                    if (flash.HasValue) Add(values, "Flash", "Flash", (flash.Value & 1) == 1 ? "Fired" : "Did not fire");
                    Add(values, "Software", "Software", ReadAsciiProperty(image, 0x0131));

                    ushort? colorSpace = ReadUShortProperty(image, 0xA001);
                    if (colorSpace.HasValue) Add(values, "ColorSpace", "Color space", DecodeExifColorSpace(colorSpace.Value));
                    else if (HasProperty(image, 0x8773)) Add(values, "ColorSpace", "Color space", "Embedded ICC profile");

                    ushort? whiteBalance = ReadUShortProperty(image, 0xA403);
                    if (whiteBalance.HasValue) Add(values, "WhiteBalance", "White balance", DecodeStandardWhiteBalance(whiteBalance.Value));

                    ushort? saturation = ReadUShortProperty(image, 0xA409);
                    if (saturation.HasValue) Add(values, "Saturation", "Saturation", DecodeStandardSaturation(saturation.Value));

                    ushort? sharpness = ReadUShortProperty(image, 0xA40A);
                    if (sharpness.HasValue) Add(values, "Sharpness", "Sharpness", DecodeStandardSharpness(sharpness.Value));

                    // Fujifilm stores the detailed camera-rendering settings requested by
                    // Lumen users in the proprietary MakerNote.  Parse it in-process so
                    // the Portable build does not require ExifTool or another dependency.
                    FujiMakerNote fuji = TryReadFujiMakerNote(image);
                    if (fuji != null)
                    {
                        Add(values, "Quality", "Quality", fuji.Quality);
                        Set(values, "Sharpness", "Sharpness", DecodeFujiSharpness(fuji.GetUShort(0x1001)));
                        Set(values, "WhiteBalance", "White balance", DecodeFujiWhiteBalance(fuji.GetUShort(0x1002)));
                        Set(values, "Saturation", "Saturation", DecodeFujiSaturation(fuji.GetUShort(0x1003)));
                        Add(values, "DynamicRange", "Dynamic range", DecodeFujiDynamicRange(fuji));
                        Add(values, "FilmMode", "Film mode", DecodeFujiFilmMode(fuji));
                    }
                }
            }
            catch { }

            return values;
        }

        public static void ExportImage(string sourcePath, string outputPath, double exposure, ExportOptions options)
        {
            using (System.Drawing.Image source = System.Drawing.Image.FromFile(sourcePath))
            {
                ApplyOrientation(source);
                int width = source.Width;
                int height = source.Height;
                if (!options.NeverUpscale || Math.Max(width, height) > options.LongEdge)
                {
                    if (Math.Max(width, height) != options.LongEdge)
                    {
                        double scale = (double)options.LongEdge / (double)Math.Max(width, height);
                        if (!options.NeverUpscale || scale < 1.0)
                        {
                            width = Math.Max(1, (int)Math.Round(width * scale));
                            height = Math.Max(1, (int)Math.Round(height * scale));
                        }
                    }
                }

                using (Bitmap rendered = Render(source, width, height, exposure))
                {
                    BitmapSource wpf = ToBitmapSource(rendered);
                    BitmapMetadata metadata = TryCloneMetadata(sourcePath);
                    if (metadata != null)
                    {
                        TrySetOrientationNormal(metadata);
                        try { metadata.SetQuery("/app1/ifd/exif/{ushort=40962}", (uint)width); } catch { }
                        try { metadata.SetQuery("/app1/ifd/exif/{ushort=40963}", (uint)height); } catch { }
                    }
                    SaveBitmap(wpf, metadata, outputPath, options.JpegQuality);
                }
            }
        }

        public static string GetUniqueOutputPath(string folder, string sourcePath)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string ext = System.IO.Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext == ".gif") ext = ".png";
            string candidate = System.IO.Path.Combine(folder, name + ext);
            int n = 1;
            while (File.Exists(candidate))
            {
                candidate = System.IO.Path.Combine(folder, name + "_" + n.ToString() + ext);
                n++;
            }
            return candidate;
        }

        private static Bitmap Render(System.Drawing.Image source, int width, int height, double exposure)
        {
            Bitmap dest = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                if (source.HorizontalResolution > 0 && source.VerticalResolution > 0)
                    dest.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            }
            catch { }

            using (Graphics g = Graphics.FromImage(dest))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);
                g.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }

            if (Math.Abs(exposure) > 0.00001)
                ApplyExposureLinearLight(dest, exposure);
            return dest;
        }

        private static void ApplyExposureLinearLight(Bitmap bitmap, double exposure)
        {
            byte[] lut = BuildExposureLut(exposure);
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * data.Height;
                byte[] buffer = new byte[bytes];
                Marshal.Copy(data.Scan0, buffer, 0, bytes);
                int y;
                for (y = 0; y < data.Height; y++)
                {
                    int row = y * Math.Abs(data.Stride);
                    int x;
                    for (x = 0; x < data.Width; x++)
                    {
                        int p = row + x * 4;
                        buffer[p] = lut[buffer[p]];
                        buffer[p + 1] = lut[buffer[p + 1]];
                        buffer[p + 2] = lut[buffer[p + 2]];
                    }
                }
                Marshal.Copy(buffer, 0, data.Scan0, bytes);
            }
            finally { bitmap.UnlockBits(data); }
        }

        private static byte[] BuildExposureLut(double exposure)
        {
            byte[] lut = new byte[256];
            double factor = Math.Pow(2.0, exposure);
            int i;
            for (i = 0; i < 256; i++)
            {
                double s = i / 255.0;
                double linear = s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
                linear = Math.Min(1.0, Math.Max(0.0, linear * factor));
                double encoded = linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
                int v = (int)Math.Round(encoded * 255.0);
                lut[i] = (byte)Math.Max(0, Math.Min(255, v));
            }
            return lut;
        }

        private static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            IntPtr h = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally { DeleteObject(h); }
        }

        private static BitmapSource ApplyOrientation(BitmapSource source, ushort orientation)
        {
            Transform transform = null;
            if (orientation == 3) transform = new RotateTransform(180);
            else if (orientation == 6) transform = new RotateTransform(90);
            else if (orientation == 8) transform = new RotateTransform(270);
            else if (orientation == 2) transform = new ScaleTransform(-1, 1);
            else if (orientation == 4) transform = new ScaleTransform(1, -1);
            else if (orientation == 5)
            {
                TransformGroup tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(-1, 1));
                tg.Children.Add(new RotateTransform(270));
                transform = tg;
            }
            else if (orientation == 7)
            {
                TransformGroup tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(-1, 1));
                tg.Children.Add(new RotateTransform(90));
                transform = tg;
            }
            if (transform == null) return source;
            TransformedBitmap tb = new TransformedBitmap(source, transform);
            tb.Freeze();
            return tb;
        }

        private static ushort GetOrientation(BitmapMetadata metadata)
        {
            if (metadata == null) return 1;
            object value = Query(metadata, new string[] { "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" });
            try { return value == null ? (ushort)1 : Convert.ToUInt16(value); }
            catch { return 1; }
        }

        private static void ApplyOrientation(System.Drawing.Image image)
        {
            try
            {
                const int OrientationId = 0x0112;
                if (Array.IndexOf(image.PropertyIdList, OrientationId) < 0) return;
                PropertyItem pi = image.GetPropertyItem(OrientationId);
                ushort o = BitConverter.ToUInt16(pi.Value, 0);
                RotateFlipType type = RotateFlipType.RotateNoneFlipNone;
                if (o == 2) type = RotateFlipType.RotateNoneFlipX;
                else if (o == 3) type = RotateFlipType.Rotate180FlipNone;
                else if (o == 4) type = RotateFlipType.Rotate180FlipX;
                else if (o == 5) type = RotateFlipType.Rotate90FlipX;
                else if (o == 6) type = RotateFlipType.Rotate90FlipNone;
                else if (o == 7) type = RotateFlipType.Rotate270FlipX;
                else if (o == 8) type = RotateFlipType.Rotate270FlipNone;
                image.RotateFlip(type);
            }
            catch { }
        }

        private static BitmapMetadata TryCloneMetadata(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    BitmapMetadata md = decoder.Frames[0].Metadata as BitmapMetadata;
                    return md == null ? null : md.Clone() as BitmapMetadata;
                }
            }
            catch { return null; }
        }

        private static void TrySetOrientationNormal(BitmapMetadata md)
        {
            try { md.SetQuery("/app1/ifd/{ushort=274}", (ushort)1); } catch { }
            try { md.SetQuery("/ifd/{ushort=274}", (ushort)1); } catch { }
        }

        private static void SaveBitmap(BitmapSource source, BitmapMetadata metadata, string outputPath, int jpegQuality)
        {
            string ext = System.IO.Path.GetExtension(outputPath).ToLowerInvariant();
            BitmapEncoder encoder;
            if (ext == ".jpg" || ext == ".jpeg")
            {
                JpegBitmapEncoder j = new JpegBitmapEncoder();
                j.QualityLevel = Math.Max(70, Math.Min(100, jpegQuality));
                encoder = j;
            }
            else if (ext == ".png") encoder = new PngBitmapEncoder();
            else if (ext == ".tif" || ext == ".tiff")
            {
                TiffBitmapEncoder t = new TiffBitmapEncoder();
                t.Compression = TiffCompressOption.Zip;
                encoder = t;
            }
            else if (ext == ".bmp") encoder = new BmpBitmapEncoder();
            else
            {
                JpegBitmapEncoder j = new JpegBitmapEncoder();
                j.QualityLevel = Math.Max(70, Math.Min(100, jpegQuality));
                encoder = j;
                outputPath = System.IO.Path.ChangeExtension(outputPath, ".jpg");
            }

            BitmapFrame frame;
            try { frame = BitmapFrame.Create(source, null, metadata, null); }
            catch { frame = BitmapFrame.Create(source); }
            encoder.Frames.Add(frame);
            using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                encoder.Save(fs);
        }

        private static void AddExposure(List<ExifValue> values, double? exposureValue)
        {
            if (!exposureValue.HasValue || exposureValue.Value <= 0) return;
            string text;
            if (exposureValue.Value < 1.0)
                text = "1/" + Math.Round(1.0 / exposureValue.Value).ToString("0") + " s";
            else
                text = exposureValue.Value.ToString("0.###") + " s";
            Add(values, "ExposureTime", "Shutter", text);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            if (!string.IsNullOrWhiteSpace(first)) return first;
            return second;
        }

        private static bool HasProperty(System.Drawing.Image image, int id)
        {
            try { return Array.IndexOf(image.PropertyIdList, id) >= 0; }
            catch { return false; }
        }

        private static PropertyItem TryGetProperty(System.Drawing.Image image, int id)
        {
            try
            {
                if (!HasProperty(image, id)) return null;
                return image.GetPropertyItem(id);
            }
            catch { return null; }
        }

        private static string ReadAsciiProperty(System.Drawing.Image image, int id)
        {
            PropertyItem pi = TryGetProperty(image, id);
            if (pi == null || pi.Value == null || pi.Value.Length == 0) return null;
            try { return System.Text.Encoding.ASCII.GetString(pi.Value).Trim('\0', ' ', '\r', '\n', '\t'); }
            catch { return null; }
        }

        private static ushort? ReadUShortProperty(System.Drawing.Image image, int id)
        {
            PropertyItem pi = TryGetProperty(image, id);
            if (pi == null || pi.Value == null || pi.Value.Length < 2) return null;
            try { return BitConverter.ToUInt16(pi.Value, 0); }
            catch { return null; }
        }

        private static double? ReadUnsignedRationalProperty(System.Drawing.Image image, int id)
        {
            PropertyItem pi = TryGetProperty(image, id);
            if (pi == null || pi.Value == null || pi.Value.Length < 8) return null;
            try
            {
                uint n = BitConverter.ToUInt32(pi.Value, 0);
                uint d = BitConverter.ToUInt32(pi.Value, 4);
                if (d == 0) return null;
                return (double)n / (double)d;
            }
            catch { return null; }
        }

        private static double? ReadSignedRationalProperty(System.Drawing.Image image, int id)
        {
            PropertyItem pi = TryGetProperty(image, id);
            if (pi == null || pi.Value == null || pi.Value.Length < 8) return null;
            try
            {
                int n = BitConverter.ToInt32(pi.Value, 0);
                int d = BitConverter.ToInt32(pi.Value, 4);
                if (d == 0) return null;
                return (double)n / (double)d;
            }
            catch { return null; }
        }

        private static ushort ToUInt16(object value)
        {
            try { return Convert.ToUInt16(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return 0xffff; }
        }

        private static string DecodeExifColorSpace(ushort value)
        {
            if (value == 1) return "sRGB";
            if (value == 2) return "Adobe RGB"; // seen in a small number of vendor encodings
            if (value == 0xffff) return "Uncalibrated / profile-defined";
            return "Value " + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string DecodeStandardWhiteBalance(ushort value)
        {
            if (value == 0) return "Auto";
            if (value == 1) return "Manual";
            return "Value " + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string DecodeStandardSaturation(ushort value)
        {
            if (value == 0) return "Normal";
            if (value == 1) return "Low";
            if (value == 2) return "High";
            return "Value " + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string DecodeStandardSharpness(ushort value)
        {
            if (value == 0) return "Normal";
            if (value == 1) return "Soft";
            if (value == 2) return "Hard";
            return "Value " + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static FujiMakerNote TryReadFujiMakerNote(System.Drawing.Image image)
        {
            try
            {
                PropertyItem pi = TryGetProperty(image, 0x927C);
                if (pi == null || pi.Value == null || pi.Value.Length < 14) return null;
                byte[] data = pi.Value;
                string header = System.Text.Encoding.ASCII.GetString(data, 0, Math.Min(8, data.Length));
                if (!string.Equals(header, "FUJIFILM", StringComparison.Ordinal)) return null;

                uint ifdOffset = ReadUInt32Little(data, 8);
                if (ifdOffset > int.MaxValue || ifdOffset + 2 > data.Length) return null;
                int ifd = (int)ifdOffset;
                ushort count = ReadUInt16Little(data, ifd);
                FujiMakerNote result = new FujiMakerNote();
                int i;
                for (i = 0; i < count; i++)
                {
                    long entryLong = (long)ifd + 2L + (long)i * 12L;
                    if (entryLong < 0 || entryLong + 12L > data.Length) break;
                    int entry = (int)entryLong;
                    ushort tag = ReadUInt16Little(data, entry);
                    ushort type = ReadUInt16Little(data, entry + 2);
                    uint valueCount = ReadUInt32Little(data, entry + 4);
                    int unit = FujiTypeSize(type);
                    if (unit <= 0 || valueCount > 1024 * 1024) continue;
                    long byteCountLong = (long)unit * (long)valueCount;
                    if (byteCountLong < 0 || byteCountLong > int.MaxValue) continue;
                    int byteCount = (int)byteCountLong;
                    byte[] valueData;
                    if (byteCount <= 4)
                    {
                        valueData = new byte[byteCount];
                        if (byteCount > 0) Buffer.BlockCopy(data, entry + 8, valueData, 0, byteCount);
                    }
                    else
                    {
                        uint valueOffset = ReadUInt32Little(data, entry + 8);
                        if (valueOffset > int.MaxValue || (long)valueOffset + byteCount > data.Length) continue;
                        valueData = new byte[byteCount];
                        Buffer.BlockCopy(data, (int)valueOffset, valueData, 0, byteCount);
                    }
                    result.Add(tag, type, valueCount, valueData);
                }
                return result;
            }
            catch { return null; }
        }

        private static int FujiTypeSize(ushort type)
        {
            if (type == 1 || type == 2 || type == 6 || type == 7) return 1;
            if (type == 3 || type == 8) return 2;
            if (type == 4 || type == 9 || type == 11) return 4;
            if (type == 5 || type == 10 || type == 12) return 8;
            return 0;
        }

        private static ushort ReadUInt16Little(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 2 > data.Length) return 0;
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32Little(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 4 > data.Length) return 0;
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static string DecodeFujiSharpness(ushort? value)
        {
            if (!value.HasValue || value.Value == 0xffff || value.Value == 0x8000) return null;
            switch (value.Value)
            {
                case 0x0: return "-4 (softest)";
                case 0x1: return "-3";
                case 0x2: return "-2";
                case 0x3: return "0 (normal)";
                case 0x4: return "+2";
                case 0x5: return "+3";
                case 0x6: return "+4 (hardest)";
                case 0x82: return "-1";
                case 0x84: return "+1";
                default: return "Value 0x" + value.Value.ToString("X");
            }
        }

        private static string DecodeFujiWhiteBalance(ushort? value)
        {
            if (!value.HasValue) return null;
            switch (value.Value)
            {
                case 0x0: return "Auto";
                case 0x1: return "Auto (white priority)";
                case 0x2: return "Auto (ambiance priority)";
                case 0x100: return "Daylight";
                case 0x200: return "Cloudy";
                case 0x300: return "Daylight fluorescent";
                case 0x301: return "Day white fluorescent";
                case 0x302: return "White fluorescent";
                case 0x303: return "Warm white fluorescent";
                case 0x304: return "Living-room warm white fluorescent";
                case 0x400: return "Incandescent";
                case 0x500: return "Flash";
                case 0x600: return "Underwater";
                case 0xf00: return "Custom 1";
                case 0xf01: return "Custom 2";
                case 0xf02: return "Custom 3";
                case 0xf03: return "Custom 4";
                case 0xf04: return "Custom 5";
                case 0xff0: return "Kelvin";
                default: return "Value 0x" + value.Value.ToString("X");
            }
        }

        private static string DecodeFujiSaturation(ushort? value)
        {
            if (!value.HasValue || value.Value == 0x8000) return null;
            switch (value.Value)
            {
                case 0x0: return "0 (normal)";
                case 0x80: return "+1";
                case 0xc0: return "+3";
                case 0xe0: return "+4";
                case 0x100: return "+2";
                case 0x180: return "-1";
                case 0x200: return "Low";
                case 0x400: return "-2";
                case 0x4c0: return "-3";
                case 0x4e0: return "-4";
                case 0x300: return "B&W";
                case 0x301: return "B&W + Red filter";
                case 0x302: return "B&W + Yellow filter";
                case 0x303: return "B&W + Green filter";
                case 0x310: return "Sepia";
                case 0x500: return "Acros";
                case 0x501: return "Acros + Red filter";
                case 0x502: return "Acros + Yellow filter";
                case 0x503: return "Acros + Green filter";
                default: return "Value 0x" + value.Value.ToString("X");
            }
        }

        private static string DecodeFujiDynamicRange(FujiMakerNote fuji)
        {
            ushort? setting = fuji.GetUShort(0x1402);
            ushort? developed = fuji.GetUShort(0x1403);
            ushort? autoDr = fuji.GetUShort(0x140B);
            ushort? range = fuji.GetUShort(0x1400);

            ushort actual = 0;
            if (developed.HasValue && (developed.Value == 100 || developed.Value == 200 || developed.Value == 400 || developed.Value == 800)) actual = developed.Value;
            else if (autoDr.HasValue && (autoDr.Value == 100 || autoDr.Value == 200 || autoDr.Value == 400 || autoDr.Value == 800)) actual = autoDr.Value;

            if (setting.HasValue && setting.Value == 0 && actual > 0) return "Auto → DR" + actual.ToString();
            if (actual > 0) return "DR" + actual.ToString();
            if (setting.HasValue)
            {
                switch (setting.Value)
                {
                    case 0x0: return "Auto";
                    case 0x1: return "Manual";
                    case 0x100: return "DR100";
                    case 0x200: return "DR200";
                    case 0x201: return "DR400";
                    case 0x8000: return "Film Simulation";
                }
            }
            if (range.HasValue)
            {
                if (range.Value == 1) return "Standard";
                if (range.Value == 3) return "Wide";
            }
            return null;
        }

        private static string DecodeFujiFilmMode(FujiMakerNote fuji)
        {
            ushort? value = fuji.GetUShort(0x1401);
            if (value.HasValue && value.Value != 0x8000)
            {
                switch (value.Value)
                {
                    case 0x0: return "Provia / Standard";
                    case 0x100: return "F1 / Studio Portrait";
                    case 0x110: return "F1a / Studio Portrait Enhanced Saturation";
                    case 0x120: return "Astia / Soft";
                    case 0x130: return "F1c / Studio Portrait Increased Sharpness";
                    case 0x200: return "Fujichrome / Velvia";
                    case 0x300: return "F3 / Studio Portrait Ex";
                    case 0x400: return "Velvia / Vivid";
                    case 0x500: return "Pro Neg. Std";
                    case 0x501: return "Pro Neg. Hi";
                    case 0x600: return "Classic Chrome";
                    case 0x700: return "Eterna / Cinema";
                    case 0x800: return "Classic Negative";
                    case 0x900: return "Eterna Bleach Bypass";
                    case 0xa00: return "Nostalgic Neg.";
                    case 0xb00: return "Reala ACE";
                    default: return "Value 0x" + value.Value.ToString("X");
                }
            }

            // Fujifilm encodes monochrome simulations in the Saturation tag on
            // cameras/files where FilmMode is absent.
            ushort? saturation = fuji.GetUShort(0x1003);
            if (!saturation.HasValue) return null;
            switch (saturation.Value)
            {
                case 0x300: return "Monochrome";
                case 0x301: return "Monochrome + Red filter";
                case 0x302: return "Monochrome + Yellow filter";
                case 0x303: return "Monochrome + Green filter";
                case 0x310: return "Sepia";
                case 0x500: return "Acros";
                case 0x501: return "Acros + Red filter";
                case 0x502: return "Acros + Yellow filter";
                case 0x503: return "Acros + Green filter";
            }
            return null;
        }

        private sealed class FujiMakerNote
        {
            private readonly Dictionary<ushort, FujiTagValue> _values = new Dictionary<ushort, FujiTagValue>();

            public string Quality { get { return GetString(0x1000); } }

            public void Add(ushort tag, ushort type, uint count, byte[] data)
            {
                if (!_values.ContainsKey(tag)) _values[tag] = new FujiTagValue(type, count, data);
            }

            public ushort? GetUShort(ushort tag)
            {
                FujiTagValue value;
                if (!_values.TryGetValue(tag, out value) || value.Data == null || value.Data.Length < 2) return null;
                if (value.Type != 3 && value.Type != 8) return null;
                return ReadUInt16Little(value.Data, 0);
            }

            public string GetString(ushort tag)
            {
                FujiTagValue value;
                if (!_values.TryGetValue(tag, out value) || value.Data == null || value.Data.Length == 0) return null;
                try { return System.Text.Encoding.ASCII.GetString(value.Data).Trim('\0', ' ', '\r', '\n', '\t'); }
                catch { return null; }
            }
        }

        private sealed class FujiTagValue
        {
            public FujiTagValue(ushort type, uint count, byte[] data)
            {
                Type = type;
                Count = count;
                Data = data;
            }
            public ushort Type { get; private set; }
            public uint Count { get; private set; }
            public byte[] Data { get; private set; }
        }

        private static object Query(BitmapMetadata md, string[] paths)
        {
            int i;
            for (i = 0; i < paths.Length; i++)
            {
                try
                {
                    if (md.ContainsQuery(paths[i])) return md.GetQuery(paths[i]);
                }
                catch { }
            }
            return null;
        }

        private static string QueryString(BitmapMetadata md, string[] paths)
        {
            object value = Query(md, paths);
            return value == null ? null : Convert.ToString(value).Trim();
        }

        private static void Add(List<ExifValue> values, string key, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            int i;
            for (i = 0; i < values.Count; i++)
                if (string.Equals(values[i].Key, key, StringComparison.OrdinalIgnoreCase)) return;
            values.Add(new ExifValue(key, label, value));
        }

        private static void Set(List<ExifValue> values, string key, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            int i;
            for (i = values.Count - 1; i >= 0; i--)
                if (string.Equals(values[i].Key, key, StringComparison.OrdinalIgnoreCase)) values.RemoveAt(i);
            values.Add(new ExifValue(key, label, value));
        }

        private static double? AsRational(object value)
        {
            if (value == null) return null;
            try
            {
                if (value is ulong)
                {
                    ulong u = (ulong)value;
                    uint n = (uint)(u >> 32);
                    uint d = (uint)(u & 0xffffffff);
                    return d == 0 ? (double?)null : (double)n / (double)d;
                }
                if (value is long)
                {
                    ulong u = unchecked((ulong)(long)value);
                    uint n = (uint)(u >> 32);
                    uint d = (uint)(u & 0xffffffff);
                    return d == 0 ? (double?)null : (double)n / (double)d;
                }
                return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        private static double? AsSignedRational(object value)
        {
            if (value == null) return null;
            try
            {
                if (value is long)
                {
                    long l = (long)value;
                    int n = (int)(l >> 32);
                    uint d = (uint)(l & 0xffffffff);
                    return d == 0 ? (double?)null : (double)n / (double)d;
                }
                if (value is ulong)
                {
                    ulong u = (ulong)value;
                    int n = unchecked((int)(u >> 32));
                    uint d = (uint)(u & 0xffffffff);
                    return d == 0 ? (double?)null : (double)n / (double)d;
                }
                return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
    }
}
