// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.VoiceChat.Server;

namespace Content.VoiceChat.Effects;

public sealed class VoiceEffectSystem : EntitySystem
{
    [Dependency] private VoiceFeatures _features = default!;

    private readonly VoiceEffectProcessor _processor = new();

    public override void Initialize()
    {
        base.Initialize();
        _features.Pcm = _processor;
    }

    public override void Shutdown()
    {
        if (_features.Pcm == _processor)
            _features.Pcm = null;

        base.Shutdown();
    }
}
