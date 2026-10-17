using Perpetuum.ExportedTypes;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Sessions;
using System;
using System.Collections.Generic;

namespace Perpetuum.Items
{
    public class ServerWideEpBooster : Item
    {
        private ItemProperty epBonus = ItemProperty.None;
        private ItemProperty epBonusDuration = ItemProperty.None;

        public override void Initialize()
        {
            epBonus = new InfoProperty<ServerWideEpBooster>(this, AggregateField.server_wide_ep_bonus);
            AddProperty(epBonus);

            epBonusDuration = new InfoProperty<ServerWideEpBooster>(this, AggregateField.server_wide_ep_bonus_duration);
            AddProperty(epBonusDuration);

            base.Initialize();
        }

        public void Activate(ISession session)
        {
            Request epBoost = new Request
            {
                Command = Commands.EPBonusSet,
                Session = session,
                Data = new Dictionary<string, object>
                    {
                        { k.bonus, Convert.ToInt32(epBonus.Value) },
                        { k.duration, Convert.ToInt32(epBonusDuration.Value) },
                    },
            };

            session.HandleLocalRequest(epBoost);

        }
    }
}
