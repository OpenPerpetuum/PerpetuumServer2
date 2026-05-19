# Database Schema Documentation

Generated from DBML structure.

## Table of Contents

- [accountcampaignitems](#accountcampaignitems)
- [accountcreditqueue](#accountcreditqueue)
- [accountextensionbought](#accountextensionbought)
- [accountextensionpenalty](#accountextensionpenalty)
- [entitydefaults](#entitydefaults)
- [extensioncategories](#extensioncategories)
- [aggregatefields](#aggregatefields)
- [extensions](#extensions)
- [accountextensionspent](#accountextensionspent)
- [accountonlinetime](#accountonlinetime)
- [packages](#packages)
- [accounts](#accounts)
- [accountpremiumpackages](#accountpremiumpackages)
- [accountredeemableitems](#accountredeemableitems)
- [accounttransactionlog](#accounttransactionlog)
- [adminCommandLog](#admincommandlog)
- [aggregatemodifiers](#aggregatemodifiers)
- [aggregatevalues](#aggregatevalues)
- [corporations](#corporations)
- [alliances](#alliances)
- [alliancemembers](#alliancemembers)
- [artifactloot](#artifactloot)
- [artifacts](#artifacts)
- [artifactspawninfo](#artifactspawninfo)
- [artifacttypes](#artifacttypes)
- [attributeFlags](#attributeflags)
- [automarket_unbought_resources](#automarket-unbought-resources)
- [automarket_unsold_leftovers](#automarket-unsold-leftovers)
- [beams](#beams)
- [beamassignment](#beamassignment)
- [bulletinentries](#bulletinentries)
- [bulletins](#bulletins)
- [calibrationdefaults](#calibrationdefaults)
- [calibrationtemplateitems](#calibrationtemplateitems)
- [campaigns](#campaigns)
- [campaigngoodiepacks](#campaigngoodiepacks)
- [categoryFlags](#categoryflags)
- [categorygroups](#categorygroups)
- [categorygroupsnames](#categorygroupsnames)
- [centralbanklog](#centralbanklog)
- [centralbanktransactions](#centralbanktransactions)
- [channelbans](#channelbans)
- [channelmembers](#channelmembers)
- [channels](#channels)
- [characterextensions](#characterextensions)
- [characterhighscore](#characterhighscore)
- [characterkillreports](#characterkillreports)
- [charactermessages](#charactermessages)
- [characternickhistory](#characternickhistory)
- [characternotes](#characternotes)
- [characternpcdeath](#characternpcdeath)
- [characterreimburselog](#characterreimburselog)
- [characters](#characters)
- [charactersettings](#charactersettings)
- [charactersocial](#charactersocial)
- [charactersparks](#charactersparks)
- [charactersparkteleports](#charactersparkteleports)
- [charactertransactions](#charactertransactions)
- [chassisbonus](#chassisbonus)
- [cmails](#cmails)
- [combatlog](#combatlog)
- [components](#components)
- [connectedips](#connectedips)
- [containerlog](#containerlog)
- [corporationApplication](#corporationapplication)
- [corporationceotakeover](#corporationceotakeover)
- [corporationdocumentconfig](#corporationdocumentconfig)
- [corporationdocumentregistration](#corporationdocumentregistration)
- [corporationdocuments](#corporationdocuments)
- [corporationhistory](#corporationhistory)
- [corporationleave](#corporationleave)
- [corporationlog](#corporationlog)
- [corporationmembers](#corporationmembers)
- [corporationnamehistory](#corporationnamehistory)
- [corporationrolehistory](#corporationrolehistory)
- [corporationtransactions](#corporationtransactions)
- [countries](#countries)
- [cw_race](#cw-race)
- [cw_school](#cw-school)
- [cw_corporation](#cw-corporation)
- [cw_corporation_extension](#cw-corporation-extension)
- [cw_major](#cw-major)
- [cw_major_extension](#cw-major-extension)
- [cw_race_extension](#cw-race-extension)
- [cw_school_extension](#cw-school-extension)
- [cw_spark](#cw-spark)
- [cw_spark_extension](#cw-spark-extension)
- [decorcategories](#decorcategories)
- [decor](#decor)
- [defaultfieldscalculation](#defaultfieldscalculation)
- [definitionconfig](#definitionconfig)
- [definitionconfigunits](#definitionconfigunits)
- [dynamiccalibrationtemplates](#dynamiccalibrationtemplates)
- [effectcategories](#effectcategories)
- [effectdefaultmodifiers](#effectdefaultmodifiers)
- [effects](#effects)
- [enablerextensions](#enablerextensions)
- [entities](#entities)
- [entitystorage](#entitystorage)
- [entitytemplates](#entitytemplates)
- [entitytrash](#entitytrash)
- [environmentdescription](#environmentdescription)
- [environmentdescriptionstaging](#environmentdescriptionstaging)
- [epforactivitylog](#epforactivitylog)
- [extensionpointpenalty](#extensionpointpenalty)
- [extensionpoints](#extensionpoints)
- [extensionpointworklog](#extensionpointworklog)
- [extensionprerequire](#extensionprerequire)
- [extensionremovelog](#extensionremovelog)
- [extensionsubscription](#extensionsubscription)
- [facilitymap](#facilitymap)
- [gameglobals](#gameglobals)
- [gang](#gang)
- [gangmembers](#gangmembers)
- [giftloots](#giftloots)
- [hardwareinfo](#hardwareinfo)
- [harvestlog](#harvestlog)
- [hostconfig](#hostconfig)
- [icetracker](#icetracker)
- [insurance](#insurance)
- [insuranceprices](#insuranceprices)
- [intrusiondockingrightslog](#intrusiondockingrightslog)
- [intrusioneffectlog](#intrusioneffectlog)
- [intrusionloot](#intrusionloot)
- [intrusionproductionlog](#intrusionproductionlog)
- [intrusionproductionstack](#intrusionproductionstack)
- [intrusionsapdeploylog](#intrusionsapdeploylog)
- [intrusionsaps](#intrusionsaps)
- [intrusionsitelog](#intrusionsitelog)
- [intrusionsitemessagelog](#intrusionsitemessagelog)
- [intrusionsites](#intrusionsites)
- [intrusionsitestabilitythreshold](#intrusionsitestabilitythreshold)
- [itemcreation](#itemcreation)
- [itemprices](#itemprices)
- [itemresearchlevels](#itemresearchlevels)
- [itemscore](#itemscore)
- [itemshop](#itemshop)
- [itemshoppresets](#itemshoppresets)
- [itemshoplocations](#itemshoplocations)
- [killreports](#killreports)
- [locktest](#locktest)
- [lootitems](#lootitems)
- [lotteryitemweights](#lotteryitemweights)
- [market_orders_configuration](#market-orders-configuration)
- [marketaverageprices](#marketaverageprices)
- [marketaveragesbycomponent](#marketaveragesbycomponent)
- [marketitems](#marketitems)
- [markettaxlog](#markettaxlog)
- [mineralconfigs](#mineralconfigs)
- [mineralnodes](#mineralnodes)
- [minerals](#minerals)
- [mineralscan](#mineralscan)
- [mininglog](#mininglog)
- [missionagents](#missionagents)
- [missionbonus](#missionbonus)
- [missionconstants](#missionconstants)
- [zones](#zones)
- [teleportdescriptions](#teleportdescriptions)
- [missiontypes](#missiontypes)
- [missionissuer](#missionissuer)
- [missions](#missions)
- [missionenterpoints](#missionenterpoints)
- [missiongrind](#missiongrind)
- [missionlocations](#missionlocations)
- [missionlog](#missionlog)
- [missionparticipants](#missionparticipants)
- [missionpayoutlog](#missionpayoutlog)
- [missionrequiredextensions](#missionrequiredextensions)
- [missionrequiredmissions](#missionrequiredmissions)
- [missionrequiredstanding](#missionrequiredstanding)
- [missionrewards](#missionrewards)
- [missionspotinfo](#missionspotinfo)
- [missionstandingchange](#missionstandingchange)
- [missionstartitem](#missionstartitem)
- [missiontargettypes](#missiontargettypes)
- [missiontargets](#missiontargets)
- [missiontargetsarchive](#missiontargetsarchive)
- [missiontargetslog](#missiontargetslog)
- [missiontoagent](#missiontoagent)
- [missiontolocation](#missiontolocation)
- [modulepropertymodifiers](#modulepropertymodifiers)
- [mtproductprices](#mtproductprices)
- [newscategories](#newscategories)
- [news](#news)
- [npcbossinfo](#npcbossinfo)
- [npccontaineritems](#npccontaineritems)
- [npcescalactions](#npcescalactions)
- [npcspawn](#npcspawn)
- [npcpresence](#npcpresence)
- [npcflock](#npcflock)
- [npcflockloot](#npcflockloot)
- [npcinterzonegroup](#npcinterzonegroup)
- [npckills](#npckills)
- [npcloot](#npcloot)
- [npcpoolpresets](#npcpoolpresets)
- [npcpoolpresetvalues](#npcpoolpresetvalues)
- [npcrandomflockpool](#npcrandomflockpool)
- [npcreinforcements](#npcreinforcements)
- [npcreinforcementtypes](#npcreinforcementtypes)
- [npcsafespawnpoints](#npcsafespawnpoints)
- [npcSpecialTypes](#npcspecialtypes)
- [nspools](#nspools)
- [nspoolmembers](#nspoolmembers)
- [nspoolrelation](#nspoolrelation)
- [nstemplates](#nstemplates)
- [opp_reimburselog](#opp-reimburselog)
- [ownerincome](#ownerincome)
- [packageitems](#packageitems)
- [passablemappoints](#passablemappoints)
- [paymentproducts](#paymentproducts)
- [paypal_transactions_history](#paypal-transactions-history)
- [pbsconnections](#pbsconnections)
- [pbslog](#pbslog)
- [pbsregisteredmembers](#pbsregisteredmembers)
- [pbsreimburse](#pbsreimburse)
- [pbstrash](#pbstrash)
- [plantdamagetype](#plantdamagetype)
- [plantrules](#plantrules)
- [plasma_gathered](#plasma-gathered)
- [plasma_gathered_daily](#plasma-gathered-daily)
- [plasma_sold](#plasma-sold)
- [polls](#polls)
- [pollanswers](#pollanswers)
- [pollchoices](#pollchoices)
- [premadechatmessage](#premadechatmessage)
- [premademail](#premademail)
- [productioncost](#productioncost)
- [productiondecalibration](#productiondecalibration)
- [productionduration](#productionduration)
- [productionlines](#productionlines)
- [productionlog](#productionlog)
- [prototypes](#prototypes)
- [rarematerials](#rarematerials)
- [raw_material_prices](#raw-material-prices)
- [reimbursementlog](#reimbursementlog)
- [relays](#relays)
- [relicloot](#relicloot)
- [relicspawninfo](#relicspawninfo)
- [relictypes](#relictypes)
- [reliczoneconfig](#reliczoneconfig)
- [resource_market_prices](#resource-market-prices)
- [resources_gathered](#resources-gathered)
- [resources_gathered_daily](#resources-gathered-daily)
- [riftconfigs](#riftconfigs)
- [riftdestinations](#riftdestinations)
- [robotassembler](#robotassembler)
- [robotfittingpresets](#robotfittingpresets)
- [robotsavedeffects](#robotsavedeffects)
- [robotsetup](#robotsetup)
- [robottemplates](#robottemplates)
- [robottemplaterelation](#robottemplaterelation)
- [runningproduction](#runningproduction)
- [runningproductionreserveditem](#runningproductionreserveditem)
- [savedeffects](#savedeffects)
- [season_activity_rates](#season-activity-rates)
- [season_character_points](#season-character-points)
- [season_leaderboard_rewards](#season-leaderboard-rewards)
- [season_objective_progress](#season-objective-progress)
- [season_objectives](#season-objectives)
- [season_tier_claims](#season-tier-claims)
- [season_tiers](#season-tiers)
- [seasons](#seasons)
- [serverinfo](#serverinfo)
- [settings](#settings)
- [siegeitems](#siegeitems)
- [slotFlags](#slotflags)
- [sparks](#sparks)
- [sparkextensions](#sparkextensions)
- [standinglog](#standinglog)
- [standings](#standings)
- [steamkeys](#steamkeys)
- [steamkeyscomp](#steamkeyscomp)
- [storecategories](#storecategories)
- [storeitems](#storeitems)
- [strongholdexitconfig](#strongholdexitconfig)
- [techline](#techline)
- [techlineincrement](#techlineincrement)
- [techlinemember](#techlinemember)
- [techtree](#techtree)
- [techtreegroups](#techtreegroups)
- [techtreelog](#techtreelog)
- [techtreenodeprices](#techtreenodeprices)
- [techtreepoints](#techtreepoints)
- [techtreepointtypes](#techtreepointtypes)
- [techtreeunlockednodes](#techtreeunlockednodes)
- [terraformprojectregistration](#terraformprojectregistration)
- [terraformprojects](#terraformprojects)
- [tiertypes](#tiertypes)
- [traceips](#traceips)
- [traceroutelog](#traceroutelog)
- [trainingartifacts](#trainingartifacts)
- [trainingrewards](#trainingrewards)
- [transactiontypes](#transactiontypes)
- [transportassignments](#transportassignments)
- [transportassignmentslog](#transportassignmentslog)
- [transportassignmenttimes](#transportassignmenttimes)
- [usercount](#usercount)
- [vendorpresets](#vendorpresets)
- [vendorpresetvalues](#vendorpresetvalues)
- [vendors](#vendors)
- [votes](#votes)
- [voteentries](#voteentries)
- [yellowpages](#yellowpages)
- [zoneeffects](#zoneeffects)
- [zoneentities](#zoneentities)
- [zoneriftsconfig](#zoneriftsconfig)
- [zonesectors](#zonesectors)
- [zoneteleportdevicemap](#zoneteleportdevicemap)
- [zoneuserentities](#zoneuserentities)

---

## accountcampaignitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `accountid` | `int [not null]` |
| `campaignid` | `int [not null]` |
| `redeemed` | `bit [not null, default: 0]` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `redeemdate` | `datetime` |

---

## accountcreditqueue

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `credit` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |

### Indexes

- `id [pk, name: "PK_accountcreditqueue"]`

---

## accountextensionbought

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `accountid` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `points` | `int [not null]` |
| `packagetype` | `int [not null, default: 0]` |

---

## accountextensionpenalty

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `points` | `int [not null, default: 0]` |
| `forever` | `bit [not null, default: 0]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |

---

## entitydefaults

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `"int IDENTITY(1,1)" [not null]` |
| `definitionname` | `varchar(100) [not null]` |
| `quantity` | `int [not null, default: 1]` |
| `attributeflags` | `bigint [not null, default: 0]` |
| `categoryflags` | `bigint [not null]` |
| `options` | `varchar(MAX)` |
| `note` | `nvarchar(2048)` |
| `enabled` | `bit [not null, default: 1]` |
| `volume` | `float [default: 0]` |
| `mass` | `float [default: 0]` |
| `hidden` | `bit [not null, default: 0]` |
| `health` | `float [not null, default: 100]` |
| `descriptiontoken` | `nvarchar(100)` |
| `purchasable` | `bit [not null, default: 1]` |
| `tiertype` | `int` |
| `tierlevel` | `int` |

### Indexes

- `definition [pk, name: "PK_entitydefaults"]`
- `definitionname [unique, name: "IX_entitydefaults_name"]`

### Relations

- `definition` → `aggregatevalues.definition`
- `definition` → `beamassignment.definition`
- `definition` → `chassisbonus.definition`
- `definition` → `components.definition`
- `definition` → `components.componentdefinition`
- `definition` → `decor.definition`
- `definition` → `definitionconfig.definition`
- `definition` → `dynamiccalibrationtemplates.definition`
- `definition` → `dynamiccalibrationtemplates.targetdefinition`
- `definition` → `enablerextensions.definition`
- `definition` → `environmentdescription.definition`
- `definition` → `environmentdescriptionstaging.definition`
- `definition` → `giftloots.definition`
- `definition` → `insuranceprices.definition`
- `definition` → `intrusionloot.itemdefinition`
- `definition` → `intrusionloot.sitedefinition`
- `definition` → `intrusionloot.sapdefinition`
- `definition` → `itemprices.definition`
- `definition` → `itemresearchlevels.definition`
- `definition` → `itemresearchlevels.calibrationprogram`
- `definition` → `itemshop.targetdefinition`
- `definition` → `missionrewards.definition`
- `definition` → `missionstartitem.definition`
- `definition` → `missiontargets.definition`
- `definition` → `npccontaineritems.definition`
- `definition` → `npccontaineritems.lootdefinition`
- `definition` → `npcflock.definition`
- `definition` → `npcflockloot.lootdefinition`
- `definition` → `npcloot.definition`
- `definition` → `npcloot.lootdefinition`
- `definition` → `nspoolmembers.definition`
- `definition` → `nstemplates.definition`
- `definition` → `plantdamagetype.definition`
- `definition` → `prototypes.definition`
- `definition` → `prototypes.prototype`
- `definition` → `robotsetup.robotshell`
- `definition` → `robotsetup.head`
- `definition` → `robotsetup.chassis`
- `definition` → `robotsetup.leg`
- `definition` → `robotsetup.container`
- `definition` → `robotsetup.hybridshell`
- `definition` → `robottemplaterelation.definition`
- `definition` → `siegeitems.definition`
- `definition` → `storeitems.definition`
- `definition` → `techlineincrement.definition`
- `definition` → `techlinemember.definition`
- `definition` → `vendorpresetvalues.definition`

---

## extensioncategories

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `extensioncategoryid` | `int [not null]` |
| `categoryname` | `varchar(50) [not null]` |
| `hidden` | `bit [not null, default: 0]` |
| `note` | `nvarchar(2048)` |

### Relations

- `extensioncategoryid` → `extensions.category`

---

## aggregatefields

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `nvarchar(100) [not null]` |
| `formula` | `int [not null, default: 1]` |
| `measurementunit` | `varchar(100)` |
| `measurementmultiplier` | `float [not null, default: 1]` |
| `measurementoffset` | `float [not null, default: 0]` |
| `category` | `int [not null, default: 0]` |
| `digits` | `int [not null, default: 0]` |
| `moreisbetter` | `bit` |
| `usedinconfig` | `bit` |
| `note` | `nvarchar(MAX)` |

### Indexes

- `id [pk, name: "PK_aggregatefields"]`
- `name [unique, name: "IX_aggregatefields"]`

### Relations

- `id` → `extensions.targetpropertyID`
- `id` → `aggregatevalues.field`
- `id` → `chassisbonus.targetpropertyID`
- `id` → `effectdefaultmodifiers.field`
- `id` → `nstemplates.field`

---

## extensions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `extensionid` | `int [not null]` |
| `extensionname` | `varchar(128) [not null]` |
| `category` | `int [not null]` |
| `rank` | `int [not null]` |
| `targetlearningattribute` | `varchar(50)` |
| `learningattributeprimary` | `varchar(50) [not null]` |
| `learningattributesecondary` | `varchar(50)` |
| `bonus` | `float [not null]` |
| `note` | `nvarchar(2048)` |
| `price` | `int [not null, default: 103]` |
| `active` | `bit [not null, default: 1]` |
| `description` | `varchar(128)` |
| `targetpropertyID` | `int` |
| `effectenhancer` | `bit [not null, default: 0]` |
| `hidden` | `bit [not null, default: 0]` |
| `freezelimit` | `int` |

### Indexes

- `extensionname [unique, name: "IX_extensions_name"]`

### Relations

- Referenced by `aggregatefields.id`
- Referenced by `extensioncategories.extensioncategoryid`
- `extensionid` → `accountextensionspent.extensionid`
- `extensionid` → `characterextensions.extensionid`
- `extensionid` → `chassisbonus.extension`
- `extensionid` → `cw_corporation_extension.extensionid`
- `extensionid` → `cw_major_extension.extensionid`
- `extensionid` → `cw_race_extension.extensionid`
- `extensionid` → `cw_school_extension.extensionid`
- `extensionid` → `cw_spark_extension.extensionid`
- `extensionid` → `enablerextensions.extensionid`
- `extensionid` → `extensionprerequire.extensionid`
- `extensionid` → `extensionprerequire.requiredextension`
- `extensionid` → `missionrequiredextensions.extensionid`
- `extensionid` → `sparkextensions.extensionid`

---

## accountextensionspent

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `accountid` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `points` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `extensionlevel` | `int [not null]` |
| `characterid` | `int [not null]` |
| `id` | `"int IDENTITY(1,1)" [not null]` |

### Relations

- Referenced by `extensions.extensionid`

---

## accountonlinetime

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `accountid` | `int [not null]` |
| `loggedin` | `datetime [not null, default: `getdate()`]` |
| `loggedout` | `datetime` |
| `ip` | `varchar(50) [not null]` |
| `safelogout` | `bit [not null, default: 0]` |
| `hwhash` | `varchar(50)` |
| `istrial` | `bit [not null, default: 0]` |

---

## packages

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(64) [not null]` |
| `note` | `nvarchar(MAX)` |

### Indexes

- `id [pk, name: "PK_premiumpackages"]`

### Relations

- `id` → `accountpremiumpackages.packageid`
- `id` → `packageitems.packageid`

---

## accounts

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `accountID` | `"int IDENTITY(1,1)" [not null]` |
| `email` | `varchar(50)` |
| `password` | `varchar(100)` |
| `firstName` | `nvarchar(50)` |
| `lastName` | `nvarchar(50)` |
| `born` | `smalldatetime` |
| `state` | `int [not null, default: 1]` |
| `accLevel` | `int [not null, default: 16777216]` |
| `totalMinsOnline` | `int [not null, default: 0]` |
| `lastLoggedIn` | `smalldatetime` |
| `creation` | `smalldatetime [default: `getdate()`]` |
| `clientType` | `tinyint [not null, default: 0]` |
| `isLoggedIn` | `bit [not null, default: 0]` |
| `bantime` | `smalldatetime [default: `getdate()`]` |
| `banlength` | `int [not null, default: 120]` |
| `bannote` | `nvarchar(512)` |
| `emailConfirmed` | `bit [not null, default: 1]` |
| `firstcharacter` | `datetime` |
| `note` | `nvarchar(1024)` |
| `steamID` | `varchar(20)` |
| `twitchAuthToken` | `varchar(256)` |
| `credit` | `int [not null, default: 0]` |
| `isactive` | `bit [not null, default: 1]` |
| `resetcount` | `int [not null, default: 0]` |
| `wasreset` | `bit [not null, default: 0]` |
| `validUntil` | `smalldatetime` |
| `payingcustomer` | `bit [not null, default: 0]` |
| `campaignid` | `varchar(512)` |

### Indexes

- `accountID [pk, name: "PK_accounts_aid"]`
- `email [unique, name: "UK_accounts"]`

### Relations

- `accountID` → `accountpremiumpackages.accountid`

---

## accountpremiumpackages

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `packageid` | `int [not null]` |
| `purchasetime` | `datetime [not null, default: `getdate()`]` |

### Indexes

- `id [pk, name: "PK_accountpremiumpackages"]`

### Relations

- Referenced by `accounts.accountID`
- Referenced by `packages.id`

---

## accountredeemableitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `definition` | `int [not null]` |
| `quantity` | `int [not null, default: 1]` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `redeemed` | `datetime` |
| `characterid` | `int` |
| `wasredeemed` | `bit [not null, default: 0]` |
| `packageid` | `int` |

### Indexes

- `id [pk, name: "PK_accountredeemableitems"]`

---

## accounttransactionlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountId` | `nchar(10) [not null]` |
| `transactionType` | `int [not null]` |
| `definition` | `int` |
| `quantity` | `int` |
| `eid` | `bigint` |
| `credit` | `int [not null, default: 0]` |
| `creditChange` | `int [not null, default: 0]` |
| `created` | `datetime [not null]` |

---

## adminCommandLog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `characterid` | `int [not null]` |
| `accLevel` | `int [not null]` |
| `message` | `nvarchar(255)` |

---

## aggregatemodifiers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `categoryflag` | `bigint [not null]` |
| `basefield` | `int [not null]` |
| `modifierfield` | `int [not null]` |

---

## aggregatevalues

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `field` | `int [not null]` |
| `[value]` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_aggregatevalues"]`
- `(definition, field) [unique, name: "IX_aggregatevalues"]`

### Relations

- Referenced by `aggregatefields.id`
- Referenced by `entitydefaults.definition`

---

## corporations

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `name` | `varchar(128) [not null]` |
| `nick` | `varchar(6)` |
| `wallet` | `float [not null, default: 0]` |
| `taxrate` | `int [not null, default: 0]` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `defaultcorp` | `bit [not null, default: 0]` |
| `active` | `bit [not null, default: 1]` |
| `founder` | `int` |
| `publicprofile` | `nvarchar(MAX)` |
| `privateprofile` | `nvarchar(MAX)` |
| `color` | `int` |

### Indexes

- `eid [pk, name: "PK_corporation"]`

### Relations

- `eid` → `alliancemembers.corporationEID`
- `eid` → `corporationApplication.corporationEID`
- `eid` → `corporationhistory.corporationEID`
- `eid` → `corporationmembers.corporationEID`
- `eid` → `corporationrolehistory.corporationEID`
- `eid` → `cw_corporation.corporationEID`
- `eid` → `missionrequiredstanding.corporationeid`

---

## alliances

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `allianceEID` | `bigint [not null]` |
| `name` | `nvarchar(50) [not null]` |
| `nick` | `varchar(6) [not null]` |
| `note` | `nvarchar(2048)` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `defaultAlliance` | `bit [not null, default: 0]` |
| `active` | `bit [not null, default: 1]` |
| `logoresource` | `varchar(50)` |
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `raceid` | `int` |

### Indexes

- `allianceEID [pk, name: "PK_alliances_eid"]`

### Relations

- `allianceEID` → `alliancemembers.allianceEID`

---

## alliancemembers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `allianceEID` | `bigint [not null]` |
| `corporationEID` | `bigint [not null]` |

### Relations

- Referenced by `alliances.allianceEID`
- Referenced by `corporations.eid`

---

## artifactloot

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `artifacttype` | `int [not null]` |
| `definition` | `int [not null]` |
| `minquantity` | `int [not null, default: 1]` |
| `maxquantity` | `int [not null]` |
| `chance` | `float [not null]` |
| `packed` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_artifactloot"]`

---

## artifacts

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `artifacttype` | `int [not null]` |
| `characterid` | `int [not null]` |
| `zoneid` | `int [not null]` |
| `positionx` | `int [not null]` |
| `positiony` | `int [not null]` |
| `missionguid` | `uniqueidentifier` |
| `created` | `datetime [default: `getdate()`]` |

### Indexes

- `id [pk, name: "PK_artifacts"]`

---

## artifactspawninfo

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `artifacttype` | `int [not null]` |
| `zoneid` | `int [not null]` |
| `rate` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_artifactspawninfo"]`

---

## artifacttypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50) [not null]` |
| `goalrange` | `int [not null, default: 1]` |
| `npcpresenceid` | `int` |
| `persistent` | `bit [not null, default: 1]` |
| `minimumloot` | `int [not null, default: 1]` |
| `dynamic` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_artifacttypes"]`
- `name [unique, name: "IX_artifacttypes_unique"]`

---

## attributeFlags

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `offset` | `int [not null]` |
| `name` | `nvarchar(50) [not null]` |
| `note` | `nvarchar(2048)` |

### Indexes

- `offset [unique, name: "IX_attributeFlags_offset"]`

---

## automarket_unbought_resources

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `itemdefinition` | `int [not null]` |
| `quantity` | `bigint [not null]` |

---

## automarket_unsold_leftovers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `itemdefinition` | `int [not null]` |
| `quantity` | `bigint [not null]` |

---

## beams

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50) [not null]` |
| `cycletime` | `int [not null]` |
| `startdelay` | `int [not null, default: 0]` |
| `description` | `varchar(MAX)` |

### Indexes

- `id [pk, name: "PK_beams"]`

### Relations

- `id` → `beamassignment.beam`

---

## beamassignment

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `beam` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_beamassignment"]`
- `definition [unique, name: "IX_beamassignment"]`

### Relations

- Referenced by `beams.id`
- Referenced by `entitydefaults.definition`

---

## bulletinentries

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `entryID` | `"int IDENTITY(1,1)" [not null]` |
| `bulletinID` | `int [not null]` |
| `characterID` | `int [not null]` |
| `entrytext` | `nvarchar(2000) [not null]` |
| `entrydate` | `datetime [not null, default: `getdate()`]` |

---

## bulletins

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `bulletinID` | `"int IDENTITY(1,1)" [not null]` |
| `groupEID` | `bigint [not null]` |
| `title` | `nvarchar(256) [not null]` |
| `startdate` | `datetime [not null, default: `getdate()`]` |
| `startedby` | `int [not null]` |

---

## calibrationdefaults

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int [not null]` |
| `materialefficiency` | `float [not null]` |
| `timeefficiency` | `float [not null]` |

### Indexes

- `definition [pk, name: "PK_calibrationdefaults"]`

---

## calibrationtemplateitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int` |
| `targetdefinition` | `int` |

---

## campaigns

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `campaigntoken` | `varchar(128) [not null]` |
| `note` | `nvarchar(2048)` |

### Indexes

- `id [pk, name: "PK_campaigns"]`

### Relations

- `id` → `campaigngoodiepacks.campaignid`

---

## campaigngoodiepacks

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(512) [not null]` |
| `description` | `varchar(512)` |
| `campaignid` | `int [not null]` |
| `credit` | `int` |
| `ep` | `int` |
| `faction` | `varchar(8)` |
| `item0` | `int` |
| `quantity0` | `int` |
| `item1` | `int` |
| `quantity1` | `int` |
| `item2` | `int` |
| `quantity2` | `int` |
| `item3` | `int` |
| `quantity3` | `int` |
| `item4` | `int` |
| `quantity4` | `int` |
| `item5` | `int` |
| `quantity5` | `int` |
| `item6` | `int` |
| `quantity6` | `int` |
| `item7` | `int` |
| `quantity7` | `int` |
| `item8` | `int` |
| `quantity8` | `int` |
| `item9` | `int` |
| `quantity9` | `int` |

### Indexes

- `id [pk, name: "PK_campaigngoodiepacks"]`
- `name [unique, name: "IX_campaigngoodiepacks"]`

### Relations

- Referenced by `campaigns.id`

---

## categoryFlags

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `[value]` | `bigint [not null]` |
| `name` | `varchar(50)` |
| `note` | `nvarchar(2048)` |
| `hidden` | `bit [not null, default: 0]` |
| `isunique` | `bit [not null, default: 0]` |

### Indexes

- `"[value]" [unique, name: "IX_categoryFlags"]`

### Relations

- `[value]` → `productiondecalibration.categoryflag`
- `[value]` → `productionduration.category`

---

## categorygroups

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `groupId` | `int [not null]` |
| `category` | `bigint [not null]` |

### Indexes

- `id [pk]`

---

## categorygroupsnames

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(100) [not null]` |

### Indexes

- `id [pk]`

---

## centralbanklog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventday` | `datetime [not null]` |
| `amount` | `bigint [not null]` |

---

## centralbanktransactions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null]` |
| `transactiontype` | `int [not null]` |
| `amount` | `float [not null]` |
| `bankcredit` | `bigint [not null]` |

---

## channelbans

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `memberid` | `int [not null]` |
| `channelid` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_channelbans"]`

---

## channelmembers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `channelid` | `int [not null]` |
| `memberid` | `int [not null]` |
| `role` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_channelmembers_"]`

---

## channels

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `nvarchar(50) [not null]` |
| `password` | `nvarchar(50)` |
| `topic` | `nvarchar(200)` |
| `type` | `int [not null, default: 0]` |
| `isForcedJoin` | `bit` |
| `DiscordId` | `varchar(128)` |

### Indexes

- `id [pk, name: "PK_channels_"]`
- `name [unique, name: "IX_channels_name"]`

---

## characterextensions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterextensionid` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `extensionlevel` | `int` |

### Indexes

- `characterextensionid [pk, name: "PK_characterextensions"]`

### Relations

- Referenced by `extensions.extensionid`

---

## characterhighscore

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `npcskilled` | `int [not null]` |
| `playerskilled` | `int [not null]` |
| `date` | `datetime` |

### Indexes

- `id [pk, name: "PK_characterhighscore"]`

---

## characterkillreports

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `reportid` | `uniqueidentifier [not null]` |
| `victim` | `bit` |
| `attacker` | `bit` |
| `killer` | `bit` |

### Indexes

- `id [pk, name: "PK_characterkillreports"]`

---

## charactermessages

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `mailid` | `"bigint IDENTITY(1,1)" [not null]` |
| `owner` | `int [not null]` |
| `sender` | `int [not null]` |
| `folder` | `int [not null]` |
| `type` | `int [not null, default: 0]` |
| `targets` | `varchar(512) [not null]` |
| `creation` | `datetime [default: `getdate()`]` |
| `subject` | `nvarchar(128)` |
| `body` | `nvarchar(2000)` |
| `wasread` | `bit [not null, default: 0]` |

---

## characternickhistory

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `accountid` | `int [not null]` |
| `nick` | `varchar(50)` |
| `eventdate` | `datetime [not null, default: `getdate()`]` |

---

## characternotes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `targetid` | `int [not null]` |
| `note` | `nvarchar(2000) [not null]` |

---

## characternpcdeath

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null, default: 0]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `npcdefinition` | `int [not null]` |
| `playersrobot` | `int [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |

---

## characterreimburselog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `containereid` | `bigint` |
| `characterid` | `int` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `note` | `nvarchar(2048)` |

---

## characters

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterID` | `"int IDENTITY(1,1)" [not null]` |
| `accountID` | `int` |
| `rootEID` | `bigint [not null]` |
| `nick` | `varchar(50)` |
| `moodMessage` | `nvarchar(2000)` |
| `creation` | `smalldatetime [default: `getdate()`]` |
| `lastLogOut` | `smalldatetime` |
| `lastUsed` | `smalldatetime` |
| `credit` | `float [not null, default: 0]` |
| `inUse` | `bit [not null, default: 0]` |
| `totalMinsOnline` | `int [not null, default: 0]` |
| `activeChassis` | `bigint` |
| `active` | `bit [not null, default: 1]` |
| `deletedAt` | `smalldatetime` |
| `baseEID` | `bigint` |
| `defaultcorporationEID` | `bigint` |
| `majorID` | `int [not null, default: 0]` |
| `raceID` | `int [not null, default: 0]` |
| `schoolID` | `int [not null, default: 0]` |
| `sparkID` | `int [not null, default: 0]` |
| `lastdocked` | `datetime` |
| `docked` | `bit [not null, default: 1]` |
| `lastteleported` | `datetime` |
| `zoneID` | `int` |
| `nickcorrected` | `bit [not null, default: 0]` |
| `offensivenick` | `bit [not null, default: 0]` |
| `positionX` | `float` |
| `positionY` | `float` |
| `homeBaseEID` | `bigint` |
| `blockTrades` | `bit [not null, default: 0]` |
| `globalMute` | `bit [not null, default: 0]` |
| `avatar` | `varchar(MAX)` |
| `note` | `varchar(MAX)` |
| `corporationeid` | `bigint [not null, default: 0]` |
| `allianceeid` | `bigint` |
| `language` | `int [not null, default: 0]` |
| `LastRespec` | `datetime` |

### Indexes

- `characterID [pk, name: "PK_characters"]`
- `nick [unique, name: "IX_nickUnique"]`

---

## charactersettings

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `settingsstring` | `nvarchar(MAX)` |

---

## charactersocial

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `friendid` | `int [not null]` |
| `socialstate` | `tinyint [not null]` |
| `note` | `nvarchar(2000)` |
| `laststateupdate` | `datetime [not null, default: `getdate()`]` |

---

## charactersparks

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `sparkid` | `int [not null]` |
| `active` | `bit [not null, default: 0]` |
| `activationtime` | `datetime` |

---

## charactersparkteleports

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `baseeid` | `bigint [not null]` |
| `basedefinition` | `int [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_charactersparkteleports"]`
- `(characterid, baseeid) [unique, name: "IX_charactersparkteleports"]`

---

## charactertransactions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `transactiontype` | `int [not null]` |
| `amount` | `float [not null]` |
| `transactiondate` | `datetime [not null, default: `getdate()`]` |
| `definition` | `int` |
| `quantity` | `int` |
| `currentcredit` | `float [not null, default: 0]` |
| `othercharacter` | `int` |
| `containereid` | `bigint` |

---

## chassisbonus

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null, default: 0]` |
| `extension` | `int [not null]` |
| `bonus` | `float [not null]` |
| `note` | `nvarchar(2000)` |
| `targetpropertyID` | `int [not null]` |
| `effectenhancer` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_chassisbonus"]`
- `(definition, extension, targetpropertyID) [unique, name: "IX_chassis_bonus"]`

### Relations

- Referenced by `aggregatefields.id`
- Referenced by `entitydefaults.definition`
- Referenced by `extensions.extensionid`

---

## cmails

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `owner` | `int [not null]` |
| `sender` | `int [not null]` |
| `target` | `int [not null]` |
| `subject` | `nvarchar(128) [not null]` |
| `body` | `nvarchar(2000) [not null]` |
| `type` | `tinyint [not null]` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `wasread` | `bit [not null, default: 0]` |
| `folder` | `tinyint [not null]` |
| `mailid` | `uniqueidentifier` |
| `sourceid` | `uniqueidentifier` |

---

## combatlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `date` | `datetime [not null]` |
| `zoneId` | `int [not null]` |
| `characterId` | `int [not null]` |
| `data` | `varchar(MAX) [not null]` |

### Indexes

- `id [pk, name: "PK_combatlog"]`

---

## components

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `componentdefinition` | `int [not null]` |
| `componentamount` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_components"]`
- `(definition, componentdefinition) [unique, name: "IX_components"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## connectedips

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `ipaddress` | `varchar(16) [not null]` |
| `sessionstart` | `smalldatetime [not null, default: `getdate()`]` |
| `banned` | `bit [not null, default: 0]` |
| `note` | `nvarchar(512)` |
| `bantime` | `smalldatetime` |
| `bannedby` | `int` |
| `clientid` | `int [not null, default: 0]` |
| `accountid` | `int` |

---

## containerlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `containerEID` | `bigint [not null]` |
| `memberID` | `int [not null]` |
| `containeraccess` | `int [not null]` |
| `operationdate` | `datetime [not null, default: `getdate()`]` |
| `definition` | `int` |
| `quantity` | `int` |

---

## corporationApplication

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterID` | `int [not null]` |
| `corporationEID` | `bigint [not null]` |
| `applyTime` | `smalldatetime [not null, default: `getdate()`]` |
| `motivation` | `nvarchar(512)` |

### Relations

- Referenced by `corporations.eid`

---

## corporationceotakeover

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `corporationeid` | `bigint [not null]` |
| `characterid` | `int [not null]` |
| `expiry` | `datetime [not null]` |

---

## corporationdocumentconfig

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `documenttype` | `int [not null]` |
| `creationprice` | `int [not null, default: 0]` |
| `rentprice` | `int [not null, default: 0]` |
| `rentperioddays` | `int [not null, default: 0]` |
| `maxpercharacter` | `int [not null, default: 0]` |
| `note` | `varchar(2048)` |

### Indexes

- `id [pk, name: "PK_corporationdocumentconfig"]`

---

## corporationdocumentregistration

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `documentid` | `int [not null]` |
| `characterid` | `int [not null]` |
| `role` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_corporationdocumentregistration"]`

---

## corporationdocuments

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `lastmodified` | `datetime [not null, default: `getdate()`]` |
| `validuntil` | `datetime` |
| `ownercharacterid` | `int [not null]` |
| `documenttype` | `int [not null]` |
| `version` | `int [not null, default: 0]` |
| `body` | `nvarchar(MAX)` |

### Indexes

- `id [pk, name: "PK_corporationdocuments"]`

---

## corporationhistory

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterID` | `int [not null]` |
| `corporationEID` | `bigint [not null]` |
| `corporationJoined` | `smalldatetime [not null, default: `getdate()`]` |
| `corporationLeft` | `smalldatetime` |
| `id` | `"int IDENTITY(1,1)" [not null]` |

### Indexes

- `id [pk, name: "PK_corporationhistory"]`

### Relations

- Referenced by `corporations.eid`

---

## corporationleave

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `leavetime` | `datetime [not null, default: `getdate()`]` |

### Indexes

- `characterid [unique, name: "IX_corporationleave"]`

---

## corporationlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `timestamp` | `datetime [not null, default: `getdate()`]` |
| `corporationEid` | `bigint [not null]` |
| `type` | `int [not null]` |
| `issuerId` | `int [not null]` |
| `memberId` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_corporationlog"]`

---

## corporationmembers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `corporationEID` | `bigint [not null]` |
| `memberid` | `int [not null]` |
| `role` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_corporationmembers"]`

### Relations

- Referenced by `corporations.eid`

---

## corporationnamehistory

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `corporationeid` | `bigint [not null]` |
| `name` | `varchar(128) [not null]` |
| `nick` | `varchar(6) [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `characterid` | `int` |

---

## corporationrolehistory

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `corporationEID` | `bigint [not null]` |
| `issuerID` | `int [not null]` |
| `memberID` | `int [not null]` |
| `oldrole` | `int [not null, default: 0]` |
| `newrole` | `int [not null, default: 0]` |
| `rolesettime` | `datetime [not null, default: `getdate()`]` |

### Relations

- Referenced by `corporations.eid`

---

## corporationtransactions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `corporationEID` | `bigint [not null]` |
| `memberID` | `int` |
| `transactiontype` | `int [not null]` |
| `amount` | `float [not null]` |
| `transactiondate` | `datetime [not null, default: `getdate()`]` |
| `quantity` | `int` |
| `definition` | `int` |
| `targetMemberID` | `int` |
| `currentwallet` | `float [not null, default: 0]` |
| `involvedCorporationEID` | `bigint` |

---

## countries

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `int [not null]` |
| `country` | `varchar(50) [not null]` |
| `nick` | `varchar(8)` |

---

## cw_race

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `raceid` | `int [not null]` |
| `name` | `nvarchar(50) [not null]` |
| `attributeA` | `float [not null]` |
| `attributeB` | `float [not null]` |
| `attributeC` | `float [not null]` |
| `attributeD` | `float [not null]` |
| `attributeE` | `float [not null]` |
| `attributeF` | `float [not null]` |
| `note` | `nvarchar(2048)` |
| `descriptiontoken` | `varchar(50)` |

### Relations

- `raceid` → `cw_school.raceid`
- `raceid` → `cw_race_extension.raceid`

---

## cw_school

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `schoolid` | `int [not null]` |
| `raceid` | `int` |
| `name` | `nvarchar(50) [not null]` |
| `attributeA` | `float [not null]` |
| `attributeB` | `float [not null]` |
| `attributeC` | `float [not null]` |
| `attributeD` | `float [not null]` |
| `attributeE` | `float [not null]` |
| `attributeF` | `float [not null]` |
| `note` | `nvarchar(2048)` |
| `descriptiontoken` | `varchar(50)` |

### Relations

- Referenced by `cw_race.raceid`
- `schoolid` → `cw_corporation.schoolid`
- `schoolid` → `cw_major.schoolid`
- `schoolid` → `cw_school_extension.schoolid`

---

## cw_corporation

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `corporationEID` | `bigint` |
| `schoolid` | `int` |
| `name` | `nvarchar(50) [not null]` |
| `attributeA` | `float [not null]` |
| `attributeB` | `float [not null]` |
| `attributeC` | `float [not null]` |
| `attributeD` | `float [not null]` |
| `attributeE` | `float [not null]` |
| `attributeF` | `float [not null]` |
| `note` | `nvarchar(2048)` |
| `descriptiontoken` | `varchar(50)` |
| `baseEID` | `bigint` |
| `missionstatement` | `varchar(50)` |

### Indexes

- `id [pk, name: "PK_cw_corporation_ix"]`
- `corporationEID [unique, name: "IX_cw_corporation"]`

### Relations

- Referenced by `corporations.eid`
- Referenced by `cw_school.schoolid`
- `corporationEID` → `cw_corporation_extension.corporationEID`

---

## cw_corporation_extension

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `corporation_extension_id` | `"int IDENTITY(1,1)" [not null]` |
| `corporationEID` | `bigint [not null]` |
| `extensionid` | `int [not null]` |
| `levelincrement` | `int [not null, default: 1]` |

### Indexes

- `corporation_extension_id [pk, name: "PK_cw_corporation_extension"]`
- `(corporationEID, extensionid) [unique, name: "IX_cw_corporation_extension"]`

### Relations

- Referenced by `cw_corporation.corporationEID`
- Referenced by `extensions.extensionid`

---

## cw_major

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `majorid` | `int [not null]` |
| `schoolid` | `int` |
| `name` | `nvarchar(50) [not null]` |
| `attributeA` | `float [not null]` |
| `attributeB` | `float [not null]` |
| `attributeC` | `float [not null]` |
| `attributeD` | `float [not null]` |
| `attributeE` | `float [not null]` |
| `attributeF` | `float [not null]` |
| `note` | `nvarchar(2048)` |
| `descriptiontoken` | `varchar(50)` |

### Relations

- Referenced by `cw_school.schoolid`
- `majorid` → `cw_major_extension.majorid`

---

## cw_major_extension

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `major_extension_id` | `"int IDENTITY(1,1)" [not null]` |
| `majorid` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `levelincrement` | `int [not null, default: 1]` |

### Indexes

- `(majorid, extensionid) [unique, name: "IX_cw_major_extension"]`

### Relations

- Referenced by `cw_major.majorid`
- Referenced by `extensions.extensionid`

---

## cw_race_extension

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `race_extension_id` | `"int IDENTITY(1,1)" [not null]` |
| `raceid` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `levelincrement` | `int [not null, default: 1]` |

### Indexes

- `(raceid, extensionid) [unique, name: "IX_cw_race_extension"]`

### Relations

- Referenced by `cw_race.raceid`
- Referenced by `extensions.extensionid`

---

## cw_school_extension

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `school_extension_id` | `"int IDENTITY(1,1)" [not null]` |
| `schoolid` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `levelincrement` | `int [not null, default: 1]` |

### Indexes

- `(schoolid, extensionid) [unique, name: "IX_cw_school_extension"]`

### Relations

- Referenced by `cw_school.schoolid`
- Referenced by `extensions.extensionid`

---

## cw_spark

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `sparkid` | `int [not null]` |
| `name` | `nvarchar(50) [not null]` |
| `attributeA` | `float [not null]` |
| `attributeB` | `float [not null]` |
| `attributeC` | `float [not null]` |
| `attributeD` | `float [not null]` |
| `attributeE` | `float [not null]` |
| `attributeF` | `float [not null]` |
| `note` | `nvarchar(2048)` |
| `descriptiontoken` | `varchar(50)` |

### Relations

- `sparkid` → `cw_spark_extension.sparkid`

---

## cw_spark_extension

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `spark_extension_id` | `"int IDENTITY(1,1)" [not null]` |
| `sparkid` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `levelincrement` | `int [not null, default: 1]` |

### Indexes

- `(sparkid, extensionid) [unique, name: "IX_cw_spark_extension"]`

### Relations

- Referenced by `cw_spark.sparkid`
- Referenced by `extensions.extensionid`

---

## decorcategories

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `categoryname` | `varchar(256) [not null]` |

### Indexes

- `id [pk, name: "PK_decorcategories"]`
- `categoryname [unique, name: "IX_decorcategories"]`

### Relations

- `id` → `decor.category`

---

## decor

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `quaternionx` | `float [not null]` |
| `quaterniony` | `float [not null]` |
| `quaternionz` | `float [not null]` |
| `quaternionw` | `float [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |
| `z` | `int [not null]` |
| `scale` | `float [not null, default: 1]` |
| `changed` | `bit [not null, default: 1]` |
| `fadedistance` | `float [not null, default: 0]` |
| `category` | `int [not null, default: 1]` |
| `locked` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_decor"]`

### Relations

- Referenced by `decorcategories.id`
- Referenced by `entitydefaults.definition`

---

## defaultfieldscalculation

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `fieldname` | `nvarchar(50) [not null]` |
| `formula` | `int [not null]` |
| `display` | `bit [not null]` |
| `runtime` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_defaultfieldscalculation"]`

---

## definitionconfig

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `targetdefinition` | `int` |
| `summonerscount` | `int` |
| `npcpresenceid` | `int` |
| `item_work_range` | `float` |
| `explosion_radius` | `float` |
| `cycle_time` | `int` |
| `damage_chemical` | `float` |
| `damage_explosive` | `float` |
| `damage_kinetic` | `float` |
| `damage_thermal` | `float` |
| `lifetime` | `int` |
| `activationtime` | `int` |
| `waves` | `int` |
| `missionrelated` | `bit` |
| `constructionradius` | `int` |
| `action_delay` | `int` |
| `deploy_radius` | `int` |
| `transmitradius` | `int` |
| `constructionlevelmax` | `int` |
| `blockingradius` | `int` |
| `chargeamount` | `int` |
| `inconnections` | `int` |
| `outconnections` | `int` |
| `coretransferred` | `float` |
| `transferefficiency` | `float` |
| `productionupgradeamount` | `int` |
| `productionlevel` | `int` |
| `coreconsumption` | `float` |
| `effectid` | `int` |
| `corecalories` | `float` |
| `corekickstartthreshold` | `float` |
| `reinforcecountermax` | `int` |
| `bandwidthusage` | `int` |
| `bandwidthcapacity` | `int` |
| `emitradius` | `int` |
| `tint` | `varchar(50)` |
| `typeexclusiverange` | `int` |
| `network_node_range` | `int` |
| `hitsize` | `float` |
| `note` | `varchar(2000)` |
| `damage_toxic` | `float` |

### Indexes

- `id [pk, name: "PK_deployablerelation"]`
- `definition [unique, name: "IX_definitionconfig"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## definitionconfigunits

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `configname` | `varchar(128) [not null]` |
| `measurementoffset` | `float [not null, default: 0]` |
| `measurementmultiplier` | `float [not null, default: 1]` |
| `digits` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_definitionconfigunits"]`
- `configname [unique, name: "IX_definitionconfigunits"]`

---

## dynamiccalibrationtemplates

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `materialefficiency` | `float [not null, default: 0.5]` |
| `timeefficiency` | `float [not null, default: 0.5]` |
| `targetdefinition` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_dynamiccalibrationtemplates"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## effectcategories

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `name` | `nvarchar(50) [not null]` |
| `flag` | `bigint [not null]` |
| `maxlevel` | `int [not null, default: 0]` |
| `note` | `nvarchar(2048)` |

### Indexes

- `flag [pk, name: "PK_effectcategories"]`
- `name [unique, name: "IX_effectcategories"]`

---

## effectdefaultmodifiers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `effectid` | `int [not null]` |
| `field` | `int [not null]` |
| `[value]` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_effectdefaultmodifiers"]`

### Relations

- Referenced by `aggregatefields.id`

---

## effects

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `effectcategory` | `bigint [not null, default: 0]` |
| `duration` | `int [not null, default: 0]` |
| `name` | `nvarchar(50) [not null]` |
| `description` | `nvarchar(2048) [not null]` |
| `note` | `nvarchar(2048)` |
| `isaura` | `bit [not null, default: 0]` |
| `auraradius` | `int [not null, default: 0]` |
| `ispositive` | `bit [not null, default: 0]` |
| `display` | `int [not null, default: 0]` |
| `saveable` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_effects"]`
- `name [unique, name: "IX_effects"]`

---

## enablerextensions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `extensionlevel` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_enablerextensions"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `extensions.extensionid`

---

## entities

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `definition` | `int [not null]` |
| `owner` | `bigint` |
| `parent` | `bigint` |
| `health` | `float [not null, default: 100]` |
| `ename` | `nvarchar(128)` |
| `quantity` | `int [not null, default: 1]` |
| `repackaged` | `bit [not null, default: 0]` |
| `dynprop` | `varchar(MAX)` |

### Indexes

- `eid [pk, name: "PK_newentities"]`

---

## entitystorage

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `storage_name` | `nvarchar(50) [not null]` |
| `eid` | `bigint` |

---

## entitytemplates

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `parent` | `int [not null, default: 0]` |
| `name` | `varchar(50)` |

### Indexes

- `id [pk, name: "PK_entitytemplates"]`

---

## entitytrash

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `deleted` | `datetime [not null, default: `getdate()`]` |
| `wasinsured` | `bit [not null, default: 0]` |
| `killedbyplayer` | `bit [not null, default: 0]` |
| `inactiveperiod` | `int [not null, default: 0]` |
| `dctime` | `datetime` |

---

## environmentdescription

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int [not null]` |
| `descriptionstring` | `varchar(MAX) [not null]` |

### Indexes

- `definition [unique, name: "IX_environmentdescription"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## environmentdescriptionstaging

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int [not null]` |
| `descriptionstring` | `varchar(MAX) [not null]` |

### Indexes

- `definition [unique, name: "IX_environmentdescriptionstaging"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## epforactivitylog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `accountid` | `int [not null]` |
| `characterid` | `int [not null]` |
| `epforactivitytype` | `int [not null]` |
| `rawpoints` | `int [not null]` |
| `points` | `int [not null]` |
| `boostfactor` | `float [not null]` |
| `multiplier` | `int` |
| `bonusMultiplier` | `int [default: 0]` |

---

## extensionpointpenalty

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `points` | `int [not null]` |
| `penaltytype` | `int [not null]` |
| `forever` | `bit [not null, default: 0]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |

---

## extensionpoints

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `points` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |

---

## extensionpointworklog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `total` | `int [not null, default: 0]` |
| `paying` | `int [not null]` |
| `id` | `"int IDENTITY(1,1)" [not null]` |

---

## extensionprerequire

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `extensionprerequireid` | `"int IDENTITY(1,1)" [not null]` |
| `extensionid` | `int [not null]` |
| `requiredextension` | `int [not null]` |
| `requiredlevel` | `int [not null]` |

### Relations

- Referenced by `extensions.extensionid`
- Referenced by `extensions.extensionid`

---

## extensionremovelog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `characterid` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `extensionlevel` | `int [not null]` |
| `points` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |

---

## extensionsubscription

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int [not null]` |
| `starttime` | `datetime [not null, default: `getdate()`]` |
| `endtime` | `datetime [not null]` |
| `multiplierBonus` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_extensionsubscription"]`

---

## facilitymap

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `defname` | `varchar(50) [not null]` |
| `leveltag` | `nvarchar(50)` |

### Indexes

- `id [pk, name: "PK_facilitymap"]`

---

## gameglobals

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `clockoffset` | `bigint [not null]` |
| `active` | `smalldatetime` |
| `bankcredit` | `bigint [not null, default: 0]` |
| `lastonline` | `datetime` |

---

## gang

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `uniqueidentifier [not null]` |
| `leaderid` | `int [not null, default: 0]` |
| `name` | `nvarchar(50)` |

### Indexes

- `id [pk, name: "PK_gang"]`

### Relations

- `id` → `gangmembers.gangid`

---

## gangmembers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `gangid` | `uniqueidentifier [not null]` |
| `memberid` | `int [not null]` |
| `role` | `int [not null, default: 0]` |

### Relations

- Referenced by `gang.id`

---

## giftloots

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `minquantity` | `int [not null, default: 1]` |
| `maxquantity` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_giftloots"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## hardwareinfo

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `accountid` | `int [not null]` |
| `gfxcard` | `varchar(128) [not null]` |
| `gfxdriver` | `varchar(128)` |
| `gfxvendorid` | `int [not null]` |
| `gfxdeviceid` | `int [not null]` |
| `gfxdriverversion` | `bigint [not null]` |
| `pixelshader` | `bigint [not null]` |
| `vertexshader` | `bigint [not null]` |
| `maxtexturex` | `int [not null]` |
| `maxtexturey` | `int [not null]` |
| `osversion` | `varchar(128)` |

### Indexes

- `accountid [pk, name: "PK_hardwareinfo"]`

---

## harvestlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `smalldatetime [not null]` |
| `zoneid` | `int [not null]` |
| `definition` | `int [not null]` |
| `amount` | `int [not null]` |

---

## hostconfig

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `hostname` | `varchar(50) [not null]` |
| `hostip` | `varchar(50) [not null, default: 'host ip goes here']` |
| `hostport` | `int [not null, default: 18000]` |
| `sequenceid` | `int [not null, default: 0]` |
| `monitor` | `bit [not null, default: 0]` |

### Indexes

- `sequenceid [unique, name: "IX_hostconfig"]`

---

## icetracker

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `usedbytrial` | `bit [not null, default: 0]` |
| `usedat` | `datetime [not null, default: `getdate()`]` |
| `characterid` | `int [not null]` |

---

## insurance

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `characterid` | `int [not null]` |
| `corporationeid` | `bigint` |
| `insurancetype` | `int [not null]` |
| `enddate` | `datetime [not null]` |
| `payout` | `float [not null]` |

---

## insuranceprices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `fee` | `float [not null, default: 0]` |
| `payout` | `float [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_insuranceprices"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## intrusiondockingrightslog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int` |
| `siteeid` | `bigint [not null]` |
| `dockingstandinglimit` | `float` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `owner` | `bigint` |
| `eventtype` | `int [not null, default: 0]` |

---

## intrusioneffectlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int` |
| `siteeid` | `bigint [not null]` |
| `effectid` | `int` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `owner` | `bigint` |
| `eventtype` | `int [not null, default: 0]` |

---

## intrusionloot

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `sitedefinition` | `int [not null]` |
| `sapdefinition` | `int [not null]` |
| `itemdefinition` | `int [not null]` |
| `minquantity` | `int [not null, default: 1]` |
| `maxquantity` | `int [not null]` |
| `minstabilitythreshold` | `int [not null]` |
| `maxstabilitythreshold` | `int [not null]` |
| `probability` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_intrusionloot"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## intrusionproductionlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `siteeid` | `bigint [not null]` |
| `eventtype` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `facilitydefinition` | `int` |
| `facilitylevel` | `int` |
| `oldfacilitylevel` | `int` |
| `characterid` | `int` |
| `points` | `int` |
| `oldpoints` | `int` |
| `owner` | `bigint` |

---

## intrusionproductionstack

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `siteeid` | `bigint [not null]` |
| `facilityeid` | `bigint [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |

### Indexes

- `id [pk, name: "PK_intrusionproductionstack"]`

---

## intrusionsapdeploylog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `deploytime` | `datetime [not null, default: `getdate()`]` |
| `siteeid` | `bigint [not null]` |
| `sapdefinition` | `int [not null]` |

---

## intrusionsaps

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `siteeid` | `bigint [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |
| `definition` | `int [not null]` |
| `name` | `varchar(128) [not null]` |

### Indexes

- `id [pk, name: "PK_intrusionsaps"]`

---

## intrusionsitelog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `siteeid` | `bigint [not null]` |
| `owner` | `bigint` |
| `stability` | `int [not null]` |
| `winnercorporationeid` | `bigint` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `sapdefinition` | `int [not null]` |
| `oldstability` | `int [not null, default: 0]` |
| `oldowner` | `bigint` |
| `eventtype` | `int [not null, default: 0]` |

---

## intrusionsitemessagelog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `siteeid` | `bigint [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `characterid` | `int [not null]` |
| `message` | `nvarchar(256)` |
| `owner` | `bigint` |
| `eventtype` | `int [not null, default: 0]` |

---

## intrusionsites

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `siteeid` | `bigint [not null]` |
| `owner` | `bigint` |
| `enabled` | `bit [not null, default: 1]` |
| `stability` | `int [not null, default: 0]` |
| `dockingstandinglimit` | `float` |
| `dockingcontroltime` | `datetime` |
| `seteffectcontroltime` | `datetime` |
| `activeeffectid` | `int` |
| `message` | `nvarchar(256)` |
| `productionpoints` | `int [not null, default: 0]` |
| `intrusionstarttime` | `datetime` |
| `defensestandinglimit` | `float` |
| `note` | `varchar(128)` |
| `isAnnounced` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_intrusionsites"]`

---

## intrusionsitestabilitythreshold

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `categoryflag` | `bigint [not null]` |
| `threshold` | `int [not null]` |
| `bonustype` | `int [not null]` |
| `effecttype` | `int` |

### Indexes

- `id [pk, name: "PK_intrusionsitethreshold"]`

---

## itemcreation

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `Id` | `"int IDENTITY(1,1)" [not null]` |
| `Type` | `nvarchar(50) [not null]` |
| `Entity` | `int [not null]` |
| `Qty` | `int [not null]` |
| `CharacterId` | `int [not null]` |
| `IsTraining` | `int [not null]` |
| `ZoneId` | `int [not null]` |
| `DateTime` | `datetime [not null]` |

### Indexes

- `Id [pk]`

---

## itemprices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int [not null]` |
| `price` | `float [not null]` |
| `profitrate` | `float [not null, default: 1]` |
| `manualprice` | `bit [not null, default: 0]` |

### Relations

- Referenced by `entitydefaults.definition`

---

## itemresearchlevels

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `researchlevel` | `int [not null, default: 1]` |
| `calibrationprogram` | `int` |
| `enabled` | `bit [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_itemresearchlevels"]`
- `definition [unique, name: "IX_itemresearchlevels"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## itemscore

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int [not null]` |
| `score` | `int [not null]` |

---

## itemshop

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `presetid` | `int [not null]` |
| `targetdefinition` | `int [not null]` |
| `targetamount` | `int [not null, default: 1]` |
| `tmcoin` | `int [default: 1]` |
| `icscoin` | `int` |
| `asicoin` | `int` |
| `credit` | `float` |
| `unicoin` | `int [default: 1]` |
| `globallimit` | `int` |
| `purchasecount` | `int [not null, default: 0]` |
| `standing` | `float` |

### Indexes

- `id [pk, name: "PK_itemshop"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## itemshoppresets

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |
| `note` | `nvarchar(2000)` |

### Indexes

- `id [pk, name: "PK_itemshoppresets"]`

### Relations

- `id` → `itemshoplocations.presetid`

---

## itemshoplocations

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `locationeid` | `bigint [not null]` |
| `presetid` | `int [not null]` |
| `note` | `nvarchar(2000)` |

### Indexes

- `locationeid [pk, name: "PK_itemshoplocations"]`
- `(locationeid, presetid) [unique, name: "IX_itemshoplocations"]`

### Relations

- Referenced by `itemshoppresets.id`

---

## killreports

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `uniqueidentifier [not null]` |
| `date` | `datetime [not null]` |
| `data` | `varchar(MAX)` |

### Indexes

- `id [pk, name: "PK_killreports"]`

---

## locktest

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `eid` | `bigint [not null]` |

---

## lootitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `uniqueidentifier` |
| `definition` | `int [not null]` |
| `quantity` | `int [not null]` |
| `health` | `float [not null]` |
| `repackaged` | `bit [not null]` |
| `containereid` | `bigint [not null]` |

---

## lotteryitemweights

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `lotterydefinition` | `int [not null]` |
| `categoryflags` | `bigint [not null]` |
| `tiertype` | `int [not null]` |
| `tierlevel` | `int [not null]` |
| `weight` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_lotteryitemweights"]`

---

## market_orders_configuration

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definitionname` | `varchar(100) [not null]` |
| `amount` | `int [not null]` |

---

## marketaverageprices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `marketeid` | `bigint [not null]` |
| `itemdefinition` | `int [not null]` |
| `totalprice` | `float [not null]` |
| `quantity` | `bigint [not null]` |
| `date` | `smalldatetime [not null]` |
| `dailylowest` | `float [not null, default: 0]` |
| `dailyhighest` | `float [not null, default: 0]` |

---

## marketaveragesbycomponent

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `definition` | `int [not null]` |
| `price` | `float [not null]` |

---

## marketitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `marketitemid` | `"int IDENTITY(1,1)" [not null]` |
| `marketeid` | `bigint [not null]` |
| `itemeid` | `bigint` |
| `itemdefinition` | `int [not null]` |
| `submittereid` | `bigint [not null]` |
| `submitted` | `smalldatetime [not null, default: `getdate()`]` |
| `duration` | `int [not null, default: 0]` |
| `isSell` | `bit [not null]` |
| `price` | `float [not null]` |
| `quantity` | `int [not null, default: 1]` |
| `usecorporationwallet` | `bit [not null, default: 0]` |
| `isvendoritem` | `bit [not null, default: 0]` |
| `formembersof` | `bigint` |
| `isAutoOrder` | `bit` |

### Indexes

- `marketitemid [pk, name: "PK_marketitems"]`

---

## markettaxlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `owner` | `bigint [not null]` |
| `characterid` | `int [not null]` |
| `baseeid` | `bigint [not null]` |
| `changefrom` | `float [not null]` |
| `changeto` | `float [not null]` |

---

## mineralconfigs

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `materialtype` | `int [not null]` |
| `maxnodes` | `int [not null]` |
| `maxtilespernode` | `int [not null, default: 0]` |
| `totalamountpernode` | `int [not null, default: 0]` |
| `minthreshold` | `float [not null, default: 0.0]` |

### Indexes

- `id [pk, name: "PK_mineralconfigs"]`
- `(zoneid, materialtype) [unique, name: "IX_mineralconfigs"]`

---

## mineralnodes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `materialtype` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |
| `width` | `int [not null]` |
| `height` | `int [not null]` |
| `data` | `varbinary(MAX)` |

### Indexes

- `id [pk, name: "PK_mineralnodes"]`

---

## minerals

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `idx` | `int [not null]` |
| `name` | `varchar(50) [not null]` |
| `definition` | `int [not null]` |
| `amount` | `int [not null]` |
| `extractionType` | `int [not null]` |
| `enablereffectrequired` | `bit [not null, default: 0]` |
| `note` | `nvarchar(1024)` |
| `geoscandocument` | `int` |

---

## mineralscan

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `ownerid` | `int [not null]` |
| `materialprobetype` | `tinyint [not null, default: 0]` |
| `creation` | `datetime [not null]` |
| `zoneid` | `int [not null]` |
| `materialtype` | `tinyint [not null]` |
| `x1` | `int [not null]` |
| `y1` | `int [not null]` |
| `x2` | `int [not null]` |
| `y2` | `int [not null]` |
| `scanAccuracy` | `float [not null, default: 0.0]` |
| `folder` | `nvarchar(32)` |
| `quality` | `bigint [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_mineralscan"]`

---

## mininglog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `smalldatetime [not null]` |
| `zoneid` | `int [not null]` |
| `definition` | `int [not null]` |
| `amount` | `int [not null]` |

---

## missionagents

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `agentname` | `varchar(128) [not null]` |
| `owner` | `bigint` |
| `ownername` | `varchar(128)` |

### Indexes

- `id [pk, name: "PK_missionagents"]`

### Relations

- `id` → `missions.sourceagent`

---

## missionbonus

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `missioncategory` | `int [not null]` |
| `missionlevel` | `int [not null]` |
| `agentid` | `int [not null]` |
| `bonus` | `int [not null]` |

---

## missionconstants

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `name` | `varchar(50) [not null]` |
| `[value]` | `float [not null, default: 1]` |

### Indexes

- `name [unique, name: "IX_missionconstants"]`

---

## zones

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `int [not null]` |
| `x` | `int [not null, default: 0]` |
| `y` | `int [not null, default: 0]` |
| `name` | `nvarchar(50) [not null]` |
| `description` | `varchar(50)` |
| `note` | `varchar(2048)` |
| `fertility` | `int [not null, default: 60]` |
| `zoneplugin` | `nvarchar(50)` |
| `zoneip` | `varchar(50)` |
| `zoneport` | `int [not null, default: 0]` |
| `isinstance` | `bit [not null, default: 0]` |
| `enabled` | `bit [not null, default: 0]` |
| `spawnid` | `int` |
| `plantruleset` | `int [not null, default: 0]` |
| `protected` | `bit [not null, default: 0]` |
| `raceid` | `int [not null, default: 1]` |
| `width` | `int [not null, default: 2048]` |
| `height` | `int [not null, default: 2048]` |
| `terraformable` | `bit [not null, default: 0]` |
| `zonetype` | `int [not null, default: 1]` |
| `sparkcost` | `int [not null, default: 0]` |
| `maxdockingbase` | `int [not null, default: 0]` |
| `sleeping` | `bit [not null, default: 1]` |
| `plantaltitudescale` | `float [not null, default: 1]` |
| `host` | `varchar(50)` |
| `active` | `bit [not null, default: 1]` |
| `timeLimitMinutes` | `int` |
| `pbsTechLimit` | `int` |
| `PlantsGrowthTimerOverrideMin` | `int` |

### Relations

- `id` → `teleportdescriptions.sourcezone`
- `id` → `teleportdescriptions.targetzone`
- `id` → `zoneentities.zoneID`
- `id` → `zonesectors.zoneid`

---

## teleportdescriptions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `description` | `varchar(128) [not null]` |
| `sourcecolumn` | `bigint` |
| `targetcolumn` | `bigint` |
| `sourcezone` | `int` |
| `sourcerange` | `int` |
| `targetzone` | `int` |
| `targetx` | `float` |
| `targety` | `float` |
| `targetz` | `float` |
| `targetrange` | `int [default: 1]` |
| `usetimeout` | `int [not null, default: 0]` |
| `listable` | `bit [not null, default: 0]` |
| `active` | `bit [not null, default: 1]` |
| `type` | `int [not null, default: 0]` |
| `sourcecolumnname` | `nvarchar(128)` |
| `targetcolumnname` | `nvarchar(128)` |

### Indexes

- `id [pk, name: "PK_teleports"]`
- `description [unique, name: "IX_teleportdescriptions"]`

### Relations

- Referenced by `zones.id`
- Referenced by `zones.id`
- `id` → `missionenterpoints.teleportchannelid`

---

## missiontypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `int [not null]` |
| `name` | `varchar(50) [not null]` |
| `category` | `varchar(50)` |
| `categoryvalue` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_missiontypes"]`

### Relations

- `id` → `missions.missiontype`

---

## missionissuer

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128)` |
| `corporationeid` | `bigint` |
| `corporationname` | `varchar(60)` |
| `allianceeid` | `bigint` |
| `alliancename` | `varchar(60)` |

### Indexes

- `id [pk, name: "PK_missionissuercorporation"]`

### Relations

- `id` → `missions.issuerid`

---

## missions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128)` |
| `title` | `varchar(128) [not null, default: 'title']` |
| `description` | `varchar(128) [not null, default: 'description']` |
| `missiontype` | `int [not null, default: 0]` |
| `issuerid` | `int` |
| `missionidonfail` | `int` |
| `missionidonsuccess` | `int` |
| `isunique` | `bit [not null, default: 0]` |
| `note` | `nvarchar(2000)` |
| `missionpack` | `int [not null, default: 0]` |
| `periodminutes` | `int` |
| `missionlevel` | `int` |
| `durationminutes` | `int [not null, default: 360]` |
| `successmessage` | `varchar(128)` |
| `failmessage` | `varchar(128)` |
| `listable` | `bit [not null, default: 1]` |
| `alwaysenabled` | `bit [not null, default: 0]` |
| `rewardfee` | `float [not null, default: 0]` |
| `locationid` | `int` |
| `behaviourtype` | `int [not null, default: 2]` |
| `sourceagent` | `int` |
| `difficultyreward` | `int` |
| `difficultymultiplier` | `float` |

### Indexes

- `id [pk, name: "PK_missions"]`

### Relations

- Referenced by `missionagents.id`
- Referenced by `missionissuer.id`
- `id` → `missions.missionidonfail`
- Referenced by `missions.id`
- `id` → `missions.missionidonsuccess`
- Referenced by `missions.id`
- Referenced by `missiontypes.id`
- `id` → `missionenterpoints.missionid`
- `id` → `missionrequiredextensions.missionid`
- `id` → `missionrequiredmissions.mission`
- `id` → `missionrequiredmissions.requiredmission`
- `id` → `missionrequiredstanding.missionid`
- `id` → `missionrewards.missionid`
- `id` → `missionstandingchange.missionid`
- `id` → `missionstartitem.missionid`

---

## missionenterpoints

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `teleportchannelid` | `int [not null]` |
| `missionid` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_missionenterpoints"]`

### Relations

- Referenced by `missions.id`
- Referenced by `teleportdescriptions.id`

---

## missiongrind

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `missionlevel` | `int [not null, default: 0]` |
| `amount` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_missiongrind"]`

---

## missionlocations

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `agentid` | `int [not null]` |
| `locationeid` | `bigint [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `float [not null]` |
| `y` | `float [not null]` |
| `maxmissionlevel` | `int [not null, default: 6]` |
| `locationname` | `nvarchar(128)` |
| `dontsync` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_missionlocations"]`
- `locationeid [unique, name: "IX_missionlocations_locationeid_unique"]`
- `(agentid, locationeid) [unique, name: "IX_missionlocations_unique"]`

---

## missionlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `missionGuid` | `uniqueidentifier [not null]` |
| `missionID` | `int [not null]` |
| `characterID` | `int [not null]` |
| `started` | `datetime [not null, default: `getdate()`]` |
| `finished` | `datetime` |
| `succeeded` | `bit [not null, default: 0]` |
| `expire` | `datetime` |
| `grouporder` | `int [not null, default: 0]` |
| `spreadingang` | `bit [not null, default: 0]` |
| `bonusmultiplier` | `float [not null, default: 0]` |
| `locationid` | `int` |
| `missionlevel` | `int` |
| `issuercorporationeid` | `bigint` |
| `issuerallianceeid` | `bigint` |
| `selectedrace` | `int` |
| `rewarddivider` | `int [not null, default: 1]` |

### Indexes

- `missionGuid [unique, name: "IX_missionlog_guid"]`

### Relations

- `missionGuid` → `missiontargetsarchive.missionguid`

---

## missionparticipants

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"bigint IDENTITY(1,1)" [not null]` |
| `missionguid` | `uniqueidentifier [not null]` |
| `characterid` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_missionparticipants"]`

---

## missionpayoutlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `missionguid` | `uniqueidentifier [not null]` |
| `missionid` | `int [not null]` |
| `missioncategory` | `int [not null]` |
| `missionlevel` | `int [not null, default: 0]` |
| `corporationeid` | `bigint` |
| `characterid` | `int` |
| `gangsize` | `int [not null, default: 0]` |
| `amount` | `float [not null]` |
| `sumamount` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_missionpayoutlog"]`

---

## missionrequiredextensions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `extensionid` | `int [not null]` |
| `extensionlevel` | `int [not null]` |
| `missionid` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_missionrequiredextensions"]`

### Relations

- Referenced by `extensions.extensionid`
- Referenced by `missions.id`

---

## missionrequiredmissions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `mission` | `int [not null]` |
| `requiredmission` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_missionrequiredmissions"]`
- `(mission, requiredmission) [unique, name: "IX_missionrequiredmissions_unique"]`

### Relations

- Referenced by `missions.id`
- Referenced by `missions.id`

---

## missionrequiredstanding

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `missionid` | `int [not null]` |
| `corporationeid` | `bigint` |
| `standingabove` | `bit [not null]` |
| `standingthreshold` | `float [not null]` |
| `corporationname` | `varchar(128)` |

### Indexes

- `id [pk, name: "PK_missionrequiredstanding"]`
- `(corporationeid, missionid) [unique, name: "IX_missionrequiredstanding"]`

### Relations

- Referenced by `corporations.eid`
- Referenced by `missions.id`

---

## missionrewards

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128)` |
| `definition` | `int [not null]` |
| `quantity` | `int [not null]` |
| `probability` | `int [not null, default: 0]` |
| `missionid` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_missionrewards2"]`

### Relations

- Referenced by `missions.id`
- Referenced by `entitydefaults.definition`

---

## missionspotinfo

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `type` | `int [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_missionspotinfo"]`

---

## missionstandingchange

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `missionid` | `int [not null, default: 0]` |
| `change` | `float [not null, default: 0]` |
| `alliancename` | `varchar(50)` |
| `allianceeid` | `bigint` |

### Indexes

- `id [pk, name: "PK_missionstandingchange"]`

### Relations

- Referenced by `missions.id`

---

## missionstartitem

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `quantity` | `int [not null, default: 1]` |
| `missionid` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_missionstartitem"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `missions.id`

---

## missiontargettypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `int [not null]` |
| `name` | `varchar(50) [not null]` |
| `reward` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_missiontargettypes"]`

### Relations

- `id` → `missiontargets.targettype`

---

## missiontargets

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(512)` |
| `description` | `varchar(128)` |
| `missionid` | `int` |
| `targettype` | `int [not null, default: 1]` |
| `definition` | `int` |
| `quantity` | `int` |
| `targetpositionx` | `int` |
| `targetpositiony` | `int` |
| `targetpositionrange` | `int` |
| `targetpositionzone` | `int` |
| `scantype` | `int` |
| `checkposition` | `bit [not null, default: 0]` |
| `note` | `nvarchar(2000)` |
| `completedmessage` | `varchar(128)` |
| `activatedmessage` | `varchar(128)` |
| `artifacttype` | `int` |
| `teleportchannel` | `int` |
| `npcpresenceid` | `int` |
| `targetorder` | `int [not null, default: 0]` |
| `displayorder` | `int [not null, default: 0]` |
| `branchmissionid` | `int` |
| `optional` | `bit [not null, default: 0]` |
| `hidden` | `bit [not null, default: 0]` |
| `structureeid` | `bigint` |
| `primarydefinitionfromindex` | `int` |
| `secondarydefinitionfromindex` | `int` |
| `findradius` | `int` |
| `spawnnpcs` | `bit [not null, default: 0]` |
| `snaptonextstructure` | `bit [not null, default: 0]` |
| `generatesecondarydefinition` | `bit [not null, default: 0]` |
| `targetsecondaryasmyprimary` | `bit [not null, default: 0]` |
| `targetprimaryasmysecondary` | `bit [not null, default: 0]` |
| `anylocation` | `bit [not null, default: 0]` |
| `usequantityonly` | `bit [not null, default: 0]` |
| `generateresearchkit` | `bit [not null, default: 0]` |
| `generatecprg` | `bit [not null, default: 0]` |
| `primarycategory` | `bigint` |
| `secondarycategory` | `bigint` |
| `secondaryquantity` | `int` |
| `scaleprimaryqwithlevel` | `bit [not null, default: 0]` |
| `scalesecondaryqwithlevel` | `bit [not null, default: 0]` |
| `primaryscalemult` | `float` |
| `secondaryscalemult` | `float` |
| `structurename` | `nvarchar(128)` |

### Indexes

- `id [pk, name: "PK_missiontargets"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `missiontargettypes.id`

---

## missiontargetsarchive

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `missionid` | `int [not null]` |
| `characterid` | `int [not null]` |
| `targetid` | `int [not null]` |
| `progresscount` | `int [not null]` |
| `completed` | `bit [not null, default: 0]` |
| `missionguid` | `uniqueidentifier [not null]` |
| `targetorder` | `int` |
| `displayorder` | `int` |
| `definition` | `int` |
| `quantity` | `int` |
| `structureeid` | `bigint` |
| `secondarydefinition` | `int` |
| `secondaryquantity` | `int` |
| `zoneid` | `int` |
| `x` | `int` |
| `y` | `int` |
| `artifacttype` | `int` |
| `targettype` | `int` |
| `scantype` | `int` |
| `targetrange` | `int` |
| `successx` | `int` |
| `successy` | `int` |
| `successzoneid` | `int` |
| `successtime` | `datetime` |

### Relations

- Referenced by `missionlog.missionGuid`

---

## missiontargetslog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |
| `targettype` | `int [not null]` |
| `guid` | `uniqueidentifier [not null]` |
| `locationeid` | `bigint [not null]` |
| `missioncategory` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_missiontargetslog"]`

---

## missiontoagent

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `missionid` | `int [not null]` |
| `agentid` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_missiontoagent"]`
- `(missionid, agentid) [unique, name: "IX_missiontoagent_unique"]`

---

## missiontolocation

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `missionid` | `int [not null]` |
| `locationid` | `int [not null]` |
| `attempts` | `int [not null]` |
| `success` | `int [not null]` |
| `uniquecases` | `int [not null, default: 0]` |
| `rewardaverage` | `int` |

### Indexes

- `id [pk, name: "PK_missiontolocation"]`

---

## modulepropertymodifiers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `categoryflags` | `bigint [not null]` |
| `basefield` | `int [not null]` |
| `modifierfield` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_modulepropertymodifiers"]`

---

## mtproductprices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `productkey` | `varchar(50) [not null]` |
| `price` | `int [not null]` |

---

## newscategories

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `category` | `nvarchar(2000)` |

### Indexes

- `id [pk, name: "PK_newscategories"]`

### Relations

- `id` → `news.type`

---

## news

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `idx` | `"int IDENTITY(1,1)" [not null]` |
| `title` | `nvarchar(128) [not null]` |
| `body` | `nvarchar(4000) [not null]` |
| `ntime` | `smalldatetime [not null, default: `getdate()`]` |
| `type` | `int [not null, default: 0]` |
| `language` | `int [not null, default: 0]` |

### Indexes

- `idx [pk, name: "PK_news"]`

### Relations

- Referenced by `newscategories.id`

---

## npcbossinfo

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `flockid` | `int [not null]` |
| `respawnNoiseFactor` | `float` |
| `lootSplitFlag` | `bit [not null]` |
| `outpostEID` | `bigint` |
| `stabilityPts` | `int` |
| `overrideRelations` | `bit [not null]` |
| `customDeathMessage` | `varchar(128)` |
| `customAggressMessage` | `varchar(128)` |
| `riftConfigId` | `int` |
| `isAnnounced` | `bit [not null, default: 0]` |
| `isServerWideAnnouncement` | `bit` |
| `isNoRadioDelay` | `bit` |

### Indexes

- `id [pk]`

---

## npccontaineritems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `lootdefinition` | `int [not null]` |
| `quantity` | `int [not null]` |
| `probability` | `float [not null]` |
| `repackaged` | `bit [not null]` |
| `dontdamage` | `bit [not null]` |
| `minquantity` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_npccontaineritems"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## npcescalactions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `presenceId` | `int [not null]` |
| `flockId` | `int [not null]` |
| `level` | `int [not null]` |
| `chance` | `float [not null, default: 1.0]` |

### Indexes

- `id [pk]`

---

## npcspawn

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50) [not null]` |
| `description` | `varchar(50)` |
| `note` | `nvarchar(2000)` |

### Indexes

- `id [pk, name: "PK_npcspawn"]`
- `name [unique, name: "IX_npcspawn"]`

### Relations

- `id` → `npcpresence.spawnid`

---

## npcpresence

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |
| `topx` | `int [not null, default: 0]` |
| `topy` | `int [not null, default: 0]` |
| `bottomx` | `int [not null, default: 0]` |
| `bottomy` | `int [not null, default: 0]` |
| `note` | `nvarchar(2000)` |
| `spawnid` | `int` |
| `enabled` | `bit [not null, default: 1]` |
| `roaming` | `bit [not null, default: 0]` |
| `roamingrespawnseconds` | `int [not null, default: 0]` |
| `presencetype` | `int [not null, default: 0]` |
| `maxrandomflock` | `int` |
| `randomcenterx` | `int` |
| `randomcentery` | `int` |
| `randomradius` | `int` |
| `dynamiclifetime` | `int` |
| `isbodypull` | `bit [not null, default: 1]` |
| `isrespawnallowed` | `bit [not null, default: 1]` |
| `safebodypull` | `bit [not null, default: 0]` |
| `izgroupid` | `int` |
| `growthseconds` | `int` |

### Indexes

- `id [pk, name: "PK_npcpresence"]`
- `name [unique, name: "IX_npcpresence_name"]`

### Relations

- Referenced by `npcspawn.id`
- `id` → `npcflock.presenceid`

---

## npcflock

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |
| `presenceid` | `int [not null]` |
| `flockmembercount` | `int [not null]` |
| `definition` | `int [not null]` |
| `spawnoriginX` | `int [not null, default: 0]` |
| `spawnoriginY` | `int [not null, default: 0]` |
| `spawnrangeMin` | `int [not null, default: 0]` |
| `spawnrangeMax` | `int [not null, default: 10]` |
| `respawnseconds` | `int [not null, default: 1]` |
| `totalspawncount` | `int [not null, default: 0]` |
| `homerange` | `int [not null, default: 70]` |
| `note` | `nvarchar(2000)` |
| `respawnmultiplierlow` | `float [not null, default: 0.9]` |
| `enabled` | `bit [not null, default: 1]` |
| `iscallforhelp` | `bit [not null, default: 1]` |
| `behaviorType` | `int [not null, default: 1]` |
| `npcSpecialType` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_npcflock"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `npcpresence.id`
- `id` → `npcflockloot.flockid`
- `id` → `npcpoolpresetvalues.flockid`

---

## npcflockloot

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `flockid` | `int [not null]` |
| `lootdefinition` | `int [not null]` |
| `quantity` | `int [not null]` |
| `probability` | `float [not null, default: 1]` |
| `repackaged` | `bit [not null, default: 0]` |
| `dontdamage` | `bit [not null, default: 0]` |
| `minquantity` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_npcflockloot"]`
- `(lootdefinition, flockid) [unique, name: "IX_npcflockloot"]`

### Relations

- Referenced by `npcflock.id`
- Referenced by `entitydefaults.definition`

---

## npcinterzonegroup

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(64) [not null]` |
| `respawnTime` | `int [not null, default: 86400]` |
| `respawnNoiseFactor` | `float [not null, default: 0.15]` |

### Indexes

- `id [pk]`

---

## npckills

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int [not null]` |
| `amount` | `int [not null, default: 1]` |

---

## npcloot

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `lootdefinition` | `int [not null]` |
| `quantity` | `int [not null]` |
| `probability` | `float [not null, default: 1]` |
| `repackaged` | `bit [not null, default: 0]` |
| `dontdamage` | `bit [not null, default: 0]` |
| `minquantity` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_npcloot"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## npcpoolpresets

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |

### Indexes

- `id [pk, name: "PK_npcpoolpresets"]`

### Relations

- `id` → `npcpoolpresetvalues.presetid`

---

## npcpoolpresetvalues

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `presetid` | `int [not null]` |
| `flockid` | `int [not null]` |
| `rate` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_npcpoolpresetvalues"]`

### Relations

- Referenced by `npcflock.id`
- Referenced by `npcpoolpresets.id`

---

## npcrandomflockpool

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `presenceid` | `int [not null]` |
| `flockid` | `int [not null]` |
| `rate` | `float [not null, default: 0]` |
| `lastwave` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_npcpresenceflockrelation"]`

---

## npcreinforcements

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `reinforcementType` | `int [not null]` |
| `targetId` | `int [not null]` |
| `threshold` | `float [not null]` |
| `presenceId` | `int [not null]` |
| `zoneId` | `int` |

### Indexes

- `id [pk]`

---

## npcreinforcementtypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(64) [not null]` |

### Indexes

- `id [pk]`

---

## npcsafespawnpoints

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_npcsafespawnpoints"]`

---

## npcSpecialTypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(64) [not null]` |
| `[value]` | `int [not null]` |

### Indexes

- `id [pk]`

---

## nspools

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50) [not null]` |

### Indexes

- `id [pk, name: "PK_nspools"]`

### Relations

- `id` → `nspoolmembers.poolid`
- `id` → `nspoolrelation.sourcepool`
- `id` → `nspoolrelation.targetpool`

---

## nspoolmembers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `poolid` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_nspoolmembers"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `nspools.id`

---

## nspoolrelation

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `sourcepool` | `int [not null]` |
| `targetpool` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_nspoolrelation"]`

### Relations

- Referenced by `nspools.id`
- Referenced by `nspools.id`

---

## nstemplates

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `field` | `int [not null]` |
| `worstvalue` | `float [not null]` |
| `bestvalue` | `float [not null]` |
| `note` | `nvarchar(MAX)` |

### Indexes

- `id [pk, name: "PK_aggregatevaluesrandomconfig"]`

### Relations

- Referenced by `aggregatefields.id`
- Referenced by `entitydefaults.definition`

---

## opp_reimburselog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `Id` | `"int IDENTITY(1,1)" [not null]` |
| `ReimburseTo` | `int [not null]` |
| `ReimburseBy` | `int [not null]` |
| `ReimburseTime` | `datetime [not null]` |
| `EntityId` | `int [not null]` |
| `ItemType` | `nvarchar(16) [not null]` |
| `Qty` | `int [not null]` |

---

## ownerincome

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `corporationeid` | `bigint [not null]` |
| `amount` | `float [not null]` |
| `lastflush` | `smalldatetime [not null, default: `getdate()`]` |

---

## packageitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `packageid` | `int [not null]` |
| `definition` | `int [not null]` |
| `quantity` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_packageitems"]`

### Relations

- Referenced by `packages.id`

---

## passablemappoints

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_passablemappoints"]`

---

## paymentproducts

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `productname` | `varchar(256) [not null]` |
| `priceUSD` | `float [not null, default: 0]` |
| `priceEUR` | `float [not null, default: 0]` |
| `note` | `nvarchar(2000)` |
| `available` | `bit [not null, default: 0]` |
| `hash` | `int [not null, default: 0]` |
| `priceFormerEUR` | `float` |
| `priceFormerUSD` | `float` |
| `timespan` | `int [not null, default: 0]` |
| `recurring` | `int [default: 0]` |
| `aws_Sku` | `varchar(64)` |
| `visible` | `bit [not null, default: 0]` |
| `trialonly` | `bit [not null, default: 0]` |
| `ingame` | `bit [not null, default: 0]` |
| `displayorder` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_paymentproducts"]`
- `productname [unique, name: "IX_paymentproducts"]`
- `hash [unique, name: "IX_paymentproducts_hash"]`

---

## paypal_transactions_history

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `ID` | `"int IDENTITY(1,1)" [not null]` |
| `transactionID` | `nvarchar(32) [not null]` |
| `transactionType` | `nvarchar(50) [not null]` |
| `paymentType` | `nvarchar(50) [not null]` |
| `orderTime` | `datetime [not null]` |
| `amt` | `float [not null]` |
| `currencyCode` | `nvarchar(3) [not null]` |
| `feeAmt` | `float [not null]` |
| `settleAmt` | `float [not null, default: 0]` |
| `taxAmt` | `float [not null]` |
| `exchangeRate` | `float [default: 1]` |
| `paymentStatus` | `nvarchar(50)` |
| `pendingReason` | `nvarchar(50)` |
| `reasonCode` | `nvarchar(50)` |
| `orderID` | `int [not null]` |

### Indexes

- `ID [pk, name: "PK_paypal_transactions_history"]`

---

## pbsconnections

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `sourceeid` | `bigint [not null]` |
| `targeteid` | `bigint [not null]` |
| `weight` | `float [not null, default: 1.0]` |

---

## pbslog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `corporationeid` | `bigint [not null]` |
| `nodeeid` | `bigint [not null]` |
| `nodedefinition` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `eventtype` | `int [not null]` |
| `issuercharacterid` | `int` |
| `takeovercorporationeid` | `bigint` |
| `othernodeeid` | `bigint` |
| `othernodedefinition` | `int` |
| `materialdefinition` | `int` |
| `materialamount` | `int` |
| `zoneid` | `int` |
| `killercharacterid` | `int` |
| `reinforcecounter` | `int` |

---

## pbsregisteredmembers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `characterid` | `int [not null]` |

---

## pbsreimburse

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `corporationeid` | `bigint` |
| `baseeid` | `bigint [not null]` |

---

## pbstrash

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `baseeid` | `bigint [not null]` |
| `waskilled` | `bit [not null, default: 0]` |
| `note` | `nvarchar(2048)` |

---

## plantdamagetype

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `plantdamagetype` | `int [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_plantdamagetype"]`
- `definition [unique, name: "IX_plantdamagetype"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## plantrules

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `idx` | `"int IDENTITY(1,1)" [not null]` |
| `plantrule` | `varchar(256) [not null]` |
| `rulesetid` | `int [not null, default: 0]` |
| `note` | `nvarchar(1024)` |

### Indexes

- `idx [pk, name: "PK_plantrules"]`
- `(plantrule, rulesetid) [unique, name: "IX_plantrules"]`

---

## plasma_gathered

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `gathered_on` | `date [not null]` |
| `plasma_type` | `varchar(100) [not null]` |
| `quantity` | `bigint [not null]` |

---

## plasma_gathered_daily

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `gathered_on` | `date [not null]` |
| `plasma_type` | `varchar(100) [not null]` |
| `quantity` | `bigint [not null]` |

---

## plasma_sold

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `sold_on` | `date [not null]` |
| `plasma_type` | `varchar(100) [not null]` |
| `quantity` | `bigint [not null]` |
| `income` | `float` |

---

## polls

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `pollid` | `"int IDENTITY(1,1)" [not null]` |
| `topic` | `nvarchar(MAX) [not null]` |
| `participation` | `int [not null, default: 55]` |
| `active` | `bit [not null, default: 1]` |
| `started` | `smalldatetime [not null, default: `getdate()`]` |
| `ended` | `smalldatetime` |

### Indexes

- `pollid [pk, name: "PK_polls"]`

### Relations

- `pollid` → `pollanswers.pollid`
- `pollid` → `pollchoices.pollid`

---

## pollanswers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `pollid` | `int [not null]` |
| `accountid` | `int [not null]` |
| `answerid` | `int [not null]` |

### Relations

- Referenced by `polls.pollid`

---

## pollchoices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `pollid` | `int [not null]` |
| `choiceid` | `"int IDENTITY(1,1)" [not null]` |
| `choicetext` | `nvarchar(MAX) [not null]` |

### Indexes

- `(pollid, choiceid) [unique, name: "IX_pollchoices"]`

### Relations

- Referenced by `polls.pollid`

---

## premadechatmessage

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `int [not null]` |
| `name` | `varchar(32) [not null]` |
| `message` | `nvarchar(2000) [not null]` |

### Indexes

- `id [pk]`
- `name [unique]`

---

## premademail

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `int [not null]` |
| `name` | `varchar(32) [not null]` |
| `subject` | `nvarchar(128) [not null]` |
| `body` | `nvarchar(2000) [not null]` |

### Indexes

- `id [pk]`
- `name [unique]`

---

## productioncost

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `category` | `bigint` |
| `tiertype` | `int` |
| `tierlevel` | `int` |
| `costmodifier` | `float [not null, default: 1.0]` |

### Indexes

- `id [pk]`

---

## productiondecalibration

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `categoryflag` | `bigint [not null]` |
| `distorsionmin` | `float [not null]` |
| `distorsionmax` | `float [not null]` |
| `decrease` | `float` |

### Indexes

- `id [pk, name: "PK_productiondecalibration_1"]`
- `categoryflag [unique, name: "IX_productiondecalibration_1"]`

### Relations

- Referenced by `categoryFlags.[value]`

---

## productionduration

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `category` | `bigint [not null]` |
| `durationmodifier` | `float [not null, default: 1]` |

### Indexes

- `category [unique, name: "IX_productionduration"]`

### Relations

- Referenced by `categoryFlags.[value]`

---

## productionlines

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `facilityeid` | `bigint [not null]` |
| `runningproductionid` | `int` |
| `targetdefinition` | `int [not null]` |
| `materialefficiency` | `float [not null, default: 0.5]` |
| `timeefficiency` | `float [not null, default: 0.5]` |
| `cycles` | `int [not null, default: 0]` |
| `rounds` | `int [not null, default: 1]` |
| `cprgeid` | `bigint` |

### Indexes

- `id [pk, name: "PK_productionlines"]`

---

## productionlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `definition` | `int [not null]` |
| `amount` | `int [not null]` |
| `productiontime` | `datetime [not null, default: `getdate()`]` |
| `productiontype` | `int [not null]` |
| `durationsecs` | `int [not null, default: 0]` |
| `price` | `float [not null, default: 0]` |
| `usecorporationwallet` | `bit [not null, default: 0]` |

---

## prototypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `prototype` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_prototypes"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## rarematerials

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `raredefinition` | `int [not null]` |
| `quantity` | `int [not null]` |
| `chance` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_rarematerials"]`

---

## raw_material_prices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `material_name` | `varchar(100) [not null]` |
| `price_nic` | `decimal(18,2) [not null]` |

### Indexes

- `material_name [pk]`

---

## reimbursementlog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `targetaccountid` | `int [not null]` |
| `definition` | `int [not null]` |
| `characterid` | `int [not null]` |
| `comment` | `varchar(512)` |
| `wasinsured` | `bit [not null, default: 0]` |
| `killedbyplayer` | `bit [not null, default: 0]` |
| `inactiveperiod` | `int [not null, default: 0]` |
| `dctime` | `datetime` |
| `deleted` | `datetime` |

---

## relays

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `relayname` | `varchar(50) [not null]` |
| `maxusers` | `int [not null]` |
| `currentusers` | `int [not null, default: 0]` |
| `ipaddress` | `varchar(32) [not null]` |
| `port` | `int [not null]` |

---

## relicloot

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `minquantity` | `int [not null]` |
| `maxquantity` | `int [not null]` |
| `chance` | `decimal(9,6) [not null]` |
| `relictypeid` | `int [not null]` |
| `packed` | `bit [not null]` |

### Indexes

- `id [pk]`

---

## relicspawninfo

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `relictypeid` | `int [not null]` |
| `zoneid` | `int [not null]` |
| `rate` | `int [not null]` |
| `x` | `int` |
| `y` | `int` |

### Indexes

- `id [pk]`

---

## relictypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |
| `raceid` | `int` |
| `level` | `int` |
| `ep` | `int` |

### Indexes

- `id [pk]`

---

## reliczoneconfig

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `maxspawn` | `int [not null]` |
| `respawnrate` | `int [not null]` |

### Indexes

- `id [pk]`

---

## resource_market_prices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `calculated_on` | `date [not null]` |
| `resource_name` | `varchar(100) [not null]` |
| `unit_price` | `decimal(18,2) [not null]` |

---

## resources_gathered

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `gathered_on` | `date [not null]` |
| `resource_name` | `varchar(100) [not null]` |
| `quantity` | `bigint [not null]` |

---

## resources_gathered_daily

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `gathered_on` | `date [not null]` |
| `resource_name` | `varchar(100) [not null]` |
| `quantity` | `bigint [not null]` |

---

## riftconfigs

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(100) [not null]` |
| `destinationGroupId` | `int` |
| `lifespanSeconds` | `int` |
| `maxUses` | `int` |
| `categoryExclusionGroupId` | `int` |

### Indexes

- `id [pk]`

---

## riftdestinations

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `groupId` | `int [not null]` |
| `zoneId` | `int [not null]` |
| `x` | `int` |
| `y` | `int` |
| `weight` | `int [not null, default: 1]` |

### Indexes

- `id [pk]`

---

## robotassembler

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `charactereid` | `bigint [not null]` |
| `facilityeid` | `bigint [not null]` |
| `head` | `bigint` |
| `chassis` | `bigint` |
| `leg` | `bigint` |

### Indexes

- `(characterid, facilityeid) [unique, name: "IX_robotassembler"]`

---

## robotfittingpresets

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `ownerEid` | `bigint [not null]` |
| `preset` | `varchar(MAX) [not null]` |

### Indexes

- `id [pk, name: "PK_robotpresets"]`

---

## robotsavedeffects

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `owner` | `bigint [not null]` |
| `effects` | `text` |

---

## robotsetup

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `robotshell` | `int [not null]` |
| `head` | `int [not null]` |
| `chassis` | `int [not null]` |
| `leg` | `int [not null]` |
| `container` | `int [not null]` |
| `hybridshell` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_robotsetup"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`
- Referenced by `entitydefaults.definition`

---

## robottemplates

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50) [not null]` |
| `description` | `varchar(MAX) [not null]` |
| `note` | `nvarchar(2000)` |

### Indexes

- `id [pk, name: "PK_robottemplates"]`
- `name [unique, name: "IX_robottemplates_name"]`

### Relations

- `id` → `robottemplaterelation.templateid`

---

## robottemplaterelation

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `definition` | `int [not null]` |
| `templateid` | `int [not null]` |
| `itemscoresum` | `int [not null, default: 0]` |
| `raceid` | `int [not null, default: 0]` |
| `missionlevel` | `int` |
| `missionleveloverride` | `int` |
| `killep` | `int` |
| `note` | `varchar(256)` |

### Indexes

- `definition [pk, name: "PK_robottemplaterelation"]`
- `(definition, templateid) [unique, name: "IX_robottemplaterelation"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `robottemplates.id`

---

## runningproduction

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterID` | `int [not null]` |
| `characterEID` | `bigint [not null]` |
| `resultDefinition` | `int [not null]` |
| `type` | `int [not null]` |
| `startTime` | `datetime [not null]` |
| `finishTime` | `datetime [not null]` |
| `facilityEID` | `bigint [not null]` |
| `totalProductionTime` | `int [not null]` |
| `baseEID` | `bigint [not null]` |
| `creditTaken` | `float [not null]` |
| `pricePerSecond` | `float [not null]` |
| `licenseAmount` | `int [not null, default: 0]` |
| `amountOfCycles` | `int [not null, default: 0]` |
| `useCorporationWallet` | `bit [not null]` |
| `paused` | `bit [not null, default: 0]` |
| `pausetime` | `datetime` |

### Indexes

- `id [pk, name: "PK_runningproduction"]`

---

## runningproductionreserveditem

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `runningid` | `int [not null]` |
| `reservedEID` | `bigint [not null]` |

---

## savedeffects

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `effects` | `varchar(MAX) [not null]` |

---

## season_activity_rates

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `season_id` | `int [not null]` |
| `activity_type` | `int [not null]` |
| `points_per_unit` | `float [not null]` |
| `unit_scale` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_season_activity_rates"]`

### Relations

- `season_id` → `seasons.id`

---

## season_character_points

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `character_id` | `int [not null]` |
| `season_id` | `int [not null]` |
| `total_points` | `float [not null, default: 0]` |
| `last_updated` | `datetime [not null, default: \`getutcdate()\`]` |
| `intro_mail_sent` | `bit [not null, default: 0]` |
| `leaderboard_reward_delivered` | `bit [not null, default: 0]` |

### Indexes

- `character_id, season_id [pk, name: "PK_season_character_points"]`
- `season_id, total_points [name: "IX_season_character_points_season"]`

### Relations

- `season_id` → `seasons.id`

---

## season_leaderboard_rewards

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `season_id` | `int [not null]` |
| `rank_min` | `int [not null]` |
| `rank_max` | `int [not null]` |
| `package_id` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_season_leaderboard_rewards"]`

### Relations

- `season_id` → `seasons.id`

---

## season_objective_progress

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `character_id` | `int [not null]` |
| `season_id` | `int [not null]` |
| `objective_id` | `int [not null]` |
| `day_window` | `date [not null, default: '1900-01-01']` |
| `current_value` | `float [not null, default: 0]` |
| `completed` | `bit [not null, default: 0]` |
| `completed_time` | `datetime` |
| `bonus_awarded` | `bit [not null, default: 0]` |

### Indexes

- `character_id, season_id, objective_id, day_window [pk, name: "PK_season_objective_progress"]`
- `character_id, season_id [name: "IX_season_objective_progress_char"]`

### Relations

- `season_id` → `seasons.id`
- `objective_id` → `season_objectives.id`

---

## season_objectives

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `season_id` | `int [not null]` |
| `name` | `varchar(128) [not null]` |
| `description` | `varchar(512) [not null, default: '']` |
| `activity_type` | `int [not null]` |
| `target_value` | `bigint [not null]` |
| `bonus_points` | `int [not null]` |
| `display_order` | `int [not null, default: 0]` |
| `is_daily` | `bit [not null, default: 0]` |
| `package_id` | `int [null]` |

### Indexes

- `id [pk, name: "PK_season_objectives"]`

### Relations

- `season_id` → `seasons.id`
- `id` → `season_objective_progress.objective_id`

---

## season_tier_claims

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `character_id` | `int [not null]` |
| `season_id` | `int [not null]` |
| `tier_id` | `int [not null]` |
| `claimed_time` | `datetime [not null, default: \`getutcdate()\`]` |

### Indexes

- `character_id, season_id, tier_id [pk, name: "PK_season_tier_claims"]`
- `character_id, season_id [name: "IX_season_tier_claims_char"]`

### Relations

- `season_id` → `seasons.id`
- `tier_id` → `season_tiers.id`

---

## season_tiers

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `season_id` | `int [not null]` |
| `tier_number` | `int [not null]` |
| `tier_name` | `varchar(64) [not null]` |
| `points_required` | `int [not null]` |
| `package_id` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_season_tiers"]`

### Relations

- `season_id` → `seasons.id`
- `id` → `season_tier_claims.tier_id`

---

## seasons

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |
| `description` | `varchar(512) [not null, default: '']` |
| `start_time` | `datetime [not null]` |
| `end_time` | `datetime [not null]` |
| `is_active` | `bit [not null, default: 0]` |
| `is_recurring` | `bit [not null, default: 0]` — enables auto-recurrence |
| `recurrence_gap_days` | `int [null]` — days between end of one run and start of next |
| `recurrence_iteration` | `int [not null, default: 1]` — which run this row represents |
| `recurrence_base_name` | `nvarchar(255) [null]` — operator-entered name; server appends `, Run #N` |
| `scoring_mode` | `tinyint [not null, default: 0]` — scoring mode (0 = ActivityAndGlobal, 1 = ObjectivesOnly) |
| `daily_objectives_per_day` | `smallint [null]` — when set, draw exactly N daily objectives per UTC day using a deterministic seed; NULL = all daily objectives active |

### Indexes

- `id [pk, name: "PK_seasons"]`

### Relations

- `id` → `season_activity_rates.season_id`
- `id` → `season_objectives.season_id`
- `id` → `season_tiers.season_id`
- `id` → `season_leaderboard_rewards.season_id`
- `id` → `season_character_points.season_id`
- `id` → `season_objective_progress.season_id`
- `id` → `season_tier_claims.season_id`

---

## serverinfo

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `name` | `nvarchar(512)` |
| `description` | `nvarchar(2048)` |
| `contact` | `nvarchar(512)` |
| `isopen` | `bit [not null, default: 0]` |
| `isbroadcast` | `bit [not null, default: 0]` |

---

## settings

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `varkey` | `varchar(64) [not null]` |
| `varvalue` | `varchar(512) [not null]` |
| `notes` | `varchar(1024)` |

### Indexes

- `varkey [pk, name: "PK_settings"]`

---

## siegeitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `minquantity` | `int [not null]` |
| `maxquantity` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_siegeitemchains"]`

### Relations

- Referenced by `entitydefaults.definition`

---

## slotFlags

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `offset` | `int [not null]` |
| `name` | `varchar(50) [not null]` |
| `note` | `nvarchar(2048)` |

---

## sparks

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `sparkname` | `varchar(128) [not null]` |
| `unlockprice` | `int` |
| `energycredit` | `int` |
| `standinglimit` | `float` |
| `definition` | `int` |
| `quantity` | `int` |
| `changeprice` | `int [not null, default: 0]` |
| `displayorder` | `int [not null]` |
| `defaultspark` | `bit [not null, default: 0]` |
| `icon` | `varchar(128)` |
| `hidden` | `bit [not null, default: 1]` |
| `note` | `nvarchar(1024)` |
| `allianceeid` | `bigint` |
| `alliancename` | `varchar(128)` |
| `unlockable` | `bit [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_sparks"]`
- `sparkname [unique, name: "IX_sparks"]`

### Relations

- `id` → `sparkextensions.sparkid`

---

## sparkextensions

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `sparkid` | `int [not null]` |
| `extensionid` | `int [not null]` |
| `extensionlevel` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_sparkextensions"]`

### Relations

- Referenced by `extensions.extensionid`
- Referenced by `sparks.id`

---

## standinglog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `characterid` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `actual` | `float [not null]` |
| `change` | `float [not null]` |
| `allianceeid` | `bigint [not null]` |
| `missionid` | `int` |

---

## standings

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `source` | `bigint [not null]` |
| `target` | `bigint [not null]` |
| `standing` | `float [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_standings"]`
- `(source, target) [unique, name: "IX_standings"]`

---

## steamkeys

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `accountid` | `int` |
| `steamid` | `varchar(64)` |
| `steamkey` | `varchar(32) [not null]` |
| `assigned` | `datetime` |

### Indexes

- `id [pk, name: "PK_steamkeys"]`

---

## steamkeyscomp

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `steamkey` | `varchar(32) [not null]` |
| `givenaway` | `date` |
| `note` | `nvarchar(2048)` |

### Indexes

- `id [pk, name: "PK_steamkeyscomp"]`

---

## storecategories

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |

### Indexes

- `id [pk, name: "PK_shopcategories"]`

### Relations

- `id` → `storeitems.category`

---

## storeitems

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `quantity` | `int [not null, default: 1]` |
| `price` | `int [not null]` |
| `category` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_shopitems"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `storecategories.id`

---

## strongholdexitconfig

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |
| `riftConfigId` | `int` |

### Indexes

- `id [pk]`

---

## techline

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50) [not null]` |
| `note` | `nvarchar(2000)` |

### Indexes

- `id [pk, name: "PK_techline"]`

### Relations

- `id` → `techlineincrement.techlineid`
- `id` → `techlinemember.techlineid`

---

## techlineincrement

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `techlineid` | `int [not null]` |
| `multiplier` | `float [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_techlineincrement"]`
- `(definition, techlineid) [unique, name: "IX_techlineincrement"]`

### Relations

- Referenced by `techline.id`
- Referenced by `entitydefaults.definition`

---

## techlinemember

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `techlineid` | `int [not null]` |
| `definition` | `int [not null]` |
| `position` | `int [not null]` |
| `points` | `float [not null]` |

### Indexes

- `id [pk, name: "PK_techlinemember"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `techline.id`

---

## techtree

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `parentdefinition` | `int [not null]` |
| `childdefinition` | `int [not null]` |
| `groupID` | `int [not null, default: 0]` |
| `x` | `int [not null, default: 0]` |
| `y` | `int [not null, default: 0]` |
| `enablerextensionid` | `int` |

### Indexes

- `id [pk, name: "PK_techtree"]`
- `(parentdefinition, childdefinition) [unique, name: "IX_parentchild"]`

---

## techtreegroups

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(64) [not null]` |
| `enablerextensionid` | `int` |
| `displayOrder` | `int` |

### Indexes

- `id [pk, name: "PK_techtreegroups"]`

---

## techtreelog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `logType` | `int [not null]` |
| `character` | `int [not null]` |
| `corporationEid` | `bigint` |
| `definition` | `int [not null]` |
| `quantity` | `int [not null]` |
| `pointType` | `int [not null]` |
| `amount` | `int [not null]` |
| `created` | `datetime [not null, default: `getdate()`]` |

### Indexes

- `id [pk, name: "PK_techtreelog"]`

---

## techtreenodeprices

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `pointtype` | `int [not null]` |
| `amount` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_techtreepoints"]`

---

## techtreepoints

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `owner` | `bigint [not null]` |
| `pointtype` | `int [not null]` |
| `amount` | `int [not null]` |

---

## techtreepointtypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(64) [not null]` |

### Indexes

- `id [pk, name: "PK_techtreepointtypes"]`

---

## techtreeunlockednodes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `definition` | `int [not null]` |
| `owner` | `bigint [not null]` |
| `created` | `datetime [not null, default: `getdate()`]` |

### Indexes

- `id [pk, name: "PK_techtreeunlockeddefinitions"]`

---

## terraformprojectregistration

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `projectid` | `int [not null]` |
| `characterid` | `int [not null]` |
| `role` | `int [not null]` |

### Indexes

- `(projectid, characterid) [unique, name: "IX_terraformprojectregistration_1"]`

---

## terraformprojects

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `title` | `nvarchar(512)` |
| `ownercharacterid` | `int [not null]` |
| `zoneid` | `int [not null]` |
| `topx` | `int [not null]` |
| `topy` | `int [not null]` |
| `bottomx` | `int [not null]` |
| `bottomy` | `int [not null]` |
| `version` | `int [not null, default: 0]` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `lastmodified` | `datetime [not null, default: `getdate()`]` |
| `validuntil` | `datetime [not null, default: `getdate()`]` |
| `data` | `varbinary(MAX)` |

### Indexes

- `id [pk, name: "PK_terraformprojects"]`

---

## tiertypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50)` |

### Indexes

- `id [pk, name: "PK_tiertypes"]`

---

## traceips

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `ip` | `varchar(50) [not null]` |
| `name` | `varchar(1024)` |

### Indexes

- `id [pk, name: "PK_traceips"]`

---

## traceroutelog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `accountid` | `int [not null]` |
| `sessionguid` | `varchar(64) [not null]` |
| `ip` | `varchar(32)` |
| `step` | `int [not null]` |
| `ipstatus` | `int [not null]` |
| `roundtriptime` | `bigint [not null]` |
| `tracetime` | `datetime [not null, default: `getdate()`]` |
| `fromclient` | `bit [not null, default: 0]` |
| `country` | `varchar(128)` |
| `tracedipid` | `int [not null, default: 0]` |

---

## trainingartifacts

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `artifactType` | `int [not null]` |
| `x` | `int [not null]` |
| `y` | `int [not null]` |

### Indexes

- `id [pk, name: "PK_trainingartifacts"]`

---

## trainingrewards

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `level` | `int [not null, default: 0]` |
| `definition` | `int` |
| `quantity` | `int` |
| `robottemplateid` | `int` |
| `raceid` | `int [not null, default: 1]` |

### Indexes

- `id [pk, name: "PK_trainingrewards"]`

---

## transactiontypes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `[value]` | `int [not null]` |
| `name` | `varchar(256) [not null]` |

### Indexes

- `"[value]" [pk, name: "PK_transactiontypes"]`

---

## transportassignments

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `creation` | `datetime [not null, default: `getdate()`]` |
| `sourcebaseeid` | `bigint [not null]` |
| `targetbaseeid` | `bigint [not null]` |
| `ownercharacterid` | `int [not null]` |
| `reward` | `bigint [not null]` |
| `collateral` | `bigint [not null]` |
| `taken` | `bit [not null, default: 0]` |
| `volunteercharacterid` | `int` |
| `containereid` | `bigint [not null]` |
| `containername` | `varchar(10) [not null]` |
| `volume` | `float [not null]` |
| `expiry` | `datetime [not null]` |
| `started` | `datetime` |
| `retrieved` | `bit [not null, default: 0]` |

### Indexes

- `id [pk, name: "PK_transportassignments"]`

---

## transportassignmentslog

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `assignmentevent` | `int [not null]` |
| `baseeid` | `bigint [not null]` |
| `ownercharacterid` | `int [not null]` |
| `volunteercharacterid` | `int` |
| `assignmentid` | `int [not null]` |
| `containername` | `varchar(10) [not null]` |

---

## transportassignmenttimes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `characterid` | `int [not null]` |
| `eventtime` | `datetime [not null, default: `getdate()`]` |
| `sourcebase` | `bigint [not null]` |
| `targetbase` | `bigint [not null]` |
| `distance` | `float [not null]` |
| `volume` | `float [not null]` |
| `totalseconds` | `float [not null]` |
| `multiplier` | `float [not null]` |

---

## usercount

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `time` | `datetime [not null, default: `getdate()`]` |
| `usercount` | `int [not null]` |

---

## vendorpresets

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `nvarchar(50) [not null]` |

### Indexes

- `id [pk, name: "PK_vendorpresets"]`

### Relations

- `id` → `vendorpresetvalues.presetid`

---

## vendorpresetvalues

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `idx` | `"int IDENTITY(1,1)" [not null]` |
| `presetid` | `int [not null]` |
| `definition` | `int [not null]` |
| `issell` | `bit [not null, default: 1]` |
| `price` | `float [not null, default: 10]` |
| `quantity` | `int [not null, default: 1]` |
| `duration` | `int [not null]` |

### Indexes

- `idx [pk, name: "PK_vendorpresetvalues"]`

### Relations

- Referenced by `entitydefaults.definition`
- Referenced by `vendorpresets.id`

---

## vendors

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `vendorEID` | `bigint [not null]` |
| `marketEID` | `bigint` |
| `vendorsellprofit` | `float [not null, default: 1]` |
| `vendorbuyprofit` | `float [not null, default: 1]` |
| `note` | `nchar(2048)` |

### Indexes

- `(vendorEID, marketEID) [unique, name: "IX_vendors"]`

---

## votes

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `voteid` | `"int IDENTITY(1,1)" [not null]` |
| `groupEID` | `bigint [not null]` |
| `votename` | `nvarchar(50) [not null]` |
| `votetopic` | `nvarchar(2048)` |
| `participation` | `int [not null, default: 1]` |
| `votetype` | `int [not null, default: 0]` |
| `closed` | `bit [not null, default: 0]` |
| `startdate` | `smalldatetime [not null, default: `getdate()`]` |
| `enddate` | `smalldatetime` |
| `result` | `bit` |
| `startedby` | `int [not null]` |
| `consensusrate` | `int [not null, default: 50]` |

### Indexes

- `voteid [pk, name: "PK_votes_1"]`

### Relations

- `voteid` → `voteentries.voteid`

---

## voteentries

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `voteid` | `int [not null]` |
| `characterid` | `int [not null]` |
| `voteentry` | `bit [not null]` |
| `entrydate` | `smalldatetime [not null, default: `getdate()`]` |

### Relations

- Referenced by `votes.voteid`

---

## yellowpages

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `ID` | `"int IDENTITY(1,1)" [not null]` |
| `corporationEID` | `bigint [not null]` |
| `primaryActivity` | `int [not null, default: 0]` |
| `zoneID` | `int` |
| `baseEID` | `bigint` |
| `orientation` | `int [not null, default: 0]` |
| `lookingFor` | `int [not null, default: 0]` |
| `preferredFaction` | `int` |
| `providesInsurance` | `int [not null, default: 0]` |
| `timeZone` | `int [not null, default: 0]` |
| `requiredActivity` | `int [not null, default: 0]` |
| `communication` | `int [not null, default: 0]` |
| `services` | `int [not null, default: 0]` |

### Indexes

- `ID [pk, name: "PK_yellowpages"]`
- `corporationEID [unique, name: "IX_yellowpages"]`

---

## zoneeffects

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `effectid` | `int [not null]` |

### Indexes

- `id [pk]`

---

## zoneentities

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneID` | `int [not null]` |
| `eid` | `bigint` |
| `definition` | `int` |
| `owner` | `bigint` |
| `ename` | `varchar(128)` |
| `x` | `float [not null]` |
| `y` | `float [not null]` |
| `z` | `float [not null]` |
| `orientation` | `tinyint [not null, default: 0]` |
| `enabled` | `bit [not null, default: 1]` |
| `note` | `nvarchar(2000)` |
| `runtime` | `bit [not null, default: 0]` |
| `synckey` | `varchar(50)` |

### Indexes

- `id [pk, name: "PK_zoneentities"]`
- `eid [unique, name: "IX_zoneentities_eid_uk"]`

### Relations

- Referenced by `zones.id`

---

## zoneriftsconfig

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `zoneid` | `int [not null]` |
| `maxrifts` | `int [not null]` |
| `maxlevel` | `int [not null]` |

### Indexes

- `id [pk]`
- `zoneid [unique]`

---

## zonesectors

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(50) [not null]` |
| `zoneid` | `int [not null]` |
| `sector` | `varbinary(512) [not null]` |

### Indexes

- `id [pk, name: "PK_zonesectors"]`
- `name [unique, name: "IX_zonesectors_name"]`

### Relations

- Referenced by `zones.id`

---

## zoneteleportdevicemap

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `sourcedefinition` | `int [not null]` |
| `zoneid` | `int [not null]` |

### Indexes

- `id [pk]`

---

## zoneuserentities

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `eid` | `bigint [not null]` |
| `zoneid` | `int [not null]` |
| `x` | `float [not null]` |
| `y` | `float [not null]` |
| `z` | `float [not null]` |
| `orientation` | `tinyint [not null, default: 0]` |

### Indexes

- `eid [pk, name: "PK_zoneuserentities"]`

---
