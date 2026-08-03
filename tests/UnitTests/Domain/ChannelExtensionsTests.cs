using Kart.Notification.Domain.Enums;
using Xunit;

namespace Kart.Notification.UnitTests.Domain;

public class ChannelExtensionsTests
{
    [Theory]
    [InlineData(Channel.Email, "Email")]
    [InlineData(Channel.Sms, "SMS")]
    [InlineData(Channel.Push, "Push")]
    public void ToDbValue_matches_the_notification_attempts_channel_check_constraint(Channel channel, string expected)
    {
        Assert.Equal(expected, channel.ToDbValue());
    }

    [Theory]
    [InlineData("Email", Channel.Email)]
    [InlineData("SMS", Channel.Sms)]
    [InlineData("Push", Channel.Push)]
    public void FromDbValue_round_trips_ToDbValue(string dbValue, Channel expected)
    {
        Assert.Equal(expected, ChannelExtensions.FromDbValue(dbValue));
    }

    [Fact]
    public void FromDbValue_throws_for_an_unrecognized_value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChannelExtensions.FromDbValue("Fax"));
    }
}
