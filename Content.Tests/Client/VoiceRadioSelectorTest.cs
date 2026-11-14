#nullable enable
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Radio;
using Content.VoiceChat.Client.UI;
using Content.VoiceChat.Shared;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Client;

[TestFixture]
public sealed class VoiceRadioSelectorTest
{
    [Test]
    public void ChatInputHostsTheRadioSelector()
    {
        var field = typeof(ChatInputBox).GetField(nameof(ChatInputBox.VoiceRadio));
        Assert.That(field, Is.Not.Null);
        Assert.That(field!.FieldType, Is.EqualTo(typeof(VoiceRadioButton)));
    }

    [Test]
    public void CycleWalksHeadsetChannels()
    {
        var radio = new VoiceRadioComponent();
        radio.Channels.Add("Common");
        radio.Channels.Add("Security");
        radio.Channels.Add("Command");

        Assert.That(Id(VoiceRadioClient.Cycle(radio)), Is.EqualTo("Common"));

        radio.Active = "Common";
        Assert.That(Id(VoiceRadioClient.Cycle(radio)), Is.EqualTo("Security"));

        radio.Active = "Security";
        Assert.That(Id(VoiceRadioClient.Cycle(radio)), Is.EqualTo("Command"));

        radio.Active = "Command";
        Assert.That(Id(VoiceRadioClient.Cycle(radio)), Is.EqualTo("Common"));

        radio.Active = "Engineering";
        Assert.That(Id(VoiceRadioClient.Cycle(radio)), Is.EqualTo("Common"));
    }

    [Test]
    public void CycleDoesNothingWithoutChannels()
    {
        var radio = new VoiceRadioComponent();
        Assert.That(VoiceRadioClient.Cycle(radio), Is.Null);
    }

    private static string? Id(ProtoId<RadioChannelPrototype>? channel)
    {
        return channel?.Id;
    }
}
