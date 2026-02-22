namespace FxSsh.Messages
{
    public class KeyExchangeDhReplyMessage : KeyExchangeXReplyMessage
    {
        public byte[] HostKey { get; set; } = null!;
        public byte[] F { get; set; } = null!;
        public byte[] Signature { get; set; } = null!;

        protected override void OnGetPacket(SshDataWriter writer)
        {
            writer.WriteBinary(HostKey);
            writer.WriteMpint(F);
            writer.WriteBinary(Signature);
        }
    }
}
