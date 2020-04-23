using Stateless;
using Stateless.Graph;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc
{
    public class AircraftState
    {
        public enum State { OnTaxiwayRunwayBoundary, OnGround, Flying, Inbound, Downwind, Base, Final, ShortFinal, OnRunway, Outbound }
        public enum Trigger { EnterTaxiwayRunwayBoundary, EnterTaxiway, EnterRunway, Takeoff, StartInbound, TurnDownwind, TurnFinal, TurnBase, EnterShortFinal, Land }

        private readonly StateMachine<State, Trigger> _aircraftState;

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

        public AircraftState(State state)
        {
            _aircraftState = new StateMachine<State, Trigger>(state);
            _aircraftState.Configure(State.Flying)
                .Permit(Trigger.StartInbound, State.Inbound);
            _aircraftState.Configure(State.Inbound)
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
                .Permit(Trigger.EnterTaxiwayRunwayBoundary, State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterTaxiway, State.OnGround); // If a plane taxis fast enough this is possible although it shouldn't happen!
            _aircraftState.Configure(State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterTaxiway, State.OnGround)
                .Permit(Trigger.EnterRunway, State.OnRunway);
            _aircraftState.Configure(State.Outbound)
                .Permit(Trigger.StartInbound, State.Inbound) // RTB
                .Permit(Trigger.TurnDownwind, State.Downwind) // Immediate return?
                .Permit(Trigger.Land, State.OnRunway); // Bad bouncy take-off that loses altitude again back to the runway. Or an aborted one
            _aircraftState.Configure(State.OnGround)
                .Permit(Trigger.EnterTaxiwayRunwayBoundary, State.OnTaxiwayRunwayBoundary)
                .Permit(Trigger.EnterRunway, State.OnRunway); // If a plane taxis fast enough this is possible although it shouldn't happen!
        }

        public void Update(Trigger trigger)
        {
            _aircraftState.FireAsync(trigger);
        }
    }
}
