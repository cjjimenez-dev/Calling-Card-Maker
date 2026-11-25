using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Calling_Card_Generator.Models;
using Calling_Card_Generator.Services;

namespace Calling_Card_Generator.Controls
{
    public partial class CardCanvasControl : UserControl
    {
        private CardTemplate? _template;
        private CardSide? _side;
        private CardElement? _selectedElement;
        private readonly List<CardElement> _selectedElements = new();

        private bool _isDraggingElement = false;
        private Point _dragStartMousePoint;
        private Point _elementStartPos;
        private readonly Dictionary<CardElement, Point> _multiDragStartPositions = new();

        private bool _isMarqueeSelecting = false;
        private Point _marqueeStartPoint;
        private Rectangle? _marqueeRect;

        private bool _isResizingHandle = false;
        private string _activeHandle = string.Empty;
        private Rect _resizeStartBounds;
        private readonly Dictionary<CardElement, Rect> _groupResizeStartChildren = new();

        private Canvas? _activeSelContainer;
        private Rectangle? _activeSelBorder;
        private readonly Dictionary<string, Ellipse> _activeHandles = new();

        private bool _showBleedGuide = true;
        private bool _showSafeGuide = true;
        private double _zoomFactor = 1.5; // Default 150% zoom for comfortable editing

        public event EventHandler<CardElement?>? SelectedElementChanged;
        public event EventHandler<IReadOnlyList<CardElement>>? SelectedElementsChanged;
        public event EventHandler<CardElement>? ElementDeleteRequested;
        public event EventHandler<CardElement>? ElementDuplicateRequested;
        public event EventHandler? CanvasModified;
        public event EventHandler? ElementTransforming;

        public CardCanvasControl()
        {
            InitializeComponent();
            ApplyZoom();

            ElementsCanvas.MouseMove += ElementsCanvas_MouseMove;
            ElementsCanvas.MouseLeftButtonUp += ElementsCanvas_MouseLeftButtonUp;
            ElementsCanvas.LostMouseCapture += (s, e) =>
            {
                if (_isMarqueeSelecting)
                {
                    _isMarqueeSelecting = false;
                    if (_marqueeRect != null) SelectionCanvas.Children.Remove(_marqueeRect);
                    _marqueeRect = null;
                }
            };
        }

        public CardTemplate? CardTemplate => _template;
        public CardSide? CurrentSide => _side;
        public IReadOnlyList<CardElement> SelectedElements => _selectedElements;

        public CardElement? SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (value == null)
                {
                    SetSelection(Array.Empty<CardElement>());
                }
                else
                {
                    SetSelection(new[] { value }, value);
                }
            }
        }

        public void SetSelection(IEnumerable<CardElement> elements, CardElement? primary = null)
        {
            foreach (var old in _selectedElements)
            {
                old.IsSelected = false;
            }
            _selectedElements.Clear();

            foreach (var el in elements)
            {
                if (!_selectedElements.Contains(el))
                {
                    el.IsSelected = true;
                    _selectedElements.Add(el);
                }
            }

            _selectedElement = primary != null && _selectedElements.Contains(primary)
                ? primary
                : (_selectedElements.Count == 1 ? _selectedElements[0] : _selectedElements.LastOrDefault());

            UpdateSelectionOverlay();
            SelectedElementChanged?.Invoke(this, _selectedElement);
            SelectedElementsChanged?.Invoke(this, _selectedElements.AsReadOnly());
        }

        public void ToggleElementSelection(CardElement elem)
        {
            var list = _selectedElements.ToList();
            if (list.Contains(elem))
            {
                list.Remove(elem);
                SetSelection(list);
            }
            else
            {
                list.Add(elem);
                SetSelection(list, elem);
            }
        }

        public bool ShowBleedGuide
        {
            get => _showBleedGuide;
            set
            {
                _showBleedGuide = value;
                UpdateGuides();
            }
        }

        public bool ShowSafeGuide
        {
            get => _showSafeGuide;
            set
            {
                _showSafeGuide = value;
                UpdateGuides();
            }
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                _zoomFactor = Math.Clamp(value, 0.4, 4.0);
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            var scale = new ScaleTransform(_zoomFactor, _zoomFactor);
            RootCanvasGrid.LayoutTransform = scale;
            if (ShadowBorder != null)
            {
                ShadowBorder.LayoutTransform = scale;
            }
        }

        public void SetCardData(CardTemplate template, CardSide side)
        {
            if (_side != null)
            {
                _side.Elements.CollectionChanged -= Elements_CollectionChanged;
                _side.PropertyChanged -= Side_PropertyChanged;
            }

            _template = template;
            _side = side;

            if (_side != null)
            {
                _side.Elements.CollectionChanged += Elements_CollectionChanged;
                _side.PropertyChanged += Side_PropertyChanged;
            }

            SelectedElement = null;
            RefreshCanvas();
        }

        private void Side_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateCanvasBackground();
        }

        private void Elements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshCanvas();
            CanvasModified?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshCanvas()
        {
            if (_template == null || _side == null)
            {
                ElementsCanvas.Children.Clear();
                SelectionCanvas.Children.Clear();
                GuidesCanvas.Children.Clear();
                return;
            }

            double w = _template.WidthWpf;
            double h = _template.HeightWpf;

            ShadowBorder.Width = w;
            ShadowBorder.Height = h;
            RootCanvasGrid.Width = w;
            RootCanvasGrid.Height = h;
            ElementsCanvas.Width = w;
            ElementsCanvas.Height = h;
            GuidesCanvas.Width = w;
            GuidesCanvas.Height = h;
            SelectionCanvas.Width = w;
            SelectionCanvas.Height = h;

            UpdateCanvasBackground();
            RebuildElementVisuals();
            UpdateGuides();
            UpdateSelectionOverlay();
        }

        private void UpdateCanvasBackground()
        {
            if (_side == null) return;

            var bgBrush = CardRenderer.ParseColorBrush(_side.BackgroundColor, Brushes.White);
            ElementsCanvas.Background = bgBrush;
            CardBorder.Background = bgBrush;

            if (!string.IsNullOrEmpty(_side.BackgroundImageBase64))
            {
                var bmp = CardRenderer.GetBitmapFromBase64OrPath(_side.BackgroundImageBase64, string.Empty);
                if (bmp != null)
                {
                    var imgBrush = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                    ElementsCanvas.Background = imgBrush;
                    CardBorder.Background = imgBrush;
                }
            }
        }

        private void RebuildElementVisuals()
        {
            ElementsCanvas.Children.Clear();
            if (_side == null) return;

            var sorted = _side.Elements.OrderBy(e => e.ZIndex).ToList();
            foreach (var elem in sorted)
            {
                var visual = CreateElementVisual(elem);
                ElementsCanvas.Children.Add(visual);
            }
        }

        private FrameworkElement BuildContentVisual(CardElement elem)
        {
            if (elem is TextElement textElem)
            {
                var tb = new TextBlock
                {
                    Text = textElem.Text,
                    FontFamily = new FontFamily(string.IsNullOrWhiteSpace(textElem.FontFamily) ? "Segoe UI" : textElem.FontFamily),
                    FontSize = textElem.FontSize,
                    FontWeight = textElem.IsBold ? FontWeights.Bold : FontWeights.Normal,
                    FontStyle = textElem.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                    Foreground = CardRenderer.ParseColorBrush(textElem.TextColor, Brushes.Black),
                    TextAlignment = textElem.Alignment,
                    TextWrapping = TextWrapping.Wrap,
                    Width = elem.Width,
                    Height = elem.Height,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true
                };

                TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
                TextOptions.SetTextHintingMode(tb, TextHintingMode.Auto);
                RenderOptions.SetClearTypeHint(tb, ClearTypeHint.Enabled);

                if (textElem.IsUnderline)
                {
                    tb.TextDecorations = TextDecorations.Underline;
                }

                return tb;
            }
            else if (elem is ImageElement imgElem)
            {
                var img = new Image
                {
                    Stretch = imgElem.Stretch,
                    Opacity = imgElem.Opacity,
                    Width = elem.Width,
                    Height = elem.Height,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true
                };

                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                var bmp = CardRenderer.GetBitmapFromBase64OrPath(imgElem.ImageBase64, imgElem.ImagePath);
                if (bmp != null) img.Source = bmp;

                return img;
            }
            else if (elem is ShapeElement shapeElem)
            {
                Brush fill = CardRenderer.ParseColorBrush(shapeElem.FillColor, Brushes.Transparent);
                Brush stroke = CardRenderer.ParseColorBrush(shapeElem.StrokeColor, Brushes.Transparent);

                Shape shape;
                if (shapeElem.ShapeKind == ShapeType.Ellipse)
                {
                    shape = new Ellipse();
                }
                else if (shapeElem.ShapeKind == ShapeType.Line)
                {
                    shape = new Rectangle { Height = shapeElem.StrokeThickness, Fill = stroke };
                }
                else
                {
                    shape = new Rectangle { RadiusX = shapeElem.CornerRadius, RadiusY = shapeElem.CornerRadius };
                }

                shape.Fill = fill;
                shape.Stroke = stroke;
                shape.StrokeThickness = shapeElem.StrokeThickness;
                shape.Width = elem.Width;
                shape.Height = elem.Height;
                shape.UseLayoutRounding = true;
                shape.SnapsToDevicePixels = true;

                return shape;
            }
            else if (elem is QrCodeElement qrElem)
            {
                var qrImg = new Image
                {
                    Source = QrCodeHelper.CreateQrDrawingImage(qrElem),
                    Stretch = Stretch.Uniform,
                    Width = elem.Width,
                    Height = elem.Height,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(qrImg, BitmapScalingMode.NearestNeighbor);
                return qrImg;
            }
            else if (elem is GroupElement groupElem)
            {
                var groupCanvas = new Canvas
                {
                    Width = elem.Width,
                    Height = elem.Height,
                    Background = Brushes.Transparent,
                    ClipToBounds = false,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true
                };

                foreach (var child in groupElem.Children.OrderBy(c => c.ZIndex))
                {
                    var childWrap = new Border
                    {
                        Width = child.Width,
                        Height = child.Height,
                        Background = Brushes.Transparent,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        UseLayoutRounding = true,
                        SnapsToDevicePixels = true,
                        Child = BuildContentVisual(child)
                    };

                    var ctg = new TransformGroup();
                    if (child.FlipHorizontal || child.FlipVertical)
                    {
                        ctg.Children.Add(new ScaleTransform(child.FlipHorizontal ? -1 : 1, child.FlipVertical ? -1 : 1));
                    }
                    if (child.Rotation != 0)
                    {
                        ctg.Children.Add(new RotateTransform(child.Rotation));
                    }
                    childWrap.RenderTransform = ctg;

                    Canvas.SetLeft(childWrap, child.X);
                    Canvas.SetTop(childWrap, child.Y);
                    Panel.SetZIndex(childWrap, child.ZIndex);
                    groupCanvas.Children.Add(childWrap);
                }

                return groupCanvas;
            }

            return new Rectangle { Fill = Brushes.LightGray };
        }

        private FrameworkElement CreateElementVisual(CardElement elem)
        {
            var container = new Border
            {
                Width = elem.Width,
                Height = elem.Height,
                Tag = elem,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                RenderTransformOrigin = new Point(0.5, 0.5),
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            var tg = new TransformGroup();
            if (elem.FlipHorizontal || elem.FlipVertical)
            {
                tg.Children.Add(new ScaleTransform(elem.FlipHorizontal ? -1 : 1, elem.FlipVertical ? -1 : 1));
            }
            if (elem.Rotation != 0)
            {
                tg.Children.Add(new RotateTransform(elem.Rotation));
            }
            container.RenderTransform = tg;

            Canvas.SetLeft(container, elem.X);
            Canvas.SetTop(container, elem.Y);
            Panel.SetZIndex(container, elem.ZIndex);

            container.Child = BuildContentVisual(elem);

            // Hook mouse events for element selection & dragging (supports Ctrl/Shift multi-select)
            container.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                Focus();

                bool isMultiModifier = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
                                       (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (isMultiModifier)
                {
                    ToggleElementSelection(elem);
                    if (!_selectedElements.Contains(elem))
                    {
                        return;
                    }
                }
                else if (!_selectedElements.Contains(elem))
                {
                    SelectedElement = elem;
                }

                _isDraggingElement = true;
                _dragStartMousePoint = e.GetPosition(ElementsCanvas);
                _elementStartPos = new Point(elem.X, elem.Y);
                _multiDragStartPositions.Clear();
                foreach (var sel in _selectedElements)
                {
                    _multiDragStartPositions[sel] = new Point(sel.X, sel.Y);
                }
                container.CaptureMouse();
            };

            container.MouseMove += (s, e) =>
            {
                if (_isDraggingElement)
                {
                    if (e.LeftButton != MouseButtonState.Pressed || !_selectedElements.Contains(elem))
                    {
                        _isDraggingElement = false;
                        container.ReleaseMouseCapture();
                        return;
                    }

                    Point cur = e.GetPosition(ElementsCanvas);
                    double dx = cur.X - _dragStartMousePoint.X;
                    double dy = cur.Y - _dragStartMousePoint.Y;

                    foreach (var kvp in _multiDragStartPositions)
                    {
                        kvp.Key.X = Math.Round(kvp.Value.X + dx, 1);
                        kvp.Key.Y = Math.Round(kvp.Value.Y + dy, 1);
                    }

                    foreach (UIElement child in ElementsCanvas.Children)
                    {
                        if (child is Border b && b.Tag is CardElement ce && _multiDragStartPositions.ContainsKey(ce))
                        {
                            Canvas.SetLeft(b, ce.X);
                            Canvas.SetTop(b, ce.Y);
                        }
                    }

                    UpdateSelectionOverlayGeometry();
                    ElementTransforming?.Invoke(this, EventArgs.Empty);
                }
            };

            container.MouseLeftButtonUp += (s, e) =>
            {
                if (_isDraggingElement)
                {
                    _isDraggingElement = false;
                    container.ReleaseMouseCapture();
                    CanvasModified?.Invoke(this, EventArgs.Empty);
                }
            };

            container.LostMouseCapture += (s, e) =>
            {
                _isDraggingElement = false;
            };

            return container;
        }

        private void ElementsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();

            bool isMultiModifier = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
                                   (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (!isMultiModifier)
            {
                SelectedElement = null;
            }

            _isMarqueeSelecting = true;
            _marqueeStartPoint = e.GetPosition(ElementsCanvas);

            _marqueeRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(32, 37, 99, 235)),
                IsHitTestVisible = false,
                Width = 0,
                Height = 0
            };
            Canvas.SetLeft(_marqueeRect, _marqueeStartPoint.X);
            Canvas.SetTop(_marqueeRect, _marqueeStartPoint.Y);
            SelectionCanvas.Children.Add(_marqueeRect);
            ElementsCanvas.CaptureMouse();
        }

        private void ElementsCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMarqueeSelecting || _marqueeRect == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isMarqueeSelecting = false;
                SelectionCanvas.Children.Remove(_marqueeRect);
                _marqueeRect = null;
                ElementsCanvas.ReleaseMouseCapture();
                return;
            }

            Point cur = e.GetPosition(ElementsCanvas);
            double x = Math.Min(_marqueeStartPoint.X, cur.X);
            double y = Math.Min(_marqueeStartPoint.Y, cur.Y);
            double w = Math.Abs(cur.X - _marqueeStartPoint.X);
            double h = Math.Abs(cur.Y - _marqueeStartPoint.Y);

            Canvas.SetLeft(_marqueeRect, x);
            Canvas.SetTop(_marqueeRect, y);
            _marqueeRect.Width = w;
            _marqueeRect.Height = h;
        }

        private void ElementsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isMarqueeSelecting) return;

            _isMarqueeSelecting = false;
            ElementsCanvas.ReleaseMouseCapture();

            if (_marqueeRect != null)
            {
                double x = Canvas.GetLeft(_marqueeRect);
                double y = Canvas.GetTop(_marqueeRect);
                double w = _marqueeRect.Width;
                double h = _marqueeRect.Height;
                SelectionCanvas.Children.Remove(_marqueeRect);
                _marqueeRect = null;

                if (w >= 4 || h >= 4)
                {
                    var marquee = new Rect(x, y, w, h);
                    if (_side != null)
                    {
                        var hits = _side.Elements.Where(el =>
                        {
                            var elRect = new Rect(el.X, el.Y, el.Width, el.Height);
                            return marquee.IntersectsWith(elRect);
                        }).ToList();

                        bool isMultiModifier = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
                                               (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                        if (isMultiModifier)
                        {
                            var combined = _selectedElements.Union(hits).ToList();
                            SetSelection(combined);
                        }
                        else
                        {
                            SetSelection(hits);
                        }
                    }
                }
            }
        }

        private void UpdateGuides()
        {
            GuidesCanvas.Children.Clear();
            if (_template == null) return;

            double w = _template.WidthWpf;
            double h = _template.HeightWpf;

            // Safe Area Guide (0.125 in = 12 px inward)
            if (_showSafeGuide)
            {
                double margin = 12.0;
                var safeRect = new Rectangle
                {
                    Width = Math.Max(1, w - (margin * 2)),
                    Height = Math.Max(1, h - (margin * 2)),
                    Stroke = new SolidColorBrush(Color.FromArgb(140, 16, 185, 129)), // Emerald green
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(safeRect, margin);
                Canvas.SetTop(safeRect, margin);
                GuidesCanvas.Children.Add(safeRect);
            }

            // Bleed / Outer Edge Guide
            if (_showBleedGuide)
            {
                var edgeRect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Stroke = new SolidColorBrush(Color.FromArgb(120, 239, 68, 68)), // Red dashed
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(edgeRect, 0);
                Canvas.SetTop(edgeRect, 0);
                GuidesCanvas.Children.Add(edgeRect);
            }
        }

        public void UpdateSelectionOverlay()
        {
            SelectionCanvas.Children.Clear();
            _activeSelContainer = null;
            _activeSelBorder = null;
            _activeHandles.Clear();

            if (_selectedElements.Count == 0) return;

            if (_selectedElements.Count > 1)
            {
                // Multi-selection: draw crisp dashed highlight border around each selected element
                foreach (var el in _selectedElements)
                {
                    var box = new Rectangle
                    {
                        Width = el.Width,
                        Height = el.Height,
                        Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        IsHitTestVisible = false,
                        Tag = el,
                        RenderTransformOrigin = new Point(0.5, 0.5)
                    };
                    if (el.Rotation != 0)
                    {
                        box.RenderTransform = new RotateTransform(el.Rotation);
                    }
                    Canvas.SetLeft(box, el.X);
                    Canvas.SetTop(box, el.Y);
                    SelectionCanvas.Children.Add(box);
                }
                return;
            }

            if (_selectedElement == null) return;

            double x = _selectedElement.X;
            double y = _selectedElement.Y;
            double w = _selectedElement.Width;
            double h = _selectedElement.Height;

            _activeSelContainer = new Canvas
            {
                Width = w,
                Height = h,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(_activeSelContainer, x);
            Canvas.SetTop(_activeSelContainer, y);

            if (_selectedElement.Rotation != 0)
            {
                _activeSelContainer.RenderTransform = new RotateTransform(_selectedElement.Rotation);
            }

            // Selection box border
            _activeSelBorder = new Rectangle
            {
                Width = w,
                Height = h,
                Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)), // Blue
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_activeSelBorder, 0);
            Canvas.SetTop(_activeSelBorder, 0);
            _activeSelContainer.Children.Add(_activeSelBorder);

            // 8 Resize Handles (relative to container)
            AddHandle(_activeSelContainer, "NW", 0, 0, Cursors.SizeNWSE);
            AddHandle(_activeSelContainer, "N", w / 2, 0, Cursors.SizeNS);
            AddHandle(_activeSelContainer, "NE", w, 0, Cursors.SizeNESW);
            AddHandle(_activeSelContainer, "E", w, h / 2, Cursors.SizeWE);
            AddHandle(_activeSelContainer, "SE", w, h, Cursors.SizeNWSE);
            AddHandle(_activeSelContainer, "S", w / 2, h, Cursors.SizeNS);
            AddHandle(_activeSelContainer, "SW", 0, h, Cursors.SizeNESW);
            AddHandle(_activeSelContainer, "W", 0, h / 2, Cursors.SizeWE);

            SelectionCanvas.Children.Add(_activeSelContainer);
        }

        private void UpdateSelectionOverlayGeometry()
        {
            if (_selectedElements.Count > 1)
            {
                foreach (UIElement child in SelectionCanvas.Children)
                {
                    if (child is Rectangle box && box.Tag is CardElement el)
                    {
                        Canvas.SetLeft(box, el.X);
                        Canvas.SetTop(box, el.Y);
                        box.Width = el.Width;
                        box.Height = el.Height;
                    }
                }
                return;
            }

            if (_selectedElement == null || _activeSelContainer == null || _activeSelBorder == null)
            {
                UpdateSelectionOverlay();
                return;
            }

            double x = _selectedElement.X;
            double y = _selectedElement.Y;
            double w = _selectedElement.Width;
            double h = _selectedElement.Height;

            _activeSelContainer.Width = w;
            _activeSelContainer.Height = h;
            Canvas.SetLeft(_activeSelContainer, x);
            Canvas.SetTop(_activeSelContainer, y);

            _activeSelBorder.Width = w;
            _activeSelBorder.Height = h;

            double halfHandle = 4.5;
            void Reposition(string id, double cx, double cy)
            {
                if (_activeHandles.TryGetValue(id, out var thumb))
                {
                    Canvas.SetLeft(thumb, cx - halfHandle);
                    Canvas.SetTop(thumb, cy - halfHandle);
                }
            }

            Reposition("NW", 0, 0);
            Reposition("N", w / 2, 0);
            Reposition("NE", w, 0);
            Reposition("E", w, h / 2);
            Reposition("SE", w, h);
            Reposition("S", w / 2, h);
            Reposition("SW", 0, h);
            Reposition("W", 0, h / 2);
        }

        private void AddHandle(Canvas container, string handleId, double cx, double cy, Cursor cursor)
        {
            double size = 9.0;
            var thumb = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                StrokeThickness = 2.0,
                Cursor = cursor,
                Tag = handleId
            };
            thumb.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 3,
                ShadowDepth = 1,
                Opacity = 0.25
            };

            Canvas.SetLeft(thumb, cx - (size / 2.0));
            Canvas.SetTop(thumb, cy - (size / 2.0));

            thumb.MouseLeftButtonDown += (s, e) =>
            {
                if (_selectedElement == null) return;
                e.Handled = true;
                _isResizingHandle = true;
                _activeHandle = handleId;
                _dragStartMousePoint = e.GetPosition(SelectionCanvas);
                _resizeStartBounds = new Rect(_selectedElement.X, _selectedElement.Y, _selectedElement.Width, _selectedElement.Height);

                _groupResizeStartChildren.Clear();
                if (_selectedElement is GroupElement grp)
                {
                    foreach (var c in grp.Children)
                    {
                        _groupResizeStartChildren[c] = new Rect(c.X, c.Y, c.Width, c.Height);
                    }
                }

                thumb.CaptureMouse();
            };

            thumb.MouseMove += (s, e) =>
            {
                if (_isResizingHandle)
                {
                    if (e.LeftButton != MouseButtonState.Pressed || _selectedElement == null)
                    {
                        _isResizingHandle = false;
                        thumb.ReleaseMouseCapture();
                        return;
                    }

                    Point cur = e.GetPosition(SelectionCanvas);
                    double dx = cur.X - _dragStartMousePoint.X;
                    double dy = cur.Y - _dragStartMousePoint.Y;

                    ApplyResize(_activeHandle, dx, dy);
                }
            };

            thumb.MouseLeftButtonUp += (s, e) =>
            {
                if (_isResizingHandle)
                {
                    _isResizingHandle = false;
                    thumb.ReleaseMouseCapture();
                    RefreshCanvas();
                    CanvasModified?.Invoke(this, EventArgs.Empty);
                }
            };

            thumb.LostMouseCapture += (s, e) =>
            {
                _isResizingHandle = false;
            };

            _activeHandles[handleId] = thumb;
            container.Children.Add(thumb);
        }

        private void ApplyResize(string handle, double dx, double dy)
        {
            if (_selectedElement == null) return;

            double minSize = 10.0;
            double x = _resizeStartBounds.X;
            double y = _resizeStartBounds.Y;
            double w = _resizeStartBounds.Width;
            double h = _resizeStartBounds.Height;

            switch (handle)
            {
                case "E":
                    w = Math.Max(minSize, _resizeStartBounds.Width + dx);
                    break;
                case "W":
                    double newW = Math.Max(minSize, _resizeStartBounds.Width - dx);
                    x = _resizeStartBounds.Right - newW;
                    w = newW;
                    break;
                case "S":
                    h = Math.Max(minSize, _resizeStartBounds.Height + dy);
                    break;
                case "N":
                    double newH = Math.Max(minSize, _resizeStartBounds.Height - dy);
                    y = _resizeStartBounds.Bottom - newH;
                    h = newH;
                    break;
                case "SE":
                    w = Math.Max(minSize, _resizeStartBounds.Width + dx);
                    h = Math.Max(minSize, _resizeStartBounds.Height + dy);
                    break;
                case "SW":
                    newW = Math.Max(minSize, _resizeStartBounds.Width - dx);
                    x = _resizeStartBounds.Right - newW;
                    w = newW;
                    h = Math.Max(minSize, _resizeStartBounds.Height + dy);
                    break;
                case "NE":
                    w = Math.Max(minSize, _resizeStartBounds.Width + dx);
                    newH = Math.Max(minSize, _resizeStartBounds.Height - dy);
                    y = _resizeStartBounds.Bottom - newH;
                    h = newH;
                    break;
                case "NW":
                    newW = Math.Max(minSize, _resizeStartBounds.Width - dx);
                    x = _resizeStartBounds.Right - newW;
                    w = newW;
                    newH = Math.Max(minSize, _resizeStartBounds.Height - dy);
                    y = _resizeStartBounds.Bottom - newH;
                    h = newH;
                    break;
            }

            _selectedElement.X = Math.Round(x, 1);
            _selectedElement.Y = Math.Round(y, 1);
            _selectedElement.Width = Math.Round(w, 1);
            _selectedElement.Height = Math.Round(h, 1);

            if (_selectedElement is GroupElement grp && _resizeStartBounds.Width > 0 && _resizeStartBounds.Height > 0)
            {
                double scaleX = _selectedElement.Width / _resizeStartBounds.Width;
                double scaleY = _selectedElement.Height / _resizeStartBounds.Height;
                foreach (var c in grp.Children)
                {
                    if (_groupResizeStartChildren.TryGetValue(c, out var orig))
                    {
                        c.X = Math.Round(orig.X * scaleX, 1);
                        c.Y = Math.Round(orig.Y * scaleY, 1);
                        c.Width = Math.Max(5, Math.Round(orig.Width * scaleX, 1));
                        c.Height = Math.Max(5, Math.Round(orig.Height * scaleY, 1));
                    }
                }
            }

            // Update matching visual directly
            foreach (UIElement child in ElementsCanvas.Children)
            {
                if (child is Border b && b.Tag == _selectedElement)
                {
                    b.Width = _selectedElement.Width;
                    b.Height = _selectedElement.Height;
                    Canvas.SetLeft(b, _selectedElement.X);
                    Canvas.SetTop(b, _selectedElement.Y);

                    if (b.Child is FrameworkElement fe)
                    {
                        fe.Width = _selectedElement.Width;
                        fe.Height = _selectedElement.Height;

                        if (_selectedElement is GroupElement && fe is Canvas grpCanvas)
                        {
                            grpCanvas.Children.Clear();
                            foreach (var gc in ((GroupElement)_selectedElement).Children.OrderBy(c => c.ZIndex))
                            {
                                var cw = new Border
                                {
                                    Width = gc.Width,
                                    Height = gc.Height,
                                    Background = Brushes.Transparent,
                                    Child = BuildContentVisual(gc)
                                };
                                Canvas.SetLeft(cw, gc.X);
                                Canvas.SetTop(cw, gc.Y);
                                grpCanvas.Children.Add(cw);
                            }
                        }
                    }
                    break;
                }
            }

            UpdateSelectionOverlayGeometry();
            ElementTransforming?.Invoke(this, EventArgs.Empty);
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (_selectedElement == null) return;

            double step = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 10.0 : 1.0;

            if (e.Key == Key.Left)
            {
                _selectedElement.X -= step;
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                _selectedElement.X += step;
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _selectedElement.Y -= step;
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _selectedElement.Y += step;
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                ElementDeleteRequested?.Invoke(this, _selectedElement);
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ElementDuplicateRequested?.Invoke(this, _selectedElement);
                e.Handled = true;
                return;
            }

            if (e.Handled)
            {
                RefreshCanvas();
                CanvasModified?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
