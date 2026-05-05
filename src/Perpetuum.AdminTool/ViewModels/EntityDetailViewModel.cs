using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.ExportedTypes;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EntityDetailViewModel : ObservableObject
    {
        private readonly ChangeQueue _queue;
        private readonly IReadOnlyDictionary<int, AggregateFieldInfo> _fields;

        [ObservableProperty] private EntityDefaultRow? _row;
        [ObservableProperty] private StatRow? _selectedStat;
        [ObservableProperty] private AggregateFieldInfo? _newStatField;
        [ObservableProperty] private double _newStatValue;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        public ObservableCollection<AggregateFieldInfo> AvailableFields { get; } = new();

        public EntityDetailViewModel(ChangeQueue queue, IReadOnlyDictionary<int, AggregateFieldInfo> fields)
        {
            _queue = queue;
            _fields = fields;
        }

        partial void OnRowChanged(EntityDefaultRow? value)
        {
            RebuildAvailableFields();
            StatusMessage = "";
            StatusIsError = false;
        }

        public void RebuildAvailableFields()
        {
            AvailableFields.Clear();
            if (Row == null) return;

            var used = Row.Stats.Select(s => (int)s.Field).ToHashSet();
            foreach (var info in _fields.Values
                                        .Where(f => !used.Contains(f.Id))
                                        .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                AvailableFields.Add(info);
            }
            NewStatField = AvailableFields.FirstOrDefault();
        }

        public void AddStat()
        {
            if (Row == null || NewStatField == null) return;
            var fieldId = NewStatField.Id;
            if (Row.Stats.Any(s => (int)s.Field == fieldId))
            {
                StatusIsError = true;
                StatusMessage = "That field is already on this entity.";
                return;
            }
            Row.Stats.Add(new StatRow(Row.Definition, (AggregateField)fieldId, NewStatValue, wasInDb: false));
            NewStatValue = 0d;
            RebuildAvailableFields();
            StatusIsError = false;
            StatusMessage = $"Added stat {NewStatField.Name}.";
        }

        public void RemoveSelectedStat()
        {
            if (Row == null || SelectedStat == null) return;
            Row.Stats.Remove(SelectedStat);
            SelectedStat = null;
            RebuildAvailableFields();
        }

        public void Save()
        {
            if (Row == null) return;
            var changes = EntityChanges.ComputeChanges(Row).ToList();
            if (changes.Count == 0)
            {
                StatusIsError = false;
                StatusMessage = "No changes to save.";
                return;
            }
            foreach (var c in changes) _queue.Add(c);

            // Refresh original snapshots so subsequent edits diff correctly.
            Row.RefreshOriginalFromCurrent();
            Row.OriginalStats.Clear();
            foreach (var s in Row.Stats)
            {
                Row.OriginalStats[(int)s.Field] = s.Value;
            }
            // Replace stat instances so WasInDb flips to true going forward.
            var rebuilt = Row.Stats
                .Select(s => new StatRow(Row.Definition, s.Field, s.Value, wasInDb: true))
                .ToList();
            Row.Stats.Clear();
            foreach (var s in rebuilt) Row.Stats.Add(s);

            StatusIsError = false;
            StatusMessage = $"Queued {changes.Count} change(s). Use the main Commit button to apply.";
        }

        public void Discard()
        {
            if (Row == null) return;
            // Revert primitive fields
            Row.ApplySnapshot(Row.Original);

            // Revert stats: re-add only the originals
            Row.Stats.Clear();
            foreach (var (fieldId, value) in Row.OriginalStats)
            {
                Row.Stats.Add(new StatRow(Row.Definition, (AggregateField)fieldId, value, wasInDb: true));
            }
            RebuildAvailableFields();
            StatusIsError = false;
            StatusMessage = "Reverted to last-saved state.";
        }
    }
}
