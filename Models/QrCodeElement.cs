namespace Calling_Card_Generator.Models
{
    public class QrCodeElement : CardElement
    {
        private string _qrContent = "https://www.vcsi.biz";
        private string _darkColor = "#000000";
        private string _lightColor = "#FFFFFF";
        private bool _includeQuietZone = true;

        public string QrContent
        {
            get => _qrContent;
            set => SetField(ref _qrContent, value);
        }

        public string DarkColor
        {
            get => _darkColor;
            set => SetField(ref _darkColor, value);
        }

        public string LightColor
        {
            get => _lightColor;
            set => SetField(ref _lightColor, value);
        }

        public bool IncludeQuietZone
        {
            get => _includeQuietZone;
            set => SetField(ref _includeQuietZone, value);
        }

        public override CardElement Clone()
        {
            return new QrCodeElement
            {
                X = X + 10,
                Y = Y + 10,
                Width = Width,
                Height = Height,
                ZIndex = ZIndex + 1,
                IsLocked = IsLocked,
                Rotation = Rotation,
                FlipHorizontal = FlipHorizontal,
                FlipVertical = FlipVertical,
                QrContent = QrContent,
                DarkColor = DarkColor,
                LightColor = LightColor,
                IncludeQuietZone = IncludeQuietZone
            };
        }
    }
}
