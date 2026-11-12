// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.VoiceChat.Shared;

public enum VoiceMuffle : byte
{
    None,
    Light,
    Heavy,
}

public readonly record struct VoiceEffectSettings(VoiceEffect Effect, VoiceMuffle Muffle, bool Megaphone = false);
