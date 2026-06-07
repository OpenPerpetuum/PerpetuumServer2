using System.Windows;

namespace Perpetuum.AdminTool.Views
{
    public partial class ExportScriptWindow : Window
    {
        public ExportScriptWindow(Perpetuum.AdminTool.Export.ExportScriptViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
