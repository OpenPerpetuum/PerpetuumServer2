# Season Intro Mail Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix three gaps in the Season intro-mail flow: online players miss the mail at activation time; docked-but-logged-in players miss the mail entirely; and the email body contains no useful content about the season.

**Architecture:** All changes live in `SeasonService` (the single authoritative mail sender) and the single hook site in `Player.cs`. Task 1 fixes the activation path. Task 2 moves the login hook from zone-entry to character-selection so docked players are covered. Task 3 rewrites `SendIntroMail` with a rich body and injects `ICustomDictionary` for item-name translation.

**Tech Stack:** .NET 8 / C#, Autofac DI, `ISessionManager` session events, `ICustomDictionary`, `EntityDefault.Reader`.

---

## File Structure

| File | Change |
|---|---|
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | All three tasks: fix activation path, wire session events, rich email body |
| `src/Perpetuum/Players/Player.cs` | Task 2 only: remove `OnEnterZone` hook |

`SeasonModule.cs` needs no changes — Autofac resolves `ICustomDictionary` from the constructor automatically.

---

## Task 1: Fix activation mail for online players

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

The bug: `SendActivationMailToOnlineCharacters` sends mail without calling `RefreshCache()` first
(so `_activeRates`/`_activeTiers`/`_activeObjectives` are empty stale snapshots) and without calling
`TryMarkIntroMailSent` (so online players receive a duplicate on their next reconnect).

- [ ] **Step 1: Replace `SendActivationMailToOnlineCharacters`**

Find this method (currently at the bottom of `SeasonService.cs`):

```csharp
public void SendActivationMailToOnlineCharacters(Season season)
{
    foreach (var character in _sessionManager.SelectedCharacters)
    {
        if (character == null || character == Character.None)
            continue;

        SendIntroMail(character, season);
    }
}
```

Replace it entirely with:

```csharp
public void SendActivationMailToOnlineCharacters(Season season)
{
    RefreshCache();
    var freshSeason = _activeSeason;
    if (freshSeason == null) return;

    foreach (var character in _sessionManager.SelectedCharacters)
    {
        if (character == null || character == Character.None)
            continue;

        if (_repository.TryMarkIntroMailSent(character.Id, freshSeason.Id))
            SendIntroMail(character, freshSeason);
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "fix(seasons): refresh cache and mark intro-mail flag before sending to online players at activation"
```

---

## Task 2: Move login hook from zone-entry to character-selection

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`
- Modify: `src/Perpetuum/Players/Player.cs`

The bug: `OnCharacterLogin` is hooked in `Player.OnEnterZone`, which only fires when a player
deploys into a zone. Players who log in and stay docked in a terminal never trigger it.

The fix: subscribe to `ISessionManager.SessionAdded` in the `SeasonService` constructor, and for
each session subscribe to `session.CharacterSelected`. `CharacterSelected` fires as soon as a
character is picked from the character screen — before any zone is entered.

`SessionEventHandler` signature: `delegate void SessionEventHandler(ISession session)`
`SessionEventHandler<T>` signature: `delegate void SessionEventHandler<in T>(ISession session, T args)`

Both events are already declared on `ISessionManager` and `ISession`.

- [ ] **Step 1: Add `OnSessionAdded` private method to `SeasonService`**

Insert this method anywhere in the private section of `SeasonService` (e.g., after the constructor):

```csharp
private void OnSessionAdded(ISession session)
{
    session.CharacterSelected += (_, character) => OnCharacterLogin(character);
}
```

- [ ] **Step 2: Wire `SessionAdded` in the `SeasonService` constructor**

The constructor currently reads:

```csharp
public SeasonService(SeasonRepository repository, ISessionManager sessionManager)
{
    _repository     = repository;
    _sessionManager = sessionManager;
}
```

Add the event subscription at the end of the constructor body (no DB calls involved):

```csharp
public SeasonService(SeasonRepository repository, ISessionManager sessionManager)
{
    _repository     = repository;
    _sessionManager = sessionManager;
    _sessionManager.SessionAdded += OnSessionAdded;
}
```

You also need to add `using Perpetuum.Services.Sessions;` if it is not already present at the top
of the file. Check existing usings — it is already there (`using Perpetuum.Services.Sessions;` on
line 6), so no change needed.

- [ ] **Step 3: Remove the `OnEnterZone` hook from `Player.cs`**

Open `src/Perpetuum/Players/Player.cs`. Find (around line 922):

```csharp
protected override void OnEnterZone(IZone zone, ZoneEnterType enterType)
{
    base.OnEnterZone(zone, enterType);
    SeasonServiceLocator.Instance?.OnCharacterLogin(Character);
    check = PlayerMoveCheckQueue.Create(this, CurrentPosition);
```

Remove the season hook line so it reads:

```csharp
protected override void OnEnterZone(IZone zone, ZoneEnterType enterType)
{
    base.OnEnterZone(zone, enterType);
    check = PlayerMoveCheckQueue.Create(this, CurrentPosition);
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git add src/Perpetuum/Players/Player.cs
git commit -m "fix(seasons): move intro-mail hook from OnEnterZone to CharacterSelected so docked players are covered"
```

---

## Task 3: Rich intro email body with objectives, tiers, and reward items

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

Adds `ICustomDictionary` constructor injection, a `Translate` helper, an `ActivityTypeName` helper,
and rewrites `SendIntroMail` to include scoring rates, objectives, and tier rewards with translated
item names and quantities.

`EntityDefault.Reader.Get(int definition)` returns an `EntityDefault` whose `.Name` property is the
dictionary key (e.g. `"def_syndicate_novice_license"`). Look up that key in the English dictionary
(language `0`) to get the display name. Fall back to the key itself if missing.

- [ ] **Step 1: Add `using` directives and the `_customDictionary` field**

At the top of `SeasonService.cs`, the current usings are:

```csharp
using System;
using System.Collections.Immutable;
using System.Linq;
using Perpetuum.Accounting.Characters;
using Perpetuum.Services.Mail;
using Perpetuum.Services.Sessions;
using Perpetuum.Threading.Process;
```

Replace with (adds `System.Collections.Generic`, `System.Text`, and `Perpetuum.EntityFramework`):

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Perpetuum.Accounting.Characters;
using Perpetuum.EntityFramework;
using Perpetuum.Services.Mail;
using Perpetuum.Services.Sessions;
using Perpetuum.Threading.Process;
```

Add the field alongside the other readonly fields (after `_sessionManager`):

```csharp
private readonly ISessionManager    _sessionManager;
private readonly ICustomDictionary  _customDictionary;
private readonly Lazy<Character>    _announcer = new(() => Character.GetByNick(AnnouncerNick));
```

- [ ] **Step 2: Update the constructor signature to inject `ICustomDictionary`**

Change the constructor from:

```csharp
public SeasonService(SeasonRepository repository, ISessionManager sessionManager)
{
    _repository     = repository;
    _sessionManager = sessionManager;
    _sessionManager.SessionAdded += OnSessionAdded;
}
```

To:

```csharp
public SeasonService(SeasonRepository repository, ISessionManager sessionManager,
    ICustomDictionary customDictionary)
{
    _repository       = repository;
    _sessionManager   = sessionManager;
    _customDictionary = customDictionary;
    _sessionManager.SessionAdded += OnSessionAdded;
}
```

Autofac resolves `ICustomDictionary` automatically — `CustomDictionary` is already registered as
`ICustomDictionary` singleton in `PerpetuumBootstrapper.InitContainer` via:
```csharp
_builder.RegisterType<CustomDictionary>().As<ICustomDictionary>().SingleInstance().AutoActivate();
```
No module change needed.

- [ ] **Step 3: Add `Translate` and `ActivityTypeName` helpers**

Add these two private static methods anywhere in the private section of `SeasonService`:

```csharp
private static string Translate(string key, Dictionary<string, object>? dict)
{
    if (dict != null && dict.TryGetValue(key, out var val) && val is string s && s.Length > 0)
        return s;
    return key;
}

private static string ActivityTypeName(SeasonActivityType type) => type switch
{
    SeasonActivityType.NpcKill         => "NPC Kill",
    SeasonActivityType.PvpKill         => "PvP Kill",
    SeasonActivityType.MissionComplete => "Mission Completed",
    SeasonActivityType.MineralMined    => "Mineral Mined",
    SeasonActivityType.EpSpent         => "EP Spent",
    SeasonActivityType.NicEarned       => "NIC Earned",
    SeasonActivityType.NicSpent        => "NIC Spent",
    SeasonActivityType.IntrusionPoint  => "Intrusion SAP",
    _                                  => type.ToString(),
};
```

- [ ] **Step 4: Rewrite `SendIntroMail`**

Find the current `SendIntroMail`:

```csharp
private void SendIntroMail(Character character, Season season)
{
    string subject = $"Season Active: {season.Name}";
    string body    = $"{season.Description}\n\nSeason ends: {season.EndTime:yyyy-MM-dd HH:mm} UTC";
    MailHandler.SendMail(_announcer.Value, character, subject, body,
        MailType.character, out _, out _);
}
```

Replace it with:

```csharp
private void SendIntroMail(Character character, Season season)
{
    var dict = _customDictionary.GetDictionary(0);
    var sb   = new StringBuilder();

    if (!string.IsNullOrWhiteSpace(season.Description))
        sb.AppendLine(season.Description).AppendLine();

    sb.AppendLine($"Season ends: {season.EndTime:yyyy-MM-dd HH:mm} UTC");

    var rates = _activeRates;
    if (rates.Count > 0)
    {
        sb.AppendLine().AppendLine("-- Scoring --");
        foreach (var rate in rates)
        {
            string unitDesc = rate.UnitScale > 1 ? $" per {rate.UnitScale:N0}" : "";
            sb.AppendLine($"  {ActivityTypeName(rate.ActivityType)}: {rate.PointsPerUnit:G} pts{unitDesc}");
        }
    }

    var objectives = _activeObjectives;
    if (objectives.Count > 0)
    {
        sb.AppendLine().AppendLine("-- Objectives --");
        foreach (var obj in objectives.OrderBy(o => o.DisplayOrder))
            sb.AppendLine($"  {obj.Name}: reach {obj.TargetValue:N0} {ActivityTypeName(obj.ActivityType)} → +{obj.BonusPoints} pts bonus");
    }

    var tiers = _activeTiers;
    if (tiers.Count > 0)
    {
        sb.AppendLine().AppendLine("-- Tier Rewards --");
        foreach (var tier in tiers)
        {
            sb.AppendLine($"  {tier.TierName} ({tier.PointsRequired:N0} pts):");
            foreach (var item in _repository.GetPackageItems(tier.PackageId))
            {
                var ed   = EntityDefault.Reader.Get(item.Definition);
                string name = (ed != null && ed != EntityDefault.None)
                    ? Translate(ed.Name, dict)
                    : item.Definition.ToString();
                sb.AppendLine($"    - {name} x{item.Quantity}");
            }
        }
    }

    MailHandler.SendMail(_announcer.Value, character, $"Season Active: {season.Name}",
        sb.ToString(), MailType.character, out _, out _);
}
```

- [ ] **Step 5: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): enrich intro email with scoring rates, objectives, and tier reward items with translated names"
```
