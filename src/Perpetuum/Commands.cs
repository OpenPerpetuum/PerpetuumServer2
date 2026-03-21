using System.Reflection;
using SkiaSharp;

namespace Perpetuum
{
    public static class Commands
    {
        private static readonly Dictionary<string, Command> _commands;

        static Commands()
        {
            _commands = typeof(Commands).GetFields(BindingFlags.Static | BindingFlags.Public)
                .Select(info => (Command)info.GetValue(null))
                .ToDictionary(cmd => cmd.Text);
        }




        public static Command GetCommandByText(string commandText)
        {
            return _commands.GetOrDefault(commandText);
        }

        public static readonly Command Welcome = new()
        {
            Text = "welcome"
        };

        public static readonly Command MarketCleanUp = new()
        {
            Text = "marketCleanUp",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneDrawAllDecors = new()
        {
            Text = "zoneDrawAllDecors",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ReloadStandingForCharacter = new()
        {
            Text = "reloadStandingForCharacter",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command ZoneDrawDecorEnvByDef = new()
        {
            Text = "zoneDrawDecorEnvByDef",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command ZoneMakeGotoXY = new()
        {
            Text = "zoneMakeGotoXY",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command ZoneUpdateStructure = new()
        {
            Text = "zoneUpdateStructure",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        //[22:30:34] ERR [UREQ] NoSuchCommand Data = {command=zoneDrawRamp} ip: 127.0.0.1 account: 9 character: 4 Req: zoneDrawRamp:zone_39:#max=n0#size=n60#range=f0.494141#positionx=n1411#positiony=n916#blend=f0.500000
        public static readonly Command ZoneDrawRamp = new()
        {
            Text = "zoneDrawRamp",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.max),
                new Argument<int>(k.size),
                new Argument<double>(k.range),
                new Argument<int>("positionx"),
                new Argument<int>("positiony"),
                new Argument<double>("blend")
            }
        };

        public static readonly Command ZoneSmooth = new()
        {
            Text = "zoneSmooth",
            AccessLevel = AccessLevel.admin,
        };

        public static readonly Command ZoneDisplayMissionSpots = new()
        {
            Text = "zoneDisplayMissionSpots",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZonePBSFixOrphaned = new()
        {
            Text = "zonePBSFixOrphaned",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneDisplayMissionRandomPoints = new()
        {
            Text = "zoneDisplayMissionRandomPoints",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionStartedFromFieldTerminal = new()
        {
            Text = "missionStartedFromFieldTerminal",
        };

        public static readonly Command MissionResolveTest = new()
        {
            Text = "missionResolveTest",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionSpotPlace = new()
        {
            Text = "missionSpotPlace",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionSpotUpdate = new()
        {
            Text = "missionSpotUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneKillNPlants = new()
        {
            Text = "zoneKillNPlants",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command BaseSelect = new()
        {
            Text = "baseSelect",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneSwitchDegrade = new()
        {
            Text = "zoneSwitchDegrade",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneRestoreOriginalGamma = new()
        {
            Text = "zoneRestoreOriginalGamma",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ExtensionTest = new()
        {
            Text = "extensionTest",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneSetReinforceCounter = new()
        {
            Text = "zoneSetReinforceCounter",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ExtensionPointsIncreased = new()
        {
            Text = "extensionPointsIncreased",
        };

        public static readonly Command ZoneForceDeconstruct = new()
        {
            Text = "zoneForceDeconstruct",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneFixPBS = new()
        {
            Text = "zoneFixPBS",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneTerraformTest = new()
        {
            Text = "zoneTerraformTest",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionBonusUpdate = new()
        {
            Text = "missionBonusUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionAdminTake = new()
        {
            Text = "missionAdminTake",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.missionID)
            }
        };

        public static readonly Command MissionAdminListAll = new()
        {
            Text = "missionAdminListAll",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketRemoveItems = new()
        {
            Text = "marketRemoveItems",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneResetMissions = new()
        {
            Text = "zoneResetMissions",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketCreateGammaPlasmaOrders = new()
        {
            Text = "marketCreateGammaPlasmaOrders",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CorporationBulletinUpdate = new()
        {
            Text = "corporationBulletinUpdate",
        };

        public static readonly Command SparkTeleportBaseDeleted = new()
        {
            Text = "sparkTeleportBaseDeleted",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentRetrieved = new()
        {
            Text = "transportAssignmentRetrieved",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentExpired = new()
        {
            Text = "transportAssignmentExpired",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentAccepted = new()
        {
            Text = "transportAssignmentAccepted",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentBaseDeleted = new()
        {
            Text = "transportAssignmentBaseDeleted",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentFailed = new()
        {
            Text = "transportAssignmentFailed",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentDelivered = new()
        {
            Text = "transportAssignmentDelivered",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentGaveUp = new()
        {
            Text = "transportAssignmentGaveUp",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TransportAssignmentContainerRetrieved = new()
        {
            Text = "transportAssignmentContainerRetrieved",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionLineDead = new()
        {
            Text = "productionLineDead",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command NpcCheckCondition = new()
        {
            Text = "NPCCheckCondition",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionFacilityState = new()
        {
            Text = "productionFacilityState",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CorporationInfoFlushCache = new()
        {
            Text = "corporationInfoFlushCache",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TeleportEnabled = new()
        {
            Text = "teleportEnabled",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionUpdate = new()
        {
            Text = "productionUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command PbsEvent = new()
        {
            Text = "PBSEvent",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZonePBSTest = new()
        {
            Text = "zonePBSTest",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneHealAllWalls = new()
        {
            Text = "zoneHealAllWalls",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZonePlaceWall = new()
        {
            Text = "zonePlaceWall",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneClearWalls = new()
        {
            Text = "zoneClearWalls",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TeleportTargetSet = new()
        {
            Text = "teleportTargetSet",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneSapActivityEnd = new()
        {
            Text = "zoneSapActivityEnd",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneRemoveByDefinition = new()
        {
            Text = "zoneRemoveByDefinition",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command ZoneServerMessage = new()
        {
            Text = "zoneServerMessage",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CharacterForcedToBase = new()
        {
            Text = "characterForcedToBase",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command SparkSetDefault = new()
        {
            Text = "sparkSetDefault",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneCheckRoaming = new()
        {
            Text = "zoneCheckRoaming",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProximityProbeUpdate = new()
        {
            Text = "proximityProbeUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProximityProbeCreated = new()
        {
            Text = "proximityProbeCreated",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProximityProbeDead = new()
        {
            Text = "proximityProbeDead",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProximityProbeInfo = new()
        {
            Text = "proximityProbeInfo",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionRemoteStart = new()
        {
            Text = "productionRemoteStart",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionRemoteEnd = new()
        {
            Text = "productionRemoteEnd",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionRemoteCancel = new()
        {
            Text = "productionRemoteCancel",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneSetRuntimeZoneEntityName = new()
        {
            Text = "zoneSetRuntimeZoneEntityName",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command ZoneDrawBeam = new()
        {
            Text = "zoneDrawBeam",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<double>(k.x),
                new Argument<double>(k.y),
            }
        };

        public static readonly Command MissionError = new()
        {
            Text = "missionError",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ContainerMover = new()
        {
            Text = "containerMover",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<int>(k.characterID),
                new Argument<long>(k.container)
            }
        };

        public static readonly Command ServerShutDown = new()
        {
            Text = "serverShutDown",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<DateTime>(k.date),
                new Argument<string>(k.message),
            }
        };

        public static readonly Command ServerShutDownCancel = new()
        {
            Text = "serverShutDownCancel",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ServerShutDownState = new()
        {
            Text = "serverShutDownState",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command TriggerMissionStructure = new()
        {
            Text = "triggerMissionStructure",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command JumpAnywhere = new()
        {
            Text = "jumpAnywhere",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.zoneID),
                new Argument<int>(k.x),
                new Argument<int>(k.y)
            }
        };

        public static readonly Command MovePlayer = new()
        {
            Text = "movePlayer",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID),
                new Argument<int>(k.zoneID),
                new Argument<int>(k.x),
                new Argument<int>(k.y)
            }
        };

        public static readonly Command MissionTargetUpdate = new()
        {
            Text = "missionTargetUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionTargetCompleted = new()
        {
            Text = "missionTargetCompleted",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionTargetActivated = new()
        {
            Text = "missionTargetActivated",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command StandingSetOnMyCorporation = new()
        {
            Text = "standingSetOnMyCorporation",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command AlarmOver = new()
        {
            Text = "alarmOver",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command RemoveMissionStructure = new()
        {
            Text = "removeMissionStructure",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command BaseReown = new()
        {
            Text = "baseReown",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ExtensionRevert = new()
        {
            Text = "extensionRevert",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int>(k.fee),
            }
        };

        public static readonly Command ChannelCreateForTerminals = new()
        {
            Text = "channelCreateForTerminals",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TeleportConnectColumns = new()
        {
            Text = "teleportConnectColumns",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.source),
                new Argument<long>(k.target),
            }
        };

        public static readonly Command NpcAddSafeSpawnPoint = new()
        {
            Text = "npcAddSafeSpawnPoint",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command NpcListSafeSpawnPoint = new()
        {
            Text = "npcListSafeSpawnPoint",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command NpcDeleteSafeSpawnPoint = new()
        {
            Text = "npcDeleteSafeSpawnPoint",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command NpcPlaceSafeSpawnPoint = new()
        {
            Text = "npcPlaceSafeSpawnPoint",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.x),
                new Argument<int>(k.y),
            }
        };

        public static readonly Command NpcSetSafeSpawnPoint = new()
        {
            Text = "npcSetSafeSpawnPoint",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int>(k.x),
                new Argument<int>(k.y)
            }
        };

        public static readonly Command CharacterUpdate = new()
        {
            Text = "characterUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionReset = new()
        {
            Text = "missionReset",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command SetMaxUserCount = new()
        {
            Text = "setMaxUserCount",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.amount)
            }
        };

        public static readonly Command DecorCategoryList = new()
        {
            Text = "decorCategoryList",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command NpcCheckFlocks = new()
        {
            Text = "npcCheckFlocks",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionCorporationInsuranceList = new()
        {
            Text = "productionCorporationInsuranceList",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketListFacilities = new()
        {
            Text = "marketListFacilities",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketInsertStats = new()
        {
            Text = "marketInsertStats",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.day),
                new Argument<long>(k.marketEID),
                new Argument<double>(k.price),
                new Argument<int>(k.amount)
            }
        };

        public static readonly Command MarketInsertAverageForCF = new()
        {
            Text = "marketInsertAverageForCF",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.amount),
                new Argument<int>(k.day),
                new Argument<string>(k.category)
            }
        };

        public static readonly Command ProductionGetInsurance = new()
        {
            Text = "productionGetInsurance",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionSetInsurance = new()
        {
            Text = "productionSetInsurance",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ReturnCorporateOwnedItems = new()
        {
            Text = "returnCorporateOwnedItems",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ForceFactionStandings = new()
        {
            Text = "forceFactionStandings",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<double>(k.standing)
            }
        };

        public static readonly Command ZoneTest = new()
        {
            Text = "zoneTest",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command DockAll = new()
        {
            Text = "dockAll",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CharacterSetCredit = new()
        {
            Text = "characterSetCredit",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.credit)
            }
        };

        public static readonly Command ZoneCreateTeleportColumn = new()
        {
            Text = "zoneCreateTeleportColumn",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command State = new()
        {
            Text = "state",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command RobotTemplateAdd = new()
        {
            Text = "robotTemplateAdd",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<Dictionary<string, object>>(k.description),
            }
        };

        public static readonly Command RobotTemplateUpdate = new()
        {
            Text = "robotTemplateUpdate",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<Dictionary<string, object>>(k.description),
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command RobotTemplateDelete = new()
        {
            Text = "robotTemplateDelete",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command RobotTemplateList = new()
        {
            Text = "robotTemplateList",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command RobotTemplateBuild = new()
        {
            Text = "robotTemplateBuild",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command FittingPresetList = new()
        {
            Text = "fittingPresetList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command FittingPresetSave = new()
        {
            Text = "fittingPresetSave",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID),
                new Argument<string>(k.name),
            }
        };

        public static readonly Command FittingPresetDelete = new()
        {
            Text = "fittingPresetDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command FittingPresetApply = new()
        {
            Text = "fittingPresetApply",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<long>(k.robotEID),
                new Argument<long>(k.containerEID)
            }
        };

        public static readonly Command ServerMessage = new()
        {
            Text = "serverMessage",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.message),
                new Argument<int>(k.type),
                new Argument<int>(k.recipients),
                new Argument<int>(k.translate)
            }
        };

        public static readonly Command UpdateMoodMessage = new()
        {
            Text = "update_moodMessage",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID),
                new Argument<string>(k.moodMessage),
            }
        };

        public static readonly Command DecorUpdate = new()
        {
            Text = "decorUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command DecorDelete = new()
        {
            Text = "decorDelete",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CharacterSetAvatar = new()
        {
            Text = "characterSetAvatar",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<Dictionary<string, object>>(k.avatar),
                new Argument<string>(k.rendered),
            }
        };

        public static readonly Command CharacterGetZoneInfo = new()
        {
            Text = "characterGetZoneInfo",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command ExtensionGive = new()
        {
            Text = "extensionGive",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ExtensionReset = new()
        {
            Text = "extensionReset",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command AddNews = new()
        {
            Text = "addNews",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.title),
                new Argument<string>(k.body),
                new Argument<int>(k.type),
                new Argument<int>(k.language)
            }
        };

        public static readonly Command UpdateNews = new()
        {
            Text = "updateNews",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.title),
                new Argument<string>(k.body),
                new Argument<int>(k.type),
                new Argument<int>(k.language),
                new Argument<int>(k.ID),
                new Argument<DateTime>(k.time)
            }
        };

        public static readonly Command CorporationHangarRentExpired = new()
        {
            Text = "corporationHangarRentExpired",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CorporationForceInfo = new()
        {
            Text = "corporationForceInfo",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<Dictionary<string, object>>(k.publicProfile),
            }
        };

        public static readonly Command GangAddMember = new()
        {
            Text = "gangAddMember",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command GangRemoveMember = new()
        {
            Text = "gangRemoveMember",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command GangKickMember = new()
        {
            Text = "gangKickMember",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionExpired = new()
        {
            Text = "missionExpired",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionDone = new()
        {
            Text = "missionDone",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionStartItems = new()
        {
            Text = "missionStartItems",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionReloadCache = new()
        {
            Text = "missionReloadCache",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MissionFlush = new()
        {
            Text = "missionFlush",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command RelayOpen = new()
        {
            Text = "relayOpen",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command RelayClose = new()
        {
            Text = "relayClose",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ContainerUpdate = new()
        {
            Text = "containerUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CreateItem = new()
        {
            Text = "createItem",
            AccessLevel = AccessLevel.normal
        }; //%%% na ez egy sechole, fix it!!!

        public static readonly Command CreateCorporationHangarStorage = new()
        {
            Text = "createCorporationHangarStorage",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.baseEID)
            }
        };

        public static readonly Command ForceStanding = new()
        {
            Text = "forceStanding",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.source),
                new Argument<long>(k.target),
                new Argument<double>(k.standing)
            }
        };

        public static readonly Command ZoneGetQueueInfo = new()
        {
            Text = "zoneGetQueueInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ZoneSetQueueLength = new()
        {
            Text = "zoneSetQueueLength",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ZoneCancelEnterQueue = new()
        {
            Text = "zoneCancelEnterQueue",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ZoneEntityChangeState = new()
        {
            Text = "zoneEntityChangeState",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.targetEID),
                new Argument<long>(k.cloneEID),
                new Argument<int>(k.bit),
                new Argument<int>(k.state)
            }
        };

        public static readonly Command ZoneDecorAdd = new()
        {
            Text = "zoneDecorAdd",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<int>(k.x),
                new Argument<int>(k.y),
                new Argument<int>(k.z),
                new Argument<double>(k.quaternionX),
                new Argument<double>(k.quaternionY),
                new Argument<double>(k.quaternionZ),
                new Argument<double>(k.quaternionW),
                new Argument<double>(k.scale),
                new Argument<int>(k.category)
            }
        };

        public static readonly Command ZoneDecorSet = new()
        {
            Text = "zoneDecorSet",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<int>(k.x),
                new Argument<int>(k.y),
                new Argument<int>(k.z),
                new Argument<double>(k.quaternionX),
                new Argument<double>(k.quaternionY),
                new Argument<double>(k.quaternionZ),
                new Argument<double>(k.quaternionW),
                new Argument<double>(k.scale),
                new Argument<int>(k.ID),
                new Argument<double>(k.fadeDistance),
                new Argument<int>(k.category)
            }
        };

        public static readonly Command ZoneDecorDelete = new()
        {
            Text = "zoneDecorDelete",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command ZoneDecorLock = new()
        {
            Text = "zoneDecorLock",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int>(k.locked),
            }
        };

        public static readonly Command ZoneEnvironmentDescriptionList = new()
        {
            Text = "zoneEnvironmentDescriptionList",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneDrawDecorEnvironment = new()
        {
            Text = "zoneDrawDecorEnvironment",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneSampleDecorEnvironment = new()
        {
            Text = "zoneSampleDecorEnvironment",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int>(k.range),
            }
        };

        public static readonly Command ZoneCreateIsland = new()
        {
            Text = "zoneCreateIsland",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.low)
            }
        };

        public static readonly Command ZoneCreateTerraformLimit = new()
        {
            Text = "ZoneCreateTerraformLimit",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.mode),
                new Argument<int>(k.distance)
            }
        };

        public static readonly Command ZoneSetLayerWithBitMap = new()
        {
            Text = "ZoneSetLayerWithBitMap",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.file),
                new Argument<int>(k.flags)
            }
        };

        public static readonly Command ZoneSampleEnvironment = new()
        {
            Text = "zoneSampleEnvironment",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.range),
            }
        };

        public static readonly Command ZoneSetPlantsSpeed = new()
        {
            Text = "zoneSetPlantsSpeed",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.speed)
            }
        };

        public static readonly Command ZoneSetPlantsMode = new()
        {
            Text = "zoneSetPlantsMode",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.mode)
            }
        };

        public static readonly Command ZoneGetPlantsMode = new()
        {
            Text = "zoneGetPlantsMode",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneCreateGarden = new()
        {
            Text = "zoneCreateGarden",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.x),
                new Argument<int>(k.y),
            }
        };

        public static readonly Command ZoneClearLayer = new()
        {
            Text = "zoneClearLayer",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<string>(k.layerName)
            }
        };

        public static readonly Command ZoneCopyGroundType = new()
        {
            Text = "zoneCopyGroundType",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.source),
                new Argument<int>(k.target)
            }
        };

        public static readonly Command ZoneFillGroundTypeRandom = new()
        {
            Text = "zoneFillGroundTypeRandom",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.size),
                new Argument<int>(k.numberOfRuns)
            }
        };

        public static readonly Command ZoneSetBaseDetails = new()
        {
            Text = "zoneSetBaseDetails",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command ZonePutPlant = new()
        {
            Text = "zonePutPlant",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.x),
                new Argument<int>(k.y),
                new Argument<int>(k.index),
                new Argument<int>(k.state)
            }
        };

        public static readonly Command ZoneDrawBlockingByEid = new()
        {
            Text = "zoneDrawBlockingByEid",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command ZoneDrawBlockingByDefinition = new()
        {
            Text = "zoneDrawBlockingByDefinition",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int[]>(k.definition)
            }
        };

        public static readonly Command ZoneCleanBlockingByDefinition = new()
        {
            Text = "zoneCleanBlockingByDefinition",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int[]>(k.definition)
            }
        };

        public static readonly Command ZoneDrawStatMap = new()
        {
            Text = "zoneDrawStatMap",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneCleanObstacleBlocking = new()
        {
            Text = "zoneCleanObstacleBlocking",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneMoveUnit = new()
        {
            Text = "zoneMoveUnit",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.x),
                new Argument<int>(k.y),
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command ZoneListPresences = new()
        {
            Text = "zoneListPresences",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneNpcFlockSet = new()
        {
            Text = "zoneNPCFlockSet",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneNpcFlockSetParameter = new()
        {
            Text = "zoneNPCFlockSetParameter",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneNpcFlockKill = new()
        {
            Text = "zoneNPCFlockKill",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.presenceID),
                new Argument<int>(k.flockID),
            }
        };

        public static readonly Command ZoneNpcFlockNew = new()
        {
            Text = "zoneNPCFlockNew",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneNpcFlockDelete = new()
        {
            Text = "zoneNPCFlockDelete",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.presenceID),
                new Argument<int>(k.flockID),
            }
        };

        public static readonly Command ZoneMissionNew = new()
        {
            Text = "zoneMissionNew",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneDebugLOS = new()
        {
            Text = "zoneDebugLOS",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.state)
            }
        };

        public static readonly Command ZoneGetMyArtifacts = new()
        {
            Text = "zoneGetMyArtifacts",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneGetZoneObjectDebugInfo = new()
        {
            Text = "zoneGetZoneObjectDebugInfo",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.targetEID)
            }
        };

        public static readonly Command ZoneUploadScanResult = new()
        {
            Text = "zoneUploadScanResult",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ZoneRemoveObject = new()
        {
            Text = "zoneRemoveObject",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.target)
            }
        };

        public static readonly Command MarketItemSold = new()
        {
            Text = "marketItemSold",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketItemBought = new()
        {
            Text = "marketItemBought",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketItemExpired = new()
        {
            Text = "marketItemExpired",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketSellOrderCreated = new()
        {
            Text = "marketSellOrderCreated",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketBuyOrderUpdate = new()
        {
            Text = "marketBuyOrderUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketSetState = new()
        {
            Text = "marketSetState",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketBuyOrderCreated = new()
        {
            Text = "marketBuyOrderCreated",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketSellOrderUpdate = new()
        {
            Text = "marketSellOrderUpdate",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MarketFlush = new()
        {
            Text = "marketFlush",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.market)
            }
        };

        public static readonly Command MarketAddCategory = new()
        {
            Text = "marketAddCategory",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.isSell),
                new Argument<int>(k.quantity),
                new Argument<int>(k.duration),
                new Argument<int>(k.price)
            }
        };

        public static readonly Command MarketGetState = new()
        {
            Text = "marketGetState",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command RobotActivated = new()
        {
            Text = "robotActivated",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionRemoveFacility = new()
        {
            Text = "productionRemoveFacility",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command ProductionSpawnComponents = new()
        {
            Text = "productionSpawnComponents",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command ProductionScaleComponentsAmount = new()
        {
            Text = "productionScaleComponentsAmount",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.targets),
                new Argument<long>(k.materials),
                new Argument<double>(k.ratio)
            }
        };

        public static readonly Command ProductionUnrepairItem = new()
        {
            Text = "productionUnrepairItem",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.target)
            }
        };

        public static readonly Command ProductionFinished = new()
        {
            Text = "productionFinished",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionFacilityOnOff = new()
        {
            Text = "productionFacilityOnOff",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<int>(k.state),
            }
        };

        public static readonly Command ProductionForceEnd = new()
        {
            Text = "productionForceEnd",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ProductionSpawnCPRG = new()
        {
            Text = "productionSpawnCPRG",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };


        public static readonly Command MissionListAgents = new()
        {
            Text = "missionListAgents",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command EpForActivityDailyLog = new()
        {
            Text = "epForActivityDailyLog",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MissionPlayerAddsParticipant = new()
        {
            Text = "missionPlayerAddsParticipant",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int[]>(k.ID),
                new Argument<string>(k.guid),
            }
        };

        public static readonly Command ItemCountOnZone = new()
        {
            Text = "itemCountOnZone",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MissionStartFromZone = new()
        {
            Text = "missionStartFromZone",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.missionCategory),
                new Argument<int>(k.missionLevel),
            }
        };

        public static readonly Command FieldTerminalInfo = new()
        {
            Text = "fieldTerminalInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command SteamGetProducts = new()
        {
            Text = "steamGetProducts",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SteamStartTransaction = new()
        {
            Text = "steamStartTransaction",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SteamFinishTransaction = new()
        {
            Text = "steamFinishTransaction",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command UseItem = new()
        {
            Text = "useItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command GateSetName = new()
        {
            Text = "gateSetName",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command ExtensionBuyEpBoost = new()
        {
            Text = "extensionBuyEpBoost",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MtProductPriceList = new()
        {
            Text = "mtProductPriceList",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command RedeemableItemActivate = new()
        {
            Text = "redeemableItemActivate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command RedeemableItemList = new()
        {
            Text = "redeemableItemList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command RedeemableItemRedeem = new()
        {
            Text = "redeemableItemRedeem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command TransportAssignmentGiveUp = new()
        {
            Text = "transportAssignmentGiveUp",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command TransportAssignmentListContent = new()
        {
            Text = "transportAssignmentListContent",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command TransportAssignmentRetrieve = new()
        {
            Text = "transportAssignmentRetrieve",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command TransportAssignmentRunning = new()
        {
            Text = "transportAssignmentRunning",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TransportAssignmentContainerInfo = new()
        {
            Text = "transportAssignmentContainerInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command TransportAssignmentLog = new()
        {
            Text = "transportAssignmentLog",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command TransportAssignmentTake = new()
        {
            Text = "transportAssignmentTake",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command TransportAssignmentCancel = new()
        {
            Text = "transportAssignmentCancel",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command TransportAssignmentSubmit = new()
        {
            Text = "transportAssignmentSubmit",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.sourceBase),
                new Argument<long>(k.targetBase),
                new Argument<long>(k.eid),
                new Argument<int>(k.duration),
                new Argument<long>(k.reward),
                new Argument<long>(k.collateral)
            }
        };

        public static readonly Command TransportAssignmentList = new()
        {
            Text = "transportAssignmentList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TransportAssignmentDeliver = new()
        {
            Text = "transportAssignmentDeliver",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command SparkTeleportList = new()
        {
            Text = "sparkTeleportList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SparkTeleportDelete = new()
        {
            Text = "sparkTeleportDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command SparkTeleportSet = new()
        {
            Text = "sparkTeleportSet",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SparkTeleportUse = new()
        {
            Text = "sparkTeleportUse",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command GoodiePackList = new()
        {
            Text = "goodiePackList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GoodiePackRedeem = new()
        {
            Text = "goodiePackRedeem",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionQueryLineNextRound = new()
        {
            Text = "productionQueryLineNextRound",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionMergeResearchKitsMulti = new()
        {
            Text = "productionMergeResearchKitsMulti",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<long>(k.facility),
                new Argument<int>(k.amount)
            }
        };

        public static readonly Command ProductionMergeResearchKitsMultiQuery = new()
        {
            Text = "productionMergeResearchKitsMultiQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<long>(k.facility),
                new Argument<int>(k.amount)
            }
        };

        public static readonly Command CorporationSetColor = new()
        {
            Text = "corporationSetColor",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.color)
            }
        };

        public static readonly Command ProductionCPRGForgeQuery = new()
        {
            Text = "productionCPRGForgeQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<long>(k.source),
                new Argument<long>(k.target)
            }
        };

        public static readonly Command ProductionCPRGForge = new()
        {
            Text = "productionCPRGForge",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<long>(k.source),
                new Argument<long>(k.target)
            }
        };

        public static readonly Command CorporationDocumentRent = new()
        {
            Text = "corporationDocumentRent",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command CorporationDocumentTransfer = new()
        {
            Text = "corporationDocumentTransfer",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int>(k.target),
            }
        };

        public static readonly Command CorporationDocumentConfig = new()
        {
            Text = "corporationDocumentConfig",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationDocumentList = new()
        {
            Text = "corporationDocumentList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationDocumentUpdateBody = new()
        {
            Text = "corporationDocumentUpdateBody",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.body),
                new Argument<int>(k.ID),
                new Argument<int>(k.version)
            }
        };

        public static readonly Command CorporationDocumentCreate = new()
        {
            Text = "corporationDocumentCreate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.type)
            }
        };

        public static readonly Command CorporationDocumentOpen = new()
        {
            Text = "corporationDocumentOpen",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int[]>(k.ID)
            }
        };

        public static readonly Command CorporationDocumentDelete = new()
        {
            Text = "corporationDocumentDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command CorporationDocumentUnmonitor = new()
        {
            Text = "corporationDocumentUnmonitor",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command CorporationDocumentMonitor = new()
        {
            Text = "corporationDocumentMonitor",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command CorporationDocumentRegisterList = new()
        {
            Text = "corporationDocumentRegisterList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command CorporationDocumentRegisterSet = new()
        {
            Text = "corporationDocumentRegisterSet",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int[]>(k.members),
                new Argument<int[]>(k.writeAccess)
            }
        };

        public static readonly Command PBSSetBaseDeconstruct = new()
        {
            Text = "PBSSetBaseDeconstruct",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.state),
            }
        };

        public static readonly Command PBSGetTerritories = new()
        {
            Text = "PBSGetTerritories",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command PBSSetTerritoryVisibility = new()
        {
            Text = "PBSSetTerritoryVisibility",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command PBSNodeInfo = new()
        {
            Text = "PBSNodeInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command PBSSetStandingLimit = new()
        {
            Text = "PBSSetStandingLimit",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command PBSCheckDeployment = new()
        {
            Text = "PBSCheckDeployment",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<int>(k.x),
                new Argument<int>(k.y)
            }
        };

        public static readonly Command PBSGetNetwork = new()
        {
            Text = "PBSGetNetwork",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command PBSSetOnline = new()
        {
            Text = "PBSSetOnline",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.state),
            }
        };

        public static readonly Command PBSRenameNode = new()
        {
            Text = "PBSRenameNode",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<string>(k.name),
            }
        };

        public static readonly Command PBSSetConnectionWeight = new()
        {
            Text = "PBSSetConnectionWeight",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.source),
                new Argument<long>(k.target),
                new Argument<double>(k.weight)
            }
        };

        public static readonly Command PBSBreakConnection = new()
        {
            Text = "PBSBreakConnection",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.source),
                new Argument<long>(k.target),
            }
        };

        public static readonly Command PBSMakeConnection = new()
        {
            Text = "PBSMakeConnection",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.source),
                new Argument<long>(k.target),
            }
        };

        public static readonly Command PBSFeedableInfo = new()
        {
            Text = "PBSFeedableInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command PBSFeedItems = new()
        {
            Text = "PBSFeedItems",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.target),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command PBSGetLog = new()
        {
            Text = "PBSGetLog",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command PBSSetReinforceOffset = new()
        {
            Text = "PBSSetReinforceOffset",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.offset),
            }
        };

        public static readonly Command PBSSetEffect = new()
        {
            Text = "PBSSetEffect",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.effect),
            }
        };

        public static readonly Command PBSGetReimburseInfo = new()
        {
            Text = "PBSGetReimburseInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command PBSSetReimburseInfo = new()
        {
            Text = "PBSSetReimburseInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationCeoTakeOverStatus = new()
        {
            Text = "corporationCEOTakeOverStatus",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationVolunteerForCeo = new()
        {
            Text = "corporationVolunteerForCEO",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TeleportQueryWorldChannels = new()
        {
            Text = "teleportQueryWorldChannels",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command IntrusionSetDefenseThreshold = new()
        {
            Text = "intrusionSetDefenseThreshold",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.siteEID)
            }
        };

        public static readonly Command GiftOpen = new()
        {
            Text = "giftOpen",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command IntrusionSAPSubmitItem = new()
        {
            Text = "intrusionSAPSubmitItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.target),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command IntrusionSAPGetItemInfo = new()
        {
            Text = "intrusionSAPGetItemInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.target)
            }
        };

        public static readonly Command GetIntrusionMySitesLog = new()
        {
            Text = "getIntrusionMySitesLog",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetIntrusionPublicLog = new()
        {
            Text = "getIntrusionPublicLog",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command IntrusionUpgradeFacility = new()
        {
            Text = "intrusionUpgradeFacility",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility)
            }
        };

        public static readonly Command SetIntrusionSiteMessage = new()
        {
            Text = "setIntrusionSiteMessage",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.message),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command GetIntrusionStabilityLog = new()
        {
            Text = "getIntrusionStabilityLog",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.day),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command GetIntrusionLog = new()
        {
            Text = "getIntrusionLog",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.offset),
            }
        };

        public static readonly Command BaseSetDockingRights = new()
        {
            Text = "baseSetDockingRights",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.baseEID)
            }
        };

        public static readonly Command BaseGetOwnershipInfo = new()
        {
            Text = "baseGetOwnershipInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SparkRemove = new()
        {
            Text = "sparkRemove",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SparkList = new()
        {
            Text = "sparkList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SparkChange = new()
        {
            Text = "sparkChange",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.sparkID)
            }
        };

        public static readonly Command SparkUnlock = new()
        {
            Text = "sparkUnlock",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.sparkID)
            }
        };

        public static readonly Command ProximityProbeRemove = new()
        {
            Text = "proximityProbeRemove",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command ProximityProbeRegisterSet = new()
        {
            Text = "proximityProbeRegisterSet",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int[]>(k.members),
            }
        };

        public static readonly Command ProximityProbeList = new()
        {
            Text = "proximityProbeList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProximityProbeSetName = new()
        {
            Text = "proximityProbeSetName",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<string>(k.name),
            }
        };

        public static readonly Command ProximityProbeGetRegistrationInfo = new()
        {
            Text = "proximityProbeGetRegistrationInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command ExtensionRemoveLevel = new()
        {
            Text = "extensionRemoveLevel",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.extensionID)
            }
        };

        public static readonly Command GetDefinitionConfigUnits = new()
        {
            Text = "getDefinitionConfigUnits",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ItemShopList = new()
        {
            Text = "itemShopList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command ItemShopBuy = new()
        {
            Text = "itemShopBuy",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.ID),
            }
        };

        public static readonly Command ProductionInProgressCorporation = new()
        {
            Text = "productionInProgressCorporation",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MissionGetSupply = new()
        {
            Text = "missionGetSupply",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command GetDistances = new()
        {
            Text = "getDistances",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command YellowPagesSearch = new()
        {
            Text = "yellowPagesSearch",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command YellowPagesSubmit = new()
        {
            Text = "yellowPagesSubmit",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command YellowPagesGet = new()
        {
            Text = "yellowPagesGet",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command YellowPagesDelete = new()
        {
            Text = "yellowPagesDelete",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command AlarmStart = new()
        {
            Text = "alarmStart",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command KioskInfo = new()
        {
            Text = "kioskInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command KioskSubmitItem = new()
        {
            Text = "kioskSubmitItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<long>(k.target),
            }
        };

        public static readonly Command ItemCount = new()
        {
            Text = "itemCount",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SystemInfo = new()
        {
            Text = "systemInfo",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command GetItemSummary = new()
        {
            Text = "getItemSummary",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command ProductionFacilityDescription = new()
        {
            Text = "productionFacilityDescription",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionInsuranceList = new()
        {
            Text = "productionInsuranceList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionInsuranceQuery = new()
        {
            Text = "productionInsuranceQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<long[]>(k.target),
                new Argument<long>(k.container)
            }
        };

        public static readonly Command ProductionInsuranceDelete = new()
        {
            Text = "productionInsuranceDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.target)
            }
        };

        public static readonly Command ProductionInsuranceBuy = new()
        {
            Text = "productionInsuranceBuy",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<long[]>(k.target),
            }
        };

        public static readonly Command StackTo = new()
        {
            Text = "stackTo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.inventory),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command ForceDock = new()
        {
            Text = "forceDock",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ForceDockAdmin = new()
        {
            Text = "forceDockAdmin",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command ZoneSaveLayer = new()
        {
            Text = "zoneSaveLayer",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TeleportList = new()
        {
            Text = "teleportList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TeleportUse = new()
        {
            Text = "teleportUse",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.ID),
            }
        };

        public static readonly Command TeleportToZoneObject = new()
        {
            Text = "teleportToZoneObject",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.target)
            }
        };

        public static readonly Command TeleportGetChannelList = new()
        {
            Text = "teleportGetChannelList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command SignIn = new()
        {
            Text = "signIn",
            AccessLevel = AccessLevel.notDefined,
            Arguments =
            {
                new Argument<string>(k.email),
                new Argument<string>(k.password),
                new Argument<int>(k.client)
            }
        };

        public static readonly Command SignInSteam = new()
        {
            Text = "signInSteam",
            AccessLevel = AccessLevel.notDefined,
            Arguments =
            {
                new Argument<byte[]>("encData"),
                new Argument<int>(k.client),
            }
        };

        public static readonly Command SignOut = new()
        {
            Text = "signOut",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SteamListAccounts = new()
        {
            Text = "steamListAccounts",
            AccessLevel = AccessLevel.notDefined,
            Arguments =
            {
                new Argument<byte[]>("encData")
            }
        };

        public static readonly Command CharacterSettingsGet = new()
        {
            Text = "characterSettingsGet",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterSettingsSet = new()
        {
            Text = "characterSettingsSet",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<Dictionary<string, object>>(k.data)
            }
        };

        public static readonly Command CharacterSearch = new()
        {
            Text = "characterSearch",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.name)
            }
        };

        public static readonly Command PollGet = new()
        {
            Text = "pollGet",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command PollAnswer = new()
        {
            Text = "pollAnswer",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int>(k.answer),
            }
        };

        public static readonly Command Ping = new()
        {
            Text = "ping",
            AccessLevel = AccessLevel.notDefined,
            Arguments =
            {
                new Argument<string>(k.state)
            }
        };

        public static readonly Command Quit = new()
        {
            Text = "quit",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command GetEnums = new()
        {
            Text = "getEnums",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command GetCommands = new()
        {
            Text = "getCommands",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command GetZoneInfo = new()
        {
            Text = "getZoneInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetEntityDefaults = new()
        {
            Text = "getEntityDefaults",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command ExtensionHistory = new()
        {
            Text = "extensionHistory",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationTransactionHistory = new()
        {
            Text = "corporationTransactionHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command CharacterTransactionHistory = new()
        {
            Text = "characterTransactionHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command ProductionHistory = new()
        {
            Text = "productionHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command StandingHistory = new()
        {
            Text = "standingHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command CharacterListNpcDeath = new()
        {
            Text = "characterListNpcDeath",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetEffects = new()
        {
            Text = "getEffects",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command RobotEmpty = new()
        {
            Text = "robotEmpty",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.container),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command ZoneSectorList = new()
        {
            Text = "zoneSectorList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetHighScores = new()
        {
            Text = "getHighScores",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetMyHighScores = new()
        {
            Text = "getMyHighScores",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command IsOnline = new()
        {
            Text = "isOnline",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int[]>(k.characterID)
            }
        };

        public static readonly Command Chat = new()
        {
            Text = "chat",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.message),
                new Argument<int>(k.target),
            }
        };

        public static readonly Command CharacterGetProfiles = new()
        {
            Text = "characterGetProfiles",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterCreate = new()
        {
            Text = "characterCreate",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterSelect = new()
        {
            Text = "characterSelect",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterWizardData = new()
        {
            Text = "characterWizardData",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterCheckNick = new()
        {
            Text = "characterCheckNick",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.nick)
            }
        };

        public static readonly Command CharacterUpdateBalance = new()
        {
            Text = "characterUpdateBalance",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterCorporationHistory = new()
        {
            Text = "characterCorporationHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterSetMoodMessage = new()
        {
            Text = "characterSetMoodmessage",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.moodMessage)
            }
        };

        public static readonly Command CharacterRemoveFromCache = new()
        {
            Text = "characterRemoveFromCache",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterSetBlockTrades = new()
        {
            Text = "characterSetBlockTrades",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.state)
            }
        };

        public static readonly Command CharacterForceDeselect = new()
        {
            Text = "characterForceDeselect",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterForceDisconnect = new()
        {
            Text = "characterForceDisconnect",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterNickHistory = new()
        {
            Text = "characterNickHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterRename = new()
        {
            Text = "characterRename",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.nick),
                new Argument<int>(k.characterID),
            }
        };

        public static readonly Command SocialGetMyList = new()
        {
            Text = "socialGetMyList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SocialFriendRequest = new()
        {
            Text = "socialFriendRequest",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.friend)
            }
        };

        public static readonly Command SocialConfirmPendingFriendRequest = new()
        {
            Text = "socialConfirmPendingFriendRequest",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.friend),
                new Argument<int>(k.accept),
            }
        };

        public static readonly Command SocialFriendRequestReply = new()
        {
            Text = "socialFriendRequestReply",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command SocialDeleteFriend = new()
        {
            Text = "socialDeleteFriend",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.friend)
            }
        };

        public static readonly Command SocialBlockFriend = new()
        {
            Text = "socialBlockFriend",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.friend)
            }
        };

        public static readonly Command MailOpen = new()
        {
            Text = "mailOpen",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.ID)
            }
        };

        public static readonly Command MailDelete = new()
        {
            Text = "mailDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.ID)
            }
        };

        public static readonly Command MailList = new()
        {
            Text = "mailList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.folder)
            }
        };

        public static readonly Command MailDeleteFolder = new()
        {
            Text = "mailDeleteFolder",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.folder)
            }
        };

        public static readonly Command MailMoveToFolder = new()
        {
            Text = "mailMoveToFolder",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.folder),
                new Argument<string>(k.ID),
            }
        };

        public static readonly Command MailNewCount = new()
        {
            Text = "mailNewCount",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MailUsedFolders = new()
        {
            Text = "mailUsedFolders",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MailSend = new()
        {
            Text = "mailSend",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MassMailOpen = new()
        {
            Text = "massMailOpen",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.ID)
            }
        };

        public static readonly Command MassMailDelete = new()
        {
            Text = "massMailDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.ID)
            }
        };

        public static readonly Command MassMailSend = new()
        {
            Text = "massMailSend",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.subject),
                new Argument<string>(k.body),
                new Argument<int[]>(k.target)
            }
        };

        public static readonly Command MassMailList = new()
        {
            Text = "massMailList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.folder)
            }
        };

        public static readonly Command MassMailNewCount = new()
        {
            Text = "massMailNewCount",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ChannelTalk = new()
        {
            Text = "channelTalk",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel),
                new Argument<string>(k.message),
            }
        };

        public static readonly Command ChannelList = new()
        {
            Text = "channelList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ChannelListAll = new()
        {
            Text = "channelListAll",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ChannelMyList = new()
        {
            Text = "channelMyList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ChannelNotification = new()
        {
            Text = "channelNotification",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ChannelCreate = new()
        {
            Text = "channelCreate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel)
            }
        };

        public static readonly Command ChannelJoin = new()
        {
            Text = "channelJoin",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel)
            }
        };

        public static readonly Command ChannelLeave = new()
        {
            Text = "channelLeave",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel)
            }
        };

        public static readonly Command ChannelKick = new()
        {
            Text = "channelKick",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel),
                new Argument<int>(k.memberID),
            }
        };

        public static readonly Command ChannelSetTopic = new()
        {
            Text = "channelSetTopic",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel),
                new Argument<string>(k.topic),
            }
        };

        public static readonly Command ChannelSetMemberRole = new()
        {
            Text = "channelModifyMemberRole",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel),
                new Argument<int>(k.memberID),
                new Argument<int>(k.role)
            }
        };

        public static readonly Command ChannelSetPassword = new()
        {
            Text = "channelSetPassword",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel)
            }
        };

        public static readonly Command ChannelBan = new()
        {
            Text = "channelBan",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel),
                new Argument<int>(k.memberID),
            }
        };

        public static readonly Command ChannelRemoveBan = new()
        {
            Text = "channelRemoveBan",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel),
                new Argument<int>(k.memberID),
            }
        };

        public static readonly Command ChannelGetBannedMembers = new()
        {
            Text = "channelGetBannedMembers",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.channel)
            }
        };

        public static readonly Command ChannelGlobalMute = new()
        {
            Text = "channelGlobalMute",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID),
                new Argument<int>(k.state),
            }
        };

        public static readonly Command ChannelGetMutedCharacters = new()
        {
            Text = "channelGetMutedCharacters",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ExtensionGetAll = new()
        {
            Text = "extensionGetAll",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ExtensionLearntList = new()
        {
            Text = "extensionLearntList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ExtensionPrerequireList = new()
        {
            Text = "extensionPrerequireList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ExtensionCategoryList = new()
        {
            Text = "extensionCategoryList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ExtensionGetAvailablePoints = new()
        {
            Text = "extensionGetAvailablePoints",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ExtensionBuyForPoints = new()
        {
            Text = "extensionBuyForPoints",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.extensionID)
            }
        };

        public static readonly Command ExtensionGetPointParameters = new()
        {
            Text = "extensionGetPointParameters",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ExtensionResetCharacter = new()
        {
            Text = "extensionResetCharacter",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command ExtensionFreeLockedEp = new()
        {
            Text = "extensionFreeLockedEp",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.amount)
            }
        }; //

        //GameAdmin Command
        public static readonly Command ExtensionFreeAllLockedEpCommand = new()
        {
            Text = "extensionFreeAllLockedEpByCommand",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.accountID)
            }
        };

        //GameAdmin Command
        public static readonly Command EPBonusSet = new()
        {
            Text = "EPBonusSet",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.bonus),
                new Argument<int>(k.duration),
            }
        };

        public static readonly Command FreshNewsCount = new()
        {
            Text = "freshNewsCount",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.language)
            }
        };

        public static readonly Command GetNews = new()
        {
            Text = "getNews",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.language),
                new Argument<int>(k.amount),
            }
        };

        public static readonly Command NewsCategory = new()
        {
            Text = "newsCategory",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command RequestInfiniteBox = new()
        {
            Text = "requestInfiniteBox",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationCreate = new()
        {
            Text = "corporationCreate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<string>(k.nick),
                new Argument<int>(k.taxRate),
                new Argument<Dictionary<string, object>>(k.publicProfile)
            }
        };

        public static readonly Command CorporationGetMyInfo = new()
        {
            Text = "corporationGetMyInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationRemoveMember = new()
        {
            Text = "corporationRemoveMember",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID)
            }
        };

        public static readonly Command CorporationSetMemberRole = new()
        {
            Text = "corporationSetMemberRole",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID),
                new Argument<int>(k.role),
            }
        };

        public static readonly Command CorporationCharacterInvite = new()
        {
            Text = "corporationCharacterInvite",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID),
                new Argument<string>(k.message),
            }
        };

        public static readonly Command CorporationInviteReply = new()
        {
            Text = "corporationInviteReply",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.answer)
            }
        };

        public static readonly Command CorporationMemberTransferred = new()
        {
            Text = "corporationMemberTransferred",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command CorporationInfo = new()
        {
            Text = "corporationInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.eid)
            }
        };

        public static readonly Command CorporationLeave = new()
        {
            Text = "corporationLeave",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationSearch = new()
        {
            Text = "corporationSearch",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.name)
            }
        };

        public static readonly Command CorporationSetInfo = new()
        {
            Text = "corporationSetInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationDonate = new()
        {
            Text = "corporationDonate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.amount)
            }
        };

        public static readonly Command CorporationDropRoles = new()
        {
            Text = "corporationDropRoles",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationCancelLeave = new()
        {
            Text = "corporationCancelLeave",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationHangarListOnBase = new()
        {
            Text = "corporationHangarListOnBase",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command CorporationHangarListAll = new()
        {
            Text = "corporationHangarListAll",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationRentHangar = new()
        {
            Text = "corporationRentHangar",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.baseEID)
            }
        };

        public static readonly Command CorporationHangarLogSet = new()
        {
            Text = "corporationHangarLogSet",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.log),
            }
        };

        public static readonly Command CorporationHangarLogClear = new()
        {
            Text = "corporationHangarLogClear",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command CorporationHangarSetAccess = new()
        {
            Text = "corporationHangarSetAccess",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.hangarAccess),
            }
        };

        public static readonly Command CorporationHangarClose = new()
        {
            Text = "corporationHangarClose",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command CorporationHangarLogList = new()
        {
            Text = "corporationHangarLogList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.offset),
            }
        };

        public static readonly Command CorporationPayOut = new()
        {
            Text = "corporationPayOut",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID),
                new Argument<long>(k.amount),
            }
        };

        public static readonly Command CorporationHangarPayRent = new()
        {
            Text = "corporationHangarPayRent",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command CorporationVoteStart = new()
        {
            Text = "corporationVoteStart",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<string>(k.topic),
                new Argument<int>(k.participation),
                new Argument<int>(k.consensusRate)
            }
        };

        public static readonly Command CorporationVoteList = new()
        {
            Text = "corporationVoteList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationVoteDelete = new()
        {
            Text = "corporationVoteDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.voteID)
            }
        };

        public static readonly Command CorporationVoteCast = new()
        {
            Text = "corporationVoteCast",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.voteID),
                new Argument<int>(k.answer),
            }
        };

        public static readonly Command CorporationVoteSetTopic = new()
        {
            Text = "corporationVoteSetTopic",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.voteID),
                new Argument<string>(k.topic),
            }
        };

        public static readonly Command CorporationBulletinStart = new()
        {
            Text = "corporationBulletinStart",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.title),
                new Argument<string>(k.text),
            }
        };

        public static readonly Command CorporationBulletinEntry = new()
        {
            Text = "corporationBulletinEntry",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.text),
                new Argument<int>(k.bulletinID),
            }
        };

        public static readonly Command CorporationBulletinDelete = new()
        {
            Text = "corporationBulletinDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.bulletinID)
            }
        };

        public static readonly Command CorporationBulletinList = new()
        {
            Text = "corporationBulletinList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationBulletinDetails = new()
        {
            Text = "corporationBulletinDetails",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.bulletinID)
            }
        };

        public static readonly Command CorporationHangarSetName = new()
        {
            Text = "corporationHangarSetName",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<string>(k.name),
            }
        };

        public static readonly Command CorporationHangarRentPrice = new()
        {
            Text = "corporationHangarRentPrice",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command CorporationApply = new()
        {
            Text = "corporationApply",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.corporationEID),
                new Argument<string>(k.note),
            }
        };

        public static readonly Command CorporationListMyApplications = new()
        {
            Text = "corporationListMyApplications",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationListApplications = new()
        {
            Text = "corporationListApplications",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationDeleteMyApplication = new()
        {
            Text = "corporationDeleteMyApplication",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.corporationEID),
                new Argument<int>(k.all),
            }
        };

        public static readonly Command CorporationDeleteApplication = new()
        {
            Text = "corporationDeleteApplication",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID),
                new Argument<int>(k.all),
            }
        };

        public static readonly Command CorporationAcceptApplication = new()
        {
            Text = "corporationAcceptApplication",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID),
                new Argument<long>(k.corporationEID),
            }
        };

        public static readonly Command CorporationHangarFolderSectionCreate = new()
        {
            Text = "corporationHangarFolderCreate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command CorporationHangarFolderSectionDelete = new()
        {
            Text = "corporationHangarFolderDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<long>(k.container),
            }
        };

        public static readonly Command CorporationGetDelegates = new()
        {
            Text = "corporationGetDelegates",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command CorporationBulletinEntryDelete = new()
        {
            Text = "corporationBulletinEntryDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.bulletinID),
                new Argument<int>(k.ID),
            }
        };

        public static readonly Command CorporationTransfer = new()
        {
            Text = "corporationTransfer",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.amount),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command CorporationBulletinNewEntries = new()
        {
            Text = "corporationBulletinNewEntries",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<DateTime>(k.time)
            }
        };

        public static readonly Command CorporationBulletinModerate = new()
        {
            Text = "corporationBulletinModerate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<int>(k.bulletinID),
                new Argument<string>(k.text)
            }
        };

        public static readonly Command CorporationGetReputation = new()
        {
            Text = "corporationGetReputation",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationMyStandings = new()
        {
            Text = "corporationGetMyStandings",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationSetMembersNeutral = new()
        {
            Text = "corporationSetMembersNeutral",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationLogHistory = new()
        {
            Text = "corporationLogHistory",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CorporationRename = new()
        {
            Text = "corporationRename",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<string>(k.nick),
            }
        };

        public static readonly Command CorporationNameHistory = new()
        {
            Text = "corporationNameHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.corporationEID)
            }
        };

        public static readonly Command AllianceGetMyInfo = new()
        {
            Text = "allianceGetMyInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterGetNote = new()
        {
            Text = "characterGetNote",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterSetNote = new()
        {
            Text = "characterSetNote",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.target)
            }
        };

        public static readonly Command SetStanding = new()
        {
            Text = "setStanding",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.source),
                new Argument<long>(k.target),
                new Argument<double>(k.standing)
            }
        };

        public static readonly Command StandingList = new()
        {
            Text = "standingList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command GangInviteReply = new()
        {
            Text = "gangInviteReply",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.answer)
            }
        };

        public static readonly Command GangInvite = new()
        {
            Text = "gangInvite",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID)
            }
        };

        public static readonly Command GangCreate = new()
        {
            Text = "gangCreate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.name)
            }
        };

        public static readonly Command GangInfo = new()
        {
            Text = "gangInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GangDelete = new()
        {
            Text = "gangDelete",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GangLeave = new()
        {
            Text = "gangLeave",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GangKick = new()
        {
            Text = "gangKick",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID)
            }
        };

        public static readonly Command GangSetLeader = new()
        {
            Text = "gangSetLeader",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID)
            }
        };

        public static readonly Command GangSetRole = new()
        {
            Text = "gangSetRole",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID),
                new Argument<int>(k.role),
            }
        };

        public static readonly Command MissionStart = new()
        {
            Text = "missionStart",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.missionCategory),
                new Argument<int>(k.missionLevel),
            }
        };

        public static readonly Command MissionLogList = new()
        {
            Text = "missionLogList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command MissionData = new()
        {
            Text = "missionData",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MissionGetOptions = new()
        {
            Text = "missionGetOptions",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command MissionListRunning = new()
        {
            Text = "missionListRunning",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MissionDeliver = new()
        {
            Text = "missionDeliver",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MissionAbort = new()
        {
            Text = "missionAbort",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command AccountUpdateBalance = new()
        {
            Text = "accountUpdateBalance",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command AccountList = new()
        {
            Text = "accountList",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command AccountGetTransactionHistory = new()
        {
            Text = "accountGetTransactionHistory",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command AccountEpForActivityHistory = new()
        {
            Text = "accountEpForActivityHistory",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterDeselect = new()
        {
            Text = "characterDeselect",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterDelete = new()
        {
            Text = "characterDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.characterID)
            }
        };

        public static readonly Command CharacterList = new()
        {
            Text = "characterList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterGetMyProfile = new()
        {
            Text = "characterGetMyProfile",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command BaseGetMyItems = new()
        {
            Text = "baseGetMyItems",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command BaseListFacilities = new()
        {
            Text = "baseListFacilities",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command BaseGetInfo = new()
        {
            Text = "baseGetInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.baseEID)
            }
        };

        public static readonly Command TransferData = new()
        {
            Text = "transferData",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int[]>(k.target),
                new Argument<Dictionary<string, object>>(k.data),
            }
        };

        public static readonly Command ConnectionStart = new()
        {
            Text = "connectionStart",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ConnectionEnd = new()
        {
            Text = "connectionEnd",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MailReceived = new()
        {
            Text = "mailReceived",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MailGotRead = new()
        {
            Text = "mailGotRead",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MailGotDeleted = new()
        {
            Text = "mailGotDeleted",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command StackItems = new()
        {
            Text = "stackItems",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.source),
                new Argument<long>(k.target),
                new Argument<long>(k.container)
            }
        };

        public static readonly Command PackItems = new()
        {
            Text = "packItems",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.target),
                new Argument<long>(k.container),
            }
        };

        public static readonly Command UnpackItems = new()
        {
            Text = "unpackItems",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.target),
                new Argument<long>(k.container),
            }
        };

        public static readonly Command RelocateItems = new()
        {
            Text = "relocateItems",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.eid),
                new Argument<long>(k.targetContainer),
                new Argument<long>(k.sourceContainer)
            }
        };

        public static readonly Command TrashItems = new()
        {
            Text = "trashItems",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.target),
                new Argument<long>(k.container),
            }
        };

        public static readonly Command SetItemName = new()
        {
            Text = "setItemName",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.target),
                new Argument<string>(k.name),
                new Argument<long>(k.container)
            }
        };

        public static readonly Command StackSelection = new()
        {
            Text = "stackSelection",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.eid),
                new Argument<long>(k.container),
            }
        };

        public static readonly Command UnstackAmount = new()
        {
            Text = "unStackAmount",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<int>(k.amount),
                new Argument<int>(k.size),
                new Argument<long>(k.container)
            }
        };

        public static readonly Command CharacterTransferCredit = new()
        {
            Text = "characterTransferCredit",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.amount),
                new Argument<int>(k.target),
            }
        };

        public static readonly Command RequestStarterRobot = new()
        {
            Text = "requestStarterRobot",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command StarterRobotCreated = new()
        {
            Text = "starterRobotCreated",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command CorporationRoleHistory = new()
        {
            Text = "corporationRoleHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset)
            }
        };

        public static readonly Command CorporationMemberRoleHistory = new()
        {
            Text = "corporationMemberRoleHistory",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.memberID),
                new Argument<int>(k.offset),
            }
        };

        public static readonly Command AllianceRoleHistory = new()
        {
            Text = "allianceRoleHistory",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command Undock = new()
        {
            Text = "undock",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command Dock = new()
        {
            Text = "dock",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetAggregateFields = new()
        {
            Text = "getAggregateFields",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetStandingForDefaultCorporations = new()
        {
            Text = "getStandingForDefaultCorporations",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetStandingForDefaultAlliances = new()
        {
            Text = "getStandingForDefaultAlliances",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterSetHomeBase = new()
        {
            Text = "characterSetHomeBase",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterClearHomeBase = new()
        {
            Text = "characterClearHomeBase",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command AllianceGetDefaults = new()
        {
            Text = "allianceGetDefaults",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetMyKillReports = new()
        {
            Text = "getMyKillReports",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command CharacterCorrectNick = new()
        {
            Text = "characterCorrectNick",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.nick),
                new Argument<int>(k.characterID),
            }
        };

        public static readonly Command ListContainer = new()
        {
            Text = "listContainer",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.container)
            }
        };

        public static readonly Command ZoneSOS = new()
        {
            Text = "zoneSOS",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ZoneSelfDestruct = new()
        {
            Text = "zoneSelfDestruct",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command ZoneGetBuildings = new()
        {
            Text = "zoneGetBuildings",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MarketModifyOrder = new()
        {
            Text = "marketModifyOrder",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MarketItemList = new()
        {
            Text = "marketItemList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command MarketGetMyItems = new()
        {
            Text = "marketGetMyItems",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MarketCancelItem = new()
        {
            Text = "marketCancelItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.marketItemID)
            }
        };

        public static readonly Command MarketCreateSellOrder = new()
        {
            Text = "marketCreateSellOrder",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.itemEID),
                new Argument<int>(k.duration),
                new Argument<double>(k.price),
                new Argument<int>(k.useCorporationWallet),
                new Argument<long>(k.container)
            }
        };

        public static readonly Command MarketBuyItem = new()
        {
            Text = "marketBuyItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.marketItemID),
                new Argument<int>(k.useCorporationWallet),
                new Argument<int>(k.quantity)
            }
        };

        public static readonly Command MarketCreateBuyOrder = new()
        {
            Text = "marketCreateBuyOrder",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<int>(k.duration),
                new Argument<double>(k.price),
                new Argument<int>(k.useCorporationWallet)
            }
        };

        public static readonly Command MarketGetAveragePrices = new()
        {
            Text = "marketGetAveragePrices",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.marketEID),
                new Argument<int>(k.definition),
                new Argument<int>(k.day)
            }
        };

        public static readonly Command MarketGlobalAveragePrices = new()
        {
            Text = "marketGlobalAveragePrices",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<int>(k.day),
            }
        };

        public static readonly Command MarketGetDefinitionAveragePrice = new()
        {
            Text = "marketGetDefinitionAveragePrice",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command MarketAvailableItems = new()
        {
            Text = "marketAvailableItems",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid)
            }
        };

        public static readonly Command MarketItemsInRange = new()
        {
            Text = "marketItemsInRange",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command MarketTaxLogList = new()
        {
            Text = "marketTaxLogList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.marketEID),
                new Argument<int>(k.offset),
            }
        };

        public static readonly Command MarketTaxChange = new()
        {
            Text = "marketTaxChange",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.marketEID),
                new Argument<double>(k.tax),
            }
        };

        public static readonly Command MarketGetInfo = new()
        {
            Text = "marketGetInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.eid)
            }
        };

        public static readonly Command SelectActiveRobot = new()
        {
            Text = "selectActiveRobot",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID),
                new Argument<long>(k.containerEID),
            }
        };

        public static readonly Command GetRobotInfo = new()
        {
            Text = "getRobotInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID)
            }
        };

        public static readonly Command GetRobotFittingInfo = new()
        {
            Text = "getRobotFittingInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID)
            }
        };

        public static readonly Command SetRobotTint = new()
        {
            Text = "setRobotTint",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<long>(k.robotEID),
                new Argument<SKColor>(k.tint),
            }
        };

        public static readonly Command EquipModule = new()
        {
            Text = "equipModule",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.moduleEID),
                new Argument<int>(k.slot),
                new Argument<string>(k.robotComponent)
            }
        };

        public static readonly Command RemoveModule = new()
        {
            Text = "removeModule",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID),
                new Argument<long>(k.moduleEID),
            }
        };

        public static readonly Command ChangeModule = new()
        {
            Text = "changeModule",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.source),
                new Argument<int>(k.target),
                new Argument<string>(k.sourceComponent)
            }
        };

        public static readonly Command EquipAmmo = new()
        {
            Text = "equipAmmo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID),
                new Argument<long>(k.ammoEID),
                new Argument<long>(k.moduleEID)
            }
        };

        public static readonly Command UnequipAmmo = new()
        {
            Text = "unEquipAmmo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID),
                new Argument<long>(k.moduleEID),
            }
        };

        public static readonly Command ChangeAmmo = new()
        {
            Text = "changeAmmo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.robotEID),
                new Argument<long>(k.sourceModuleEID),
                new Argument<long>(k.targetModuleEID)
            }
        };

        public static readonly Command GetResearchLevels = new()
        {
            Text = "getResearchLevels",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionRefine = new()
        {
            Text = "productionRefine",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<long>(k.facility),
                new Argument<int>(k.amount)
            }
        };

        public static readonly Command ProductionRefineQuery = new()
        {
            Text = "productionRefineQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<long>(k.facility),
                new Argument<int>(k.amount)
            }
        };

        public static readonly Command ProductionReprocess = new()
        {
            Text = "productionReprocess",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.target),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionReprocessQuery = new()
        {
            Text = "productionReprocessQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long[]>(k.target),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionComponentsList = new()
        {
            Text = "productionComponentsList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionFacilityInfo = new()
        {
            Text = "productionFacilityInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionRepair = new()
        {
            Text = "productionRepair",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<long[]>(k.target),
            }
        };

        public static readonly Command ProductionInProgress = new()
        {
            Text = "productionInProgress",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionCancel = new()
        {
            Text = "productionCancel",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command ProductionRepairQuery = new()
        {
            Text = "productionRepairQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<long[]>(k.target),
            }
        };

        public static readonly Command ProductionServerInfo = new()
        {
            Text = "productionServerInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command ProductionResearch = new()
        {
            Text = "productionResearch",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.item),
                new Argument<long>(k.researchKitEID),
                new Argument<long>(k.facility),
                new Argument<int>(k.useCorporationWallet)
            }
        };

        public static readonly Command ProductionResearchQuery = new()
        {
            Text = "productionResearchQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition),
                new Argument<int>(k.target),
                new Argument<long>(k.facility)
            }
        };

        public static readonly Command ProductionLineList = new()
        {
            Text = "productionLineList",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility)
            }
        };

        public static readonly Command ProductionLineCalibrate = new()
        {
            Text = "productionLineCalibrate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.eid),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionLineDelete = new()
        {
            Text = "productionLineDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionLineStart = new()
        {
            Text = "productionLineStart",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionCPRGInfo = new()
        {
            Text = "productionCPRGInfo",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<long>(k.eid),
            }
        };

        public static readonly Command ProductionPrototypeStart = new()
        {
            Text = "productionPrototypeStart",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<int>(k.definition),
                new Argument<int>(k.useCorporationWallet)
            }
        };

        public static readonly Command ProductionPrototypeQuery = new()
        {
            Text = "productionPrototypeQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<int>(k.definition),
            }
        };

        public static readonly Command ProductionGetCprgFromLine = new()
        {
            Text = "productionGetCPRGFromLine",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionGetCprgFromLineQuery = new()
        {
            Text = "productionGetCPRGFromLineQuery",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID),
                new Argument<long>(k.facility),
            }
        };

        public static readonly Command ProductionLineSetRounds = new()
        {
            Text = "productionLineSetRounds",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.facility),
                new Argument<int>(k.ID),
                new Argument<int>(k.rounds)
            }
        };

        public static readonly Command IntrusionSiteSetEffectBonus = new()
        {
            Text = "intrusionSiteSetEffectBonus",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.target),
                new Argument<int>(k.effectType),
            }
        };

        public static readonly Command IntrusionSapItemInfo = new()
        {
            Text = "intrusionSAPItemInfo",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command GetStabilityBonusThresholds = new()
        {
            Text = "getStabilityBonusThresholds",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command GetIntrusionSiteInfo = new()
        {
            Text = "getIntrusionSiteInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command IntrusionEnabler = new()
        {
            Text = "intrusionEnabler",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.state)
            }
        };

        public static readonly Command IntrusionState = new()
        {
            Text = "intrusionState",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command IntrusionGetPauseTime = new()
        {
            Text = "intrusionGetPauseTime",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command IntrusionSetPauseTime = new()
        {
            Text = "intrusionSetPauseTime",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TradeBegin = new()
        {
            Text = "tradeBegin",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.traderID)
            }
        };

        public static readonly Command TradeCancel = new()
        {
            Text = "tradeCancel",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TradeSetOffer = new()
        {
            Text = "tradeSetOffer",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.credit)
            }
        };

        public static readonly Command TradeAccept = new()
        {
            Text = "tradeAccept",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TradeRetractOffer = new()
        {
            Text = "tradeRetractOffer",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TradeState = new()
        {
            Text = "tradeState",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TradeOffer = new()
        {
            Text = "tradeOffer",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TradeFinished = new()
        {
            Text = "tradeFinished",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command MineralScanResultList = new()
        {
            Text = "mineralScanResultList",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command MineralScanResultDelete = new()
        {
            Text = "mineralScanResultDelete",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int[]>(k.items)
            }
        };

        public static readonly Command MineralScanResultMove = new()
        {
            Text = "mineralScanResultMove",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int[]>(k.items),
                new Argument<string>(k.folder),
            }
        };

        public static readonly Command MineralScanResultCreateItem = new()
        {
            Text = "mineralScanResultCreateItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.ID)
            }
        };

        public static readonly Command MineralScanResultUploadFromItem = new()
        {
            Text = "mineralScanResultUploadFromItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.containerEID),
                new Argument<long>(k.itemEID),
            }
        };

        public static readonly Command TechTreeInfo = new()
        {
            Text = "techTreeInfo",
            AccessLevel = AccessLevel.normal
        };

        public static readonly Command TechTreeUnlock = new()
        {
            Text = "techTreeUnlock",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.definition)
            }
        };

        public static readonly Command TechTreeResearch = new()
        {
            Text = "techTreeResearch",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.container),
                new Argument<long[]>(k.items),
            }
        };

        public static readonly Command TechTreeDonate = new()
        {
            Text = "techTreeDonate",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<Dictionary<string, object>>(k.points)
            }
        };

        public static readonly Command TechTreeCorporationInfo = new()
        {
            Text = "techTreeCorporationInfo",
            AccessLevel = AccessLevel.admin
        };

        public static readonly Command TechTreeGetLogs = new()
        {
            Text = "techTreeGetLogs",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<int>(k.offset),
                new Argument<int>(k.duration),
            }
        };

        public static readonly Command EnableSelfTeleport = new()
        {
            Text = "enableSelfTeleport",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.characterID),
                new Argument<int>(k.durationMinutes),
            }
        };

        public static readonly Command UseLotteryItem = new()
        {
            Text = "useLotteryItem",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<long>(k.itemEID),
                new Argument<long>(k.containerEID),
            }
        };

        public static readonly Command GetRifts = new()
        {
            Text = "getRifts",
            AccessLevel = AccessLevel.normal
        };


        //--------- admin tool commands -------- 

        // account list with extra character info for the admintool 
        public static readonly Command GetAccountsWithCharacters = new()
        {
            Text = "getAccountsWithCharacters",
            AccessLevel = AccessLevel.admin,
        };

        public static readonly Command GetCharactersOnline = new()
        {
            Text = "getCharactersOnline",
            AccessLevel = AccessLevel.admin,
        };

        public static readonly Command AccountGet = new()
        {
            Text = "accountGet",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.accountID)
            }
        };

        // admin tool stuff
        // updates email,pass,acclevel
        //
        // open version: use the ChangeSessionEmail or ChangeSessionPassword commands
        public static readonly Command AccountUpdate = new()
        {
            Text = "accountUpdate",
            AccessLevel = AccessLevel.toolAdmin,
            Arguments =
            {
                new Argument<int>(k.accountID)
            }
        };

        public static readonly Command ReimburseItem = new()
        {
            Text = "ReimburseItem",
            AccessLevel = AccessLevel.admin
        };

        // creates an account from the tool. 
        // 
        // open version: use AccountOpenCreate
        public static readonly Command AccountCreate = new()
        {
            Text = "accountCreate",
            AccessLevel = AccessLevel.toolAdmin,
            Arguments =
            {
                new Argument<string>(k.email),
                new Argument<int>(k.accessLevel),
                new Argument<string>(k.password)
            }
        };

        // create an account for yourself if the server is open
        public static readonly Command AccountOpenCreate = new()
        {
            Text = "accountOpenCreate",
            AccessLevel = AccessLevel.notDefined,
            Arguments =
            {
                new Argument<string>(k.email),
                new Argument<string>(k.password)
            }
        };


        // changes the password of the sender's account - safe to be available always
        //
        // requires login -> no old pass or other validation is needed
        public static readonly Command ChangeSessionPassword = new()
        {
            Text = "changeSessionPassword",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.password)
            }
        };

        // changes the email of the sender's account - safe, like the pass change
        // not yet implemented
        public static readonly Command ChangeSessionEmail = new()
        {
            Text = "changeSessionEmail",
            AccessLevel = AccessLevel.normal,
            Arguments =
            {
                new Argument<string>(k.email)
            }
        };

        // confirm email for account. From GM interface.
        public static readonly Command AccountConfirmEmail = new()
        {
            Text = "accountConfirmEmail",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.accountID)
            }
        };

        // ban account and disconnect if online
        //
        public static readonly Command AccountBan = new()
        {
            Text = "accountBan",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.accountID),
                new Argument<int>(k.banLength),
            }
        };

        public static readonly Command AccountUnban = new()
        {
            Text = "accountUnban",
            AccessLevel = AccessLevel.admin,
            Arguments =
            {
                new Argument<int>(k.accountID),
            }
        };

        public static readonly Command ServerInfoSet = new()
        {
            Text = "serverInfoSet",
            AccessLevel = AccessLevel.toolAdmin,
            Arguments =
            {
                new Argument<string>(k.name),
                new Argument<string>(k.description),
                new Argument<string>(k.contact),
                new Argument<int>(k.isOpen),
                new Argument<int>(k.isBroadcast),
            }
        };

        // safe for open
        public static readonly Command ServerInfoGet = new()
        {
            Text = "serverInfoGet",
            AccessLevel = AccessLevel.notDefined
        };

        public static readonly Command AccountDelete = new()
        {
            Text = "accountDelete",
            AccessLevel = AccessLevel.toolAdmin,
            Arguments =
            {
                new Argument<int>(k.accountID),
            }
        };

    }
}
