namespace OpenRA.Platforms.Default
{
    public class DefaultEcosystem : IEcosystem
    {
        public bool AllowPlayerNameChange => true;

        public string PlayerName { get; set; } = "Commander";

        public void Dispose()
        {
        }
    }
}
