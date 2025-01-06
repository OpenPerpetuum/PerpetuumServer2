using Perpetuum.EntityFramework;
using Perpetuum.Log;
using Perpetuum.Services.Looting;
using Perpetuum.Units;
using Perpetuum.Zones.Finders.PositionFinders;
using Perpetuum.Zones.NpcSystem.AI.Behaviors;
using Perpetuum.Zones.NpcSystem.Presences;
using Perpetuum.Zones.NpcSystem.Presences.InterzonePresences;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Perpetuum.Zones.NpcSystem.Flocks
{
    public delegate Flock FlockFactory(IFlockConfiguration flockConfiguration, Presence presence);

    public class Flock
    {
        private ImmutableList<Npc> members = ImmutableList<Npc>.Empty;

        public ILootService LootService { get; set; }

        public IEntityServices EntityService { get; set; }

        public IFlockConfiguration Configuration { get; }

        public Presence Presence { get; }

        public NpcBossInfo BossInfo { get { return Configuration.BossInfo; } }

        public bool IsBoss { get { return BossInfo != null; } }

        public int Id => Configuration.ID;

        public int HomeRange => Configuration.HomeRange;

        public Position SpawnOrigin { get; }

        public IReadOnlyCollection<Npc> Members => members;

        public int MembersCount => members.Count;

        public event Action<Flock> AllMembersDead;

        public event Action<Npc> NpcCreated;

        public Flock(IFlockConfiguration configuration, Presence presence)
        {
            Configuration = configuration;
            Presence = presence;
            SpawnOrigin = SpawnOriginSelector(presence);
        }

        public virtual IDictionary<string, object> ToDictionary()
        {
            var dictionary = new Dictionary<string, object>
            {
                {k.ID, Configuration.ID},
                {k.presenceID, Presence.Configuration.ID},
                {k.definition, Configuration.EntityDefault.Definition},
                {k.name, Configuration.Name},
                {k.spawnRangeMin, Configuration.SpawnRange.Min},
                {k.spawnRangeMax, Configuration.SpawnRange.Max},
                {k.flockMemberCount, Configuration.FlockMemberCount},
                {k.respawnSeconds, Configuration.RespawnTime.Seconds},
                {k.homeRange, HomeRange},
                {k.totalSpawnCount, Configuration.TotalSpawnCount},
                {k.spawnOriginX,SpawnOrigin.intX},
                {k.spawnOriginY,SpawnOrigin.intY}
            };

            return dictionary;
        }

        public void SpawnAllMembers()
        {
            var totalToSpawn = Configuration.FlockMemberCount - MembersCount;

            for (var i = 0; i < totalToSpawn; i++)
            {
                CreateMemberInZone();
            }

            Log($"{Configuration.FlockMemberCount} NPCs created");
        }

        public virtual void Update(TimeSpan time)
        {
        }

        public override string ToString()
        {
            return $"{Configuration.Name}:{Configuration.ID}";
        }

        public void RemoveAllMembersFromZone(bool withTeleportExit = false)
        {
            foreach (var npc in Members)
            {
                if (withTeleportExit)
                {
                    npc.States.Teleport = true;
                }

                npc.RemoveFromZone();
                RemoveMember(npc);
            }
        }

        protected virtual void CreateMemberInZone()
        {
            var npc = (Npc)EntityService.Factory.Create(Configuration.EntityDefault, EntityIDGenerator.Random);
            var zone = Presence.Zone;
            var spawnPosition = GetSpawnPosition(SpawnOrigin);
            var finder = new ClosestWalkablePositionFinder(zone, spawnPosition, npc);

            if (!finder.Find(out spawnPosition))
            {
                Log($"invalid spawnposition in CreateMemberInZone: {spawnPosition} {Configuration.Name} {Presence.Configuration.Name} zone:{zone.Id}");
            }

            npc.Behavior = GetBehavior();
            npc.SpecialType = Configuration.SpecialType;
            npc.BossInfo = BossInfo;

            var gen = new CompositeLootGenerator(
                new LootGenerator(LootService.GetNpcLootInfos(npc.Definition)),
                new LootGenerator(LootService.GetFlockLootInfos(Id))
            );

            npc.LootGenerator = gen;
            npc.HomeRange = HomeRange;
            npc.HomePosition = GetHomePosition(spawnPosition);
            npc.CallForHelp = Configuration.IsCallForHelp;

            OnNpcCreated(npc);

            npc.AddToZone(zone, spawnPosition, ZoneEnterType.NpcSpawn);

            AddMember(npc);
            Log($"member spawned to zone:{zone.Id} EID:{npc.Eid}");
        }

        protected virtual void OnMemberDead(Unit killer, Unit npc)
        {
            RemoveMember((Npc)npc);

            if (members.Count <= 0)
            {
                OnAllMembersDead();
            }
        }

        protected virtual Position GetSpawnPosition(Position spawnOrigin)
        {
            var spawnRangeMin = Configuration.SpawnRange.Min;
            var spawnRangeMax = Configuration.SpawnRange.Max.Min(HomeRange);
            var spawnPosition = spawnOrigin.GetRandomPositionInRange2D(spawnRangeMin, spawnRangeMax).Clamp(Presence.Zone.Size);

            return spawnPosition;
        }

        protected virtual Position GetHomePosition(Position spawnOrigin)
        {
            return spawnOrigin;
        }

        protected virtual void OnNpcCreated(Npc npc)
        {
            NpcCreated?.Invoke(npc);
        }

        protected void Log(string message)
        {
            Logger.Info($"[Flock] ({ToString()}) - {message}");
        }

        private void AddMember(Npc npc)
        {
            ImmutableInterlocked.Update(ref members, m => m.Add(npc));
            npc.Dead += OnMemberDead;
        }

        private void RemoveMember(Npc npc)
        {
            ImmutableInterlocked.Update(ref members, m => m.Remove(npc));
        }

        private void OnAllMembersDead()
        {
            AllMembersDead?.Invoke(this);
        }

        private Behavior GetBehavior()
        {

            if (Presence is InterzonePresence)
            {
                return Behavior.Create(Configuration.BehaviorType);
            }
            else if (Configuration.BehaviorType == BehaviorType.Aggressive && Presence is DynamicPresenceExtended)
            {
                return Behavior.Create(Configuration.BehaviorType);
            }
            else if (Configuration.BehaviorType == BehaviorType.Aggressive && Presence is DynamicPresence)
            {
                return Behavior.Create(BehaviorType.Neutral);
            }

            return Behavior.Create(Configuration.BehaviorType);
        }

        private Position SpawnOriginSelector(Presence presence)
        {
            switch (presence)
            {
                case InterzonePresence interzonePresense:
                    {
                        return Configuration.SpawnOrigin;
                    }
                case DynamicPresence dynamicPresence:
                    {
                        return dynamicPresence.DynamicPosition;
                    }
                case RandomPresence randomPresense:
                    {
                        return randomPresense.SpawnOriginForRandomPresence;
                    }
                case IRoamingPresence roaming:
                    {
                        return roaming.SpawnOrigin;
                    }
            }

            return Configuration.SpawnOrigin;
        }
    }
}