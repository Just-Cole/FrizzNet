namespace FrizzNet.Messaging
{
    /// <summary>
    /// Reserved system message IDs used internally by FrizzNet.
    /// User-defined messages should use positive values (1+).
    /// </summary>
    public static class FrizzSystemMessages
    {
        public const short Spawn = -10;
        public const short Destroy = -11;
        public const short Transform = -12;
        public const short Voice = -13;
        public const short Animation = -14;
        public const short Rpc = -15;
        public const short SyncVar = -16;
        public const short HostSnapshot = -17;
        public const short SceneLoad = -18;
        public const short Rigidbody = -19;
    }
}
