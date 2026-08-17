using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views;

public partial class NewRobotDialog : Window
{
    private NewRobotDialogViewModel Vm => (NewRobotDialogViewModel)DataContext;

    public NewRobotDialog(NewRobotDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += (_, success) =>
        {
            DialogResult = success;
            Close();
        };
    }

    // Enter activates the default Save button without moving focus, so a TextBox bound with the
    // default LostFocus trigger would still be holding an uncommitted value when Save runs. Push
    // it to the source first. Multi-line boxes consume Enter themselves and are left alone.
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (Keyboard.FocusedElement is TextBox { AcceptsReturn: false } box)
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
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

    // Tab 9 — Head
    private void PickCategoryHead_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.HeadPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.HeadPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeHead_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.HeadPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.HeadPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 10 — Chassis
    private void PickCategoryChassis_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.ChassisPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.ChassisPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeChassis_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.ChassisPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.ChassisPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 11 — Leg
    private void PickCategoryLeg_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.LegPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.LegPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeLeg_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.LegPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.LegPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 12 — Inventory
    private void PickCategoryInventory_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.InventoryPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.InventoryPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeInventory_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.InventoryPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.InventoryPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }
}
