using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Perpetuum.AdminTool.Translations;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class TranslationsView : UserControl
    {
        private TranslationsViewModel? _vm;

        public TranslationsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
                _vm.Languages.CollectionChanged -= OnLanguagesChanged;
            }

            _vm = DataContext as TranslationsViewModel;

            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                _vm.Languages.CollectionChanged += OnLanguagesChanged;
                RebuildColumns();
            }
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TranslationsViewModel.Store) ||
                e.PropertyName == nameof(TranslationsViewModel.Languages) ||
                e.PropertyName == nameof(TranslationsViewModel.Rows))
            {
                if (_vm != null)
                {
                    _vm.Languages.CollectionChanged -= OnLanguagesChanged;
                    _vm.Languages.CollectionChanged += OnLanguagesChanged;
                }
                RebuildColumns();
            }
        }

        private void OnLanguagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildColumns();
        }

        private void RebuildColumns()
        {
            Grid.Columns.Clear();

            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Key",
                Binding = new Binding(nameof(TranslationRow.Key))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                },
                Width = new DataGridLength(280)
            });

            if (_vm == null) return;

            foreach (var langId in _vm.Languages)
            {
                var header = $"[{langId}] {LanguageCatalog.NameOf(langId)}";
                var col = new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding($"[{langId}]")
                    {
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    MinWidth = 160
                };
                Grid.Columns.Add(col);
            }
        }

        private void OnReloadClick(object sender, RoutedEventArgs e)
        {
            _vm?.Load();
            RebuildColumns();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // Force any in-flight cell edit to commit before serializing.
            Grid.CommitEdit(DataGridEditingUnit.Cell, true);
            Grid.CommitEdit(DataGridEditingUnit.Row, true);
            _vm?.Save();
        }

        private void OnAddKeyClick(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var win = new AddKeyWindow { Owner = Window.GetWindow(this) };
            while (true)
            {
                if (win.ShowDialog() != true) return;
                if (_vm.TryAddKey(win.ViewModel.Key.Trim(), out var error))
                {
                    RebuildColumns();
                    return;
                }
                win.ViewModel.ErrorMessage = error;
                win = new AddKeyWindow { Owner = Window.GetWindow(this) };
                win.ViewModel.Key = "";
                win.ViewModel.ErrorMessage = error;
            }
        }

        private void OnAddLanguageClick(object sender, RoutedEventArgs e)
        {
            if (_vm?.Store == null) return;
            var win = new AddLanguageWindow(_vm.Store.UnusedLanguages()) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;
            var picked = win.ViewModel.Selected;
            if (picked == null) return;

            if (_vm.TryAddLanguage(picked.Id, out var error))
            {
                RebuildColumns();
            }
            else
            {
                MessageBox.Show(Window.GetWindow(this), error, "Cannot add language",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnRemoveSelectedClick(object sender, RoutedEventArgs e)
        {
            if (_vm?.SelectedRow == null) return;
            var key = _vm.SelectedRow.Key;
            var ok = MessageBox.Show(Window.GetWindow(this),
                $"Remove key '{key}' from all languages?\n(Save to persist.)",
                "Remove key", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;
            _vm.RemoveSelected();
        }
    }
}
