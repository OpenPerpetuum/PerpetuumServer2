using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.ViewModels
{
    public class ConfirmSqlViewModel : ObservableObject
    {
        public string Sql { get; }
        public string Header { get; }

        public ConfirmSqlViewModel(string header, string sql)
        {
            Header = header;
            Sql = sql;
        }
    }
}
