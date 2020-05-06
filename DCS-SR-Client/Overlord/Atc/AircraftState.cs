using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation;
using NLog;
using Stateless;
using Stateless.Graph;

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
        private readonly GameObject _aircraft;
        private Runway _runway;


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

            SendToDiscord($"New Aircraft, ID: {aircraft.Id} (Pilot: {aircraft.Pilot}), State: {initialState}, Airfield: {airfield.Name}");

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
                .Permit(Trigger.TurnDownwind, State.Downwind);

            _aircraftState.Configure(State.Downwind)
                .Permit(Trigger.TurnBase, State.Base);

            _aircraftState.Configure(State.Base)
                .Permit(Trigger.TurnFinal, State.Final);

            _aircraftState.Configure(State.Final)
                .Permit(Trigger.EnterShortFinal, State.ShortFinal);

            _aircraftState.Configure(State.ShortFinal)
                .Permit(Trigger.Land, State.OnRunway);

            _aircraftState.Configure(State.OnRunway)
                .Permit(Trigger.Takeoff, State.Outbound) // Do we need a specific "MissedApproach" State ?
                .Permit(Trigger.EnterTaxiwayRunwayBoundaryFromRunway, State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterTaxiwayFromRunway, State.OnGround) // If a plane taxis fast enough this is possible although it shouldn't happen!
                .Ignore(Trigger.Land)
                .Ignore(Trigger.EnterRunway)
                .OnEntryFrom(_enterRunwayTrigger, runway => OnEnterRunway(runway))
                .OnEntryFrom(_landTrigger, runway => OnLandRunway(runway));


            _aircraftState.Configure(State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterTaxiwayFromTaxiwayRunwayBoundary, State.OnGround)
                .Permit(Trigger.EnterRunway, State.OnRunway)
                .OnEntryFrom(_enterTaxiwayRunwayBoundaryFromRunwayTrigger, navigationPoint => OnEnterTaxiwayRunwayBoundaryFromTaxiway(navigationPoint))
                .OnEntryFrom(_enterTaxiwayRunwayBoundaryFromTaxiwayTrigger, navigationPoint => OnEnterTaxiwayRunwayBoundaryFromRunway(navigationPoint));

            _aircraftState.Configure(State.Outbound)
                .Permit(Trigger.StartInbound, State.Inbound) // RTB
                .Permit(Trigger.TurnDownwind, State.Downwind) // Immediate return?
                .Permit(Trigger.Land, State.OnRunway) // Bad bouncy take-off that loses altitude again back to the runway. Or an aborted one
                .OnEntryFrom(_takeoffTrigger, runway => OnTakeoffRunway(runway));

            _aircraftState.Configure(State.OnGround)
                .Permit(Trigger.EnterTaxiwayRunwayBoundaryFromTaxiway, State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterRunway, State.OnRunway) // If a plane taxis fast enough this is possible although it shouldn't happen!
                .OnEntryFrom(_enterTaxiwayFromRunwayTrigger, runway => OnEnterTaxiway(runway))
                .OnEntryFrom(_enterTaxiwayFromBoundaryTrigger, navigationPoint => OnEnterTaxiway(navigationPoint));
        }

        private void UnhandledTrigger(State state, Trigger trigger)
        {
            Logger.Error($"Unhandled Trigger: {trigger}, State: {state}, Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}), Airfield : {_airfield.Name}");
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
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered {runway.Name} at {_airfield.Name}");
        }

        public void Land(Runway runway) {_aircraftState.Fire(_landTrigger, runway);}
        private void OnLandRunway(Runway runway)
        {
            _runway = runway;
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) landed on {runway.Name} at {_airfield.Name}");
        }

        public void Takeoff() { _aircraftState.Fire(_takeoffTrigger, _runway); }
        private void OnTakeoffRunway(Runway runway)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) took off from {runway.Name} at {_airfield.Name}");
        }

        public void EnterTaxiway(NavigationPoint navigationPoint) { _aircraftState.Fire(_enterTaxiwayFromBoundaryTrigger, navigationPoint); }
        private void OnEnterTaxiway(NavigationPoint navigationPoint)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered taxiway from {navigationPoint.Name} at {_airfield.Name}");
        }

        public void EnterTaxiway(Runway runway) { _aircraftState.Fire(_enterTaxiwayFromRunwayTrigger, runway); }
        private void OnEnterTaxiway(Runway runway)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered taxiway from {runway.Name}  at {_airfield.Name}");
        }

        public void EnterBoundaryFromRunway(NavigationPoint navigationPoint) { _aircraftState.Fire(_enterTaxiwayRunwayBoundaryFromRunwayTrigger, navigationPoint); }
        private void OnEnterTaxiwayRunwayBoundaryFromTaxiway(NavigationPoint navigationPoint)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered {navigationPoint.Name} from taxiway at {_airfield.Name}");
        }

        public void EnterBoundaryFromTaxiway(NavigationPoint navigationPoint) { _aircraftState.Fire(_enterTaxiwayRunwayBoundaryFromTaxiwayTrigger, navigationPoint); }
        private void OnEnterTaxiwayRunwayBoundaryFromRunway(NavigationPoint navigationPoint)
        {
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entered {navigationPoint.Name} from runway at {_airfield.Name}");
        }

        public void TurnEntryDownwind() {
            _aircraftState.Fire(Trigger.TurnEntryDownwind);
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) turning for entry to the downwind for {_airfield.Name}");
        }

        public void TurnDownwind()
        {
            _aircraftState.Fire(Trigger.TurnDownwind);
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) turning downwind  for {_airfield.Name}");
        }

        public void TurnBase()
        {
            _aircraftState.Fire(Trigger.TurnBase);
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) turning base  for {_airfield.Name}");
        }

        public void TurnFinal()
        {
            _aircraftState.Fire(Trigger.TurnFinal);
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) turning final  for {_airfield.Name}");
        }

        public void EnterShortFinal()
        {
            _aircraftState.Fire(Trigger.EnterShortFinal);
            SendToDiscord($"Aircraft ID: {_aircraft.Id} (Pilot {_aircraft.Pilot}) entering short final for {_airfield.Name}");
        }
    }
}
