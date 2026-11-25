using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Calling_Card_Generator.Models
{
    public class CardSide : INotifyPropertyChanged
    {
        private string _backgroundColor = "#FFFFFF";
        private string _backgroundImageBase64 = string.Empty;
        private ObservableCollection<CardElement> _elements = new();

        public string BackgroundColor
        {
            get => _backgroundColor;
            set => SetField(ref _backgroundColor, value);
        }

        public string BackgroundImageBase64
        {
            get => _backgroundImageBase64;
            set => SetField(ref _backgroundImageBase64, value);
        }

        public ObservableCollection<CardElement> Elements
        {
            get => _elements;
            set => SetField(ref _elements, value);
        }

        public CardSide Clone()
        {
            var side = new CardSide
            {
                BackgroundColor = this.BackgroundColor,
                BackgroundImageBase64 = this.BackgroundImageBase64
            };

            foreach (var el in this.Elements)
            {
                side.Elements.Add(el.Clone());
            }

            return side;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
