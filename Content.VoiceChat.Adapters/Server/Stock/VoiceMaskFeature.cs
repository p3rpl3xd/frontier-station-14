// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

#if VOICECHAT_UPSTREAM
using Content.Shared.VoiceMask;
#else
using Content.Server.VoiceMask;
#endif
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.VoiceChat.Server;

namespace Content.VoiceChat.Server;

public sealed partial class VoiceMaskFeature : EntitySystem, IVoiceFeature
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private VoiceFeatures _features = default!;

    public override void Initialize()
    {
        base.Initialize();
        _features.Add(this);
    }

    public bool HasVoiceChanger(EntityUid uid)
    {
        if (_inventory.TryGetContainerSlotEnumerator(uid, out var slots, SlotFlags.WITHOUT_POCKET))
        {
            while (slots.NextItem(out var item))
            {
                if (IsActive(item))
                    return true;
            }
        }

        if (TryComp<ImplantedComponent>(uid, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities)
            {
                if (IsActive(implant))
                    return true;
            }
        }

        return false;
    }

    public void DescribeState(EntityUid speaker, VoiceStateHints hints)
    {
        if (HasVoiceChanger(speaker))
            hints.VoiceChanger = true;
    }

    private bool IsActive(EntityUid uid)
    {
#if VOICECHAT_SERVER_COMPONENTS
        return HasComp<VoiceMaskComponent>(uid);
#else
        return TryComp<VoiceMaskComponent>(uid, out var mask) && mask.Active;
#endif
    }
}
