namespace FxSsh.Messages
{
    public class KeyExchangeECDhInitMessage : KeyExchangeXInitMessage
    {
        public byte[] Q { get; private set; } = null!;

        protected override void OnLoad(SshDataReader reader)
        {
            Q = reader.ReadBinary();
        }
    }
}
