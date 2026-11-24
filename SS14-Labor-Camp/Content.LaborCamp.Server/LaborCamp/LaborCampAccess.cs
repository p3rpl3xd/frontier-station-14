// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.LaborCamp;

/// <summary>
/// Frontier exposes <c>TrySetAccesses</c>. HardLight exposes <c>SetAccesses</c>.
/// Each tag is its own list, so the reader accepts any one of them.
/// </summary>
internal static class LaborCampAccess
{
    private static readonly MethodInfo? TrySet = typeof(AccessReaderSystem).GetMethod(
        "TrySetAccesses",
        new[] { typeof(Entity<AccessReaderComponent>), typeof(List<ProtoId<AccessLevelPrototype>>) });

    private static readonly MethodInfo? Set = typeof(AccessReaderSystem).GetMethod(
        "SetAccesses",
        new[] { typeof(EntityUid), typeof(AccessReaderComponent), typeof(List<ProtoId<AccessLevelPrototype>>) });

    public static void SetAny(AccessReaderSystem access, EntityUid uid, AccessReaderComponent reader, params ProtoId<AccessLevelPrototype>[] tags)
    {
        var list = new List<ProtoId<AccessLevelPrototype>>(tags);
        if (TrySet != null)
        {
            TrySet.Invoke(access, new object[] { new Entity<AccessReaderComponent>(uid, reader), list });
            return;
        }

        Set?.Invoke(access, new object[] { uid, reader, list });
    }
}
