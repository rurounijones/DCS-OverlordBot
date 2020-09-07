using System;
using System.Globalization;
using System.Windows.Data;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils.ValueConverters
{
    internal class ConnectionStatusImageConverter : IValueConverter
    {
		private ClientStateSingleton ClientState { get; } = ClientStateSingleton.Instance;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var connected = (bool)value;
			if (connected) {
				return Images.IconConnected;
			}

            return ClientState.IsConnectionErrored ? Images.IconDisconnectedError : Images.IconDisconnected;
        }

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
