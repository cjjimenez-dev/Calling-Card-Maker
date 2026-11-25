using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Calling_Card_Generator.Models
{
    public enum PaperType
    {
        Letter, // 8.5 x 11 in
        A4      // 8.27 x 11.69 in (210 x 297 mm)
    }

    public enum PrintMethod
    {
        ManualTwoPass, // Print all fronts, prompt to flip, print all backs
        AutoDuplex,    // Two-sided hardware duplex
        SingleSided    // Single sided only (front or back as specified)
    }

    public class SheetLayout : INotifyPropertyChanged
    {
        private PaperType _paperType = PaperType.Letter;
        private CardOrientation _paperOrientation = CardOrientation.Portrait;
        private int _columns = 2;
        private int _rows = 5;
        private double _marginLeftInches = 0.75;
        private double _marginTopInches = 0.5;
        private double _gapHorizontalInches = 0.0;
        private double _gapVerticalInches = 0.0;
        private bool _drawCropMarks = true;
        private bool _drawCutLines = true;
        private PrintMethod _printMethod = PrintMethod.ManualTwoPass;

        public PaperType PaperType
        {
            get => _paperType;
            set
            {
                if (SetField(ref _paperType, value))
                {
                    OnPropertyChanged(nameof(PaperWidthInches));
                    OnPropertyChanged(nameof(PaperHeightInches));
                    OnPropertyChanged(nameof(PaperWidthWpf));
                    OnPropertyChanged(nameof(PaperHeightWpf));
                }
            }
        }

        public CardOrientation PaperOrientation
        {
            get => _paperOrientation;
            set
            {
                if (SetField(ref _paperOrientation, value))
                {
                    OnPropertyChanged(nameof(PaperWidthInches));
                    OnPropertyChanged(nameof(PaperHeightInches));
                    OnPropertyChanged(nameof(PaperWidthWpf));
                    OnPropertyChanged(nameof(PaperHeightWpf));
                }
            }
        }

        private double BaseWidthInches => PaperType == PaperType.Letter ? 8.5 : 8.27;
        private double BaseHeightInches => PaperType == PaperType.Letter ? 11.0 : 11.69;

        public double PaperWidthInches => PaperOrientation == CardOrientation.Portrait ? BaseWidthInches : BaseHeightInches;
        public double PaperHeightInches => PaperOrientation == CardOrientation.Portrait ? BaseHeightInches : BaseWidthInches;

        public double PaperWidthWpf => PaperWidthInches * 96.0;
        public double PaperHeightWpf => PaperHeightInches * 96.0;

        public int Columns
        {
            get => _columns;
            set => SetField(ref _columns, Math.Max(1, value));
        }

        public int Rows
        {
            get => _rows;
            set => SetField(ref _rows, Math.Max(1, value));
        }

        public int CardsPerSheet => Columns * Rows;

        public double MarginLeftInches
        {
            get => _marginLeftInches;
            set => SetField(ref _marginLeftInches, Math.Max(0, value));
        }

        public double MarginTopInches
        {
            get => _marginTopInches;
            set => SetField(ref _marginTopInches, Math.Max(0, value));
        }

        public double GapHorizontalInches
        {
            get => _gapHorizontalInches;
            set => SetField(ref _gapHorizontalInches, Math.Max(0, value));
        }

        public double GapVerticalInches
        {
            get => _gapVerticalInches;
            set => SetField(ref _gapVerticalInches, Math.Max(0, value));
        }

        public bool DrawCropMarks
        {
            get => _drawCropMarks;
            set => SetField(ref _drawCropMarks, value);
        }

        public bool DrawCutLines
        {
            get => _drawCutLines;
            set => SetField(ref _drawCutLines, value);
        }

        public PrintMethod PrintMethod
        {
            get => _printMethod;
            set => SetField(ref _printMethod, value);
        }

        /// <summary>
        /// Automatically recalculates optimal margins to center the card grid on the page.
        /// </summary>
        public void AutoCenter(double cardWidthInches, double cardHeightInches)
        {
            double totalCardsWidth = (Columns * cardWidthInches) + ((Columns - 1) * GapHorizontalInches);
            double totalCardsHeight = (Rows * cardHeightInches) + ((Rows - 1) * GapVerticalInches);

            MarginLeftInches = Math.Max(0.1, (PaperWidthInches - totalCardsWidth) / 2.0);
            MarginTopInches = Math.Max(0.1, (PaperHeightInches - totalCardsHeight) / 2.0);
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
