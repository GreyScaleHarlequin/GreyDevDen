// SPDX-FileCopyrightText: 2023 Debug <49997488+DebugOk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Damage.ForceSay;

/// <summary>
///     Sent to clients as a network event when their entity contains <see cref="DamageForceSayComponent"/>
///     that COMMANDS them to speak the current message in their chatbox
/// </summary>
[Serializable, NetSerializable]
public sealed class DamageForceSayEvent : EntityEventArgs
{
    public string? Suffix;
}
