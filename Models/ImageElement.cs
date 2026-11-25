using System.Windows.Media;

namespace Calling_Card_Generator.Models
{
    public class ImageElement : CardElement
    {
        private string _imagePath = string.Empty;
        private string _imageBase64 = string.Empty;
        private double _opacity = 1.0;
        private Stretch _stretch = Stretch.Uniform;

        public string ImagePath
        {
            get => _imagePath;
            set => SetField(ref _imagePath, value);
        }

        public string ImageBase64
        {
            get => _imageBase64;
            set => SetField(ref _imageBase64, value);
        }

        public double Opacity
        {
            get => _opacity;
            set => SetField(ref _opacity, Math.Clamp(value, 0.05, 1.0));
        }

        public Stretch Stretch
        {
            get => _stretch;
            set => SetField(ref _stretch, value);
        }

        public override CardElement Clone()
        {
            return new ImageElement
            {
                X = this.X + 10,
                Y = this.Y + 10,
                Width = this.Width,
                Height = this.Height,
                ZIndex = this.ZIndex + 1,
                ImagePath = this.ImagePath,
                ImageBase64 = this.ImageBase64,
                Opacity = this.Opacity,
                Stretch = this.Stretch,
                Rotation = this.Rotation,
                FlipHorizontal = this.FlipHorizontal,
                FlipVertical = this.FlipVertical
            };
        }
    }
}
