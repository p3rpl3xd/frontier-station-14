# Portable proximity voice chat

Self-contained proximity voice for a `space-wizards/space-station-14` fork. Copy the module, add the project references below, set the `[voice]` CVars, and put HTTPS in front of `/voice` and `/voice/ws`. Radio, telephones, holopads, station AI, voice logs, and voice-of-god are optional adapters. The connect window does not use any news UI.

The code is **AGPL-3.0-or-later**. Keep the SPDX headers. Do not relicense it as MIT.

Authors: Aidenkrz &lt;28298836+Aidenkrz@users.noreply.github.com&gt; and Aiden &lt;aiden@djkraz.com&gt;.

## This Frontier tree

This fork already has the projects wired:

- `Content.Server` references `Content.VoiceChat.Server` and `Content.VoiceChat.Effects`, compiles `Adapters/Server/Stock`, and defines `VOICECHAT_SERVER_COMPONENTS`.
- `Content.Client` references `Content.VoiceChat.Client` and `Content.VoiceChat.Shared`, and compiles `Adapters/Client/Stock`. The dev client zip and `Content.Packaging` both ship those two assemblies. NWaves stays on the server.
- NWaves 0.9.6 is in `Directory.Packages.props`. The MIT notice is in `THIRD-PARTY-NOTICES.md`.
- `TelephoneSystem.KeepCallAlive` keeps a call from idling out while voice is flowing. `TelephoneComponent.StateStartTime` is not writable from the adapter.
- `VoiceCVars` is marked `[CVarDefs]`. RobustToolbox 267 only loads CVars from types with that attribute.

`Adapters/Server/Goob` is not in this tree. Voice-of-god (`Adapters/Server/God` and `Adapters/Client/God`) is not compiled, so the admin command is absent. Effects still muffles proximity speech. `VoiceGodEffect` is in the Effects project and does nothing until those adapters are compiled in.

Copy-paste server config: [`voice.example.toml`](voice.example.toml).

## What to copy

Copy these directories next to `Content.Server` / `Content.Client`:

| Directory | Required | Role |
| --- | --- | --- |
| `Content.VoiceChat.Shared` | yes | CVars, codec, net messages. `Content.VoiceChat.Build.props` hardcodes `net9.0` so SDK 9 never sees a blank TargetFramework. |
| `Content.VoiceChat.Server` | yes | Proximity, websocket, embedded `/voice` page. `EntryPoint` registers IoC. |
| `Content.VoiceChat.Client` | yes | Playback, push-to-talk, connect window. `EntryPoint` registers IoC. |
| `Content.VoiceChat.Effects` | optional | NWaves muffling and the voice-of-god pitch effect |
| `Content.VoiceChat.Adapters` | optional | Radio, telephone, station AI, logs, voice-of-god |
| `Resources/Locale/en-US/_VoiceChat` | yes | Fluent strings |

The core projects do not reference `Content.Server` or `Content.Client`. Adapters are compiled *into* those projects so they can use fork types without a project cycle.

Any `Content.*.dll` in the server or client output is loaded when you start that build yourself. The official launcher does not download the client output directory. It downloads the client zip: Magic ACZ while developing, or the zip produced by `Content.Packaging` for a release server. That zip's assembly list has to include `Content.VoiceChat.Client` and `Content.VoiceChat.Shared` next to `Content.Client`. The stock client adapter is compiled into `Content.Client` and calls those assemblies. If they are missing, the launcher sandbox dies with `TypeCheckFailedException` / `ILVerify: Failed to load assembly 'Content.VoiceChat.Shared'`.

NWaves is only referenced by `Content.VoiceChat.Effects` on the server. Do not put it in the client zip. A packaged server build does need `NWaves.dll` beside the server, along with `Content.VoiceChat.Server`, `Content.VoiceChat.Shared`, and `Content.VoiceChat.Effects`.

Forks that copy every runtime assembly from the client `deps.json` (Goob does this) already pick the two voice assemblies up. Forks with a hardcoded list, such as Frontier and current Wizden, have to add the names:

```csharp
"Content.VoiceChat.Client",
"Content.VoiceChat.Shared",
```

## Wire-up

In `Content.Server/Content.Server.csproj`, inside the existing `ProjectReference` item group:

```xml
<ProjectReference Include="..\Content.VoiceChat.Server\Content.VoiceChat.Server.csproj" />
<ProjectReference Include="..\Content.VoiceChat.Effects\Content.VoiceChat.Effects.csproj" />
```

Skip the Effects reference if you are not shipping muffling or voice-of-god. Then compile the adapters you want (omit an include to leave that feature out):

```xml
<ItemGroup>
  <Compile Include="..\Content.VoiceChat.Adapters\Server\Stock\**\*.cs" Link="VoiceChatAdapters\Stock\%(Filename)%(Extension)" />
  <Compile Include="..\Content.VoiceChat.Adapters\Server\God\**\*.cs" Link="VoiceChatAdapters\God\%(Filename)%(Extension)" />
</ItemGroup>
```

`Server/Goob` (megaphone, intrinsic radio) is Goob-only. Do not include it on a stock fork.

In `Content.Client/Content.Client.csproj`:

```xml
<ProjectReference Include="..\Content.VoiceChat.Client\Content.VoiceChat.Client.csproj" />
<ProjectReference Include="..\Content.VoiceChat.Shared\Content.VoiceChat.Shared.csproj" />
```

```xml
<ItemGroup>
  <Compile Include="..\Content.VoiceChat.Adapters\Client\Stock\**\*.cs" Link="VoiceChatAdapters\%(Filename)%(Extension)" />
</ItemGroup>
```

`Client/God` is the voice-of-god screen cue. Include it only together with the server God adapter and Effects.

If the fork uses central package management and you reference Effects, add this to `Directory.Packages.props`:

```xml
<PackageVersion Include="NWaves" Version="0.9.6" />
```

The Effects project must not set a `Version` on that `PackageReference`.

### Compile symbols

Set these on `Content.Server` only when the fork's content matches. A fork that still matches Goob's content layout sets `VOICECHAT_RADIO_EXTRAS` and nothing else. Current Wizden master sets `VOICECHAT_UPSTREAM` only. Frontier master sets `VOICECHAT_SERVER_COMPONENTS` only.

| Symbol | When |
| --- | --- |
| `VOICECHAT_SERVER_COMPONENTS` | `ActiveRadioComponent` still lives in `Content.Server.Radio.Components`, and `SurveillanceCameraComponent` is in the `Content.Server.SurveillanceCamera` namespace. Also treats a voice mask as active whenever the component is present (`VoiceMaskComponent.Active` does not exist). Frontier master today. |
| `VOICECHAT_RADIO_EXTRAS` | `RadioChannelPrototype` has `SendWhitelist` / `ReceiveWhitelist`, and `RadioSpeakerComponent` has `SpeakNormally`. Goob does. Current Wizden master and Frontier master do not. |
| `VOICECHAT_UPSTREAM` | Content matches current Wizden master: `GameTicker`, `RoundStartingEvent`, and `StationSystem` are in `Content.Shared`; `VoiceMaskComponent` is in `Content.Shared.VoiceMask`; telephone links, the telephone speaker, and holopad holograms are `EntityUid` rather than `Entity<T>`. Goob and Frontier leave this unset. |

```xml
<DefineConstants>$(DefineConstants);VOICECHAT_SERVER_COMPONENTS</DefineConstants>
```

Channel sets that are still `HashSet<string>` (Frontier) are handled by `VOICECHAT_SERVER_COMPONENTS`. Forks that moved radio to `ProtoId` (Wizden, Goob) leave that symbol unset.

### Telephone calls

`TelephoneComponent.StateStartTime` cannot be written from another type. If you compile `TelephoneVoiceFeature`, add this public method to the fork's `TelephoneSystem`:

```csharp
public void KeepCallAlive(Entity<TelephoneComponent> entity)
{
    if (entity.Comp.CurrentState == TelephoneState.InCall)
        entity.Comp.StateStartTime = _timing.CurTime;
}
```

## CVars

Names match the old Goob `voice.*` CVars. Server and client both load `VoiceCVars`, so registering them again in the fork will throw.

```
[voice]
enabled = true
public_url = "https://example.com/voice/"
ws_bind = "127.0.0.1:1213"
trusted_proxies = ""
max_connections_per_ip = 6
ws_url = ""
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

On this fork, quote string values. A bare `https://` URL makes Nett refuse the config file.

Client-only, saved in the player config: `voice.volume` (0–2), `voice.hear_self`, `voice.speaker_list`, `voice.radio_volume`, `voice.radio_muted`.

`voice.public_url` is the page the client opens. When it is empty, the server derives it from `hub.server_url` (`ss14s`/`https` become `https`, `ss14`/`http` become `http`, path `/voice/`). `voice.ws_url` is optional. When it is empty, the page uses the same origin at `/voice/ws`.

In game, **F6** or the console command `voicechat` opens the connect window. Hold **N** for proximity push-to-talk and **M** for radio push-to-talk. Both are registered in code, so they do not need `keybinds.yml` entries.

## Hosting

The microphone page uses `getUserMedia`. Browsers only allow that on a secure context: HTTPS, or plain HTTP on localhost. Production needs TLS.

The game server serves the page on the status HTTP listener (`status.bind`), under `GET /voice/` (a request to `/voice` redirects to `/voice/`). Static files are embedded in `Content.VoiceChat.Server`. The status host does **not** accept the voice WebSocket. `GET /voice/ws` on the status port returns an error that says so.

Voice audio is a separate TCP listener at `voice.ws_bind` (default `127.0.0.1:1213`). Bind it to loopback and proxy it. Do not publish 1213 on the internet.

Example Caddy site in front of a status port on `127.0.0.1:1212` and voice on `127.0.0.1:1213`:

```
example.com {
    handle /voice/ws* {
        reverse_proxy 127.0.0.1:1213
    }
    handle /voice* {
        reverse_proxy 127.0.0.1:1212
    }
}
```

nginx is the same split: `location /voice/ws` proxies to `voice.ws_bind` with `Upgrade` and `Connection` headers; `location /voice/` proxies to the status port.

`voice.trusted_proxies` is a comma-separated list of proxy IPs. Those peers may set `X-Real-IP` and `X-Forwarded-For`. Loopback and private addresses are always trusted. Set this to the proxy's address when the proxy is not on a private IP, or the per-IP connection cap will see the proxy instead of the player.

### Example server config

Voice is off until `enabled` is true. Put this in `server_config.toml` next to the server binary (`bin/Content.Server/server_config.toml` after a build), or pass `--config-file` with a path relative to the working directory. The same three keys are in [`voice.example.toml`](voice.example.toml).

```toml
[voice]
enabled = true
public_url = "https://example.com/voice/"
ws_bind = "127.0.0.1:1213"
```

Quote every string. Frontier's TOML parser (Nett) rejects a bare URL: `public_url = https://example.com/voice/` fails to load the whole file.

`public_url` is the page the client opens. `ws_bind` is the voice audio socket. Leave it on loopback.

On this engine, `status.bind` defaults to `*:<game port>` (port 1212 unless `net.port` is changed). That listener serves `GET /voice/`. It does not serve the WebSocket.

### LAN / VPN (Radmin and similar)

The microphone page calls `getUserMedia`. Browsers only allow that in a secure context.

- The machine running the server can open `http://localhost/voice/` or `http://127.0.0.1/voice/` with a microphone. Plain HTTP on localhost counts as secure.
- Every other computer does not. A Radmin, Hamachi, or LAN address such as `http://26.x.x.x/voice/` or `http://192.168.x.x/voice/` is not a secure context, and the browser will refuse the microphone.

Those players need HTTPS on a name that resolves inside the VPN (a private CA is enough) and a reverse proxy with the same split as the Caddy site above:

- `/voice/` proxies to the status port (`status.bind`, usually 1212).
- `/voice/ws` proxies to `voice.ws_bind` (`127.0.0.1:1213`), with WebSocket `Upgrade` and `Connection` headers.

Set `voice.public_url` to that HTTPS URL, including the trailing `/voice/`. In game, F6 or the `voicechat` command opens the link.

## Core and adapters

Core, always on once the projects are referenced and `voice.enabled` is true:

- Same-map proximity, with wall and container checks in `VoiceChatSystem`
- Lobby voice (`voice.lobby`), detected without a compile-time reference to `GameTicker`
- IMA-ADPCM-style codec at 16 kHz, in C# and in the embedded page. No Opus and no native library
- Connect window, speaker list, push-to-talk

`IVoiceFeature` in `Content.VoiceChat.Server` is the extension point. An adapter is an `EntitySystem` that calls `_features.Add(this)` from `Initialize`. Default interface methods mean you only implement the hooks you need (`Contribute`, `AdjustRange`, `AdjustMuffle`, `TrySecondaryListener`, and the rest on `IVoiceFeature`).

| Adapter | Include | Needs |
| --- | --- | --- |
| `VoiceRadioSystem`, `VoiceBroadcastSystem` | Stock | Headset, encryption keys, comms console. Radio frequency is read by reflection on `RadioSystem.GetFrequency`, so a fork without that method still compiles and just skips the telecom frequency check. There is no inter-map telecom transmitter bridge. |
| `TelephoneVoiceFeature` | Stock | `KeepCallAlive` above. Holopads ride the same telephone components. |
| `StationAiVoiceFeature` | Stock | Station AI eye and cameras |
| `PressureMuffleFeature` | Stock | `PressureProtectionComponent` |
| `VoiceMaskFeature` | Stock | Voice mask name override |
| `VoiceLogSystem` and the log commands / EUI | Stock | Writes recordings under the server data dir. Set `voice.log_enabled` false to skip recording; omit the files to drop the admin UI. |
| `VoiceGodFeature`, `VoiceOfGodCommand`, client `VoiceGodCueSystem` | God | Effects project. Admin command, not proximity. |
| `MegaphoneVoiceFeature`, `IntrinsicVoiceFeature` | Goob | Goob megaphone and intrinsic-voice components. Not in the stock set. |
| Canadian indicator shader | none | Stays in Goob's client. `BeforePostShaderRenderEvent` is a different type on engine 267 than on 293, so it is not in the portable set. The speaking overlay calls `VoiceIndicatorHooks.ReplacesIndicator` instead of referencing that shader. |

Omit Effects and proximity still works. Speech is unfiltered, and voice-of-god has no pitch effect.

## Engine range

The projects set `LangVersion` 12 and import `RobustToolbox/Imports/*.props` only when those files exist (engine 293+). On 267 they fall back to direct references to `Robust.Shared`, `Robust.Server` / `Robust.Client`, Lidgren, and NetSerializer.

Verified by compiling this module into each fork's content projects (scratch trees only, nothing pushed):

| Fork | Engine | What compiled |
| --- | --- | --- |
| Goob `cursor/voice-chat-portable-e29f` | RobustToolbox `0a83625`, **293.0.0**, net10 | `Content.Goobstation.Server` with Stock + Goob + God adapters and Effects, and `Content.Goobstation.Client` with Stock + God client adapters |
| Frontier master `e1b0dc84` | RobustToolbox `feb9e1d`, **267.3.0**, net9 | `Content.Server` and `Content.Client` with Stock adapters, God server adapter, and Effects. `VOICECHAT_SERVER_COMPONENTS` set. No `VOICECHAT_RADIO_EXTRAS`. |
| Wizden master `7f413a98` | RobustToolbox `d56b0d0`, **295.0.3**, net10 | `Content.Server` with Stock + God adapters and Effects, `VOICECHAT_UPSTREAM` set, and `Content.VoiceChat.Client`. The full `Content.Client` project did not finish in the scratch tree: Robust's XAML task could not load `Robust.Xaml`. The same client sources compiled into Goob's client on 293 and Frontier's client on 267. |

Core looks up `GhostComponent` by type name, in either `Content.Shared.Ghost` or `Content.Shared.Ghost.Components`, so that namespace move does not need a symbol. Mute is enforced through `ActionBlockerSystem.CanSpeak` (`SpeakAttemptEvent`), which covers both the older `MutedComponent` and Wizden's mute status effect. Pins from 267.3.0 through 295.0.3 were built. Versions between those pins were not each built.

## License and notices

- Module source: AGPL-3.0-or-later. Keep the SPDX headers on every file you copy. Forks that are themselves AGPL-3.0-or-later can take this code. An MIT-only tree cannot.
- [NWaves](https://github.com/ar1st0crat/NWaves) 0.9.6, used only by `Content.VoiceChat.Effects`: MIT, author Tim Sharii (ar1st0crat). The package is not vendored; NuGet ships the license.
- Noto Sans woff2 files embedded in the voice page (`noto-sans-regular.woff2`, `noto-sans-bold.woff2`): Apache-2.0. The license text ships beside them as `VoiceChat/Web/NotoSans-LICENSE.txt`.
- Boxfont Round (`boxfont-round.woff2`): CC-0. Credits ship as `VoiceChat/Web/Boxfont-round-credits.txt`.
