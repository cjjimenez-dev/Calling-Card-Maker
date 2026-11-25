using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Calling_Card_Generator.Models;

namespace Calling_Card_Generator.Services
{
    public static class CardRenderer
    {
        private static readonly ConcurrentDictionary<string, BitmapSource> ImageCache = new();

        public static BitmapSource? GetBitmapFromBase64OrPath(string base64, string path)
        {
            if (!string.IsNullOrEmpty(base64))
            {
                if (ImageCache.TryGetValue(base64, out var cached))
                    return cached;

                try
                {
                    byte[] bytes = Convert.FromBase64String(base64);
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();

                    ImageCache[base64] = bmp;
                    return bmp;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                if (ImageCache.TryGetValue(path, out var cached))
                    return cached;

                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    ImageCache[path] = bmp;
                    return bmp;
                }
                catch { }
            }

            return null;
        }

        public static Brush ParseColorBrush(string colorHex, Brush fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(colorHex)) return fallback;
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// Draws a card side onto a DrawingContext at standard 96 DPI WPF units.
        /// Callers apply ScaleTransform for higher DPI rendering.
        /// </summary>
        public static void DrawCardSide(DrawingContext dc, CardSide side, double widthWpf, double heightWpf)
        {
            // Clip to card boundary
            var cardRect = new Rect(0, 0, widthWpf, heightWpf);
            dc.PushClip(new RectangleGeometry(cardRect));

            // 1. Background
            Brush bgBrush = ParseColorBrush(side.BackgroundColor, Brushes.White);
            dc.DrawRectangle(bgBrush, null, cardRect);

            if (!string.IsNullOrEmpty(side.BackgroundImageBase64))
            {
                var bgImage = GetBitmapFromBase64OrPath(side.BackgroundImageBase64, string.Empty);
                if (bgImage != null)
                {
                    dc.DrawImage(bgImage, cardRect);
                }
            }

            // 2. Elements ordered by ZIndex
            var sortedElements = side.Elements.OrderBy(e => e.ZIndex).ToList();

            foreach (var element in sortedElements)
            {
                DrawSingleElement(dc, element);
            }

            dc.Pop(); // Pop clip
        }

        private static void DrawSingleElement(DrawingContext dc, CardElement element)
        {
            bool hasTransform = element.Rotation != 0 || element.FlipHorizontal || element.FlipVertical;
            if (hasTransform)
            {
                double cx = element.X + (element.Width / 2.0);
                double cy = element.Y + (element.Height / 2.0);
                var tg = new TransformGroup();
                if (element.FlipHorizontal || element.FlipVertical)
                {
                    tg.Children.Add(new ScaleTransform(
                        element.FlipHorizontal ? -1 : 1,
                        element.FlipVertical ? -1 : 1,
                        cx, cy));
                }
                if (element.Rotation != 0)
                {
                    tg.Children.Add(new RotateTransform(element.Rotation, cx, cy));
                }
                dc.PushTransform(tg);
            }

            if (element is TextElement textElem)
            {
                DrawTextElement(dc, textElem);
            }
            else if (element is ImageElement imgElem)
            {
                DrawImageElement(dc, imgElem);
            }
            else if (element is ShapeElement shapeElem)
            {
                DrawShapeElement(dc, shapeElem);
            }
            else if (element is QrCodeElement qrElem)
            {
                QrCodeHelper.DrawQrCode(dc, qrElem);
            }
            else if (element is GroupElement groupElem)
            {
                dc.PushTransform(new TranslateTransform(groupElem.X, groupElem.Y));
                foreach (var child in groupElem.Children.OrderBy(c => c.ZIndex))
                {
                    DrawSingleElement(dc, child);
                }
                dc.Pop();
            }

            if (hasTransform)
            {
                dc.Pop();
            }
        }

        private static void DrawTextElement(DrawingContext dc, TextElement elem)
        {
            if (string.IsNullOrEmpty(elem.Text)) return;

            var typeface = new Typeface(
                new FontFamily(string.IsNullOrWhiteSpace(elem.FontFamily) ? "Segoe UI" : elem.FontFamily),
                elem.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                elem.IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);

            Brush textBrush = ParseColorBrush(elem.TextColor, Brushes.Black);

            var formattedText = new FormattedText(
                elem.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                elem.FontSize,
                textBrush,
                1.0)
            {
                MaxTextWidth = Math.Max(10, elem.Width),
                MaxTextHeight = Math.Max(10, elem.Height),
                TextAlignment = elem.Alignment
            };

            if (elem.IsUnderline)
            {
                formattedText.SetTextDecorations(TextDecorations.Underline);
            }

            double drawX = elem.X;
            // Note: FormattedText with TextAlignment.Center or Right aligns relative to origin if MaxTextWidth is set
            if (elem.Alignment == TextAlignment.Center)
            {
                drawX = elem.X;
            }
            else if (elem.Alignment == TextAlignment.Right)
            {
                drawX = elem.X;
            }

            dc.DrawText(formattedText, new Point(drawX, elem.Y));
        }

        private static void DrawImageElement(DrawingContext dc, ImageElement elem)
        {
            var bmp = GetBitmapFromBase64OrPath(elem.ImageBase64, elem.ImagePath);
            if (bmp == null) return;

            var destRect = new Rect(elem.X, elem.Y, elem.Width, elem.Height);
            if (elem.Opacity < 1.0)
            {
                dc.PushOpacity(elem.Opacity);
                dc.DrawImage(bmp, destRect);
                dc.Pop();
            }
            else
            {
                dc.DrawImage(bmp, destRect);
            }
        }

        private static void DrawShapeElement(DrawingContext dc, ShapeElement elem)
        {
            var rect = new Rect(elem.X, elem.Y, elem.Width, elem.Height);
            Brush fillBrush = ParseColorBrush(elem.FillColor, Brushes.Transparent);
            Brush strokeBrush = ParseColorBrush(elem.StrokeColor, Brushes.Transparent);
            Pen pen = new Pen(strokeBrush, elem.StrokeThickness);
            pen.Freeze();

            switch (elem.ShapeKind)
            {
                case ShapeType.Rectangle:
                    dc.DrawRoundedRectangle(fillBrush, pen, rect, elem.CornerRadius, elem.CornerRadius);
                    break;
                case ShapeType.Ellipse:
                    dc.DrawEllipse(fillBrush, pen, new Point(elem.X + elem.Width / 2.0, elem.Y + elem.Height / 2.0), elem.Width / 2.0, elem.Height / 2.0);
                    break;
                case ShapeType.Line:
                    dc.DrawLine(pen, new Point(elem.X, elem.Y + elem.Height / 2.0), new Point(elem.X + elem.Width, elem.Y + elem.Height / 2.0));
                    break;
            }
        }

        /// <summary>
        /// Renders a card side to a high-resolution BitmapSource (e.g. 300 DPI for printing).
        /// </summary>
        public static RenderTargetBitmap RenderCardToBitmap(CardSide side, double widthWpf, double heightWpf, double targetDpi = 300.0)
        {
            double scale = targetDpi / 96.0;
            int pixelWidth = (int)Math.Round(widthWpf * scale);
            int pixelHeight = (int)Math.Round(heightWpf * scale);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                DrawCardSide(dc, side, widthWpf, heightWpf);
                dc.Pop();
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        /// <summary>
        /// Exports a rendered card side to a PNG file.
        /// </summary>
        public static void ExportToPng(CardSide side, double widthWpf, double heightWpf, string filePath, double dpi = 300.0)
        {
            var rtb = RenderCardToBitmap(side, widthWpf, heightWpf, dpi);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var stream = File.Create(filePath);
            encoder.Save(stream);
        }
    }
}
