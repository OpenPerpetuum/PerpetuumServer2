using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class SeasonWizardWindow : Window
    {
        private readonly SeasonWizardViewModel _vm;

        public SeasonWizardWindow(SeasonWizardViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }

        private void OnFinishOrNextClick(object sender, RoutedEventArgs e)
        {
            if (_vm.IsReviewStep)
            {
                _vm.FinishCommand.Execute(null);
                DialogResult = true;
                Close();
            }
            else
            {
                _vm.NextCommand.Execute(null);
            }
        }
    }
}
