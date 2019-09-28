using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    class Constants
    {
        public const string SPEECH_REGION = "japaneast";
        public const string SPEECH_SUBSCRIPTION_KEY = "6bee33c69d124444978e93529e362774";
        public const string SPEECH_CUSTOM_ENDPOINT_ID = "1765a573-4f25-41a5-a2ae-6fdd730dc1e9";

        public const string LUIS_APP_ID = "2414f770-d586-4707-8e0b-93ce0738c5bf";
        public const string LUIS_ENDPOINT_KEY = "b2c5aaf0b1d54d18a033c4efb7ec1ffc";

        public const string TAC_SCRIBE_HOST = "192.168.1.27";
        public const string TAC_SCRIBE_PORT = "5432";
        public const string TAC_SCRIBE_DATABASE = "tac_scribe";
        public const string TAC_SCRIBE_USERNAME = "tac_scribe";
        public const string TAC_SCRIBE_PASSWORD = "tac_scribe";
        public const bool TAC_SCRIBE_FORCE_SSL = false;
    }
}