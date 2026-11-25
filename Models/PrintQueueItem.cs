using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Calling_Card_Generator.Models
{
    public enum PrintSideOption
    {
        [Description("Front & Back")]
        FrontAndBack,
        [Description("Front Only")]
        FrontOnly,
        [Description("Back Only")]
        BackOnly
    }

    public class PrintQueueItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private CardTemplate _template;
        private PrintSideOption _sideOption = PrintSideOption.FrontAndBack;
        private int _quantity = 10;
        private string _status = "Ready";

        public PrintQueueItem(CardTemplate template)
        {
            _template = template;
        }

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public CardTemplate Template
        {
            get => _template;
            set
            {
                if (SetField(ref _template, value))
                {
                    OnPropertyChanged(nameof(CardName));
                }
            }
        }

        public string CardName => Template?.Name ?? "Unnamed Card";

        public PrintSideOption SideOption
        {
            get => _sideOption;
            set => SetField(ref _sideOption, value);
        }

        public int Quantity
        {
            get => _quantity;
            set => SetField(ref _quantity, Math.Max(1, value));
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
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
