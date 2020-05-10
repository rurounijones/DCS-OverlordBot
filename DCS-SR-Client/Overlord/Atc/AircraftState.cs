using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using NLog;
using Stateless;
using Stateless.Graph;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc
{
    public class AircraftState
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public enum State { OnTaxiwayRunwayBoundary, OnGround, Flying, Inbound, DownwindEntry, Downwind, Base, Final, ShortFinal, OnRunway, Outbound }
        public enum Trigger { EnterTaxiwayRunwayBoundaryFromRunway, EnterTaxiwayRunwayBoundaryFromTaxiway, EnterTaxiwayFromRunway, EnterTaxiwayFromTaxiwayRunwayBoundary,
            EnterRunway, Takeoff, StartInbound, TurnEntryDownwind, TurnDownwind, TurnFinal, TurnBase, EnterShortFinal, Land }

        private readonly StateMachine<State, Trigger> _aircraftState;

        private readonly Airfield _airfield;
        private GameObject _aircraft;
        private Runway _runway;
        private readonly string _callsign = null;

        private readonly StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> _enterTaxiwayRunwayBoundaryFromRunwayTrigger;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> _enterTaxiwayRunwayBoundaryFromTaxiwayTrigger;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<Runway> _enterRunwayTrigger;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<Runway> _landTrigger;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<Runway> _takeoffTrigger;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<Runway> _enterTaxiwayFromRunwayTrigger;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> _enterTaxiwayFromBoundaryTrigger;

        public string Graph {
            get {
                return UmlDotGraph.Format(_aircraftState.GetInfo());
            }
        }

        public State CurrentState { 
            get
            {
                return _aircraftState.State;
            } 
        }

        public AircraftState(Airfield airfield, GameObject aircraft, State initialState)
        {
            _airfield = airfield;
            _aircraft = aircraft;

            Player player = aircraft as Player;
            if (player != null)
            {
                _callsign = player.Callsign;
            }

            SendToDiscord($"New Aircraft, ID: {aircraft.Id} (Pilot: {aircraft.Pilot}), State: {initialState}, Airfield: {airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} /  {_aircraft.Position.Coordinate.Longitude}");

            // Create the State Machine
            _aircraftState = new StateMachine<State, Trigger>(initialState);
            _aircraftState.OnUnhandledTrigger((state, trigger) => UnhandledTrigger(state, trigger));

            // Configure the triggers that take parameters
            _enterTaxiwayRunwayBoundaryFromRunwayTrigger = _aircraftState.SetTriggerParameters<NavigationPoint>(Trigger.EnterTaxiwayRunwayBoundaryFromRunway);
            _enterTaxiwayRunwayBoundaryFromTaxiwayTrigger = _aircraftState.SetTriggerParameters<NavigationPoint>(Trigger.EnterTaxiwayRunwayBoundaryFromTaxiway);
            _enterRunwayTrigger = _aircraftState.SetTriggerParameters<Runway>(Trigger.EnterRunway);
            _landTrigger = _aircraftState.SetTriggerParameters<Runway>(Trigger.Land);
            _takeoffTrigger = _aircraftState.SetTriggerParameters<Runway>(Trigger.Takeoff);
            _enterTaxiwayFromRunwayTrigger = _aircraftState.SetTriggerParameters<Runway>(Trigger.EnterTaxiwayFromRunway);
            _enterTaxiwayFromBoundaryTrigger = _aircraftState.SetTriggerParameters<NavigationPoint>(Trigger.EnterTaxiwayFromTaxiwayRunwayBoundary);

            // Configure the states and transitions
            _aircraftState.Configure(State.Flying)
                .Permit(Trigger.StartInbound, State.Inbound);

            _aircraftState.Configure(State.Inbound)
                .Permit(Trigger.TurnEntryDownwind, State.DownwindEntry)
                .Permit(Trigger.TurnDownwind, State.Downwind); // In case the player cuts the entry because of limitations

            _aircraftState.Configure(State.DownwindEntry)
                .OnEntryFrom(Trigger.TurnEntryDownwind, OnTurnEntryDownwind)
                .Permit(Trigger.TurnDownwind, State.Downwind);

            _aircraftState.Configure(State.Downwind)
                .OnEntryFrom(Trigger.TurnDownwind, OnTurnDownwind)
                .Permit(Trigger.TurnBase, State.Base);

            _aircraftState.Configure(State.Base)
                .OnEntryFrom(Trigger.TurnBase, OnTurnBase)
                .Permit(Trigger.TurnFinal, State.Final);

            _aircraftState.Configure(State.Final)
                .OnEntryFrom(Trigger.TurnFinal, OnTurnFinal)
                .Permit(Trigger.EnterShortFinal, State.ShortFinal);

            _aircraftState.Configure(State.ShortFinal)
                .OnEntryFrom(Trigger.EnterShortFinal, OnEnterShortFinal)
                .Permit(Trigger.Land, State.OnRunway);

            _aircraftState.Configure(State.OnRunway)
                .Ignore(Trigger.Land)
                .Ignore(Trigger.EnterRunway)
                .OnEntryFrom(_enterRunwayTrigger, runway => OnEnterRunway(runway))
                .OnEntryFrom(_landTrigger, runway => OnLandRunway(runway))
                .Permit(Trigger.Takeoff, State.Outbound) // Do we need a specific "MissedApproach" State ?
                .Permit(Trigger.EnterTaxiwayRunwayBoundaryFromRunway, State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterTaxiwayFromRunway, State.OnGround); // If a plane taxis fast enough this is possible although it shouldn't happen!


            _aircraftState.Configure(State.OnTaxiwayRunwayBoundary)
                .OnEntryFrom(_enterTaxiwayRunwayBoundaryFromRunwayTrigger, navigationPoint => OnEnterTaxiwayRunwayBoundaryFromTaxiway(navigationPoint))
                .OnEntryFrom(_enterTaxiwayRunwayBoundaryFromTaxiwayTrigger, navigationPoint => OnEnterTaxiwayRunwayBoundaryFromRunway(navigationPoint))
                .Permit(Trigger.EnterTaxiwayFromTaxiwayRunwayBoundary, State.OnGround)
                .Permit(Trigger.EnterRunway, State.OnRunway);

            _aircraftState.Configure(State.Outbound)
                .OnEntryFrom(_takeoffTrigger, runway => OnTakeoffRunway(runway))
                .Permit(Trigger.StartInbound, State.Inbound) // RTB
                .Permit(Trigger.TurnDownwind, State.Downwind) // Immediate return?
                .Permit(Trigger.Land, State.OnRunway); // Bad bouncy take-off that loses altitude again back to the runway. Or an aborted one

            _aircraftState.Configure(State.OnGround)
                .OnEntryFrom(_enterTaxiwayFromRunwayTrigger, runway => OnEnterTaxiway(runway))
                .OnEntryFrom(_enterTaxiwayFromBoundaryTrigger, navigationPoint => OnEnterTaxiway(navigationPoint))
                .Permit(Trigger.EnterTaxiwayRunwayBoundaryFromTaxiway, State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterRunway, State.OnRunway); // If a plane taxis fast enough this is possible although it shouldn't happen!
        }

        private void UnhandledTrigger(State state, Trigger trigger)
        {
            Logger.Error($"Unhandled Trigger: {trigger}, State: {state}, Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}), Airfield : {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        private void SendToDiscord(string message)
        {
            Logger.Debug(message);
            _ = Discord.DiscordClient.SendToAtcLogChannel(message);
        }

        public void EnterRunway(Runway runway) {_aircraftState.Fire(_enterRunwayTrigger, runway);}
        private void OnEnterRunway(Runway runway)
        {
            _runway = runway;
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered {runway.Name} at {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        public void Land(Runway runway) {_aircraftState.Fire(_landTrigger, runway);}
        private void OnLandRunway(Runway runway)
        {
            _runway = runway;
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) landed on {runway.Name} at {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        public void Takeoff() { _aircraftState.Fire(_takeoffTrigger, _runway); }
        private void OnTakeoffRunway(Runway runway)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) took off from {runway.Name} at {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        public void EnterTaxiway(NavigationPoint navigationPoint) { _aircraftState.Fire(_enterTaxiwayFromBoundaryTrigger, navigationPoint); }
        private void OnEnterTaxiway(NavigationPoint navigationPoint)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered taxiway from {navigationPoint.Name} at {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        public void EnterTaxiway(Runway runway) { _aircraftState.Fire(_enterTaxiwayFromRunwayTrigger, runway); }
        private void OnEnterTaxiway(Runway runway)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered taxiway from {runway.Name}  at {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        public void EnterBoundaryFromRunway(NavigationPoint navigationPoint) { _aircraftState.Fire(_enterTaxiwayRunwayBoundaryFromRunwayTrigger, navigationPoint); }
        private void OnEnterTaxiwayRunwayBoundaryFromTaxiway(NavigationPoint navigationPoint)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered {navigationPoint.Name} from taxiway at {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        public void EnterBoundaryFromTaxiway(NavigationPoint navigationPoint) { _aircraftState.Fire(_enterTaxiwayRunwayBoundaryFromTaxiwayTrigger, navigationPoint); }
        private void OnEnterTaxiwayRunwayBoundaryFromRunway(NavigationPoint navigationPoint)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered {navigationPoint.Name} from runway at {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}");
        }

        public void TurnEntryDownwind() {
            _aircraftState.Fire(Trigger.TurnEntryDownwind);
        }

        private void OnTurnEntryDownwind()
        {
            var heading = HeadingTo("Downwind");

            var statusUpdate = $"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered start of entry to the downwind for {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}";
            var text = $"{_callsign}, {_airfield.Name} approach, turn heading {heading}, speed 1 8 0 knots";

            SendToDiscord(statusUpdate);
            SendToPlayer(text);
        }

        public void TurnDownwind()
        {
            _aircraftState.Fire(Trigger.TurnDownwind);
        }

        private void OnTurnDownwind()
        {
            var heading = HeadingTo("Base");

            var statusUpdate = $"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered start of downwind for {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}";
            var text = $"{_callsign}, turn heading {heading}";

            SendToDiscord(statusUpdate);
            SendToPlayer(text);
        }

        public void TurnBase()
        {
            _aircraftState.Fire(Trigger.TurnBase);
        }
        public void OnTurnBase()
        {
            var heading = HeadingTo("Final");

            var statusUpdate = $"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered start of base for {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}";
            var text = $"{_callsign}, turn heading {heading}";

            SendToDiscord(statusUpdate);
            SendToPlayer(text);
        }

        public void TurnFinal()
        {
            _aircraftState.Fire(Trigger.TurnFinal);
        }
        public void OnTurnFinal()
        {
            var heading = HeadingTo("ShortFinal");

            var statusUpdate = $"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered start of final for {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}";
            var text = $"{_callsign}, turn heading {heading} for final, cleared to land";

            SendToDiscord(statusUpdate);
            SendToPlayer(text);
        }

        public void EnterShortFinal()
        {
            _aircraftState.Fire(Trigger.EnterShortFinal);
        }
        public void OnEnterShortFinal()
        {
            var statusUpdate = $"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entering short final for {_airfield.Name}, Lat/Lon: {_aircraft.Position.Coordinate.Latitude} / {_aircraft.Position.Coordinate.Longitude}";
            SendToDiscord(statusUpdate);
        }

        private string HeadingTo(string name)
        {
            var nextPoint = _airfield.LandingPatternPoints.Find(x => x.Name == name);
            var trueBearingToNextPoint = Util.Geospatial.BearingTo(_aircraft.Position.Coordinate, nextPoint.Position.Center);
            var magneticHeadingToNextPoint = Util.Geospatial.TrueToMagnetic(_aircraft.Position, trueBearingToNextPoint);
            return Regex.Replace(magneticHeadingToNextPoint.ToString("000"), "\\d{1}", " $0");
        }

        private void SendToPlayer(string text)
        {
            byte[] response = null;
            if (_callsign != null)
            {
                Logger.Debug(text);
                SendToDiscord(text);
                var fullText = $"<speak version =\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"en-US-BenjaminRUS\">{text}</voice></speak>";
                response = Task.Run(() => Speaker.CreateResponse(fullText)).Result;
            }
            if(response != null) {
               AudioManager.Instance.ResponseQueues[2].Enqueue(response);
            }
        }

        public void UpdatePosition(GameObject aircraft)
        {
            _aircraft.Position = aircraft.Position;
            _aircraft.Altitude = aircraft.Altitude;
        }
    }
}