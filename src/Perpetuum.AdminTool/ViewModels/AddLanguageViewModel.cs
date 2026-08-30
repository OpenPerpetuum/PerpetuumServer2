using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddLanguageViewModel : ObservableObject
    {
        [ObservableProperty] private LanguageInfo? _selected;
        [ObservableProperty] private string _errorMessage = "";

        public ObservableCollection<LanguageInfo> Available { get; }

        public AddLanguageViewModel(IEnumerable<int> unusedLanguageIds)
        {
            Available = new ObservableCollection<LanguageInfo>(
                unusedLanguageIds
                    .Select(id => LanguageCatalog.All.FirstOrDefault(l => l.Id == id)
                                  ?? new LanguageInfo(id, $"Lang {id}"))
                    .OrderBy(l => l.Id));

            Selected = Available.FirstOrDefault();
        }
    }
}
