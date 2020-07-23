namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio
{
    public class JitterBufferAudio
    {
        public byte[] Audio { get; set; }

        public ulong PacketNumber { get; set; }
    }
}