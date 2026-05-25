public sealed class FuelChangedEvent
{
public int CurrentFuel { get; }

        public FuelChangedEvent(int currentFuel)
        {
            CurrentFuel = currentFuel;
        }
}