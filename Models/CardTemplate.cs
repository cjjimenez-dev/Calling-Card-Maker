using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Calling_Card_Generator.Models
{
    public enum CardOrientation
    {
        Landscape,
        Portrait
    }

    public class CardTemplate : INotifyPropertyChanged
    {
        private string _name = "Untitled Template";
        private CardOrientation _orientation = CardOrientation.Portrait;
        private double _widthInches = 2.0;
        private double _heightInches = 3.5;
        private CardSide _front = new();
        private CardSide _back = new();

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public CardOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (SetField(ref _orientation, value))
                {
                    OnPropertyChanged(nameof(WidthWpf));
                    OnPropertyChanged(nameof(HeightWpf));
                }
            }
        }

        public double WidthInches
        {
            get => _widthInches;
            set
            {
                if (SetField(ref _widthInches, Math.Max(1.0, value)))
                {
                    OnPropertyChanged(nameof(WidthWpf));
                }
            }
        }

        public double HeightInches
        {
            get => _heightInches;
            set
            {
                if (SetField(ref _heightInches, Math.Max(1.0, value)))
                {
                    OnPropertyChanged(nameof(HeightWpf));
                }
            }
        }

        [JsonIgnore]
        public double WidthWpf => WidthInches * 96.0;

        [JsonIgnore]
        public double HeightWpf => HeightInches * 96.0;

        public CardSide Front
        {
            get => _front;
            set => SetField(ref _front, value);
        }

        public CardSide Back
        {
            get => _back;
            set => SetField(ref _back, value);
        }

        public void SetOrientation(CardOrientation newOrientation)
        {
            if (newOrientation == _orientation) return;
            _orientation = newOrientation;
            // Swap width and height
            double temp = _widthInches;
            _widthInches = _heightInches;
            _heightInches = temp;

            OnPropertyChanged(nameof(Orientation));
            OnPropertyChanged(nameof(WidthInches));
            OnPropertyChanged(nameof(HeightInches));
            OnPropertyChanged(nameof(WidthWpf));
            OnPropertyChanged(nameof(HeightWpf));
        }

        public CardTemplate Clone()
        {
            return new CardTemplate
            {
                Name = this.Name + " (Copy)",
                Orientation = this.Orientation,
                WidthInches = this.WidthInches,
                HeightInches = this.HeightInches,
                Front = this.Front.Clone(),
                Back = this.Back.Clone()
            };
        }

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
