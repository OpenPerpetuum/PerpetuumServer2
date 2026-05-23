using System.Windows;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views;

public partial class NewItemDialog : Window
{
    private NewItemDialogViewModel Vm => (NewItemDialogViewModel)DataContext;

    public NewItemDialog(NewItemDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += (_, success) =>
        {
            DialogResult = success;
            Close();
        };
    }

    // Tab 1 — Basic
    private void PickCategoryMain_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.BasicPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.BasicPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeMain_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.BasicPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.BasicPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 2 — Calibration Template
    private void PickCalibrationCategory_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.CalibrationPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.CalibrationPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickCalibrationAttribute_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.CalibrationPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.CalibrationPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 3 — Prototype
    private void PickPrototypeCategory_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.PrototypePanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.PrototypePanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickPrototypeAttribute_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.PrototypePanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.PrototypePanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }
}
