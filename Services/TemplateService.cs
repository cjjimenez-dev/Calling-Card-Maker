using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Calling_Card_Generator.Models;

namespace Calling_Card_Generator.Services
{
    public static class TemplateService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string ImageFileToBase64(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            byte[] bytes = File.ReadAllBytes(filePath);
            return Convert.ToBase64String(bytes);
        }

        public static void SaveTemplate(CardTemplate template, string filePath)
        {
            string json = JsonSerializer.Serialize(template, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        public static CardTemplate? LoadTemplate(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<CardTemplate>(json, JsonOptions);
        }

        public static CardTemplate CreateVantageLandscapeTemplate(string baseDir)
        {
            var template = new CardTemplate
            {
                Name = "Vantage Consulting - Landscape",
                Orientation = CardOrientation.Landscape,
                WidthInches = 3.5,
                HeightInches = 2.0
            };

            // 3.5" x 2.0" in WPF units: 336 x 192
            string logoPath = Path.Combine(baseDir, "Assets", "vantage_logo.png");
            string buildingPath = Path.Combine(baseDir, "Assets", "building_original.png");
            string logoBase64 = File.Exists(logoPath) ? ImageFileToBase64(logoPath) : string.Empty;
            string buildingBase64 = File.Exists(buildingPath) ? ImageFileToBase64(buildingPath) : string.Empty;

            template.Front.BackgroundColor = "#FFFFFF";

            // Lively Top Dual-Tone Accent Bar
            template.Front.Elements.Add(new ShapeElement
            {
                X = 0, Y = 0, Width = 336, Height = 4,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#0B1D4B", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 0
            });
            template.Front.Elements.Add(new ShapeElement
            {
                X = 0, Y = 4, Width = 210, Height = 2,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#2563EB", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 0
            });
            template.Front.Elements.Add(new ShapeElement
            {
                X = 210, Y = 4, Width = 126, Height = 2,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#38BDF8", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 0
            });

            // Left Vertical Accent Bar beside Name & Title
            template.Front.Elements.Add(new ShapeElement
            {
                X = 12, Y = 30, Width = 3, Height = 50, CornerRadius = 1.5,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#2563EB", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 1
            });

            // Soft Contact Info Backdrop Card
            template.Front.Elements.Add(new ShapeElement
            {
                X = 12, Y = 86, Width = 166, Height = 44, CornerRadius = 4,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#F8FAFC", StrokeColor = "#E2E8F0", StrokeThickness = 0.7, ZIndex = 1
            });

            // Bottom Accent Bar
            template.Front.Elements.Add(new ShapeElement
            {
                X = 0, Y = 188, Width = 336, Height = 4,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#0B1D4B", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 8
            });

            // Logo Top Right
            if (!string.IsNullOrEmpty(logoBase64))
            {
                template.Front.Elements.Add(new ImageElement
                {
                    X = 175,
                    Y = 12,
                    Width = 145,
                    Height = 36,
                    ImageBase64 = logoBase64,
                    ImagePath = logoPath,
                    ZIndex = 2
                });
            }

            // Address below logo
            template.Front.Elements.Add(new TextElement
            {
                X = 170,
                Y = 48,
                Width = 155,
                Height = 50,
                Text = "Unit 1705 West Tower PSE Centre\nExchange Rd, Ortigas Centre, Pasig\nTel # 687-7492 to 94 / 687-7528\nwww.vcsi.biz",
                FontFamily = "Arial",
                FontSize = 6.2,
                TextColor = "#374151",
                Alignment = TextAlignment.Right,
                ZIndex = 2
            });

            // Person Name (Left Side)
            template.Front.Elements.Add(new TextElement
            {
                X = 20,
                Y = 30,
                Width = 150,
                Height = 22,
                Text = "Alex Zarate Wong",
                FontFamily = "Arial",
                FontSize = 13,
                IsBold = true,
                TextColor = "#0F265C",
                Alignment = TextAlignment.Left,
                ZIndex = 3
            });

            template.Front.Elements.Add(new TextElement
            {
                X = 20,
                Y = 52,
                Width = 150,
                Height = 15,
                Text = "Applications Solution Manager",
                FontFamily = "Arial",
                FontSize = 7.5,
                IsBold = true,
                TextColor = "#1E3A8A",
                Alignment = TextAlignment.Left,
                ZIndex = 4
            });

            template.Front.Elements.Add(new TextElement
            {
                X = 20,
                Y = 67,
                Width = 150,
                Height = 14,
                Text = "ETP Department",
                FontFamily = "Segoe UI",
                FontSize = 7.5,
                TextColor = "#4B5563",
                Alignment = TextAlignment.Left,
                ZIndex = 5
            });

            // Contact
            template.Front.Elements.Add(new TextElement
            {
                X = 18,
                Y = 91,
                Width = 156,
                Height = 36,
                Text = "+632 9228173767 / +632 9157535227\nalexzwong@vcsi.biz\nalexzwong@yahoo.com",
                FontFamily = "Arial",
                FontSize = 6.9,
                TextColor = "#1F2937",
                Alignment = TextAlignment.Left,
                ZIndex = 6
            });

            // Bottom Building Graphic
            if (!string.IsNullOrEmpty(buildingBase64))
            {
                template.Front.Elements.Add(new ImageElement
                {
                    X = 180,
                    Y = 100,
                    Width = 156,
                    Height = 92,
                    ImageBase64 = buildingBase64,
                    ImagePath = buildingPath,
                    Stretch = System.Windows.Media.Stretch.UniformToFill,
                    Opacity = 0.9,
                    ZIndex = 7
                });
            }

            // BACK SIDE: Landscape (Lively corporate design with 5 Software/POS Solutions & QR)
            template.Back.BackgroundColor = "#FFFFFF";

            // Top Dual-Tone Accent Bar
            template.Back.Elements.Add(new ShapeElement
            {
                X = 0, Y = 0, Width = 336, Height = 4,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#0B1D4B", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 0
            });
            template.Back.Elements.Add(new ShapeElement
            {
                X = 0, Y = 4, Width = 210, Height = 2,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#2563EB", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 0
            });
            template.Back.Elements.Add(new ShapeElement
            {
                X = 210, Y = 4, Width = 126, Height = 2,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#38BDF8", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 0
            });

            if (!string.IsNullOrEmpty(logoBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 92, Y = 12, Width = 152, Height = 38,
                    ImageBase64 = logoBase64, ImagePath = logoPath, ZIndex = 1
                });
            }

            template.Back.Elements.Add(new TextElement
            {
                X = 30, Y = 51, Width = 276, Height = 15,
                Text = "\"an end to end solutions provider\"",
                FontFamily = "Segoe UI", FontSize = 8.5, IsItalic = true,
                TextColor = "#2563EB", Alignment = TextAlignment.Center, ZIndex = 2
            });

            // Left Solutions Showcase Card
            template.Back.Elements.Add(new ShapeElement
            {
                X = 14, Y = 71, Width = 224, Height = 107, CornerRadius = 6,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#FFFFFF", StrokeColor = "#CBD5E1", StrokeThickness = 0.8, ZIndex = 2
            });
            template.Back.Elements.Add(new ShapeElement
            {
                X = 14, Y = 71, Width = 224, Height = 3, CornerRadius = 1.5,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#2563EB", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 3
            });
            template.Back.Elements.Add(new TextElement
            {
                X = 22, Y = 78, Width = 208, Height = 13,
                Text = "OUR SOFTWARE & POS SOLUTIONS",
                FontFamily = "Segoe UI", FontSize = 6.3, IsBold = true,
                TextColor = "#0B1D4B", Alignment = TextAlignment.Left, ZIndex = 4
            });

            string wboxPath = Path.Combine(baseDir, "Assets", "logo_wbox.png");
            string wboxBase64 = File.Exists(wboxPath) ? ImageFileToBase64(wboxPath) : string.Empty;
            if (!string.IsNullOrEmpty(wboxBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 22, Y = 96, Width = 62, Height = 22,
                    ImageBase64 = wboxBase64, ImagePath = wboxPath,
                    Stretch = System.Windows.Media.Stretch.Uniform, ZIndex = 5
                });
            }

            string hotelKeyPath = Path.Combine(baseDir, "Assets", "logo_hotelkey.png");
            string hotelKeyBase64 = File.Exists(hotelKeyPath) ? ImageFileToBase64(hotelKeyPath) : string.Empty;
            if (!string.IsNullOrEmpty(hotelKeyBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 93, Y = 99, Width = 68, Height = 17,
                    ImageBase64 = hotelKeyBase64, ImagePath = hotelKeyPath,
                    Stretch = System.Windows.Media.Stretch.Uniform, ZIndex = 6
                });
            }

            string posiflexPath = Path.Combine(baseDir, "Assets", "logo_posiflex.png");
            string posiflexBase64 = File.Exists(posiflexPath) ? ImageFileToBase64(posiflexPath) : string.Empty;
            if (!string.IsNullOrEmpty(posiflexBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 169, Y = 101, Width = 60, Height = 12,
                    ImageBase64 = posiflexBase64, ImagePath = posiflexPath,
                    Stretch = System.Windows.Media.Stretch.Uniform, ZIndex = 7
                });
            }

            string optCtrlPath = Path.Combine(baseDir, "Assets", "logo_optimum_control.png");
            string optCtrlBase64 = File.Exists(optCtrlPath) ? ImageFileToBase64(optCtrlPath) : string.Empty;
            if (!string.IsNullOrEmpty(optCtrlBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 24, Y = 131, Width = 104, Height = 32,
                    ImageBase64 = optCtrlBase64, ImagePath = optCtrlPath,
                    Stretch = System.Windows.Media.Stretch.Uniform, ZIndex = 8
                });
            }

            string qsolvePath = Path.Combine(baseDir, "Assets", "logo_qsolve.png");
            string qsolveBase64 = File.Exists(qsolvePath) ? ImageFileToBase64(qsolvePath) : string.Empty;
            if (!string.IsNullOrEmpty(qsolveBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 138, Y = 139, Width = 90, Height = 15,
                    ImageBase64 = qsolveBase64, ImagePath = qsolvePath,
                    Stretch = System.Windows.Media.Stretch.Uniform, ZIndex = 9
                });
            }

            // Right QR Card
            template.Back.Elements.Add(new ShapeElement
            {
                X = 246, Y = 71, Width = 76, Height = 107, CornerRadius = 6,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#F0F7FF", StrokeColor = "#BFDBFE", StrokeThickness = 0.8, ZIndex = 2
            });
            template.Back.Elements.Add(new TextElement
            {
                X = 248, Y = 77, Width = 72, Height = 11,
                Text = "SCAN TO VISIT",
                FontFamily = "Segoe UI", FontSize = 5.8, IsBold = true,
                TextColor = "#2563EB", Alignment = TextAlignment.Center, ZIndex = 3
            });
            template.Back.Elements.Add(new QrCodeElement
            {
                X = 256, Y = 90, Width = 56, Height = 56,
                QrContent = "https://www.vcsi.biz",
                DarkColor = "#0B1D4B", LightColor = "#FFFFFF", IncludeQuietZone = false, ZIndex = 4
            });
            template.Back.Elements.Add(new TextElement
            {
                X = 246, Y = 153, Width = 76, Height = 15,
                Text = "www.vcsi.biz",
                FontFamily = "Segoe UI", FontSize = 7.8, IsBold = true,
                TextColor = "#0B1D4B", Alignment = TextAlignment.Center, ZIndex = 5
            });

            // Bottom Accent Bar
            template.Back.Elements.Add(new ShapeElement
            {
                X = 0, Y = 188, Width = 336, Height = 4,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#0B1D4B", StrokeColor = "Transparent", StrokeThickness = 0, ZIndex = 10
            });

            return template;
        }

        public static CardTemplate CreateBlankTemplate(CardOrientation orientation = CardOrientation.Landscape)
        {
            double w = orientation == CardOrientation.Landscape ? 3.5 : 2.0;
            double h = orientation == CardOrientation.Landscape ? 2.0 : 3.5;

            var template = new CardTemplate
            {
                Name = $"Blank ({orientation})",
                Orientation = orientation,
                WidthInches = w,
                HeightInches = h
            };

            template.Front.BackgroundColor = "#FFFFFF";
            template.Back.BackgroundColor = "#F8FAFC";

            template.Front.Elements.Add(new TextElement
            {
                X = 20,
                Y = 20,
                Width = 160,
                Height = 25,
                Text = "Your Name Here",
                FontFamily = "Arial",
                FontSize = 14,
                IsBold = true,
                TextColor = "#111827",
                Alignment = TextAlignment.Left,
                ZIndex = 1
            });

            template.Front.Elements.Add(new TextElement
            {
                X = 20,
                Y = 48,
                Width = 160,
                Height = 18,
                Text = "Your Job Title",
                FontFamily = "Segoe UI",
                FontSize = 9,
                TextColor = "#4B5563",
                Alignment = TextAlignment.Left,
                ZIndex = 2
            });

            return template;
        }

        public static string UserDataDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CallingCardStudio");

        public static string SavedTemplatesDir => Path.Combine(UserDataDir, "SavedTemplates");
        public static string LastSessionPath => Path.Combine(UserDataDir, "last_session.cct.json");

        public static void SaveLastSession(CardTemplate? template)
        {
            if (template == null) return;
            try
            {
                Directory.CreateDirectory(UserDataDir);
                SaveTemplate(template, LastSessionPath);
            }
            catch { }
        }

        public static CardTemplate? LoadLastSession()
        {
            try
            {
                if (!File.Exists(LastSessionPath)) return null;
                return LoadTemplate(LastSessionPath);
            }
            catch
            {
                return null;
            }
        }

        public static string SaveToLibrary(CardTemplate template)
        {
            Directory.CreateDirectory(SavedTemplatesDir);
            string rawName = string.IsNullOrWhiteSpace(template.Name) ? "Untitled_Card" : template.Name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                rawName = rawName.Replace(c, '_');
            }
            string filePath = Path.Combine(SavedTemplatesDir, $"{rawName}.cct.json");
            SaveTemplate(template, filePath);
            return filePath;
        }

        public static System.Collections.Generic.List<(string FilePath, CardTemplate Template)> LoadLibraryTemplates()
        {
            var result = new System.Collections.Generic.List<(string, CardTemplate)>();
            try
            {
                if (!Directory.Exists(SavedTemplatesDir)) return result;
                foreach (string file in Directory.GetFiles(SavedTemplatesDir, "*.cct.json"))
                {
                    var tmpl = LoadTemplate(file);
                    if (tmpl != null)
                    {
                        result.Add((file, tmpl));
                    }
                }
            }
            catch { }
            return result;
        }
    }
}
