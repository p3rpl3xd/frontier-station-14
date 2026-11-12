// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Components;
using Content.Shared.Inventory;
using Content.VoiceChat.Server;
using Content.VoiceChat.Shared;

namespace Content.VoiceChat.Server;

public sealed partial class PressureMuffleFeature : EntitySystem, IVoiceFeature
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private VoiceFeatures _features = default!;

    public override void Initialize()
    {
        base.Initialize();
        _features.Add(this);
    }

    public void AdjustMuffle(EntityUid speaker, ref VoiceMuffle muffle)
    {
        if (_inventory.TryGetSlotEntity(speaker, "head", out var head) && HasComp<PressureProtectionComponent>(head))
            muffle = muffle == VoiceMuffle.Heavy ? muffle : VoiceMuffle.Light;
    }
}
