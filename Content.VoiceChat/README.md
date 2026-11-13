# Portable proximity voice chat

Proximity voice for a `space-wizards/space-station-14` fork. The sources are compiled into that fork's `Content.Shared`, `Content.Client`, and `Content.Server` assemblies. There is no `Content.VoiceChat.*.dll`. The launcher sandbox and the client zip already ship those three assemblies, so nothing extra has to be whitelisted or packed. NWaves is server-only and is not part of the client download.

The code is **AGPL-3.0-or-later**. Keep the SPDX headers. Do not relicense it as MIT.

Authors: Aidenkrz &lt;28298836+Aidenkrz@users.noreply.github.com&gt; and Aiden &lt;aiden@djkraz.com&gt;.

## This Frontier tree

This branch already imports `Content.VoiceChat.Install.targets` from `Directory.Build.targets`. `TelephoneSystem.KeepCallAlive` is present. NWaves 0.9.6 is in `Directory.Packages.props`. Voice-of-god is left off. A quoted server snippet is [`voice.example.toml`](voice.example.toml). This server's TOML parser rejects a bare `http://` URL, so keep the quotes. Notices for NWaves, Noto Sans, and Boxfont Round are in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Install

Copy these directories next to `Content.Server`:

- `Content.VoiceChat.Shared`
- `Content.VoiceChat.Server`
- `Content.VoiceChat.Client`
- `Content.VoiceChat.Effects`
- `Content.VoiceChat.Adapters`
- `Resources/Locale/en-US/_VoiceChat`

Do not add project references to voice `.csproj` files. Those projects are not part of the install. A separate `Content.VoiceChat.Client.dll` is loaded by the launcher and fails the sandbox.

Create `Directory.Build.targets` at the repository root, or add the import if that file already exists:

```xml
<Project>
  <Import Project="Content.VoiceChat.Shared/Content.VoiceChat.Install.targets" />
</Project>
```

That file compiles the sources into the content assemblies, embeds the `/voice` page, and references NWaves from the server. It also picks the compile symbols for this fork's radio, voice mask, ticker, and telephone types. You do not set those by hand.

Add NWaves to `Directory.Packages.props`:

```xml
<PackageVersion Include="NWaves" Version="0.9.6" />
```

If `Content.Server/Telephone/TelephoneSystem.cs` does not already contain `KeepCallAlive`, add:

```csharp
public void KeepCallAlive(Entity<TelephoneComponent> entity)
{
    if (entity.Comp.CurrentState == TelephoneState.InCall)
        entity.Comp.StateStartTime = _timing.CurTime;
}
```

`_timing` is the `IGameTiming` dependency already on `TelephoneSystem`. The build stops with this snippet if the method is missing. Forks with no telephones delete `Content.VoiceChat.Adapters/Server/Stock/TelephoneVoiceFeature.cs` instead.

In the server config:

```
[voice]
enabled = true
public_url = http://127.0.0.1:1212/voice/
ws_bind = 127.0.0.1:1213
```

Use the host and port from `status.bind` in `public_url`. Leave `ws_url` empty. On plain HTTP the page tries `/voice/ws` on the status port, then connects to `ws_bind`. Localhost is a secure context, so the microphone works without HTTPS. A public server needs TLS. Proxy `/voice/ws` to `voice.ws_bind` and `/voice/` to the status port. Do not publish port 1213.

Rebuild the client and the server. In game, F6 or the console command `voicechat` opens the connect window. Hold N for proximity and M for radio. Two clients on one server can hear each other in the lobby, or in range after the round starts. One client can enable hear-self on that window.

Optional admin voice-of-god is off unless the repository sets `<VoiceChatIncludeGod>true</VoiceChatIncludeGod>` before the import, the way Goob does in `Directory.Build.props`. Megaphone and intrinsic-voice adapters compile only when `LoudspeakerHolderComponent` exists.

## What the targets file checks

Building `Content.Server` fails with a specific message when:

- `Content.VoiceChat.Shared/VoiceChat/VoiceCVars.cs` is missing
- `Directory.Packages.props` has no `NWaves` package version
- `TelephoneSystem.KeepCallAlive` is missing
- `Content.Server/Telephone/TelephoneSystem.cs` does not exist

After a successful server build it prints the detected symbols (`VOICECHAT_SERVER_COMPONENTS`, `VOICECHAT_SHARED_TICKER`, `VOICECHAT_SHARED_VOICE_MASK`, `VOICECHAT_VOICE_MASK_ACTIVE`, `VOICECHAT_RADIO_EXTRAS`, `VOICECHAT_UID_TELEPHONE`).

## CVars

Server and client both load `VoiceCVars`. Registering the same names again throws.

```
[voice]
enabled = true
public_url =
ws_bind = 127.0.0.1:1213
trusted_proxies =
max_connections_per_ip = 6
ws_url =
radio_enabled = true
radio_common = false
bitrate = 64
log_enabled = true
log_rounds = 3
lobby = true
dynamic_range = true
shout_range = 15
whisper_range = 3
shout_threshold = 14
whisper_threshold = 5
```

`voice.public_url` is the page the client opens. Empty derives it from `hub.server_url`. Client-only: `voice.volume` (0–2), `voice.hear_self`, `voice.speaker_list`, `voice.radio_volume`, `voice.radio_muted`.

`voice.trusted_proxies` is a comma-separated list of proxy IPs allowed to set `X-Real-IP` and `X-Forwarded-For`. Loopback and private addresses are always trusted.

## Fork notes

The same install is enough for the forks below. Symbols are detected from the tree. No per-fork project edits.

| Fork | Layout the detector uses |
| --- | --- |
| Wizden master | Shared ticker, shared voice mask with `Active`, `EntityUid` telephone links |
| Goob | Server ticker, server voice mask with `Active`, radio send/receive whitelists, `SpeakNormally`, voice-of-god and megaphone because those types exist |
| Frontier | Server radio components (`HashSet<string>` channels), server surveillance-camera namespace, server voice mask without `Active` |
| RMC-14 | Same server radio layout as Frontier. `ActiveListenerComponent` still lives in `Content.Server.Speech`, which is detected |
| Einstein Engines | Same server radio layout. It vendors `Content.Goobstation.*` but not the loudspeaker component, so the megaphone adapter is left out. Its station-AI vision component has no `NeedsPower`, so that adapter is left out too |
| Delta-V | Shared voice mask with `Active`, server ticker, `Entity<TelephoneComponent>` links |
| Monolith | `ActiveRadioComponent` is under `Content.Shared/_Goobstation` but the namespace is `Content.Shared.Radio.Components`, and channel sets are still `HashSet<string>`. `RadioSendAttemptEvent` takes a frequency. No ingestion-blocker component, and station-AI vision has no `NeedsPower`, so those checks are skipped |

## Troubleshooting

**NETSDK1013, TargetFramework is blank.** This happened when the voice code was its own project and an empty global `TargetFramework` (a parent `Directory.Build.props` or `/p:TargetFramework=`) could not be overwritten. The install no longer has those projects. If a fork's own content project still reports it, put `TreatAsLocalProperty="TargetFramework"` on that `<Project>` element and set a literal framework such as `net9.0` or `net10.0` instead of `$(TargetFramework)`.

**Launcher client crashes during the sandbox type check, naming `Content.VoiceChat.Shared` or `Content.VoiceChat.Client`.** A previous install left those DLLs next to `Content.Client.dll`, or the fork added a `ProjectReference` to them. Delete `Content.VoiceChat.*.dll` from the client output and do not reference voice projects. The client build also deletes leftovers from `$(TargetDir)`.

**`voice.enabled` is missing when the client UI loads.** `VoiceCVars` must keep `[CVarDefs]`. Without it the configuration manager never registers the names.

**Build says NWaves is missing.** Add the `PackageVersion` line above. NWaves is MIT (Tim Sharii / ar1st0crat) and is used only on the server. It is not in the client zip.

**Build says `KeepCallAlive` is missing.** Add the method in the install section. `StateStartTime` cannot be written from another type.

**F6 does nothing.** `Directory.Build.targets` is not importing `Content.VoiceChat.Install.targets`, so the entry point was never compiled. `voicechat` is registered by that entry point.

**Microphone stays blocked.** The page is not a secure context. Use `http://127.0.0.1` or `http://localhost` for a local test, or HTTPS in production, and proxy `/voice/ws` to `voice.ws_bind`.

## License

- Module source: AGPL-3.0-or-later. Keep the SPDX headers.
- [NWaves](https://github.com/ar1st0crat/NWaves) 0.9.6: MIT, author Tim Sharii (ar1st0crat). NuGet ships the license. It is not vendored.
- Noto Sans files embedded in the voice page: Apache-2.0. The license is `Content.VoiceChat.Server/VoiceChat/Web/NotoSans-LICENSE.txt`.
- Boxfont Round: CC-0. Credits are `Content.VoiceChat.Server/VoiceChat/Web/Boxfont-round-credits.txt`.
