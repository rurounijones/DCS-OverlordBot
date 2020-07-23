using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Preferences
{
    public interface IFavouriteServerStore
    {
        IEnumerable<ServerAddress> LoadFromStore();

        bool SaveToStore(IEnumerable<ServerAddress> addresses);
    }
}