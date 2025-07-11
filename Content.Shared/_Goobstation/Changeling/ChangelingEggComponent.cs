// SPDX-FileCopyrightText: 2025 Eris <eris@erisws.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Shared.Store.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Changeling;

[RegisterComponent, NetworkedComponent]

public sealed partial class ChangelingEggComponent : Component
{
    public ChangelingComponent LingComp;
    public EntityUid LingMind;
    public StoreComponent LingStore;
    public bool AugmentedEyesightPurchased;

    /// <summary>
    ///     Countdown before spawning monkey.
    /// </summary>
    public TimeSpan UpdateTimer = TimeSpan.Zero;

    [DataField]
    public float UpdateCooldown = 120f;
    public bool Active;

    [DataField]
    public EntProtoId MobToSpawn = "MobMonkey";
}
