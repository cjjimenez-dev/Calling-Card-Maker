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
                    ZIndex = 1
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
                X = 14,
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
                X = 14,
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
                X = 14,
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
                X = 14,
                Y = 88,
                Width = 180,
                Height = 36,
                Text = "+632 9228173767 / +632 9157535227\nalexzwong@vcsi.biz\nalexzwong@yahoo.com",
                FontFamily = "Arial",
                FontSize = 7.2,
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

            // BACK SIDE: Landscape (White background with Vantage identity, Software Solutions, & QR)
            template.Back.BackgroundColor = "#FFFFFF";

            if (!string.IsNullOrEmpty(logoBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 88,
                    Y = 20,
                    Width = 160,
                    Height = 40,
                    ImageBase64 = logoBase64,
                    ImagePath = logoPath,
                    ZIndex = 1
                });
            }

            template.Back.Elements.Add(new TextElement
            {
                X = 30,
                Y = 62,
                Width = 276,
                Height = 16,
                Text = "\"an end to end solutions provider\"",
                FontFamily = "Segoe UI",
                FontSize = 8.5,
                IsItalic = true,
                TextColor = "#60A5FA",
                Alignment = TextAlignment.Center,
                ZIndex = 2
            });

            template.Back.Elements.Add(new ShapeElement
            {
                X = 22,
                Y = 83,
                Width = 292,
                Height = 1,
                ShapeKind = ShapeType.Line,
                StrokeColor = "#E2E8F0",
                StrokeThickness = 0.8,
                ZIndex = 3
            });

            template.Back.Elements.Add(new TextElement
            {
                X = 22,
                Y = 91,
                Width = 210,
                Height = 13,
                Text = "SOFTWARE SOLUTIONS",
                FontFamily = "Segoe UI",
                FontSize = 6.5,
                IsBold = true,
                TextColor = "#64748B",
                Alignment = TextAlignment.Left,
                ZIndex = 4
            });

            string wboxPath = Path.Combine(baseDir, "Assets", "logo_wbox.png");
            string wboxBase64 = File.Exists(wboxPath) ? ImageFileToBase64(wboxPath) : string.Empty;
            if (!string.IsNullOrEmpty(wboxBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 22,
                    Y = 108,
                    Width = 86,
                    Height = 31,
                    ImageBase64 = wboxBase64,
                    ImagePath = wboxPath,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    ZIndex = 5
                });
            }

            string hotelKeyPath = Path.Combine(baseDir, "Assets", "logo_hotelkey.png");
            string hotelKeyBase64 = File.Exists(hotelKeyPath) ? ImageFileToBase64(hotelKeyPath) : string.Empty;
            if (!string.IsNullOrEmpty(hotelKeyBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 122,
                    Y = 111,
                    Width = 98,
                    Height = 24,
                    ImageBase64 = hotelKeyBase64,
                    ImagePath = hotelKeyPath,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    ZIndex = 6
                });
            }

            string qsolvePath = Path.Combine(baseDir, "Assets", "logo_qsolve.png");
            string qsolveBase64 = File.Exists(qsolvePath) ? ImageFileToBase64(qsolvePath) : string.Empty;
            if (!string.IsNullOrEmpty(qsolveBase64))
            {
                template.Back.Elements.Add(new ImageElement
                {
                    X = 64,
                    Y = 147,
                    Width = 114,
                    Height = 19,
                    ImageBase64 = qsolveBase64,
                    ImagePath = qsolvePath,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    ZIndex = 7
                });
            }

            template.Back.Elements.Add(new QrCodeElement
            {
                X = 254,
                Y = 98,
                Width = 56,
                Height = 56,
                QrContent = "https://www.vcsi.biz",
                DarkColor = "#0B1D4B",
                LightColor = "#FFFFFF",
                IncludeQuietZone = false,
                ZIndex = 8
            });

            template.Back.Elements.Add(new TextElement
            {
                X = 238,
                Y = 157,
                Width = 88,
                Height = 16,
                Text = "www.vcsi.biz",
                FontFamily = "Segoe UI",
                FontSize = 8,
                IsBold = true,
                TextColor = "#1E293B",
                Alignment = TextAlignment.Center,
                ZIndex = 9
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
