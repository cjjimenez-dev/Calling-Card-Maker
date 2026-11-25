using System;
using System.Collections.ObjectModel;

namespace Calling_Card_Generator.Models
{
    public class GroupElement : CardElement
    {
        public ObservableCollection<CardElement> Children { get; set; } = new();

        public override CardElement Clone()
        {
            var copy = new GroupElement
            {
                X = X + 10,
                Y = Y + 10,
                Width = Width,
                Height = Height,
                ZIndex = ZIndex + 1,
                IsLocked = IsLocked,
                Rotation = Rotation,
                FlipHorizontal = FlipHorizontal,
                FlipVertical = FlipVertical
            };

            foreach (var child in Children)
            {
                var childClone = child.Clone();
                // Keep exact relative coordinates inside the cloned group
                childClone.X = child.X;
                childClone.Y = child.Y;
                childClone.ZIndex = child.ZIndex;
                copy.Children.Add(childClone);
            }

            return copy;
        }
    }
}
