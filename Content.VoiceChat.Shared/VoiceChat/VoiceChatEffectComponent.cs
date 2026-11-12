// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.VoiceChat.Shared;

public enum VoiceEffect : byte
{
    None,
    Robot,
    Alien,
    Deep,
    High,
    Radio,
    Muffled,
    Abductor,
    Whisper,
}

[RegisterComponent]
public sealed partial class VoiceChatEffectComponent : Component
{
    [DataField]
    public VoiceEffect Effect = VoiceEffect.None;
}
