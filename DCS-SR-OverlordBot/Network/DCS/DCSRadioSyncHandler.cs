using System;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.AwacsRadioOverlayWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS
{
    public class DCSRadioSyncHandler
    {
        private readonly DCSRadioSyncManager.SendRadioUpdate _radioUpdate;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;

        private volatile bool _stop;

        public delegate void NewAircraft(string name);

        private long _identStart = 0;

        public DCSRadioSyncHandler(DCSRadioSyncManager.SendRadioUpdate radioUpdate)
        {
            _radioUpdate = radioUpdate;
        }

        public void ProcessRadioInfo(DCSPlayerRadioInfo message)
        {
            //copy iff

            Transponder original = null;

            if (_clientStateSingleton.DcsPlayerRadioInfo.iff != null)
            {
                original = _clientStateSingleton.DcsPlayerRadioInfo.iff.Copy();
            }

            var update = UpdateRadio(message);

            if (update || _clientStateSingleton.LastSent < 1 || !_clientStateSingleton.DcsPlayerRadioInfo.iff.Equals(original))
            {
                Logger.Debug("Sending Radio Info To Server - Update");
                _clientStateSingleton.LastSent = DateTime.Now.Ticks;
                _radioUpdate();
            }
        }

        private bool UpdateRadio(DCSPlayerRadioInfo message)
        {
            var changed = false;

            var expansion = _serverSettings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION);

            var playerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

            //update common parts
            playerRadioInfo.name = message.name;
            playerRadioInfo.inAircraft = message.inAircraft;
            playerRadioInfo.intercomHotMic = message.intercomHotMic;

            /*
            if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls))
            {
                message.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
                playerRadioInfo.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
            }
            else
            {
                playerRadioInfo.control = message.control;
            }*/

            playerRadioInfo.simultaneousTransmissionControl = message.simultaneousTransmissionControl;

            playerRadioInfo.unit = message.unit;


            if (!_clientStateSingleton.ShouldUseLotATCPosition())
            {
                _clientStateSingleton.UpdatePlayerPosition(message.latLng);
            }

            var overrideFreqAndVol = false;

            var newAircraft = playerRadioInfo.unitId != message.unitId || !playerRadioInfo.IsCurrent();

            if (message.unitId >= DCSPlayerRadioInfo.UnitIdOffset &&
                playerRadioInfo.unitId >= DCSPlayerRadioInfo.UnitIdOffset)
            {
                //overriden so leave as is
            }
            else
            {
                overrideFreqAndVol = playerRadioInfo.unitId != message.unitId;
                playerRadioInfo.unitId = message.unitId;
            }

            if (newAircraft)
            {
                playerRadioInfo.iff = message.iff;
            }

            if (overrideFreqAndVol)
            {
                playerRadioInfo.selected = message.selected;
                changed = true;
            }

            if (playerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT)
            {
                playerRadioInfo.selected = message.selected;
            }

            bool simul = false;


            //copy over radio names, min + max
            for (var i = 0; i < playerRadioInfo.radios.Length; i++)
            {
                var clientRadio = playerRadioInfo.radios[i];

                //if awacs NOT open -  disable radios over 3
                if (i >= message.radios.Length
                    || (RadioOverlayWindow.AwacsActive == false
                        && (i > 3 || i == 0)
                        // disable intercom and all radios over 3 if awacs panel isnt open and we're a spectator given by the UnitId
                        && playerRadioInfo.unitId >= DCSPlayerRadioInfo.UnitIdOffset))
                {
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.guardFreqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;

                    continue;
                }

                var updateRadio = message.radios[i];


                if ((updateRadio.expansion && !expansion) ||
                    (updateRadio.modulation == RadioInformation.Modulation.DISABLED))
                {
                    //expansion radio, not allowed
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.guardFreqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;
                }
                else
                {
                    //update common parts
                    clientRadio.freqMin = updateRadio.freqMin;
                    clientRadio.freqMax = updateRadio.freqMax;
                    clientRadio.voice = updateRadio.voice;
                    clientRadio.botType = updateRadio.botType;

                    if (playerRadioInfo.simultaneousTransmissionControl == DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
                    {
                        clientRadio.simul = updateRadio.simul;
                    }

                    if (updateRadio.simul)
                    {
                        simul = true;
                    }

                    clientRadio.name = updateRadio.name;

                    clientRadio.modulation = updateRadio.modulation;

                    //update modes
                    clientRadio.freqMode = updateRadio.freqMode;
                    clientRadio.guardFreqMode = updateRadio.guardFreqMode;

                    if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION))
                    {
                        clientRadio.encMode = updateRadio.encMode;
                    }
                    else
                    {
                        clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    }

                    clientRadio.volMode = updateRadio.volMode;

                    if ((updateRadio.freqMode == RadioInformation.FreqMode.COCKPIT) || overrideFreqAndVol)
                    {
                        if (!DCSPlayerRadioInfo.FreqCloseEnough(clientRadio.freq, updateRadio.freq))
                            changed = true;

                        if (!DCSPlayerRadioInfo.FreqCloseEnough(clientRadio.secFreq, updateRadio.secFreq))
                            changed = true;

                        clientRadio.freq = updateRadio.freq;

                        if (newAircraft && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            //default guard to off
                            clientRadio.secFreq = 0;
                        }
                        else
                        {
                            if (clientRadio.secFreq != 0 && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                            {
                                //put back
                                clientRadio.secFreq = updateRadio.secFreq;
                            }
                            else if (clientRadio.secFreq == 0 && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                            {
                                clientRadio.secFreq = 0;
                            }
                            else
                            {
                                clientRadio.secFreq = updateRadio.secFreq;
                            }

                        }



                        clientRadio.channel = updateRadio.channel;
                    }
                    else
                    {
                        if (clientRadio.secFreq != 0)
                        {
                            //put back
                            clientRadio.secFreq = updateRadio.secFreq;
                        }

                        //check we're not over a limit
                        if (clientRadio.freq > clientRadio.freqMax)
                        {
                            clientRadio.freq = clientRadio.freqMax;
                        }
                        else if (clientRadio.freq < clientRadio.freqMin)
                        {
                            clientRadio.freq = clientRadio.freqMin;
                        }
                    }

                    //reset encryption
                    if (overrideFreqAndVol)
                    {
                        clientRadio.enc = false;
                        clientRadio.encKey = 0;
                    }

                    //Handle Encryption
                    if (updateRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                    {
                        if (clientRadio.encKey == 0)
                        {
                            clientRadio.encKey = 1;
                        }
                    }
                    else if (clientRadio.encMode ==
                             RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
                    {
                        clientRadio.enc = updateRadio.enc;

                        if (clientRadio.encKey == 0)
                        {
                            clientRadio.encKey = 1;
                        }
                    }
                    else if (clientRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_FULL)
                    {
                        clientRadio.enc = updateRadio.enc;
                        clientRadio.encKey = updateRadio.encKey;
                    }
                    else
                    {
                        clientRadio.enc = false;
                        clientRadio.encKey = 0;
                    }

                    //handle volume
                    if ((updateRadio.volMode == RadioInformation.VolumeMode.COCKPIT) || overrideFreqAndVol)
                    {
                        clientRadio.volume = updateRadio.volume;
                    }

                    //handle Channels load for radios
                    if (newAircraft && i > 0)
                    {
                        if (clientRadio.freqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            var channelModel = _clientStateSingleton.FixedChannels[i - 1];
                            channelModel.Max = clientRadio.freqMax;
                            channelModel.Min = clientRadio.freqMin;
                            channelModel.Reload();
                            clientRadio.channel = -1; //reset channel
                        }
                        else
                        {
                            _clientStateSingleton.FixedChannels[i - 1].Clear();
                            //clear
                        }
                    }
                }
            }


            if (playerRadioInfo.simultaneousTransmissionControl ==
                DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
            {
                playerRadioInfo.simultaneousTransmission = simul;
            }

            //HANDLE MIC IDENT
            if (!playerRadioInfo.ptt && playerRadioInfo.iff.mic > 0 && UdpVoiceHandler.RadioSendingState.IsSending)
            {
                if (UdpVoiceHandler.RadioSendingState.SendingOn == playerRadioInfo.iff.mic)
                {
                    playerRadioInfo.iff.status = Transponder.IFFStatus.IDENT;
                }
            }

            //Handle IDENT only lasting for 40 seconds at most - need to toggle it
            if (playerRadioInfo.iff.status == Transponder.IFFStatus.IDENT)
            {
                if (_identStart == 0)
                {
                    _identStart = DateTime.Now.Ticks;
                }

                if (TimeSpan.FromTicks(DateTime.Now.Ticks - _identStart).TotalSeconds > 40)
                {
                    playerRadioInfo.iff.status = Transponder.IFFStatus.NORMAL;
                }

            }
            else
            {
                _identStart = 0;
            }

            //                }
            //            }

            //update
            playerRadioInfo.LastUpdate = DateTime.Now.Ticks;

            return changed;
        }

        public void Stop()
        {
            _stop = true;
        }

    }
}
