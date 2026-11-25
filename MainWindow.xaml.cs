using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Calling_Card_Generator.Controls;
using Calling_Card_Generator.Models;
using Calling_Card_Generator.Services;
using Microsoft.Win32;
using System.Text.Json;

namespace Calling_Card_Generator
{
    public partial class MainWindow : Window
    {
        private CardTemplate _template = null!;
        private bool _isEditingFront = true;
        private readonly ObservableCollection<PrintQueueItem> _printQueue = new();
        private readonly SheetLayout _sheetLayout = new();

        private List<PrintSheetInfo> _currentPreviewSheets = new();
        private int _currentSheetIndex = 0;
        private bool _isUpdatingUi = false;
        private bool _isLoaded = false;

        private readonly System.Windows.Threading.DispatcherTimer _autoSaveTimer;

        public MainWindow()
        {
            InitializeComponent();

            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _autoSaveTimer.Tick += (s, e) => FlushAutoSave();

            Loaded += MainWindow_Loaded;
            Deactivated += (s, e) => FlushAutoSave();
            Closing += (s, e) => FlushAutoSave();
        }

        private void ScheduleAutoSave()
        {
            if (!_isLoaded || _isUpdatingUi || _template == null) return;
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void FlushAutoSave()
        {
            _autoSaveTimer.Stop();
            if (_template != null)
            {
                TemplateService.SaveLastSession(_template);
            }
        }

        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();
        private bool _isPerformingUndoRedo = false;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;

            // Restore last edited template if available, otherwise start with Vantage Landscape
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var lastSession = TemplateService.LoadLastSession();
            _template = lastSession ?? TemplateService.CreateVantageLandscapeTemplate(baseDir);

            // Hook canvas events
            CardCanvas.SelectedElementChanged += CardCanvas_SelectedElementChanged;
            CardCanvas.SelectedElementsChanged += CardCanvas_SelectedElementsChanged;
            CardCanvas.CanvasModified += CardCanvas_CanvasModified;
            CardCanvas.ElementTransforming += CardCanvas_ElementTransforming;
            CardCanvas.ElementDeleteRequested += (s, el) => DeleteElement(el);
            CardCanvas.ElementDuplicateRequested += (s, el) => DuplicateElement(el);

            // Setup Print Queue DataGrid
            DgPrintQueue.ItemsSource = _printQueue;
            _printQueue.Add(new PrintQueueItem(_template.Clone()) { Quantity = 10 });

            // Initialize Sheet Layout
            _sheetLayout.Columns = 2;
            _sheetLayout.Rows = 5;
            _sheetLayout.AutoCenter(_template.WidthInches, _template.HeightInches);
            TxtMarginLeft.Text = _sheetLayout.MarginLeftInches.ToString("0.00");
            TxtMarginTop.Text = _sheetLayout.MarginTopInches.ToString("0.00");

            RefreshPresetsDropdown(lastSession != null ? _template.Name : null);
            LoadTemplateIntoUi(_template, isFront: true);
            UpdateUndoRedoButtons();
            RefreshSheetPreview();
        }

        private CardSide CurrentSide => _isEditingFront ? _template.Front : _template.Back;

        private void LoadTemplateIntoUi(CardTemplate template, bool isFront = true)
        {
            _template = template;
            _isEditingFront = isFront;

            _isUpdatingUi = true;
            TxtCardName.Text = _template.Name;
            RbPortrait.IsChecked = _template.Orientation == CardOrientation.Portrait;
            RbLandscape.IsChecked = _template.Orientation == CardOrientation.Landscape;
            _isUpdatingUi = false;

            UpdateSideButtons();
            CardCanvas.SetCardData(_template, CurrentSide);
            UpdateCanvasHeader();
            UpdateBackgroundPropertyUi();
        }

        private void UpdateSideButtons()
        {
            if (_isEditingFront)
            {
                BtnSideFront.Style = (Style)FindResource("CleanPrimaryButton");
                BtnSideBack.Style = (Style)FindResource("CleanButton");
            }
            else
            {
                BtnSideFront.Style = (Style)FindResource("CleanButton");
                BtnSideBack.Style = (Style)FindResource("CleanPrimaryButton");
            }

            UpdateBackgroundPropertyUi();
        }

        private void UpdateBackgroundPropertyUi()
        {
            _isUpdatingUi = true;
            if (TxtActiveSideLabel != null)
                TxtActiveSideLabel.Text = _isEditingFront ? "📄 Front Side" : "📄 Back Side";
            if (TxtSideBgHex != null)
                TxtSideBgHex.Text = CurrentSide.BackgroundColor;
            if (TxtBgHex != null)
                TxtBgHex.Text = CurrentSide.BackgroundColor;

            var brush = CardRenderer.ParseColorBrush(CurrentSide.BackgroundColor, Brushes.White);
            if (RectSideBgPreview != null) RectSideBgPreview.Background = brush;
            if (RectLeftBgPreview != null) RectLeftBgPreview.Background = brush;
            if (TxtCardOrientDesc != null)
                TxtCardOrientDesc.Text = _template.Orientation == CardOrientation.Landscape ? "Landscape (3.5″ × 2.0″)" : "Portrait (2.0″ × 3.5″)";
            _isUpdatingUi = false;
        }

        private void UpdateCanvasHeader()
        {
            string sideText = _isEditingFront ? "Front Side" : "Back Side";
            TxtCanvasHeader.Text = $"{_template.Name} ({sideText})";
            TxtCanvasSizeDesc.Text = $" • {_template.WidthInches:0.0}\" × {_template.HeightInches:0.0}\" ({_template.Orientation}) @ 300 DPI";
            TxtStatusCoords.Text = $"Orientation: {_template.Orientation} | Dimensions: {_template.WidthInches:0.0}\" × {_template.HeightInches:0.0}\"";
        }

        #region Undo / Redo Management
        public void RecordUndoState()
        {
            if (_isPerformingUndoRedo || _isUpdatingUi || _template == null) return;

            try
            {
                string json = JsonSerializer.Serialize(_template);
                if (_undoStack.Count > 0 && _undoStack.Peek() == json) return;

                _undoStack.Push(json);
                _redoStack.Clear();

                if (_undoStack.Count > 50)
                {
                    var items = _undoStack.ToArray();
                    _undoStack.Clear();
                    for (int i = 49; i >= 0; i--)
                    {
                        _undoStack.Push(items[i]);
                    }
                }

                UpdateUndoRedoButtons();
                ScheduleAutoSave();
            }
            catch { }
        }

        public void PerformUndo()
        {
            if (_undoStack.Count == 0 || _template == null) return;

            try
            {
                _isPerformingUndoRedo = true;
                string currentJson = JsonSerializer.Serialize(_template);
                _redoStack.Push(currentJson);

                string prevJson = _undoStack.Pop();
                var restored = JsonSerializer.Deserialize<CardTemplate>(prevJson);
                if (restored != null)
                {
                    _template = restored;
                    LoadTemplateIntoUi(_template, _isEditingFront);
                    TxtStatusMessage.Text = "Action undone.";
                    ScheduleAutoSave();
                }
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Undo failed: {ex.Message}";
            }
            finally
            {
                _isPerformingUndoRedo = false;
                UpdateUndoRedoButtons();
            }
        }

        public void PerformRedo()
        {
            if (_redoStack.Count == 0 || _template == null) return;

            try
            {
                _isPerformingUndoRedo = true;
                string currentJson = JsonSerializer.Serialize(_template);
                _undoStack.Push(currentJson);

                string nextJson = _redoStack.Pop();
                var restored = JsonSerializer.Deserialize<CardTemplate>(nextJson);
                if (restored != null)
                {
                    _template = restored;
                    LoadTemplateIntoUi(_template, _isEditingFront);
                    TxtStatusMessage.Text = "Action redone.";
                    ScheduleAutoSave();
                }
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Redo failed: {ex.Message}";
            }
            finally
            {
                _isPerformingUndoRedo = false;
                UpdateUndoRedoButtons();
            }
        }

        private void UpdateUndoRedoButtons()
        {
            if (BtnUndo != null)
            {
                BtnUndo.IsEnabled = _undoStack.Count > 0;
                BtnUndo.Opacity = _undoStack.Count > 0 ? 1.0 : 0.45;
            }
            if (BtnRedo != null)
            {
                BtnRedo.IsEnabled = _redoStack.Count > 0;
                BtnRedo.Opacity = _redoStack.Count > 0 ? 1.0 : 0.45;
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e) => PerformUndo();
        private void BtnRedo_Click(object sender, RoutedEventArgs e) => PerformRedo();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    e.Handled = true;
                    PerformUndo();
                }
                else if (e.Key == Key.Y)
                {
                    e.Handled = true;
                    PerformRedo();
                }
                else if (e.Key == Key.G)
                {
                    e.Handled = true;
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    {
                        UngroupSelectedElement();
                    }
                    else
                    {
                        GroupSelectedElements();
                    }
                }
            }
        }
        #endregion

        #region Mode Navigation
        private void TabDesigner_Click(object sender, RoutedEventArgs e)
        {
            DesignerContainer.Visibility = Visibility.Visible;
            PrintQueueContainer.Visibility = Visibility.Collapsed;
        }

        private void TabPrintQueue_Click(object sender, RoutedEventArgs e)
        {
            DesignerContainer.Visibility = Visibility.Collapsed;
            PrintQueueContainer.Visibility = Visibility.Visible;
            RefreshSheetPreview();
        }
        #endregion

        #region Template Presets & Management
        private void RefreshPresetsDropdown(string? selectSavedName = null)
        {
            if (CmbPresets == null) return;
            _isUpdatingUi = true;

            CmbPresets.Items.Clear();
            CmbPresets.Items.Add(new ComboBoxItem { Content = "Vantage Consulting (Landscape)" });
            CmbPresets.Items.Add(new ComboBoxItem { Content = "Blank Canvas (Landscape 3.5×2.0\")" });
            CmbPresets.Items.Add(new ComboBoxItem { Content = "Blank Canvas (Portrait 2.0×3.5\")" });

            int selectedIdx = 0;
            var saved = TemplateService.LoadLibraryTemplates();
            foreach (var (filePath, tmpl) in saved)
            {
                var item = new ComboBoxItem
                {
                    Content = $"★ {tmpl.Name}",
                    Tag = filePath
                };
                int idx = CmbPresets.Items.Add(item);
                if (!string.IsNullOrEmpty(selectSavedName) &&
                    string.Equals(tmpl.Name, selectSavedName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIdx = idx;
                }
            }

            CmbPresets.SelectedIndex = selectedIdx;
            _isUpdatingUi = false;
        }

        private void CmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _isUpdatingUi || CardCanvas == null || CmbPresets.SelectedIndex < 0) return;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            CardTemplate? tmpl = null;
            if (CmbPresets.SelectedItem is ComboBoxItem item && item.Tag is string savedPath)
            {
                tmpl = TemplateService.LoadTemplate(savedPath);
            }

            tmpl ??= CmbPresets.SelectedIndex switch
            {
                0 => TemplateService.CreateVantageLandscapeTemplate(baseDir),
                1 => TemplateService.CreateBlankTemplate(CardOrientation.Landscape),
                2 => TemplateService.CreateBlankTemplate(CardOrientation.Portrait),
                _ => TemplateService.CreateVantageLandscapeTemplate(baseDir)
            };

            RecordUndoState();
            LoadTemplateIntoUi(tmpl, isFront: true);
            TemplateService.SaveLastSession(_template);
            TxtStatusMessage.Text = $"Loaded preset: {tmpl.Name}";
        }

        private void TxtCardName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _template == null) return;
            _template.Name = TxtCardName.Text;
            UpdateCanvasHeader();
        }

        private void RbOrientation_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _template == null) return;

            var newOrient = RbPortrait.IsChecked == true ? CardOrientation.Portrait : CardOrientation.Landscape;
            if (newOrient != _template.Orientation)
            {
                RecordUndoState();
                _template.SetOrientation(newOrient);

                // Reposition any elements that overflow the new card dimensions
                foreach (var el in _template.Front.Elements)
                {
                    if (el.Y + el.Height > _template.HeightWpf)
                        el.Y = Math.Max(0, _template.HeightWpf - el.Height - 8);
                    if (el.X + el.Width > _template.WidthWpf)
                        el.X = Math.Max(0, _template.WidthWpf - el.Width - 8);
                }
                foreach (var el in _template.Back.Elements)
                {
                    if (el.Y + el.Height > _template.HeightWpf)
                        el.Y = Math.Max(0, _template.HeightWpf - el.Height - 8);
                    if (el.X + el.Width > _template.WidthWpf)
                        el.X = Math.Max(0, _template.WidthWpf - el.Width - 8);
                }

                CardCanvas.RefreshCanvas();
                UpdateCanvasHeader();
                _sheetLayout.AutoCenter(_template.WidthInches, _template.HeightInches);
                TxtMarginLeft.Text = _sheetLayout.MarginLeftInches.ToString("0.00");
                TxtMarginTop.Text = _sheetLayout.MarginTopInches.ToString("0.00");
            }
        }

        private void BtnSideFront_Click(object sender, RoutedEventArgs e)
        {
            _isEditingFront = true;
            UpdateSideButtons();
            CardCanvas.SetCardData(_template, _template.Front);
            UpdateCanvasHeader();
            UpdateBackgroundPropertyUi();
        }

        private void BtnSideBack_Click(object sender, RoutedEventArgs e)
        {
            _isEditingFront = false;
            UpdateSideButtons();
            CardCanvas.SetCardData(_template, _template.Back);
            UpdateCanvasHeader();
            UpdateBackgroundPropertyUi();
        }

        private void BtnSwitchSideQuick_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingFront)
            {
                BtnSideBack_Click(sender, e);
            }
            else
            {
                BtnSideFront_Click(sender, e);
            }
        }

        private void BtnSaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_template == null) return;
            TemplateService.SaveToLibrary(_template);
            TemplateService.SaveLastSession(_template);
            RefreshPresetsDropdown(_template.Name);
            TxtStatusMessage.Text = $"Saved '{_template.Name}' to program presets.";
            MessageBox.Show($"Template '{_template.Name}' is now saved in the program!\nYou can select it anytime under Presets, and it will stay loaded when you reopen the app.", "Template Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSaveTemplateAs_Click(object sender, RoutedEventArgs e)
        {
            SaveTemplateToFile(true);
        }

        private void SaveTemplateToFile(bool forceDialog)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Calling Card Template",
                Filter = "Calling Card Template (*.cct.json;*.json)|*.cct.json;*.json",
                FileName = $"{_template.Name.Replace(" ", "_")}.cct.json"
            };

            if (dialog.ShowDialog() == true)
            {
                TemplateService.SaveTemplate(_template, dialog.FileName);
                TxtStatusMessage.Text = $"Template saved: {Path.GetFileName(dialog.FileName)}";
                MessageBox.Show($"Template successfully saved to:\n{dialog.FileName}", "Template Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Calling Card Template",
                Filter = "Calling Card Template (*.cct.json;*.json)|*.cct.json;*.json|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var loaded = TemplateService.LoadTemplate(dialog.FileName);
                if (loaded != null)
                {
                    LoadTemplateIntoUi(loaded, isFront: true);
                    TemplateService.SaveLastSession(_template);
                    TxtStatusMessage.Text = $"Template opened: {loaded.Name}";
                }
                else
                {
                    MessageBox.Show("Could not load template file. Please verify format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Card to High-Res Image (300 DPI)",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                FileName = $"{_template.Name}_{(_isEditingFront ? "Front" : "Back")}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                CardRenderer.ExportToPng(CurrentSide, _template.WidthWpf, _template.HeightWpf, dialog.FileName, 300.0);
                TxtStatusMessage.Text = $"Exported 300 DPI image: {Path.GetFileName(dialog.FileName)}";
                MessageBox.Show($"High-resolution calling card image saved to:\n{dialog.FileName}", "Card Exported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region Toolbox & Element Addition
        private void BtnAddText_Click(object sender, RoutedEventArgs e)
        {
            RecordUndoState();
            var newText = new TextElement
            {
                X = Math.Max(10, (_template.WidthWpf / 2) - 60),
                Y = Math.Max(10, (_template.HeightWpf / 2) - 15),
                Width = 120,
                Height = 30,
                Text = "New Text",
                FontSize = 11,
                TextColor = "#111827",
                ZIndex = CurrentSide.Elements.Count + 1
            };

            CurrentSide.Elements.Add(newText);
            CardCanvas.SelectedElement = newText;
        }

        private void BtnAddImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Image or Logo",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                RecordUndoState();
                string b64 = TemplateService.ImageFileToBase64(dialog.FileName);
                var newImg = new ImageElement
                {
                    X = Math.Max(10, (_template.WidthWpf / 2) - 50),
                    Y = Math.Max(10, (_template.HeightWpf / 2) - 30),
                    Width = 100,
                    Height = 60,
                    ImagePath = dialog.FileName,
                    ImageBase64 = b64,
                    ZIndex = CurrentSide.Elements.Count + 1
                };

                CurrentSide.Elements.Add(newImg);
                CardCanvas.SelectedElement = newImg;
            }
        }

        private void BtnAddLine_Click(object sender, RoutedEventArgs e)
        {
            RecordUndoState();
            var newLine = new ShapeElement
            {
                X = 15,
                Y = _template.HeightWpf / 2,
                Width = Math.Max(20, _template.WidthWpf - 30),
                Height = 2,
                ShapeKind = ShapeType.Line,
                StrokeColor = "#94A3B8",
                StrokeThickness = 1.5,
                ZIndex = CurrentSide.Elements.Count + 1
            };

            CurrentSide.Elements.Add(newLine);
            CardCanvas.SelectedElement = newLine;
        }

        private void BtnAddRectangle_Click(object sender, RoutedEventArgs e)
        {
            RecordUndoState();
            var newRect = new ShapeElement
            {
                X = 20,
                Y = 20,
                Width = 80,
                Height = 50,
                ShapeKind = ShapeType.Rectangle,
                FillColor = "#E2E8F0",
                StrokeColor = "#64748B",
                StrokeThickness = 1,
                CornerRadius = 4,
                ZIndex = CurrentSide.Elements.Count + 1
            };

            CurrentSide.Elements.Add(newRect);
            CardCanvas.SelectedElement = newRect;
        }

        private void BtnAddQrCode_Click(object sender, RoutedEventArgs e)
        {
            RecordUndoState();
            var newQr = new QrCodeElement
            {
                X = Math.Max(14, (_template.WidthWpf / 2) - 28),
                Y = Math.Max(14, (_template.HeightWpf / 2) - 28),
                Width = 56,
                Height = 56,
                QrContent = "https://www.vcsi.biz",
                DarkColor = "#0B1D4B",
                LightColor = "#FFFFFF",
                ZIndex = CurrentSide.Elements.Count + 1
            };

            CurrentSide.Elements.Add(newQr);
            CardCanvas.SelectedElement = newQr;
            ScheduleAutoSave();
        }
        #endregion

        #region Background & View Controls
        private void BtnSetBgColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                RecordUndoState();
                ApplyHexColor(hex);
            }
        }

        private void TxtSideBgHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _template == null) return;
            ApplyHexColor(TxtSideBgHex.Text);
        }

        private void TxtSideBgHex_LostFocus(object sender, RoutedEventArgs e) => ApplyHexColor(TxtSideBgHex.Text);
        private void TxtSideBgHex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyHexColor(TxtSideBgHex.Text);
        }

        private void TxtBgHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _template == null) return;
            ApplyHexColor(TxtBgHex.Text);
        }

        private void TxtBgHex_LostFocus(object sender, RoutedEventArgs e) => ApplyHexColor(TxtBgHex.Text);
        private void TxtBgHex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyHexColor(TxtBgHex.Text);
        }

        private void ApplyHexColor(string inputHex)
        {
            string hex = inputHex.Trim();
            if (string.IsNullOrEmpty(hex)) return;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (hex.Length != 7 && hex.Length != 9 && hex.Length != 4) return;

            try
            {
                ColorConverter.ConvertFromString(hex);
                CurrentSide.BackgroundColor = hex;
                var brush = CardRenderer.ParseColorBrush(hex, Brushes.White);

                _isUpdatingUi = true;
                if (TxtSideBgHex != null && TxtSideBgHex.Text != hex) TxtSideBgHex.Text = hex;
                if (TxtBgHex != null && TxtBgHex.Text != hex) TxtBgHex.Text = hex;
                if (RectSideBgPreview != null) RectSideBgPreview.Background = brush;
                if (RectLeftBgPreview != null) RectLeftBgPreview.Background = brush;
                _isUpdatingUi = false;

                CardCanvas.RefreshCanvas();
            }
            catch { }
        }

        private void BtnSetBgImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Background Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dialog.ShowDialog() == true)
            {
                RecordUndoState();
                CurrentSide.BackgroundImageBase64 = TemplateService.ImageFileToBase64(dialog.FileName);
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnClearBgImage_Click(object sender, RoutedEventArgs e)
        {
            RecordUndoState();
            CurrentSide.BackgroundImageBase64 = string.Empty;
            CardCanvas.RefreshCanvas();
        }

        private void CmbZoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || CardCanvas == null || CmbZoom.SelectedIndex < 0) return;
            double factor = CmbZoom.SelectedIndex switch
            {
                0 => 0.75,
                1 => 1.0,
                2 => 1.25,
                3 => 1.5,
                4 => 2.0,
                5 => 2.5,
                6 => 3.0,
                _ => 1.5
            };
            CardCanvas.ZoomFactor = factor;
        }

        private void ChkGuide_Changed(object sender, RoutedEventArgs e)
        {
            if (CardCanvas == null) return;
            CardCanvas.ShowSafeGuide = ChkSafeGuide.IsChecked == true;
            CardCanvas.ShowBleedGuide = ChkBleedGuide.IsChecked == true;
        }
        #endregion

        #region Element Properties Synchronization
        private void UpdateGroupButtonsState()
        {
            if (BtnGroup != null)
            {
                bool canGroup = CardCanvas.SelectedElements.Count >= 2;
                BtnGroup.IsEnabled = canGroup;
                BtnGroup.Opacity = canGroup ? 1.0 : 0.45;
            }
            if (BtnUngroup != null)
            {
                bool canUngroup = CardCanvas.SelectedElements.Count == 1 && CardCanvas.SelectedElement is GroupElement;
                BtnUngroup.IsEnabled = canUngroup;
                BtnUngroup.Opacity = canUngroup ? 1.0 : 0.45;
            }
        }

        private void CardCanvas_SelectedElementsChanged(object? sender, IReadOnlyList<CardElement> elems)
        {
            UpdateGroupButtonsState();

            if (elems.Count > 1)
            {
                CardPropertiesPanel.Visibility = Visibility.Collapsed;
                ElementPropertiesPanel.Visibility = Visibility.Collapsed;
                MultiSelectionPanel.Visibility = Visibility.Visible;
                TxtMultiSelectCount.Text = $"{elems.Count} Elements Selected";
            }
            else
            {
                MultiSelectionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CardCanvas_SelectedElementChanged(object? sender, CardElement? elem)
        {
            UpdateGroupButtonsState();

            if (CardCanvas.SelectedElements.Count > 1)
            {
                CardPropertiesPanel.Visibility = Visibility.Collapsed;
                ElementPropertiesPanel.Visibility = Visibility.Collapsed;
                MultiSelectionPanel.Visibility = Visibility.Visible;
                return;
            }

            MultiSelectionPanel.Visibility = Visibility.Collapsed;

            if (elem == null)
            {
                CardPropertiesPanel.Visibility = Visibility.Visible;
                ElementPropertiesPanel.Visibility = Visibility.Collapsed;
                UpdateBackgroundPropertyUi();
                return;
            }

            CardPropertiesPanel.Visibility = Visibility.Collapsed;
            ElementPropertiesPanel.Visibility = Visibility.Visible;

            _isUpdatingUi = true;

            // Geometry
            TxtElemX.Text = elem.X.ToString("0.0");
            TxtElemY.Text = elem.Y.ToString("0.0");
            TxtElemWidth.Text = elem.Width.ToString("0.0");
            TxtElemHeight.Text = elem.Height.ToString("0.0");
            TxtElemZIndex.Text = elem.ZIndex.ToString();

            if (elem is TextElement textElem)
            {
                TxtElementTypeHeader.Text = "Text Element";
                TextPropertiesGroup.Visibility = Visibility.Visible;
                ImagePropertiesGroup.Visibility = Visibility.Collapsed;
                ShapePropertiesGroup.Visibility = Visibility.Collapsed;
                QrPropertiesGroup.Visibility = Visibility.Collapsed;
                GroupPropertiesGroup.Visibility = Visibility.Collapsed;

                TxtElementContent.Text = textElem.Text;
                TxtFontSize.Text = textElem.FontSize.ToString("0.0");
                SliderFontSize.Value = textElem.FontSize;
                TxtTextColorHex.Text = textElem.TextColor;

                // Select font family in combo if present
                foreach (ComboBoxItem item in CmbFontFamily.Items)
                {
                    if (item.Content?.ToString() == textElem.FontFamily)
                    {
                        CmbFontFamily.SelectedItem = item;
                        break;
                    }
                }
            }
            else if (elem is ImageElement imgElem)
            {
                TxtElementTypeHeader.Text = "Image / Logo Element";
                TextPropertiesGroup.Visibility = Visibility.Collapsed;
                ImagePropertiesGroup.Visibility = Visibility.Visible;
                ShapePropertiesGroup.Visibility = Visibility.Collapsed;
                QrPropertiesGroup.Visibility = Visibility.Collapsed;
                GroupPropertiesGroup.Visibility = Visibility.Collapsed;

                var bmp = CardRenderer.GetBitmapFromBase64OrPath(imgElem.ImageBase64, imgElem.ImagePath);
                ImgPreviewThumbnail.Source = bmp;
                SliderImageOpacity.Value = imgElem.Opacity;
                SliderRotationAngle.Value = imgElem.Rotation;
                TxtRotationAngle.Text = imgElem.Rotation.ToString("0");
                CmbImageStretch.SelectedIndex = imgElem.Stretch switch
                {
                    Stretch.Uniform => 0,
                    Stretch.UniformToFill => 1,
                    Stretch.Fill => 2,
                    _ => 0
                };
            }
            else if (elem is ShapeElement shapeElem)
            {
                TxtElementTypeHeader.Text = $"Shape ({shapeElem.ShapeKind})";
                TextPropertiesGroup.Visibility = Visibility.Collapsed;
                ImagePropertiesGroup.Visibility = Visibility.Collapsed;
                ShapePropertiesGroup.Visibility = Visibility.Visible;
                QrPropertiesGroup.Visibility = Visibility.Collapsed;
                GroupPropertiesGroup.Visibility = Visibility.Collapsed;

                TxtShapeFillHex.Text = shapeElem.FillColor;
                TxtShapeStrokeHex.Text = shapeElem.StrokeColor;
                SliderShapeThickness.Value = shapeElem.StrokeThickness;
                SliderShapeCornerRadius.Value = shapeElem.CornerRadius;
            }
            else if (elem is QrCodeElement qrElem)
            {
                TxtElementTypeHeader.Text = "QR Code";
                TextPropertiesGroup.Visibility = Visibility.Collapsed;
                ImagePropertiesGroup.Visibility = Visibility.Collapsed;
                ShapePropertiesGroup.Visibility = Visibility.Collapsed;
                QrPropertiesGroup.Visibility = Visibility.Visible;
                GroupPropertiesGroup.Visibility = Visibility.Collapsed;

                TxtQrContent.Text = qrElem.QrContent;
                TxtQrDarkHex.Text = qrElem.DarkColor;
                TxtQrLightHex.Text = qrElem.LightColor;
            }
            else if (elem is GroupElement groupElem)
            {
                TxtElementTypeHeader.Text = $"Group ({groupElem.Children.Count} items)";
                TextPropertiesGroup.Visibility = Visibility.Collapsed;
                ImagePropertiesGroup.Visibility = Visibility.Collapsed;
                ShapePropertiesGroup.Visibility = Visibility.Collapsed;
                QrPropertiesGroup.Visibility = Visibility.Collapsed;
                GroupPropertiesGroup.Visibility = Visibility.Visible;
                TxtGroupInfo.Text = $"Grouped Elements ({groupElem.Children.Count} items)";
            }

            _isUpdatingUi = false;
        }

        private void CardCanvas_ElementTransforming(object? sender, EventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                _isUpdatingUi = true;
                TxtElemX.Text = CardCanvas.SelectedElement.X.ToString("0.0");
                TxtElemY.Text = CardCanvas.SelectedElement.Y.ToString("0.0");
                TxtElemWidth.Text = CardCanvas.SelectedElement.Width.ToString("0.0");
                TxtElemHeight.Text = CardCanvas.SelectedElement.Height.ToString("0.0");
                _isUpdatingUi = false;
            }
        }

        private void CardCanvas_CanvasModified(object? sender, EventArgs e)
        {
            CardCanvas_ElementTransforming(sender, e);
            RecordUndoState();
            ScheduleAutoSave();
        }

        // Text Element Controls
        private void TxtElementContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.Text = TxtElementContent.Text;
                CardCanvas.RefreshCanvas();
                ScheduleAutoSave();
            }
        }

        private void CmbFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is TextElement te && CmbFontFamily.SelectedItem is ComboBoxItem item)
            {
                te.FontFamily = item.Content?.ToString() ?? "Segoe UI";
                CardCanvas.RefreshCanvas();
            }
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.FontSize = Math.Round(SliderFontSize.Value, 1);
                TxtFontSize.Text = te.FontSize.ToString("0.0");
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtFontSize_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtFontSize.Text, out double s) && CardCanvas.SelectedElement is TextElement te)
            {
                te.FontSize = Math.Clamp(s, 5, 80);
                SliderFontSize.Value = te.FontSize;
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtFontSize_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtFontSize_LostFocus(sender, e);
        }

        private void BtnBold_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.IsBold = !te.IsBold;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnItalic_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.IsItalic = !te.IsItalic;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnUnderline_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.IsUnderline = !te.IsUnderline;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.Alignment = TextAlignment.Left;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignCenter_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.Alignment = TextAlignment.Center;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignRight_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.Alignment = TextAlignment.Right;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnSetTextColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex && CardCanvas.SelectedElement is TextElement te)
            {
                te.TextColor = hex;
                TxtTextColorHex.Text = hex;
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtTextColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyCustomTextColor();
        }

        private void TxtTextColorHex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyCustomTextColor();
        }

        private void ApplyCustomTextColor()
        {
            string hex = TxtTextColorHex.Text.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (CardCanvas.SelectedElement is TextElement te)
            {
                te.TextColor = hex;
                CardCanvas.RefreshCanvas();
            }
        }

        // Image Element Controls
        private void BtnReplaceImage_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is ImageElement imgElem)
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Replace Image",
                    Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
                };

                if (dialog.ShowDialog() == true)
                {
                    imgElem.ImagePath = dialog.FileName;
                    imgElem.ImageBase64 = TemplateService.ImageFileToBase64(dialog.FileName);
                    var bmp = CardRenderer.GetBitmapFromBase64OrPath(imgElem.ImageBase64, imgElem.ImagePath);
                    ImgPreviewThumbnail.Source = bmp;
                    CardCanvas.RefreshCanvas();
                }
            }
        }

        private void SliderImageOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is ImageElement imgElem)
            {
                imgElem.Opacity = SliderImageOpacity.Value;
                CardCanvas.RefreshCanvas();
            }
        }

        private void CmbImageStretch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is ImageElement imgElem)
            {
                imgElem.Stretch = CmbImageStretch.SelectedIndex switch
                {
                    0 => Stretch.Uniform,
                    1 => Stretch.UniformToFill,
                    2 => Stretch.Fill,
                    _ => Stretch.Uniform
                };
                CardCanvas.RefreshCanvas();
            }
        }

        private void UpdateRotationInputs()
        {
            if (CardCanvas.SelectedElement != null)
            {
                _isUpdatingUi = true;
                SliderRotationAngle.Value = CardCanvas.SelectedElement.Rotation;
                TxtRotationAngle.Text = CardCanvas.SelectedElement.Rotation.ToString("0");
                _isUpdatingUi = false;
            }
        }

        private void BtnRotateCCW_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                RecordUndoState();
                CardCanvas.SelectedElement.Rotation = (CardCanvas.SelectedElement.Rotation - 90 + 360) % 360;
                UpdateRotationInputs();
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnRotateCW_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                RecordUndoState();
                CardCanvas.SelectedElement.Rotation = (CardCanvas.SelectedElement.Rotation + 90) % 360;
                UpdateRotationInputs();
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnRotate180_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                RecordUndoState();
                CardCanvas.SelectedElement.Rotation = (CardCanvas.SelectedElement.Rotation + 180) % 360;
                UpdateRotationInputs();
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnRotateReset_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                RecordUndoState();
                CardCanvas.SelectedElement.Rotation = 0;
                CardCanvas.SelectedElement.FlipHorizontal = false;
                CardCanvas.SelectedElement.FlipVertical = false;
                UpdateRotationInputs();
                CardCanvas.RefreshCanvas();
            }
        }

        private void SliderRotationAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.Rotation = Math.Round(SliderRotationAngle.Value);
                TxtRotationAngle.Text = CardCanvas.SelectedElement.Rotation.ToString("0");
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtRotationAngle_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtRotationAngle.Text, out double a) && CardCanvas.SelectedElement != null)
            {
                RecordUndoState();
                CardCanvas.SelectedElement.Rotation = a;
                SliderRotationAngle.Value = CardCanvas.SelectedElement.Rotation;
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtRotationAngle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtRotationAngle_LostFocus(sender, e);
        }

        private void BtnFlipHorizontal_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                RecordUndoState();
                CardCanvas.SelectedElement.FlipHorizontal = !CardCanvas.SelectedElement.FlipHorizontal;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnFlipVertical_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                RecordUndoState();
                CardCanvas.SelectedElement.FlipVertical = !CardCanvas.SelectedElement.FlipVertical;
                CardCanvas.RefreshCanvas();
            }
        }

        // Shape Controls
        private void TxtShapeFillHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is ShapeElement se)
            {
                string hex = TxtShapeFillHex.Text.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                se.FillColor = hex;
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtShapeFillHex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtShapeFillHex_LostFocus(sender, e);
        }

        private void TxtShapeStrokeHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is ShapeElement se)
            {
                string hex = TxtShapeStrokeHex.Text.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                se.StrokeColor = hex;
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtShapeStrokeHex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtShapeStrokeHex_LostFocus(sender, e);
        }

        private void SliderShapeThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is ShapeElement se)
            {
                se.StrokeThickness = Math.Round(SliderShapeThickness.Value, 1);
                CardCanvas.RefreshCanvas();
            }
        }

        private void SliderShapeCornerRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is ShapeElement se)
            {
                se.CornerRadius = Math.Round(SliderShapeCornerRadius.Value, 1);
                CardCanvas.RefreshCanvas();
            }
        }

        // QR Code Controls
        private void TxtQrContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CardCanvas.SelectedElement is QrCodeElement qr)
            {
                qr.QrContent = TxtQrContent.Text;
                CardCanvas.RefreshCanvas();
                ScheduleAutoSave();
            }
        }

        private void BtnQrPresetUrl_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is not QrCodeElement qr) return;
            RecordUndoState();
            qr.QrContent = "https://www.vcsi.biz";
            _isUpdatingUi = true;
            TxtQrContent.Text = qr.QrContent;
            _isUpdatingUi = false;
            CardCanvas.RefreshCanvas();
            ScheduleAutoSave();
        }

        private void BtnQrPresetVCard_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement is not QrCodeElement qr) return;
            RecordUndoState();

            // Auto-extract text lines from Front side to prefill a vCard
            var frontTexts = _template.Front.Elements.OfType<TextElement>().OrderBy(t => t.Y).ToList();
            string fullName = frontTexts.FirstOrDefault(t => t.IsBold && t.FontSize >= 10)?.Text.Trim() ?? "Your Name";
            string title = frontTexts.FirstOrDefault(t => !string.Equals(t.Text.Trim(), fullName, StringComparison.OrdinalIgnoreCase) && t.FontSize >= 7 && t.X < 160)?.Text.Trim() ?? "";

            string allText = string.Join("\n", frontTexts.Select(t => t.Text));
            string email = System.Text.RegularExpressions.Regex.Match(allText, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}").Value;
            string phone = System.Text.RegularExpressions.Regex.Match(allText, @"\+?\d[\d\s\-]{7,}\d").Value.Trim();
            string url = System.Text.RegularExpressions.Regex.Match(allText, @"(https?://|www\.)[^\s]+").Value;
            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var vcard = $"BEGIN:VCARD\nVERSION:3.0\nFN:{fullName}\n";
            if (!string.IsNullOrEmpty(title)) vcard += $"TITLE:{title}\n";
            if (!string.IsNullOrEmpty(phone)) vcard += $"TEL;TYPE=CELL:{phone}\n";
            if (!string.IsNullOrEmpty(email)) vcard += $"EMAIL:{email}\n";
            if (!string.IsNullOrEmpty(url)) vcard += $"URL:{url}\n";
            vcard += "END:VCARD";

            qr.QrContent = vcard;
            _isUpdatingUi = true;
            TxtQrContent.Text = vcard;
            _isUpdatingUi = false;
            CardCanvas.RefreshCanvas();
            ScheduleAutoSave();
        }

        private void BtnSetQrDarkColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex && CardCanvas.SelectedElement is QrCodeElement qr)
            {
                RecordUndoState();
                qr.DarkColor = hex;
                _isUpdatingUi = true;
                TxtQrDarkHex.Text = hex;
                _isUpdatingUi = false;
                CardCanvas.RefreshCanvas();
                ScheduleAutoSave();
            }
        }

        private void TxtQrDarkHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || CardCanvas.SelectedElement is not QrCodeElement qr) return;
            string hex = TxtQrDarkHex.Text.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (hex.Length != 7 && hex.Length != 9 && hex.Length != 4) return;
            try
            {
                ColorConverter.ConvertFromString(hex);
                qr.DarkColor = hex;
                CardCanvas.RefreshCanvas();
                ScheduleAutoSave();
            }
            catch { }
        }

        private void BtnSetQrLightColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex && CardCanvas.SelectedElement is QrCodeElement qr)
            {
                RecordUndoState();
                qr.LightColor = hex;
                _isUpdatingUi = true;
                TxtQrLightHex.Text = hex;
                _isUpdatingUi = false;
                CardCanvas.RefreshCanvas();
                ScheduleAutoSave();
            }
        }

        private void TxtQrLightHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || CardCanvas.SelectedElement is not QrCodeElement qr) return;
            string hex = TxtQrLightHex.Text.Trim();
            if (string.Equals(hex, "Transparent", StringComparison.OrdinalIgnoreCase))
            {
                qr.LightColor = "Transparent";
                CardCanvas.RefreshCanvas();
                ScheduleAutoSave();
                return;
            }
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (hex.Length != 7 && hex.Length != 9 && hex.Length != 4) return;
            try
            {
                ColorConverter.ConvertFromString(hex);
                qr.LightColor = hex;
                CardCanvas.RefreshCanvas();
                ScheduleAutoSave();
            }
            catch { }
        }

        // Geometry Controls
        private void TxtElemX_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtElemX.Text, out double val) && CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.X = val;
                CardCanvas.RefreshCanvas();
            }
        }
        private void TxtElemX_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) TxtElemX_LostFocus(sender, e); }

        private void TxtElemY_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtElemY.Text, out double val) && CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.Y = val;
                CardCanvas.RefreshCanvas();
            }
        }
        private void TxtElemY_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) TxtElemY_LostFocus(sender, e); }

        private void TxtElemWidth_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtElemWidth.Text, out double val) && CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.Width = Math.Max(5, val);
                CardCanvas.RefreshCanvas();
            }
        }
        private void TxtElemWidth_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) TxtElemWidth_LostFocus(sender, e); }

        private void TxtElemHeight_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtElemHeight.Text, out double val) && CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.Height = Math.Max(5, val);
                CardCanvas.RefreshCanvas();
            }
        }
        private void TxtElemHeight_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) TxtElemHeight_LostFocus(sender, e); }

        // Alignment & Ordering Controls
        private void BtnAlignCardLeft_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.X = 12; // 0.125 in safety margin
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignCardCenterH_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.X = Math.Round((_template.WidthWpf - CardCanvas.SelectedElement.Width) / 2.0, 1);
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignCardRight_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.X = Math.Round(_template.WidthWpf - CardCanvas.SelectedElement.Width - 12, 1);
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignCardTop_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.Y = 12;
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignCardCenterV_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.Y = Math.Round((_template.HeightWpf - CardCanvas.SelectedElement.Height) / 2.0, 1);
                CardCanvas.RefreshCanvas();
            }
        }

        private void BtnAlignCardBottom_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElement != null)
            {
                CardCanvas.SelectedElement.Y = Math.Round(_template.HeightWpf - CardCanvas.SelectedElement.Height - 12, 1);
                CardCanvas.RefreshCanvas();
            }
        }

        private void ReorderSelectedLayer(Func<List<CardElement>, int, int> getNewIndex, string statusLabel)
        {
            var selected = CardCanvas.SelectedElement;
            if (selected == null || CurrentSide.Elements.Count < 2) return;

            RecordUndoState();

            var ordered = CurrentSide.Elements.OrderBy(el => el.ZIndex).ToList();
            int currentIndex = ordered.IndexOf(selected);
            if (currentIndex < 0) return;

            int targetIndex = Math.Clamp(getNewIndex(ordered, currentIndex), 0, ordered.Count - 1);
            if (targetIndex != currentIndex)
            {
                ordered.RemoveAt(currentIndex);
                ordered.Insert(targetIndex, selected);
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].ZIndex = i;
            }

            _isUpdatingUi = true;
            TxtElemZIndex.Text = selected.ZIndex.ToString();
            _isUpdatingUi = false;

            CardCanvas.RefreshCanvas();
            TxtStatusMessage.Text = statusLabel;
        }

        private void TxtElemZIndex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || CardCanvas.SelectedElement == null) return;
            if (int.TryParse(TxtElemZIndex.Text, out int z))
            {
                RecordUndoState();
                CardCanvas.SelectedElement.ZIndex = Math.Max(0, z);
                _isUpdatingUi = true;
                TxtElemZIndex.Text = CardCanvas.SelectedElement.ZIndex.ToString();
                _isUpdatingUi = false;
                CardCanvas.RefreshCanvas();
            }
        }

        private void TxtElemZIndex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtElemZIndex_LostFocus(sender, e);
        }

        private void BtnBringToFront_Click(object sender, RoutedEventArgs e)
            => ReorderSelectedLayer((list, _) => list.Count - 1, "Moved element to front.");

        private void BtnSendToBack_Click(object sender, RoutedEventArgs e)
            => ReorderSelectedLayer((_, _) => 0, "Sent element to back.");

        private void BtnBringForward_Click(object sender, RoutedEventArgs e)
            => ReorderSelectedLayer((_, idx) => idx + 1, "Brought element one layer forward.");

        private void BtnSendBackward_Click(object sender, RoutedEventArgs e)
            => ReorderSelectedLayer((_, idx) => idx - 1, "Sent element one layer backward.");

        private void BtnGroup_Click(object sender, RoutedEventArgs e) => GroupSelectedElements();
        private void BtnUngroup_Click(object sender, RoutedEventArgs e) => UngroupSelectedElement();

        private void GroupSelectedElements()
        {
            var selected = CardCanvas.SelectedElements.ToList();
            if (selected.Count < 2)
            {
                TxtStatusMessage.Text = "Select 2 or more elements first (drag a box or Ctrl+Click).";
                return;
            }

            RecordUndoState();

            double minX = selected.Min(el => el.X);
            double minY = selected.Min(el => el.Y);
            double maxRight = selected.Max(el => el.X + el.Width);
            double maxBottom = selected.Max(el => el.Y + el.Height);
            int maxZ = selected.Max(el => el.ZIndex);

            var group = new GroupElement
            {
                X = Math.Round(minX, 1),
                Y = Math.Round(minY, 1),
                Width = Math.Max(10, Math.Round(maxRight - minX, 1)),
                Height = Math.Max(10, Math.Round(maxBottom - minY, 1)),
                ZIndex = maxZ
            };

            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                CurrentSide.Elements.Remove(el);
                el.X = Math.Round(el.X - minX, 1);
                el.Y = Math.Round(el.Y - minY, 1);
                group.Children.Add(el);
            }

            CurrentSide.Elements.Add(group);
            CardCanvas.SelectedElement = group;
            TxtStatusMessage.Text = $"Grouped {group.Children.Count} elements.";
        }

        private void UngroupSelectedElement()
        {
            if (CardCanvas.SelectedElement is not GroupElement group) return;

            RecordUndoState();

            var restored = new List<CardElement>();
            CurrentSide.Elements.Remove(group);

            int baseZ = group.ZIndex;
            foreach (var child in group.Children.OrderBy(c => c.ZIndex))
            {
                child.X = Math.Round(child.X + group.X, 1);
                child.Y = Math.Round(child.Y + group.Y, 1);
                child.ZIndex = baseZ++;
                CurrentSide.Elements.Add(child);
                restored.Add(child);
            }

            CardCanvas.SetSelection(restored);
            TxtStatusMessage.Text = $"Ungrouped {restored.Count} elements.";
        }

        private void BtnDuplicateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElements.Count > 1)
            {
                RecordUndoState();
                var clones = new List<CardElement>();
                foreach (var el in CardCanvas.SelectedElements.ToList())
                {
                    var cloned = el.Clone();
                    CurrentSide.Elements.Add(cloned);
                    clones.Add(cloned);
                }
                CardCanvas.SetSelection(clones);
            }
            else if (CardCanvas.SelectedElement != null)
            {
                DuplicateElement(CardCanvas.SelectedElement);
            }
        }

        private void DuplicateElement(CardElement el)
        {
            RecordUndoState();
            var cloned = el.Clone();
            CurrentSide.Elements.Add(cloned);
            CardCanvas.SelectedElement = cloned;
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (CardCanvas.SelectedElements.Count > 1)
            {
                RecordUndoState();
                foreach (var el in CardCanvas.SelectedElements.ToList())
                {
                    CurrentSide.Elements.Remove(el);
                }
                CardCanvas.SelectedElement = null;
            }
            else if (CardCanvas.SelectedElement != null)
            {
                DeleteElement(CardCanvas.SelectedElement);
            }
        }

        private void DeleteElement(CardElement el)
        {
            RecordUndoState();
            CurrentSide.Elements.Remove(el);
            CardCanvas.SelectedElement = null;
        }
        #endregion

        #region Print Mode & Queue
        private void BtnAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            _printQueue.Add(new PrintQueueItem(_template.Clone()) { Quantity = 10 });
            TxtStatusMessage.Text = $"Added '{_template.Name}' to print queue.";
            RefreshSheetPreview();

            if (PrintQueueContainer.Visibility != Visibility.Visible)
            {
                TabPrintQueueBtn.IsChecked = true;
                TabPrintQueue_Click(sender, e);
            }
        }

        private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear the entire print queue?", "Clear Queue", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _printQueue.Clear();
                RefreshSheetPreview();
            }
        }

        private void BtnRemoveQueueItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PrintQueueItem item)
            {
                _printQueue.Remove(item);
                RefreshSheetPreview();
            }
        }

        private void DgPrintQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            RefreshSheetPreview();
        }

        private void QueueSideChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            RefreshSheetPreview();
        }

        private void CmbSheetSetting_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _sheetLayout == null || _template == null || CmbPaperSize == null) return;
            _sheetLayout.PaperType = CmbPaperSize.SelectedIndex == 0 ? PaperType.Letter : PaperType.A4;
            if (CmbPaperOrientation != null)
            {
                _sheetLayout.PaperOrientation = CmbPaperOrientation.SelectedIndex == 1
                    ? CardOrientation.Landscape
                    : CardOrientation.Portrait;
            }
            _sheetLayout.AutoCenter(_template.WidthInches, _template.HeightInches);
            TxtMarginLeft.Text = _sheetLayout.MarginLeftInches.ToString("0.00");
            TxtMarginTop.Text = _sheetLayout.MarginTopInches.ToString("0.00");
            RefreshSheetPreview();
        }

        private void CmbGridLayout_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _sheetLayout == null || _template == null || CmbGridLayout == null) return;

            switch (CmbGridLayout.SelectedIndex)
            {
                case 0: // 10-Up (2x5)
                    _sheetLayout.Columns = 2;
                    _sheetLayout.Rows = 5;
                    break;
                case 1: // 8-Up (2x4)
                    _sheetLayout.Columns = 2;
                    _sheetLayout.Rows = 4;
                    break;
                case 2: // 9-Up (3x3)
                    _sheetLayout.Columns = 3;
                    _sheetLayout.Rows = 3;
                    break;
                case 3: // 12-Up (4x3)
                    _sheetLayout.Columns = 4;
                    _sheetLayout.Rows = 3;
                    break;
            }

            _sheetLayout.AutoCenter(_template.WidthInches, _template.HeightInches);
            TxtMarginLeft.Text = _sheetLayout.MarginLeftInches.ToString("0.00");
            TxtMarginTop.Text = _sheetLayout.MarginTopInches.ToString("0.00");
            RefreshSheetPreview();
        }

        private void BtnAutoCenterGrid_Click(object sender, RoutedEventArgs e)
        {
            _sheetLayout.AutoCenter(_template.WidthInches, _template.HeightInches);
            TxtMarginLeft.Text = _sheetLayout.MarginLeftInches.ToString("0.00");
            TxtMarginTop.Text = _sheetLayout.MarginTopInches.ToString("0.00");
            RefreshSheetPreview();
        }

        private void TxtMargin_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtMarginLeft.Text, out double ml)) _sheetLayout.MarginLeftInches = ml;
            if (double.TryParse(TxtMarginTop.Text, out double mt)) _sheetLayout.MarginTopInches = mt;
            RefreshSheetPreview();
        }

        private void RbPrintMethod_Click(object sender, RoutedEventArgs e)
        {
            if (RbManualTwoPass.IsChecked == true) _sheetLayout.PrintMethod = PrintMethod.ManualTwoPass;
            else if (RbAutoDuplex.IsChecked == true) _sheetLayout.PrintMethod = PrintMethod.AutoDuplex;
            else if (RbFrontsOnly.IsChecked == true) _sheetLayout.PrintMethod = PrintMethod.SingleSided;
            else if (RbBacksOnly.IsChecked == true) _sheetLayout.PrintMethod = PrintMethod.SingleSided;

            RefreshSheetPreview();
        }

        private void ChkPrintGuide_Click(object sender, RoutedEventArgs e)
        {
            _sheetLayout.DrawCropMarks = ChkPrintCropMarks.IsChecked == true;
            _sheetLayout.DrawCutLines = ChkPrintCutLines.IsChecked == true;
            RefreshSheetPreview();
        }

        public void RefreshSheetPreview()
        {
            bool interleave = _sheetLayout.PrintMethod == PrintMethod.AutoDuplex;
            _currentPreviewSheets = PrintImpositionEngine.GenerateSheets(_printQueue, _sheetLayout, interleave);

            // Filter if SingleSided
            if (RbFrontsOnly.IsChecked == true)
            {
                _currentPreviewSheets = _currentPreviewSheets.Where(s => !s.IsBackSide).ToList();
            }
            else if (RbBacksOnly.IsChecked == true)
            {
                _currentPreviewSheets = _currentPreviewSheets.Where(s => s.IsBackSide).ToList();
            }

            int totalCards = _printQueue.Sum(q => q.Quantity);
            int totalSheets = _currentPreviewSheets.Count;

            TxtQueueSummary.Text = $"Queue: {totalCards} Cards | {totalSheets} Print Sheets Required";

            int frontSheets = _currentPreviewSheets.Count(s => !s.IsBackSide);
            int backSheets = _currentPreviewSheets.Count(s => s.IsBackSide);

            if (_sheetLayout.PrintMethod == PrintMethod.ManualTwoPass)
            {
                TxtQueueDetails.Text = $"Manual 2-Pass: Pass 1 has {frontSheets} Front sheet(s) • Pass 2 has {backSheets} Back sheet(s) with Mirrored columns";
            }
            else if (_sheetLayout.PrintMethod == PrintMethod.AutoDuplex)
            {
                TxtQueueDetails.Text = $"Automatic Duplex: {totalSheets} total interleaved pages ({frontSheets} Fronts, {backSheets} Backs)";
            }
            else
            {
                TxtQueueDetails.Text = $"Single-Sided: {totalSheets} sheet(s)";
            }

            if (_currentSheetIndex >= _currentPreviewSheets.Count)
            {
                _currentSheetIndex = Math.Max(0, _currentPreviewSheets.Count - 1);
            }

            RenderCurrentPreviewSheet();
        }

        private void RenderCurrentPreviewSheet()
        {
            if (!_currentPreviewSheets.Any())
            {
                ImgSheetPreview.Source = null;
                TxtSheetPageNumber.Text = "No Sheets (Queue Empty)";
                return;
            }

            var sheet = _currentPreviewSheets[_currentSheetIndex];
            TxtSheetPageNumber.Text = $"Page {_currentSheetIndex + 1} of {_currentPreviewSheets.Count} ({sheet.Title})";

            // Render at 120 DPI for crisp screen preview
            var bmp = PrintImpositionEngine.RenderSheetToBitmap(sheet, _sheetLayout, 120.0);
            ImgSheetPreview.Source = bmp;
            ApplySheetZoom();
        }

        private double _sheetZoomFactor = 0.75;
        private bool _sheetFitToPage = true;

        private void ApplySheetZoom()
        {
            if (SheetPreviewBorder == null || ImgSheetPreview?.Source is not BitmapSource bmp) return;

            double scale = _sheetZoomFactor;
            if (_sheetFitToPage && SheetScrollViewer != null && SheetScrollViewer.ViewportWidth > 0 && SheetScrollViewer.ViewportHeight > 0)
            {
                double availW = Math.Max(120, SheetScrollViewer.ViewportWidth - 36);
                double availH = Math.Max(120, SheetScrollViewer.ViewportHeight - 36);
                double fitW = availW / Math.Max(1, bmp.Width);
                double fitH = availH / Math.Max(1, bmp.Height);
                scale = Math.Clamp(Math.Min(fitW, fitH), 0.15, 3.0);
                _sheetZoomFactor = scale;
            }

            SheetPreviewBorder.LayoutTransform = new ScaleTransform(scale, scale);
        }

        private void CmbSheetZoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || CmbSheetZoom == null || CmbSheetZoom.SelectedIndex < 0) return;

            if (CmbSheetZoom.SelectedIndex == 0)
            {
                _sheetFitToPage = true;
            }
            else
            {
                _sheetFitToPage = false;
                _sheetZoomFactor = CmbSheetZoom.SelectedIndex switch
                {
                    1 => 0.5,
                    2 => 0.75,
                    3 => 1.0,
                    4 => 1.25,
                    5 => 1.5,
                    6 => 2.0,
                    _ => 1.0
                };
            }

            ApplySheetZoom();
        }

        private void BtnSheetZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _sheetFitToPage = false;
            _sheetZoomFactor = Math.Clamp(_sheetZoomFactor + 0.15, 0.25, 3.0);
            ApplySheetZoom();
        }

        private void BtnSheetZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _sheetFitToPage = false;
            _sheetZoomFactor = Math.Clamp(_sheetZoomFactor - 0.15, 0.25, 3.0);
            ApplySheetZoom();
        }

        private void SheetScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                _sheetFitToPage = false;
                _sheetZoomFactor = Math.Clamp(_sheetZoomFactor + (e.Delta > 0 ? 0.1 : -0.1), 0.25, 3.0);
                ApplySheetZoom();
            }
        }

        private void SheetScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_sheetFitToPage)
            {
                ApplySheetZoom();
            }
        }

        private void BtnPrevSheet_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSheetIndex > 0)
            {
                _currentSheetIndex--;
                RenderCurrentPreviewSheet();
            }
        }

        private void BtnNextSheet_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSheetIndex < _currentPreviewSheets.Count - 1)
            {
                _currentSheetIndex++;
                RenderCurrentPreviewSheet();
            }
        }

        private void BtnExportAllSheets_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPreviewSheets.Any())
            {
                MessageBox.Show("Print queue is empty. Please add cards to the queue first.", "Empty Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Select Destination Folder and Base Filename for Export",
                Filter = "PNG Image (*.png)|*.png",
                FileName = "CallingCard_Sheet_01.png"
            };

            if (dialog.ShowDialog() == true)
            {
                string dir = Path.GetDirectoryName(dialog.FileName)!;
                string baseName = Path.GetFileNameWithoutExtension(dialog.FileName);

                for (int i = 0; i < _currentPreviewSheets.Count; i++)
                {
                    var sheet = _currentPreviewSheets[i];
                    string sideName = sheet.IsBackSide ? "Back" : "Front";
                    string filePath = Path.Combine(dir, $"{baseName}_Sheet{i + 1}_{sideName}.png");

                    var rtb = PrintImpositionEngine.RenderSheetToBitmap(sheet, _sheetLayout, 300.0);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));

                    using var fs = File.Create(filePath);
                    encoder.Save(fs);
                }

                MessageBox.Show($"Successfully exported {_currentPreviewSheets.Count} print sheets at 300 DPI to:\n{dir}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnPrintCallingCards_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPreviewSheets.Any())
            {
                MessageBox.Show("Print queue is empty. Please add cards to the queue first.", "Empty Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var printDlg = new PrintDialog();
            try
            {
                printDlg.PrintTicket.PageMediaSize = _sheetLayout.PaperType == PaperType.A4
                    ? new System.Printing.PageMediaSize(System.Printing.PageMediaSizeName.ISOA4)
                    : new System.Printing.PageMediaSize(System.Printing.PageMediaSizeName.NorthAmericaLetter);
                printDlg.PrintTicket.PageOrientation = _sheetLayout.PaperOrientation == CardOrientation.Landscape
                    ? System.Printing.PageOrientation.Landscape
                    : System.Printing.PageOrientation.Portrait;
            }
            catch { }

            if (printDlg.ShowDialog() != true) return;

            if (_sheetLayout.PrintMethod == PrintMethod.ManualTwoPass)
            {
                // STEP 1: Print Front Sheets
                var frontSheets = _currentPreviewSheets.Where(s => !s.IsBackSide).ToList();
                var backSheets = _currentPreviewSheets.Where(s => s.IsBackSide).ToList();

                if (frontSheets.Any())
                {
                    var frontDoc = PrintImpositionEngine.CreateFixedDocument(frontSheets, _sheetLayout);
                    printDlg.PrintDocument(frontDoc.DocumentPaginator, "Calling Cards - Pass 1 (Fronts)");
                }

                if (backSheets.Any())
                {
                    // Prompt user to re-insert paper for Pass 2 (Backs)
                    var result = MessageBox.Show(
                        "PASS 1 (FRONT SIDES) HAS BEEN SENT TO THE PRINTER!\n\n" +
                        "Instructions for Pass 2 (Back Sides):\n" +
                        "1. Wait for Pass 1 sheets to finish printing completely.\n" +
                        "2. Take the printed stack of pages and flip it over according to your printer's tray instructions.\n" +
                        "3. Place the paper stack back into the paper feed tray.\n\n" +
                        "Click 'OK' when ready to print Pass 2 (Back sides with mirrored columns for exact alignment),\n" +
                        "or 'Cancel' to skip printing backs.",
                        "Step 2: Paper Reload for Back Sides",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.OK)
                    {
                        var backDoc = PrintImpositionEngine.CreateFixedDocument(backSheets, _sheetLayout);
                        printDlg.PrintDocument(backDoc.DocumentPaginator, "Calling Cards - Pass 2 (Backs)");
                        MessageBox.Show("Both front and back passes have been sent to the printer!", "Printing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Front pass sent to printer!", "Printing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                // Auto Duplex or Single Sided
                var doc = PrintImpositionEngine.CreateFixedDocument(_currentPreviewSheets, _sheetLayout);
                printDlg.PrintDocument(doc.DocumentPaginator, "Calling Cards Print Job");
                MessageBox.Show("Calling cards sent to printer!", "Printing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion
    }
}
