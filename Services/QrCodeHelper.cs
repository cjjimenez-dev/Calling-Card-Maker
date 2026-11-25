using System;
using System.Windows;
using System.Windows.Media;
using Calling_Card_Generator.Models;
using QRCoder;

namespace Calling_Card_Generator.Services
{
    public static class QrCodeHelper
    {
        public static void DrawQrCode(DrawingContext dc, QrCodeElement elem)
        {
            string payload = string.IsNullOrWhiteSpace(elem.QrContent) ? "https://www.vcsi.biz" : elem.QrContent;

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);

            var matrix = qrData.ModuleMatrix;
            int rawModules = matrix.Count;
            if (rawModules == 0) return;

            // QRCoder ModuleMatrix already includes a 4-module quiet zone by default.
            int trim = elem.IncludeQuietZone ? 2 : 4;
            int start = Math.Min(trim, rawModules / 4);
            int moduleCount = rawModules - (start * 2);
            if (moduleCount <= 0)
            {
                start = 0;
                moduleCount = rawModules;
            }

            double size = Math.Min(elem.Width, elem.Height);
            double offsetX = elem.X + (elem.Width - size) / 2.0;
            double offsetY = elem.Y + (elem.Height - size) / 2.0;
            double cellSize = size / moduleCount;

            Brush lightBrush = string.Equals(elem.LightColor, "Transparent", StringComparison.OrdinalIgnoreCase)
                ? Brushes.Transparent
                : CardRenderer.ParseColorBrush(elem.LightColor, Brushes.White);
            Brush darkBrush = CardRenderer.ParseColorBrush(elem.DarkColor, Brushes.Black);

            if (lightBrush != Brushes.Transparent)
            {
                dc.DrawRectangle(lightBrush, null, new Rect(offsetX, offsetY, size, size));
            }

            // Use a GeometryGroup so adjacent dark modules have zero subpixel gaps
            var darkGeom = new GeometryGroup { FillRule = FillRule.Nonzero };
            for (int r = 0; r < moduleCount; r++)
            {
                var row = matrix[r + start];
                for (int c = 0; c < moduleCount; c++)
                {
                    if (row[c + start])
                    {
                        double x = offsetX + (c * cellSize);
                        double y = offsetY + (r * cellSize);
                        // Slight 0.02 overlap avoids hairline seams between adjacent squares
                        darkGeom.Children.Add(new RectangleGeometry(new Rect(x, y, cellSize + 0.02, cellSize + 0.02)));
                    }
                }
            }
            darkGeom.Freeze();
            dc.DrawGeometry(darkBrush, null, darkGeom);
        }

        public static DrawingImage CreateQrDrawingImage(QrCodeElement elem)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var localElem = new QrCodeElement
                {
                    X = 0,
                    Y = 0,
                    Width = elem.Width,
                    Height = elem.Height,
                    QrContent = elem.QrContent,
                    DarkColor = elem.DarkColor,
                    LightColor = elem.LightColor,
                    IncludeQuietZone = elem.IncludeQuietZone
                };
                DrawQrCode(dc, localElem);
            }

            var di = new DrawingImage(dv.Drawing);
            di.Freeze();
            return di;
        }
    }
}
