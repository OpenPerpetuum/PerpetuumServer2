using Perpetuum.Accounting.Characters;
using Perpetuum.Log;
using Perpetuum.Services.MissionEngine.MissionProcessorObjects;
using Perpetuum.Services.MissionEngine.Missions;
using Perpetuum.Services.MissionEngine.MissionStructures;
using Perpetuum.Zones;

namespace Perpetuum.Services.MissionEngine
{
    internal class OneLocationTest
    {
        private readonly MissionProcessor _missionProcessor;
        private readonly List<Position> _terminalsOnZones;
        public OneLocationTest(MissionProcessor missionProcessor, List<Position> terminalsOnZones)
        {
            _missionProcessor = missionProcessor;
            _terminalsOnZones = terminalsOnZones;
        }

        public void TestOne(MissionLocation location, Mission mission, Character testCharacter, int missionLevel, int maxAttempts = 100, bool writeResult = true)
        {
            double rewardCollector = 0.0;

            bool wasException = false;
            List<long> structureHashList = new();
            //Logger.Info(" location " + location.id);

            int successCount = 0;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                MissionInProgress? missionInProgress = null;
                bool success = false;
                try
                {
                    success =
                        _missionProcessor.MissionAdministrator.TryCreateMission(testCharacter, false, mission, location, missionLevel, out missionInProgress, true);
                }
                catch (Exception ex)
                {
                    //this mission has some content/config problem
                    Logger.Error("exception occured in mission resolve: " + mission);
                    Logger.Exception(ex);
                    wasException = true;
                    break;
                }

                if (success)
                {
                    long sHash = missionInProgress.GenerateStructureHash();
                    if (sHash > 0)
                    {
                        structureHashList.Add(sHash);
                    }

                    successCount++;

                    missionInProgress.GenerateSuccessInfoForTest(_terminalsOnZones);

                    missionInProgress.GetFinalReward(out double rewardSum, out double distanceReward, out double difficultyReward, out double rewardByTargets, out double riskCompensation, out double zoneFactor);
                    rewardCollector += rewardSum;

                    if (writeResult)
                    {
                        missionInProgress.WriteSuccessLogAllTargets();
                    }

                }


            }

            if (!wasException)
            {
                int rewardAverage = 0;
                if (successCount > 0)
                {
                    rewardAverage = (int)(rewardCollector / successCount);
                }
                else
                {
                    Logger.Error("100% failure: " + location + " " + mission);
                }

                int uniqueHash = structureHashList.Distinct().Count();
                Logger.Info("success:" + successCount + " unique:" + uniqueHash);

                Logger.Info("paid reward " + rewardAverage);
                if (writeResult)
                {
                    //make it blocking, on purpose
                    MissionResolveInfo.InsertToDb(mission, location, maxAttempts, successCount, uniqueHash, rewardAverage);
                }

            }
            else
            {
                Logger.Error("--------------------------");
                Logger.Error("--------------------------");
                Logger.Error("exception:" + location + " " + mission);
                Logger.Error("--------------------------");
                Logger.Error("--------------------------");


            }

        }

    }
}