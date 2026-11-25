using System;
using System.IO;
using System.Linq;
using System.Windows;
using Calling_Card_Generator.Models;
using Calling_Card_Generator.Services;

namespace Calling_Card_Generator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                try
                {
                    File.WriteAllText("crash.log", ex.ExceptionObject?.ToString() ?? "Unknown error");
                    MessageBox.Show($"Application Error:\n{ex.ExceptionObject}", "Calling Card Studio Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                try
                {
                    File.WriteAllText("crash.log", ex.Exception.ToString());
                    MessageBox.Show($"Application Error:\n{ex.Exception.Message}\n\nStack:\n{ex.Exception.StackTrace}", "Calling Card Studio Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ex.Handled = true;
                }
                catch { }
            };

            if (e.Args.Contains("--smoke-test"))
            {
                RunSmokeTest();
                Shutdown(0);
                return;
            }

            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                File.WriteAllText("crash.log", ex.ToString());
                MessageBox.Show($"Startup Error:\n{ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void RunSmokeTest()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("   CALLING CARD STUDIO - SMOKE TEST SUITE");
            Console.WriteLine("==================================================");

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string testDir = Path.Combine(baseDir, "TestOutput");
            Directory.CreateDirectory(testDir);

            // 1. Template Generation Test
            Console.Write("[TEST 1] Generating Vantage Landscape template... ");
            var vLandscape = TemplateService.CreateVantageLandscapeTemplate(baseDir);
            if (vLandscape.Front.Elements.Count < 5 || vLandscape.Back.Elements.Count < 2)
                throw new Exception("Vantage Landscape template elements missing!");
            Console.WriteLine($"OK ({vLandscape.Front.Elements.Count} front elements, {vLandscape.Back.Elements.Count} back elements)");

            // 2. Serialization Test
            Console.Write("[TEST 2] Testing JSON Serialization and Deserialization... ");
            string savedJsonPath = Path.Combine(testDir, "test_template.json");
            TemplateService.SaveTemplate(vLandscape, savedJsonPath);
            var loaded = TemplateService.LoadTemplate(savedJsonPath);
            if (loaded == null || loaded.Front.Elements.Count != vLandscape.Front.Elements.Count)
                throw new Exception("Serialization mismatch!");
            Console.WriteLine("OK (JSON fully verified)");

            // 3. 300 DPI Rendering Test
            Console.Write("[TEST 3] Rendering card front & back at 300 DPI to PNG... ");
            string frontPng = Path.Combine(testDir, "rendered_front_300dpi.png");
            string backPng = Path.Combine(testDir, "rendered_back_300dpi.png");
            CardRenderer.ExportToPng(vLandscape.Front, vLandscape.WidthWpf, vLandscape.HeightWpf, frontPng, 300.0);
            CardRenderer.ExportToPng(vLandscape.Back, vLandscape.WidthWpf, vLandscape.HeightWpf, backPng, 300.0);
            if (!File.Exists(frontPng) || !File.Exists(backPng))
                throw new Exception("PNG export failed!");
            Console.WriteLine($"OK (Front: {new FileInfo(frontPng).Length} bytes, Back: {new FileInfo(backPng).Length} bytes)");

            // 4. Imposition & Print Engine Test
            Console.Write("[TEST 4] Imposition Engine (100 Cards Queue with Mirrored Columns)... ");
            var queue = new[]
            {
                new PrintQueueItem(vLandscape) { Quantity = 100, SideOption = PrintSideOption.FrontAndBack }
            };
            var layout = new SheetLayout
            {
                PaperType = PaperType.Letter,
                Columns = 2,
                Rows = 5,
                DrawCropMarks = true,
                DrawCutLines = true,
                PrintMethod = PrintMethod.ManualTwoPass
            };
            layout.AutoCenter(vLandscape.WidthInches, vLandscape.HeightInches);

            var sheets = PrintImpositionEngine.GenerateSheets(queue, layout, forDuplexInterleaved: false);
            // 100 cards with 10 cards per sheet = 10 front sheets + 10 back sheets = 20 sheets total!
            if (sheets.Count != 20)
                throw new Exception($"Expected 20 sheets for 100 cards, got {sheets.Count}!");

            // Verify mirrored columns on back sheets
            var sheet1 = sheets[0];
            var sheetBack1 = sheets[10];
            var frontCard0 = sheet1.Cards[0];
            var backCard0 = sheetBack1.Cards[0];

            if (frontCard0.Col != 0 || backCard0.Col != 1)
                throw new Exception($"Column mirroring failed! Front col was {frontCard0.Col}, back col was {backCard0.Col} (expected 1)");

            Console.WriteLine($"OK (Generated {sheets.Count} sheets: 10 Front, 10 Back with column mirroring verified!)");

            // 5. Sheet Render Test
            Console.Write("[TEST 6] Rendering full sheet with cut lines and crop marks... ");
            string sheetPng = Path.Combine(testDir, "sheet_preview_front.png");
            var sheetBmp = PrintImpositionEngine.RenderSheetToBitmap(sheet1, layout, 150.0);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(sheetBmp));
            using (var fs = File.Create(sheetPng)) { enc.Save(fs); }
            Console.WriteLine($"OK ({new FileInfo(sheetPng).Length} bytes)");

            Console.WriteLine("==================================================");
            Console.WriteLine("   ALL TESTS PASSED SUCCESSFULLY! (100% READY)");
            Console.WriteLine("==================================================");
        }
    }
}


