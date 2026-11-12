// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Speech;
using Robust.Shared.GameObjects;

namespace Content.VoiceChat.Server;

/// <summary>
/// Content types whose namespace moved between forks. Resolved by name so the module
/// compiles against both <c>Content.Shared.Ghost</c> and <c>Content.Shared.Ghost.Components</c>.
/// </summary>
public static class VoiceContentTypes
{
    private static readonly Type? Ghost = typeof(SpeechComponent).Assembly.GetType("Content.Shared.Ghost.Components.GhostComponent")
        ?? typeof(SpeechComponent).Assembly.GetType("Content.Shared.Ghost.GhostComponent");

    public static bool IsGhost(IEntityManager entities, EntityUid uid)
    {
        return Ghost != null && entities.HasComponent(uid, Ghost);
    }
}
