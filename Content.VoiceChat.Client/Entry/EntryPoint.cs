// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.VoiceChat.Client;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;

namespace Content.VoiceChat.Client.Entry;

public sealed class EntryPoint : GameClient
{
    private VoiceChatManager? _voice;

    public override void PreInit()
    {
        var deps = Dependencies ?? IoCManager.Instance!;
        deps.Register<VoiceChatManager>();
    }

    public override void PostInit()
    {
        base.PostInit();
        _voice = IoCManager.Resolve<VoiceChatManager>();
        _voice.Initialize();
    }

    public override void Shutdown()
    {
        _voice?.Shutdown();
        base.Shutdown();
    }
}
