using System.Text;

namespace ApiIntegrationLab.UnitTests.Authentication;

public sealed class WebhookSignatureVerifierTests
{
    private const string CurrentTimestamp = "1789500000";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1789500000);

    [Fact]
    public void Verify_accepts_signature_over_timestamp_dot_exact_body()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventType\":\"demo.created\",\"value\":1}");
        var verifier = WebhookTestFactory.Create("test-secret");

        var result = verifier.Verify(
            body,
            CurrentTimestamp,
            WebhookTestFactory.Sign("test-secret", CurrentTimestamp, body),
            Now);

        Assert.True(result.IsValid);
        Assert.Null(result.Failure);
    }

    [Theory]
    [InlineData("sha256=00")]
    [InlineData("not-prefixed")]
    [InlineData("")]
    [InlineData("sha256=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sha256=gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Verify_rejects_invalid_signature_formats(string signature)
    {
        var result = WebhookTestFactory.Create("test-secret").Verify(
            "{}"u8.ToArray(), CurrentTimestamp, signature, Now);

        Assert.False(result.IsValid);
        Assert.Equal("invalid_signature", result.Failure);
    }

    [Theory]
    [InlineData(null, "sha256=00", "missing_timestamp")]
    [InlineData("1789500000", null, "missing_signature")]
    [InlineData("not-a-number", "sha256=00", "invalid_timestamp")]
    public void Verify_rejects_missing_or_malformed_headers(
        string? timestamp,
        string? signature,
        string failure)
    {
        var result = WebhookTestFactory.Create("test-secret").Verify(
            "{}"u8.ToArray(), timestamp, signature, Now);

        Assert.False(result.IsValid);
        Assert.Equal(failure, result.Failure);
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void Verify_rejects_timestamp_outside_five_minute_replay_window(int offsetSeconds)
    {
        var timestamp = Now.AddSeconds(offsetSeconds).ToUnixTimeSeconds().ToString();
        var body = "{}"u8.ToArray();

        var result = WebhookTestFactory.Create("test-secret").Verify(
            body,
            timestamp,
            WebhookTestFactory.Sign("test-secret", timestamp, body),
            Now);

        Assert.False(result.IsValid);
        Assert.Equal("timestamp_outside_replay_window", result.Failure);
    }

    [Fact]
    public void Verify_rejects_a_single_byte_body_change()
    {
        var signedBody = "{\"value\":1}"u8.ToArray();
        var receivedBody = "{\"value\":2}"u8.ToArray();

        var result = WebhookTestFactory.Create("test-secret").Verify(
            receivedBody,
            CurrentTimestamp,
            WebhookTestFactory.Sign("test-secret", CurrentTimestamp, signedBody),
            Now);

        Assert.False(result.IsValid);
        Assert.Equal("signature_mismatch", result.Failure);
    }

    [Fact]
    public void Verify_rejects_missing_secret_without_throwing()
    {
        var result = WebhookTestFactory.Create("").Verify(
            "{}"u8.ToArray(), CurrentTimestamp, "sha256=" + new string('0', 64), Now);

        Assert.False(result.IsValid);
        Assert.Equal("webhook_not_configured", result.Failure);
    }

    [Fact]
    public void Verify_rejects_the_same_valid_delivery_inside_the_replay_window()
    {
        var body = "{\"eventType\":\"demo.created\"}"u8.ToArray();
        var signature = WebhookTestFactory.Sign("test-secret", CurrentTimestamp, body);
        var verifier = WebhookTestFactory.Create("test-secret");

        var first = verifier.Verify(body, CurrentTimestamp, signature, Now);
        var replay = verifier.Verify(body, CurrentTimestamp, signature, Now.AddSeconds(1));

        Assert.True(first.IsValid);
        Assert.False(replay.IsValid);
        Assert.Equal("signature_replayed", replay.Failure);
    }

    [Fact]
    public void Verify_accepts_distinct_valid_deliveries_with_the_same_timestamp()
    {
        var firstBody = "{\"eventType\":\"demo.created\",\"value\":1}"u8.ToArray();
        var secondBody = "{\"eventType\":\"demo.created\",\"value\":2}"u8.ToArray();
        var verifier = WebhookTestFactory.Create("test-secret");

        var first = verifier.Verify(
            firstBody,
            CurrentTimestamp,
            WebhookTestFactory.Sign("test-secret", CurrentTimestamp, firstBody),
            Now);
        var second = verifier.Verify(
            secondBody,
            CurrentTimestamp,
            WebhookTestFactory.Sign("test-secret", CurrentTimestamp, secondBody),
            Now);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
    }
}
