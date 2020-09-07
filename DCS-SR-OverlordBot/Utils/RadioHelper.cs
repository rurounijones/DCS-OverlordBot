using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Common;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils
{
    public static class RadioHelper
    {
        public static void ToggleGuard(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio == null) return;
            if (radio.freqMode != RadioInformation.FreqMode.OVERLAY &&
                radio.guardFreqMode != RadioInformation.FreqMode.OVERLAY) return;
            radio.secFreq = radio.secFreq > 0 ? 0 : 1;

            //make radio data stale to force resysnc
            ClientStateSingleton.Instance.LastSent = 0;
        }

        public static bool UpdateRadioFrequency(double frequency, int radioId, bool delta = true, bool inMHz = true)
        {
            return true;
        }

        public static bool SelectRadio(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio == null) return false;
            if (radio.modulation == RadioInformation.Modulation.DISABLED ||
                ClientStateSingleton.Instance.DcsPlayerRadioInfo.control !=
                DCSPlayerRadioInfo.RadioSwitchControls.HOTAS) return false;
            ClientStateSingleton.Instance.DcsPlayerRadioInfo.selected = (short) radioId;
            return true;

        }

        public static RadioInformation GetRadio(int radio)
        {
            var dcsPlayerRadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent() &&
                radio < dcsPlayerRadioInfo.radios.Length && radio >= 0)
            {
                return dcsPlayerRadioInfo.radios[radio];
            }

            return null;
        }

        public static void ToggleEncryption(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio == null) return;
            if (radio.modulation == RadioInformation.Modulation.DISABLED) return;
            //update stuff
            if (radio.encMode != RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY) return;
            radio.enc = !radio.enc;

            //make radio data stale to force resysnc
            ClientStateSingleton.Instance.LastSent = 0;
        }

        public static void SetEncryptionKey(int radioId, int encKey)
        {
            var currentRadio = GetRadio(radioId);

            if (currentRadio == null || currentRadio.modulation == RadioInformation.Modulation.DISABLED) return;
            if (currentRadio.modulation == RadioInformation.Modulation.DISABLED) return;
            //update stuff
            if (currentRadio.encMode != RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE &&
                currentRadio.encMode != RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY) return;
            if (encKey > 252)
                encKey = 252;
            else if (encKey < 1)
                encKey = 1;

            currentRadio.encKey = (byte) encKey;
            //make radio data stale to force resysnc
            ClientStateSingleton.Instance.LastSent = 0;
        }
        
        public static void SelectRadioChannel(PresetChannel selectedPresetChannel, int radioId)
        {
            if (!UpdateRadioFrequency((double) selectedPresetChannel.Value, radioId, false, false)) return;
            var radio = GetRadio(radioId);

            if (radio != null) radio.channel = selectedPresetChannel.Channel;
        }
    }
}