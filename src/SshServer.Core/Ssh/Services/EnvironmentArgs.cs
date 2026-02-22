using System.Diagnostics.Contracts;

namespace FxSsh.Services
{
    public class EnvironmentArgs
    {
        public EnvironmentArgs(SessionChannel channel, string name, string value, UserAuthArgs userAuthArgs)
        {
            Contract.Requires(channel != null);
            Contract.Requires(name != null);
            Contract.Requires(value != null);
            Contract.Requires(userAuthArgs != null);

            Channel = channel!;
            Name = name!;
            Value = value!;
            AttachedUserAuthArgs = userAuthArgs!;
        }

        public SessionChannel Channel { get; private set; } = null!;
        public string Name { get; private set; } = null!;
        public string Value { get; private set; } = null!;
        public UserAuthArgs AttachedUserAuthArgs { get; private set; } = null!;
    }
}
