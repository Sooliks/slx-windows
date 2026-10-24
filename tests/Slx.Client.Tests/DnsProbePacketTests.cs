using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class DnsProbePacketTests
{
    [Theory]
    [InlineData(ProbeTarget.Gstatic, "0100000100000000000003777777076773746174696303636F6D0000010001")]
    [InlineData(ProbeTarget.Slx, "0100000100000000000006736C7876706E03636F6D0000010001")]
    public void QueryUsesOneRecursiveInternetAddressQuestion(ProbeTarget target, string expected)
    {
        var query = DnsProbePacket.CreateQuery(target);
        Assert.Equal(Convert.FromHexString(expected), query[2..]);
        Assert.Equal((DnsProbeOutcome.Success, (int?)0), DnsProbePacket.Evaluate(query, DnsProbeFixtures.Reply(query)));
    }

    [Fact]
    public void QuestionCaseDoesNotChangeItsDnsIdentity()
    {
        var query = DnsProbePacket.CreateQuery(ProbeTarget.Gstatic);
        var response = DnsProbeFixtures.Reply(query);
        for (int index = 12; index < query.Length - 4; index++)
            if (response[index] is >= (byte)'a' and <= (byte)'z') response[index] -= 32;
        Assert.Equal(DnsProbeOutcome.Success, DnsProbePacket.Evaluate(query, response).Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void EmptyOrRejectedResponseRetainsOnlyItsNumericCode(int code)
    {
        var query = DnsProbePacket.CreateQuery(ProbeTarget.Slx);
        Assert.Equal((DnsProbeOutcome.Rejected, (int?)code),
            DnsProbePacket.Evaluate(query, DnsProbeFixtures.Reply(query, code, false)));
    }

    [Theory]
    [InlineData("header-only")]
    [InlineData("id")]
    [InlineData("query")]
    [InlineData("opcode")]
    [InlineData("truncated-flag")]
    [InlineData("reserved-flag")]
    [InlineData("questions")]
    [InlineData("question-name")]
    [InlineData("question-type")]
    [InlineData("question-class")]
    [InlineData("record-length")]
    [InlineData("truncated-body")]
    [InlineData("answer-count")]
    [InlineData("large-count")]
    [InlineData("pointer-self")]
    [InlineData("pointer-header")]
    [InlineData("pointer-forward")]
    [InlineData("label-type")]
    [InlineData("trailing")]
    [InlineData("oversize")]
    public void MalformedOrUnrelatedPacketsCannotConfirmDns(string change)
    {
        var query = DnsProbePacket.CreateQuery(ProbeTarget.Gstatic);
        var response = DnsProbeFixtures.Reply(query);
        switch (change)
        {
            case "header-only": response = response[..12]; break;
            case "id": response[0] ^= 1; break;
            case "query": response[2] &= 0x7f; break;
            case "opcode": response[2] |= 8; break;
            case "truncated-flag": response[2] |= 2; break;
            case "reserved-flag": response[3] |= 0x40; break;
            case "questions": response[5] = 2; break;
            case "question-name": response[13] ^= 1; break;
            case "question-type": response[query.Length - 3] = 28; break;
            case "question-class": response[query.Length - 1] = 3; break;
            case "record-length": response[^5] = 5; break;
            case "truncated-body": response = response[..^1]; break;
            case "answer-count": response[7] = 2; break;
            case "large-count": response[6] = 0xff; break;
            case "pointer-self": response[query.Length + 1] = (byte)query.Length; break;
            case "pointer-header": response[query.Length + 1] = 0; break;
            case "pointer-forward": response[query.Length + 1] = (byte)(query.Length + 4); break;
            case "label-type": response[12] = 0x40; break;
            case "trailing": response = [.. response, 0]; break;
            case "oversize": Array.Resize(ref response, 4097); break;
        }
        Assert.Equal((DnsProbeOutcome.InvalidResponse, (int?)null), DnsProbePacket.Evaluate(query, response));
    }

    [Theory]
    [InlineData("unrelated-owner")]
    [InlineData("additional-only")]
    [InlineData("wrong-class")]
    public void AddressMustAnswerTheOriginalInternetQuestion(string change)
    {
        var query = DnsProbePacket.CreateQuery(ProbeTarget.Gstatic);
        var response = DnsProbeFixtures.Reply(query);
        if (change == "unrelated-owner") response[query.Length + 1] = 16; // gstatic.com, not www.gstatic.com
        if (change == "additional-only") { response[7] = 0; response[11] = 1; }
        if (change == "wrong-class") response[query.Length + 5] = 3;
        Assert.Equal((DnsProbeOutcome.Rejected, (int?)0), DnsProbePacket.Evaluate(query, response));
    }

    [Fact]
    public void CompressedNameLoopIsRejectedWithoutFollowingItIndefinitely()
    {
        var query = DnsProbePacket.CreateQuery(ProbeTarget.Gstatic);
        var response = DnsProbeFixtures.Reply(query);
        response[12] = 1; response[13] = (byte)'x'; response[14] = 0xc0; response[15] = 12;
        Assert.Equal(DnsProbeOutcome.InvalidResponse, DnsProbePacket.Evaluate(query, response).Outcome);
    }
}
