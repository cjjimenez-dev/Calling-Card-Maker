namespace Calling_Card_Generator.Models
{
    public enum ShapeType
    {
        Rectangle,
        Line,
        Ellipse
    }

    public class ShapeElement : CardElement
    {
        private ShapeType _shapeKind = ShapeType.Rectangle;
        private string _fillColor = "#0B1A60";
        private string _strokeColor = "#0B1A60";
        private double _strokeThickness = 1.0;
        private double _cornerRadius = 0.0;

        public ShapeType ShapeKind
        {
            get => _shapeKind;
            set => SetField(ref _shapeKind, value);
        }

        public string FillColor
        {
            get => _fillColor;
            set => SetField(ref _fillColor, value);
        }

        public string StrokeColor
        {
            get => _strokeColor;
            set => SetField(ref _strokeColor, value);
        }

        public double StrokeThickness
        {
            get => _strokeThickness;
            set => SetField(ref _strokeThickness, Math.Max(0.5, value));
        }

        public double CornerRadius
        {
            get => _cornerRadius;
            set => SetField(ref _cornerRadius, Math.Max(0, value));
        }

        public override CardElement Clone()
        {
            return new ShapeElement
            {
                X = this.X + 10,
                Y = this.Y + 10,
                Width = this.Width,
                Height = this.Height,
                ZIndex = this.ZIndex + 1,
                ShapeKind = this.ShapeKind,
                FillColor = this.FillColor,
                StrokeColor = this.StrokeColor,
                StrokeThickness = this.StrokeThickness,
                CornerRadius = this.CornerRadius
            };
        }
    }
}
