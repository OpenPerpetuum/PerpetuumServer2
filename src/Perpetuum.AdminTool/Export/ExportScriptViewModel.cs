using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Export
{
    public partial class ExportScriptViewModel : ObservableObject
    {
        public string Title { get; }
        public string Script { get; }

        public ExportScriptViewModel(string title, string script)
        {
            Title  = title;
            Script = script;
        }

        [RelayCommand]
        private void CopyToClipboard() =>
            Clipboard.SetText(Script);

        [RelayCommand]
        private void SaveAs()
        {
            var dlg = new SaveFileDialog
            {
                Filter           = "SQL scripts (*.sql)|*.sql|All files (*.*)|*.*",
                DefaultExt       = ".sql",
                FileName         = SqlScriptBuilder.BuildFileName("export", Title),
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, Script, System.Text.Encoding.UTF8);
        }
    }
}
