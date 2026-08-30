using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class SeasonDetailView : UserControl
    {
        public SeasonDetailView()
        {
            InitializeComponent();
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            var parent = FindAncestor<SeasonsView>(this);
            parent?.RequestBack();
        }

        private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var current = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (current != null && current is not T)
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            return current as T;
        }
    }
}
