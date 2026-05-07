using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Perpetuum.AdminTool.Editing
{
    public class ChangeQueue
    {
        public ObservableCollection<IPendingChange> Items { get; } = new();

        // Definition names of brand-new entitydefaults rows that were queued for INSERT.
        // Drained at commit time to auto-create the matching translation keys.
        // Cleared by Clear() so discarding the queue also discards the pending keys.
        public List<string> PendingNewEntityNames { get; } = new();

        public void Add(IPendingChange change) => Items.Add(change);
        public void AddNewEntityName(string definitionName) => PendingNewEntityNames.Add(definitionName);

        public void Clear()
        {
            Items.Clear();
            PendingNewEntityNames.Clear();
        }

        public bool HasPending => Items.Count > 0;
    }
}
