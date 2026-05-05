using System.Collections.ObjectModel;

namespace Perpetuum.AdminTool.Editing
{
    public class ChangeQueue
    {
        public ObservableCollection<IPendingChange> Items { get; } = new();

        public void Add(IPendingChange change) => Items.Add(change);
        public void Clear() => Items.Clear();
        public bool HasPending => Items.Count > 0;
    }
}
