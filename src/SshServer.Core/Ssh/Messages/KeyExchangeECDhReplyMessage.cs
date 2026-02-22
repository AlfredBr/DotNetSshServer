namespace FxSsh.Messages
{
    public class KeyExchangeECDhReplyMessage : KeyExchangeXReplyMessage
    {
        public byte[] HostKey { get; set; } = null!;
        public byte[] Q { get; set; } = null!;
        public byte[] Signature { get; set; } = null!;

        protected override void OnGetPacket(SshDataWriter writer)
        {
            writer.WriteBinary(HostKey);
            writer.WriteBinary(Q);
            writer.WriteBinary(Signature);
        }
    }
}
