// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.VoiceChat.Server;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;

namespace Content.VoiceChat.Server.Entry;

public sealed class EntryPoint : GameServer
{
    private VoiceChatManager? _voice;
    private VoiceLogManager? _logs;

    public override void PreInit()
    {
        var deps = Dependencies ?? IoCManager.Instance!;
        deps.Register<VoiceChatManager>();
        deps.Register<VoiceLogManager>();
        deps.Register<VoiceFeatures>();
    }

    public override void PostInit()
    {
        base.PostInit();

        _voice = IoCManager.Resolve<VoiceChatManager>();
        _voice.Initialize();
        _logs = IoCManager.Resolve<VoiceLogManager>();
        _logs.Initialize();
    }

    public override void Shutdown()
    {
        _voice?.Shutdown();
        _logs?.Shutdown();
        base.Shutdown();
    }
}
