using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class PresenceFlocksWindow : Window
    {
        public PresenceFlocksViewModel ViewModel { get; }

        public PresenceFlocksWindow(PresenceFlocksViewModel vm)
        {
            InitializeComponent();
            ViewModel = vm;
            DataContext = vm;
            Loaded += async (_, _) => await vm.ReloadAsync();
        }

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.ReloadAsync();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
