// SPDX-FileCopyrightText: 2022 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2022 Veritius <veritiusgaming@gmail.com>
// SPDX-FileCopyrightText: 2023 Debug <49997488+DebugOk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 ElectroJr <leonsfriedrich@gmail.com>
// SPDX-FileCopyrightText: 2023 Jezithyr <jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Moony <moony@hellomouse.net>
// SPDX-FileCopyrightText: 2023 ShadowCommander <10494922+ShadowCommander@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Skye <22365940+Skyedra@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Errant <35878406+Errant-4@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 FoxxoTrystan <45297731+FoxxoTrystan@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 RedFoxIV <38788538+RedFoxIV@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 VMSolidus <evilexecutive@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Afk;
using Content.Server.Afk.Events;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Station.Events;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.Customization.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Players;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Players.PlayTimeTracking;

/// <summary>
/// Connects <see cref="PlayTimeTrackingManager"/> to the simulation state. Reports trackers and such.
/// </summary>
public sealed class PlayTimeTrackingSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IAfkManager _afk = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly CharacterRequirementsSystem _characterRequirements = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;

    public override void Initialize()
    {
        base.Initialize();

        _tracking.CalcTrackers += CalcTrackers;

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleEvent);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleEvent);
        SubscribeLocalEvent<AFKEvent>(OnAFK);
        SubscribeLocalEvent<UnAFKEvent>(OnUnAFK);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnStationJobsGetCandidates);
        SubscribeLocalEvent<IsJobAllowedEvent>(OnIsJobAllowed);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
        _adminManager.OnPermsChanged += AdminPermsChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _tracking.CalcTrackers -= CalcTrackers;
        _adminManager.OnPermsChanged -= AdminPermsChanged;
    }

    private void CalcTrackers(ICommonSession player, HashSet<string> trackers)
    {
        if (_afk.IsAfk(player))
            return;

        if (_adminManager.IsAdmin(player))
        {
            trackers.Add(PlayTimeTrackingShared.TrackerAdmin);
            trackers.Add(PlayTimeTrackingShared.TrackerOverall);
            return;
        }

        if (!IsPlayerAlive(player))
            return;

        trackers.Add(PlayTimeTrackingShared.TrackerOverall);
        trackers.UnionWith(GetTimedRoles(player));
    }

    private bool IsPlayerAlive(ICommonSession session)
    {
        var attached = session.AttachedEntity;
        if (attached == null)
            return false;

        if (!TryComp<MobStateComponent>(attached, out var state))
            return false;

        return state.CurrentState is MobState.Alive or MobState.Critical;
    }

    public IEnumerable<string> GetTimedRoles(EntityUid mindId)
    {
        foreach (var role in _roles.MindGetAllRoleInfo(mindId))
        {
            if (string.IsNullOrWhiteSpace(role.PlayTimeTrackerId))
                continue;

            yield return _prototypes.Index<PlayTimeTrackerPrototype>(role.PlayTimeTrackerId).ID;
        }
    }

    private IEnumerable<string> GetTimedRoles(ICommonSession session)
    {
        var contentData = _playerManager.GetPlayerData(session.UserId).ContentData();

        if (contentData?.Mind == null)
            return Enumerable.Empty<string>();

        return GetTimedRoles(contentData.Mind.Value);
    }

    private void OnRoleEvent(RoleEvent ev)
    {
        if (_minds.TryGetSession(ev.Mind, out var session))
            _tracking.QueueRefreshTrackers(session);
    }

    private void OnRoundEnd(RoundRestartCleanupEvent ev)
    {
        _tracking.Save();
    }

    private void OnUnAFK(ref UnAFKEvent ev)
    {
        _tracking.QueueRefreshTrackers(ev.Session);
    }

    private void OnAFK(ref AFKEvent ev)
    {
        _tracking.QueueRefreshTrackers(ev.Session);
    }

    private void AdminPermsChanged(AdminPermsChangedEventArgs admin)
    {
        _tracking.QueueRefreshTrackers(admin.Player);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        _tracking.QueueRefreshTrackers(ev.Player);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        // This doesn't fire if the player doesn't leave their body. I guess it's fine?
        _tracking.QueueRefreshTrackers(ev.Player);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!TryComp(ev.Target, out ActorComponent? actor))
            return;

        _tracking.QueueRefreshTrackers(actor.PlayerSession);
    }

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        RemoveDisallowedJobs(ev.Player, ev.Jobs);
    }

    private void OnIsJobAllowed(ref IsJobAllowedEvent ev)
    {
        if (!IsAllowed(ev.Player, ev.JobId))
            ev.Cancelled = true;
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        ev.Jobs.UnionWith(GetDisallowedJobs(ev.Player));
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        _tracking.QueueRefreshTrackers(ev.PlayerSession);
        // Send timers to client when they join lobby, so the UIs are up-to-date.
        _tracking.QueueSendTimers(ev.PlayerSession);
        _tracking.QueueSendWhitelist(ev.PlayerSession); // Nyanotrasen - Send whitelist status
    }

    public bool IsAllowed(ICommonSession player, string role)
    {
        if (!_prototypes.TryIndex<JobPrototype>(role, out var job) ||
            job.Requirements == null ||
            !_cfg.GetCVar(CCVars.GameRoleTimers))
            return true;

        if (!_tracking.TryGetTrackerTimes(player, out var playTimes))
        {
            Log.Error($"Unable to check playtimes {Environment.StackTrace}");
            playTimes = new Dictionary<string, TimeSpan>();
        }

        var isWhitelisted = player.ContentData()?.Whitelisted ?? false; // DeltaV - Whitelist requirement

        return _characterRequirements.CheckRequirementsValid(
            job.Requirements,
            job,
            (HumanoidCharacterProfile) _prefs.GetPreferences(player.UserId).SelectedCharacter,
            playTimes,
            isWhitelisted,
            job,
            EntityManager,
            _prototypes,
            _cfg,
            out _);
    }

    public HashSet<ProtoId<JobPrototype>> GetDisallowedJobs(ICommonSession player)
    {
        var roles = new HashSet<ProtoId<JobPrototype>>();
        if (!_cfg.GetCVar(CCVars.GameRoleTimers))
            return roles;

        if (!_tracking.TryGetTrackerTimes(player, out var playTimes))
        {
            Log.Error($"Unable to check playtimes {Environment.StackTrace}");
            playTimes = new Dictionary<string, TimeSpan>();
        }

        var isWhitelisted = player.ContentData()?.Whitelisted ?? false; // DeltaV - Whitelist requirement

        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (job.Requirements != null)
            {
                if (_characterRequirements.CheckRequirementsValid(
                        job.Requirements,
                        job,
                        (HumanoidCharacterProfile) _prefs.GetPreferences(player.UserId).SelectedCharacter,
                        playTimes,
                        isWhitelisted,
                        job,
                        EntityManager,
                        _prototypes,
                        _cfg,
                        out _))
                    continue;

                goto NoRole;
            }

            roles.Add(job.ID);
            NoRole:;
        }

        return roles;
    }

    public void RemoveDisallowedJobs(NetUserId userId, List<ProtoId<JobPrototype>> jobs)
    {
        if (!_cfg.GetCVar(CCVars.GameRoleTimers))
            return;

        var player = _playerManager.GetSessionById(userId);
        if (!_tracking.TryGetTrackerTimes(player, out var playTimes))
        {
            // Sorry mate but your playtimes haven't loaded.
            Log.Error($"Playtimes weren't ready yet for {player} on roundstart!");
            playTimes ??= new Dictionary<string, TimeSpan>();
        }

        var isWhitelisted = player.ContentData()?.Whitelisted ?? false; // DeltaV - Whitelist requirement

        for (var i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];

            if (!_prototypes.TryIndex(job, out var jobber) ||
                jobber.Requirements == null ||
                jobber.Requirements.Count == 0)
                continue;

            if (!_characterRequirements.CheckRequirementsValid(
                jobber.Requirements,
                jobber,
                (HumanoidCharacterProfile) _prefs.GetPreferences(userId).SelectedCharacter,
                _tracking.GetPlayTimes(_playerManager.GetSessionById(userId)),
                _playerManager.GetSessionById(userId).ContentData()?.Whitelisted ?? false,
                jobber,
                EntityManager,
                _prototypes,
                _cfg,
                out _))
            {
                jobs.RemoveSwap(i);
                i--;
            }
        }
    }

    public void PlayerRolesChanged(ICommonSession player)
    {
        _tracking.QueueRefreshTrackers(player);
    }
}
