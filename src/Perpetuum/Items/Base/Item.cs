using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Perpetuum.Accounting.Characters;
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;

namespace Perpetuum.Items
{
    public delegate IPropertyModifierCollection PropertyModifierCollectionFactory(int definition);

    public delegate void ItemEventHandler<in T>(Item unit, T args);

    public class Item : Entity
    {
        private readonly List<ItemProperty> properties = new List<ItemProperty>();
        private ImmutableHashSet<ItemProperty> changedProperties = ImmutableHashSet<ItemProperty>.Empty;

        public IPropertyModifierCollection BasePropertyModifiers { get; set; }

        public IEnumerable<ItemProperty> Properties => properties;

        public void AddProperty(ItemProperty property)
        {
            properties.Add(property);
            property.PropertyChanged += OnPropertyChanged;
        }

        public virtual void UpdateRelatedProperties(AggregateField field)
        {
            foreach (var property in properties)
            {
                property.UpdateIfRelated(field);
            }
        }

        public virtual void UpdateAllProperties()
        {
            foreach (var property in properties)
            {
                property.Update();
            }
        }

        public event ItemEventHandler<ItemProperty> PropertyChanged;

        protected virtual void OnPropertyChanged(ItemProperty property)
        {
            ImmutableInterlocked.Update(ref changedProperties,c => c.Add(property));
            PropertyChanged?.Invoke(this, property);
        }

        protected ImmutableHashSet<ItemProperty> GetChangedProperties()
        {
            return Interlocked.CompareExchange(
                ref changedProperties,
                ImmutableHashSet<ItemProperty>.Empty,changedProperties);
        }

        public void AddPropertiesToDictionary(IDictionary<string, object> dictionary)
        {
            var d = BuildPropertiesDictionary();

            if (d.Count > 0)
            {
                dictionary[k.accumulator] = d;
            }
        }

        public virtual Dictionary<string, object> BuildPropertiesDictionary()
        {
            var d = new Dictionary<string, object>();

            foreach (var property in BasePropertyModifiers.All)
            {
                property.AddToDictionary(d);
            }

            foreach (var property in Properties)
            {
                property.AddToDictionary(d);
            }

            return d;
        }

        public ItemPropertyModifier GetBasePropertyModifier(AggregateField field)
        {
            var modifier = BasePropertyModifiers.GetPropertyModifier(field);

            return modifier;
        }

        public virtual ItemPropertyModifier GetPropertyModifier(AggregateField field)
        {
            return GetBasePropertyModifier(field);
        }

        protected internal virtual double ComputeHeight()
        {
            return ED.Options.Height;
        }

        public virtual bool IsTrashable
        {
            get { return !ED.AttributeFlags.NonRelocatable; }
        }

        public virtual bool IsStackable
        {
            get
            {
                if (ED.AttributeFlags.AlwaysStackable)
                {
                    return true;
                }

                if (ED.AttributeFlags.NonStackable)
                {
                    return false;
                }

                return !IsDamaged;
            }
        }

        public bool IsDamaged
        {
            get { return Health < ED.Health; }
        }

        public bool IsSingleAndUnpacked
        {
            get { return Quantity == 1 && !IsRepackaged; }
        }

        public ItemInfo ItemInfo
        {
            get { return new ItemInfo(Definition, Quantity); }
        }

        public virtual void Initialize()
        {
            foreach (var child in Children.OfType<Item>())
            {
                child.Initialize();
            }

            UpdateAllProperties();
        }

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }

        public void CheckOwnerCharacterAndCorporationAndThrowIfFailed(Character character)
        {
            if (Owner == 0)
            {
                return;
            }

            if (Owner != character.CorporationEid)
            {
                CheckOwnerOnlyCharacterAndThrowIfFailed(character);
            }
        }

        public void CheckOwnerOnlyCharacterAndThrowIfFailed(Character character)
        {
            if (Owner == 0)
            {
                return;
            }

            Owner.ThrowIfNotEqual(character.Eid, ErrorCodes.OwnerMismatch);
        }

        public override Dictionary<string, object> ToDictionary()
        {
            var dictionary = base.ToDictionary();
            AddPropertiesToDictionary(dictionary);

            return dictionary;
        }

        public ErrorCodes CanStackTo(Item target)
        {
            if (target.Eid == Eid)
            {
                return ErrorCodes.WTFErrorMedicalAttentionSuggested;
            }

            //they must be the same type
            if (target.ED != ED)
            {
                return ErrorCodes.ItemTypeMismatch;
            }

            //non stackable flag check
            if (target.ED.AttributeFlags.NonStackable)
            {
                return ErrorCodes.ItemNotStackable;
            }

            if (Math.Abs(target.Health - Health) > double.Epsilon)
            {
                return ErrorCodes.ItemHealthMismatch;
            }

            //if stackable is it repackaged?
            if (!target.ED.AttributeFlags.AlwaysStackable &&
                (!target.IsRepackaged || !IsRepackaged))
            {
                return ErrorCodes.ItemHasToBeRepackaged;
            }

            var sumQty = (decimal)target.Quantity + Quantity;
            if (sumQty > int.MaxValue)
            {
                return ErrorCodes.MaximumStackSizeExceeded;
            }

            return ErrorCodes.NoError;
        }

        public void StackToOrThrow(Item target)
        {
            CanStackTo(target).ThrowIfError();
            StackTo(target);
        }

        public void StackTo(Item target)
        {
            Repository.Delete(this);
            target.Quantity += Quantity;
            target.Name = null;
        }

        public IEnumerable<Item> UnstackAll()
        {
            while (Quantity > 1)
            {
                yield return Unstack(1);
            }

            yield return this;
        }

        public Item Unstack(int amount)
        {
            if (Quantity <= amount)
            {
                return this;
            }

            amount.ThrowIfLess(1, ErrorCodes.AccessDenied);

            var result = (Item)Factory.CreateWithRandomEID(ED);
            result.Owner = Owner;
            result.Quantity = amount;
            result.IsRepackaged = IsRepackaged;

            Quantity -= amount;

            if (Quantity <= 0)
            {
                Repository.Delete(this);
            }

            return result;
        }

        [NotNull]
        public static Item GetOrThrow(long itemEid)
        {
            return (Item)Repository
                .LoadTree(itemEid, null)
                .ThrowIfNull(ErrorCodes.ItemNotFound);
        }

        public static Item CreateWithRandomEid(ItemInfo itemInfo)
        {
            var item = (Item)Factory.CreateWithRandomEID(itemInfo.Definition);
            item.Quantity = itemInfo.Quantity;
            item.Health = itemInfo.Health;
            item.IsRepackaged = itemInfo.IsRepackaged;

            return item;
        }
    }
}
