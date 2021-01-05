using Steamworks;

namespace OpenRA.Platforms.Default
{
    // TODO to make steam work, add the redistributables from: https://partner.steamgames.com/downloads/list version: 1.48
    public class SteamEcosystem : IEcosystem
    {
        private const uint GameId = 480;

        public bool AllowPlayerNameChange => false;
        public string PlayerName
        {
            get => SteamClient.Name;
            set { }
        }

        public SteamEcosystem()
        {
            SteamClient.Init(GameId);
        }

        public void Dispose()
        {
            SteamClient.Shutdown();
        }
    }
}
