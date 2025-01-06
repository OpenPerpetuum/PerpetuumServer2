using Perpetuum.Data;
using Perpetuum.Zones;
using System.Data;

namespace Perpetuum.Services.Relics
{
    public class RelicInfo
    {
        public static RelicInfo CreateRelicInfoFromRecord(IDataRecord record)
        {
            int id = record.GetValue<int>("id");
            string name = record.GetValue<string>("name");
            int? raceid = record.GetValue<int?>("raceid");
            int? level = record.GetValue<int?>("level");
            int? ep = record.GetValue<int?>("ep");
            RelicInfo info = new(id, name, raceid, level, ep);

            return info;
        }

        public static RelicInfo GetByIDFromDB(int id)
        {
            IEnumerable<RelicInfo> relicinfos = Db.Query().CommandText("SELECT TOP 1 id, name, raceid, level, ep FROM relictypes WHERE id = @relicInfoId")
                .SetParameter("@relicInfoId", id)
                .Execute()
                .Select(CreateRelicInfoFromRecord);

            return relicinfos.SingleOrDefault();
        }

        public static RelicInfo GetByNameFromDB(string name)
        {
            IEnumerable<RelicInfo> relicinfos = Db.Query().CommandText("SELECT TOP 1 id, name, raceid, level, ep FROM relictypes WHERE name = @name")
                .SetParameter("@name", name)
                .Execute()
                .Select(CreateRelicInfoFromRecord);

            return relicinfos.SingleOrDefault();
        }

        private readonly int _id;
        private readonly string _name;
        private readonly int? _raceid;
        private readonly int? _level;
        private readonly int? _ep;
        private Position _staticRelicPosistion;
        public bool HasStaticPosistion = false;

        public RelicInfo(int id, string name, int? raceid, int? level, int? ep)
        {
            _id = id;
            _name = name;
            _ep = ep;
            _raceid = raceid;
            _level = level;
        }

        public int GetLevel()
        {
            return _level ?? 0;
        }

        public int GetFaction()
        {
            return _raceid ?? 0;
        }

        public void SetPosition(Position p)
        {
            HasStaticPosistion = true;
            _staticRelicPosistion = p;
        }

        public Position GetPosition()
        {
            return _staticRelicPosistion;
        }

        public int GetEP()
        {
            return _ep ?? 5;
        }

        public int GetID()
        {
            return _id;
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> dictionary = new()
            {
                {k.name, _name },
                {k.raceID, GetFaction()},
                {k.level, GetLevel()},
                {k.extensionPoints, GetEP()},
                {"isStatic", HasStaticPosistion},
            };

            return dictionary;
        }

    }
}
