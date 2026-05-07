using System.Windows;

namespace Perpetuum.AdminTool.Common
{
    // Forwards the parent's DataContext into places where the visual tree breaks the binding chain
    // — most commonly DataGridColumn cell templates, which live outside the grid's visual tree and
    // therefore cannot reach the grid's DataContext via RelativeSource.
    public class BindingProxy : Freezable
    {
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

        public object? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        protected override Freezable CreateInstanceCore() => new BindingProxy();
    }
}
