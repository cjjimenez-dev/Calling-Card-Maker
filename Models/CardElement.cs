using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Calling_Card_Generator.Models
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(TextElement), "text")]
    [JsonDerivedType(typeof(ImageElement), "image")]
    [JsonDerivedType(typeof(ShapeElement), "shape")]
    [JsonDerivedType(typeof(GroupElement), "group")]
    [JsonDerivedType(typeof(QrCodeElement), "qrcode")]
    public abstract class CardElement : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private double _x;
        private double _y;
        private double _width = 100;
        private double _height = 50;
        private int _zIndex = 0;
        private bool _isLocked = false;
        private bool _isSelected = false;
        private double _rotation = 0.0;
        private bool _flipHorizontal = false;
        private bool _flipVertical = false;

        public double Rotation
        {
            get => _rotation;
            set => SetField(ref _rotation, Math.Round((value % 360 + 360) % 360, 1));
        }

        public bool FlipHorizontal
        {
            get => _flipHorizontal;
            set => SetField(ref _flipHorizontal, value);
        }

        public bool FlipVertical
        {
            get => _flipVertical;
            set => SetField(ref _flipVertical, value);
        }

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public double X
        {
            get => _x;
            set => SetField(ref _x, Math.Round(value, 2));
        }

        public double Y
        {
            get => _y;
            set => SetField(ref _y, Math.Round(value, 2));
        }

        public double Width
        {
            get => _width;
            set => SetField(ref _width, Math.Max(5, Math.Round(value, 2)));
        }

        public double Height
        {
            get => _height;
            set => SetField(ref _height, Math.Max(5, Math.Round(value, 2)));
        }

        public int ZIndex
        {
            get => _zIndex;
            set => SetField(ref _zIndex, value);
        }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetField(ref _isLocked, value);
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public abstract CardElement Clone();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
