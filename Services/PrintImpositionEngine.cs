using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Calling_Card_Generator.Models;

namespace Calling_Card_Generator.Services
{
    public class PrintSheetInfo
    {
        public int SheetNumber { get; set; }
        public int TotalSheets { get; set; }
        public bool IsBackSide { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<SheetCardPlacement> Cards { get; set; } = new();
    }

    public class SheetCardPlacement
    {
        public CardTemplate Template { get; set; } = null!;
        public CardSide Side { get; set; } = null!;
        public int Row { get; set; }
        public int Col { get; set; }
        public Rect Bounds { get; set; }
    }

    public static class PrintImpositionEngine
    {
        public static List<PrintSheetInfo> GenerateSheets(
            IEnumerable<PrintQueueItem> queueItems,
            SheetLayout layout,
            bool forDuplexInterleaved = false)
        {
            var sheetList = new List<PrintSheetInfo>();

            // 1. Flatten queue into individual card tasks
            var frontCards = new List<CardTemplate>();
            var backCards = new List<CardTemplate>();

            foreach (var item in queueItems)
            {
                for (int i = 0; i < item.Quantity; i++)
                {
                    if (item.SideOption == PrintSideOption.FrontAndBack)
                    {
                        frontCards.Add(item.Template);
                        backCards.Add(item.Template);
                    }
                    else if (item.SideOption == PrintSideOption.FrontOnly)
                    {
                        frontCards.Add(item.Template);
                    }
                    else if (item.SideOption == PrintSideOption.BackOnly)
                    {
                        backCards.Add(item.Template);
                    }
                }
            }

            int cardsPerSheet = layout.CardsPerSheet;
            if (cardsPerSheet <= 0) return sheetList;

            // Generate Front Sheets
            var frontSheets = new List<PrintSheetInfo>();
            int frontCount = frontCards.Count;
            int totalFrontSheets = (int)Math.Ceiling((double)frontCount / cardsPerSheet);

            for (int s = 0; s < totalFrontSheets; s++)
            {
                var sheet = new PrintSheetInfo
                {
                    SheetNumber = s + 1,
                    TotalSheets = totalFrontSheets,
                    IsBackSide = false,
                    Title = $"Sheet {s + 1} of {totalFrontSheets} (Front Sides)"
                };

                int startIdx = s * cardsPerSheet;
                for (int slot = 0; slot < cardsPerSheet; slot++)
                {
                    int cardIdx = startIdx + slot;
                    if (cardIdx >= frontCount) break;

                    var tmpl = frontCards[cardIdx];
                    int row = slot / layout.Columns;
                    int col = slot % layout.Columns;

                    double x = (layout.MarginLeftInches * 96.0) + (col * ((tmpl.WidthWpf) + (layout.GapHorizontalInches * 96.0)));
                    double y = (layout.MarginTopInches * 96.0) + (row * ((tmpl.HeightWpf) + (layout.GapVerticalInches * 96.0)));

                    sheet.Cards.Add(new SheetCardPlacement
                    {
                        Template = tmpl,
                        Side = tmpl.Front,
                        Row = row,
                        Col = col,
                        Bounds = new Rect(x, y, tmpl.WidthWpf, tmpl.HeightWpf)
                    });
                }
                frontSheets.Add(sheet);
            }

            // Generate Back Sheets (with Mirrored Columns)
            var backSheets = new List<PrintSheetInfo>();
            int backCount = backCards.Count;
            int totalBackSheets = (int)Math.Ceiling((double)backCount / cardsPerSheet);

            for (int s = 0; s < totalBackSheets; s++)
            {
                var sheet = new PrintSheetInfo
                {
                    SheetNumber = s + 1,
                    TotalSheets = totalBackSheets,
                    IsBackSide = true,
                    Title = $"Sheet {s + 1} of {totalBackSheets} (Back Sides - Mirrored)"
                };

                int startIdx = s * cardsPerSheet;
                for (int slot = 0; slot < cardsPerSheet; slot++)
                {
                    int cardIdx = startIdx + slot;
                    if (cardIdx >= backCount) break;

                    var tmpl = backCards[cardIdx];
                    int row = slot / layout.Columns;
                    int col = slot % layout.Columns;

                    // Mirror column horizontally for duplex alignment
                    int mirroredCol = (layout.Columns - 1) - col;

                    double x = (layout.MarginLeftInches * 96.0) + (mirroredCol * ((tmpl.WidthWpf) + (layout.GapHorizontalInches * 96.0)));
                    double y = (layout.MarginTopInches * 96.0) + (row * ((tmpl.HeightWpf) + (layout.GapVerticalInches * 96.0)));

                    sheet.Cards.Add(new SheetCardPlacement
                    {
                        Template = tmpl,
                        Side = tmpl.Back,
                        Row = row,
                        Col = mirroredCol,
                        Bounds = new Rect(x, y, tmpl.WidthWpf, tmpl.HeightWpf)
                    });
                }
                backSheets.Add(sheet);
            }

            if (forDuplexInterleaved)
            {
                // Interleave: Front 1, Back 1, Front 2, Back 2...
                int maxSheets = Math.Max(frontSheets.Count, backSheets.Count);
                for (int i = 0; i < maxSheets; i++)
                {
                    if (i < frontSheets.Count) sheetList.Add(frontSheets[i]);
                    if (i < backSheets.Count) sheetList.Add(backSheets[i]);
                }
            }
            else
            {
                // Front sheets first, then Back sheets
                sheetList.AddRange(frontSheets);
                sheetList.AddRange(backSheets);
            }

            return sheetList;
        }

        /// <summary>
        /// Renders a full print sheet with cards, crop marks, and cut guides onto a DrawingContext.
        /// </summary>
        /// <summary>
        /// Renders a full print sheet with cards, crop marks, and cut guides onto a DrawingContext.
        /// All drawing is in 96-DPI WPF units; callers apply ScaleTransform for higher DPI.
        /// </summary>
        public static void DrawSheet(DrawingContext dc, PrintSheetInfo sheet, SheetLayout layout)
        {
            double pageW = layout.PaperWidthWpf;
            double pageH = layout.PaperHeightWpf;

            // Page background (White)
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pageW, pageH));

            // Render each card
            foreach (var placement in sheet.Cards)
            {
                dc.PushTransform(new TranslateTransform(placement.Bounds.X, placement.Bounds.Y));
                CardRenderer.DrawCardSide(dc, placement.Side, placement.Bounds.Width, placement.Bounds.Height);
                dc.Pop();
            }

            // Draw crop marks / cut lines
            if (layout.DrawCutLines || layout.DrawCropMarks)
            {
                DrawGuides(dc, sheet, layout);
            }

            // Draw subtle page footer
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var footerText = new FormattedText(
                $"{sheet.Title} | Calling Card Generator",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                8.0,
                Brushes.Gray,
                1.0);

            dc.DrawText(footerText, new Point(layout.MarginLeftInches * 96.0, pageH - 24));
        }

        private static void DrawGuides(DrawingContext dc, PrintSheetInfo sheet, SheetLayout layout)
        {
            Pen guidePen = new Pen(new SolidColorBrush(Color.FromArgb(160, 180, 180, 180)), 0.6)
            {
                DashStyle = DashStyles.Dash
            };
            guidePen.Freeze();

            Pen cropPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 50, 50, 50)), 0.8);
            cropPen.Freeze();

            double tickLen = 14.0; // Crop mark tick length in points

            foreach (var card in sheet.Cards)
            {
                Rect r = card.Bounds;

                if (layout.DrawCutLines)
                {
                    // Draw outer border outline of the card
                    dc.DrawRectangle(null, guidePen, r);
                }

                if (layout.DrawCropMarks)
                {
                    // Top-Left corner ticks
                    dc.DrawLine(cropPen, new Point(r.Left - tickLen, r.Top), new Point(r.Left, r.Top));
                    dc.DrawLine(cropPen, new Point(r.Left, r.Top - tickLen), new Point(r.Left, r.Top));

                    // Top-Right corner ticks
                    dc.DrawLine(cropPen, new Point(r.Right, r.Top), new Point(r.Right + tickLen, r.Top));
                    dc.DrawLine(cropPen, new Point(r.Right, r.Top - tickLen), new Point(r.Right, r.Top));

                    // Bottom-Left corner ticks
                    dc.DrawLine(cropPen, new Point(r.Left - tickLen, r.Bottom), new Point(r.Left, r.Bottom));
                    dc.DrawLine(cropPen, new Point(r.Left, r.Bottom), new Point(r.Left, r.Bottom + tickLen));

                    // Bottom-Right corner ticks
                    dc.DrawLine(cropPen, new Point(r.Right, r.Bottom), new Point(r.Right + tickLen, r.Bottom));
                    dc.DrawLine(cropPen, new Point(r.Right, r.Bottom), new Point(r.Right, r.Bottom + tickLen));
                }
            }
        }

        /// <summary>
        /// Renders a sheet to a BitmapSource for live preview or export.
        /// </summary>
        public static RenderTargetBitmap RenderSheetToBitmap(PrintSheetInfo sheet, SheetLayout layout, double dpi = 150.0)
        {
            double scale = dpi / 96.0;
            int pixelW = (int)Math.Round(layout.PaperWidthWpf * scale);
            int pixelH = (int)Math.Round(layout.PaperHeightWpf * scale);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                DrawSheet(dc, sheet, layout);
                dc.Pop();
            }

            var rtb = new RenderTargetBitmap(pixelW, pixelH, 96.0, 96.0, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        /// <summary>
        /// Generates a WPF FixedDocument containing the print sheets.
        /// </summary>
        public static FixedDocument CreateFixedDocument(List<PrintSheetInfo> sheets, SheetLayout layout)
        {
            var fixedDoc = new FixedDocument();

            foreach (var sheet in sheets)
            {
                var pageContent = new PageContent();
                var fixedPage = new FixedPage
                {
                    Width = layout.PaperWidthWpf,
                    Height = layout.PaperHeightWpf
                };

                // Add visual element
                var container = new SheetDrawingControl(sheet, layout);
                container.Width = layout.PaperWidthWpf;
                container.Height = layout.PaperHeightWpf;
                fixedPage.Children.Add(container);

                ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
                fixedDoc.Pages.Add(pageContent);
            }

            return fixedDoc;
        }
    }

    public class SheetDrawingControl : FrameworkElement
    {
        private readonly PrintSheetInfo _sheet;
        private readonly SheetLayout _layout;

        public SheetDrawingControl(PrintSheetInfo sheet, SheetLayout layout)
        {
            _sheet = sheet;
            _layout = layout;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            PrintImpositionEngine.DrawSheet(drawingContext, _sheet, _layout);
        }
    }
}
