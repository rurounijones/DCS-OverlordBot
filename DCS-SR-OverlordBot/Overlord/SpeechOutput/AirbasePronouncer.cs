namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput
{
    public static class AirbasePronouncer
    {
        /// <summary>
        /// Pronounces the airbase as a human GCI / ATC without *all* the words and using correct IPA pronunciation
        /// </summary>
        /// <remarks>
        /// The output of this method is intended to be used in an SSML string for pronunciation by the Azure Speech Service.
        /// </remarks>
        /// <example>
        /// "Al Dhafra AB" -> "Al Dhafra"
        /// "Henderson Executive -> "Henderson"
        /// "McCarren Intl Airport" -> "McCarren"
        /// </example>
        /// <param name="airbase">The DCS airbase name.</param>
        /// <returns>An SSML compatible string with the colloqial airbase name</returns>
        public static string PronounceAirbase(string airbase)
        {
            // TODO - Try and find the phonetic representation of all airbases on caucasus, including the russian carrier
            switch (airbase.ToLower())
            {
                case "krymsk":
                    return "<phoneme alphabet=\"ipa\" ph=\"ˈkrɨm.sk\">Krymsk</phoneme>";
                case "kutaisi":
                    return "<phoneme alphabet=\"ipa\" ph=\"kuˈtaɪ si\">Kutaisi</phoneme>";
                case "mineralnye vody":
                    return "<phoneme alphabet=\"ipa\" ph=\"mʲɪnʲɪˈralʲnɨjə ˈvodɨ\">Mineralnye Vody</phoneme>";
                case "gelendzhik":
                    return "<phoneme alphabet=\"ipa\" ph=\"ɡʲɪlʲɪnd͡ʐˈʐɨk\">Gelendzhik</phoneme>";
                case "kobuleti":
                    return "<phoneme alphabet=\"ipa\" ph=\"kʰɔbulɛtʰi\">Kobuleti</phoneme>";
            }
            // Remove all the ancillery words that we do not care about when spoken. Include the leading space. This is a 
            // catch-all for reducing the length of airfields that do not have a specific pronunciation defined above.
            return airbase
                .Replace(" Airport", "")
                .Replace(" AB", "")
                .Replace(" Intl", "")
                .Replace(" Airfield", "")
                .Replace(" AFB", "")
                .Replace(" International", "")
                .Replace(" Executive", "") // Henderson in Persian gulf
                .Replace(" Airstrip", "")
                .Replace(" Island", "");
        }
    }

}
