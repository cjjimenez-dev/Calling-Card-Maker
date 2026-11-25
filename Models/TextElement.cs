using System.Windows;

namespace Calling_Card_Generator.Models
{
    public class TextElement : CardElement
    {
        private string _text = "Sample Text";
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 11;
        private bool _isBold = false;
        private bool _isItalic = false;
        private bool _isUnderline = false;
        private string _textColor = "#111827";
        private TextAlignment _alignment = TextAlignment.Left;

        public string Text
        {
            get => _text;
            set => SetField(ref _text, value);
        }

        public string FontFamily
        {
            get => _fontFamily;
            set => SetField(ref _fontFamily, value);
        }

        public double FontSize
        {
            get => _fontSize;
            set => SetField(ref _fontSize, Math.Max(4, Math.Round(value, 1)));
        }

        public bool IsBold
        {
            get => _isBold;
            set => SetField(ref _isBold, value);
        }

        public bool IsItalic
        {
            get => _isItalic;
            set => SetField(ref _isItalic, value);
        }

        public bool IsUnderline
        {
            get => _isUnderline;
            set => SetField(ref _isUnderline, value);
        }

        public string TextColor
        {
            get => _textColor;
            set => SetField(ref _textColor, value);
        }

        public TextAlignment Alignment
        {
            get => _alignment;
            set => SetField(ref _alignment, value);
        }

        public override CardElement Clone()
        {
            return new TextElement
            {
                X = this.X + 10,
                Y = this.Y + 10,
                Width = this.Width,
                Height = this.Height,
                ZIndex = this.ZIndex + 1,
                Text = this.Text,
                FontFamily = this.FontFamily,
                FontSize = this.FontSize,
                IsBold = this.IsBold,
                IsItalic = this.IsItalic,
                IsUnderline = this.IsUnderline,
                TextColor = this.TextColor,
                Alignment = this.Alignment
            };
        }
    }
}
