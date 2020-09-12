using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.RadioCalls.LuisModels;

namespace RurouniJones.DCS.OverlordBot.RadioCalls
{
    public interface IRadioCall
    {
        /// <summary>
        /// The intent of the radio transmission
        /// </summary>
        string Intent { get; }

        string Message { get; }

        /// <summary>
        /// The player that sent the radio call.
        /// </summary>
        Player Sender { get; set; }

        /// <summary>
        /// The name of the bot that the player is attempting to contact.
        /// </summary>
        /// <example>
        /// For an AWACS bot this is the Callsign such as "Overlord" or "Magic".
        /// For an ATC bot this is the normalized name of an airfield such as "Krasnodar-Center"
        /// </example>
        string ReceiverName { get; }

        string AwacsCallsign { get; }
        string AirbaseName { get; }

        /// <summary>
        /// The deserialized response from the Azure Language Understanding application.
        /// </summary>
        LuisResponse LuisResponse { get; }
    }
}