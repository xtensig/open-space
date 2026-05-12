using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._OpenSpace.TypeAuth;

/// <summary>Server → Client: show the Discord auth overlay.</summary>
public sealed class TypeAuthShowMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string AuthLink { get; set; } = "";

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        => AuthLink = buffer.ReadString();

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        => buffer.Write(AuthLink);
}

/// <summary>Client → Server: player clicked "I've authorized".</summary>
public sealed class TypeAuthCheckMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer) { }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer) { }
}

/// <summary>Server → Client: result of the authorization check.</summary>
public sealed class TypeAuthResultMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Success = buffer.ReadBoolean();
        var err = buffer.ReadString();
        ErrorMessage = string.IsNullOrEmpty(err) ? null : err;
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Success);
        buffer.Write(ErrorMessage ?? "");
    }
}
