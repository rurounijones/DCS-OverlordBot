using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites
{
    public class FavouriteServersViewModel
    {
        private readonly IFavouriteServerStore _favouriteServerStore;
        private readonly SettingsStore _settings = SettingsStore.Instance;

        public FavouriteServersViewModel(IFavouriteServerStore favouriteServerStore)
        {
            _favouriteServerStore = favouriteServerStore;

            Addresses.CollectionChanged += OnServerAddressesCollectionChanged;

            foreach (var favourite in _favouriteServerStore.LoadFromStore())
            {
                Addresses.Add(favourite);
            }

            NewAddressCommand = new DelegateCommand(OnNewAddress);
            RemoveSelectedCommand = new DelegateCommand(OnRemoveSelected);
            OnDefaultChangedCommand = new DelegateCommand(OnDefaultChanged);
        }

        public ObservableCollection<ServerAddress> Addresses { get; } = new ObservableCollection<ServerAddress>();

        public string NewName { get; set; }

        public string NewAddress { get; set; }

        public string NewEamCoalitionPassword { get; set; }

        public ICommand NewAddressCommand { get; }

        public ICommand RemoveSelectedCommand { get; set; }

        public ICommand OnDefaultChangedCommand { get; set; }

        public ServerAddress SelectedItem { get; set; }

        public ServerAddress DefaultServerAddress
        {
            get
            {
                var defaultAddress = Addresses.FirstOrDefault(x => x.IsDefault);
                if (defaultAddress == null && Addresses.Count > 0)
                {
                    defaultAddress = Addresses.First();
                }
                return defaultAddress;
            }
        }

        private void OnNewAddress()
        {
            var isDefault = Addresses.Count == 0;
            Addresses.Add(new ServerAddress(NewName, NewAddress, string.IsNullOrWhiteSpace(NewEamCoalitionPassword) ? null : NewEamCoalitionPassword, isDefault));

            Save();
        }

        private void OnRemoveSelected()
        {
            if (SelectedItem == null)
            {
                return;
            }

            Addresses.Remove(SelectedItem);

            if (Addresses.Count == 0 && !string.IsNullOrEmpty(_settings.GetClientSetting(SettingsKeys.LastServer).StringValue))
            {
                var oldAddress = new ServerAddress(_settings.GetClientSetting(SettingsKeys.LastServer).StringValue,
                    _settings.GetClientSetting(SettingsKeys.LastServer).StringValue, null, true);
                Addresses.Add(oldAddress);
            }

            Save();
        }

        private void Save()
        {
            var saveSucceeded = _favouriteServerStore.SaveToStore(Addresses);
            if (!saveSucceeded)
            {
                MessageBox.Show(Application.Current.MainWindow,
                    "Failed to save favourite servers. Please check logs for details.",
                    "Favourite server save failure",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDefaultChanged(object obj)
        {
            if (!(obj is ServerAddress address))
            {
                throw new InvalidOperationException();
            }

            if (address.IsDefault)
            {
                return;
            }

            address.IsDefault = true;

            foreach (var serverAddress in Addresses)
            {
                if (serverAddress != address)
                {
                    serverAddress.IsDefault = false;
                }
            }

            Save();
        }

        private void OnServerAddressesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ServerAddress address in e.NewItems)
                {
                    address.PropertyChanged += OnServerAddressPropertyChanged;
                }
            }

            if (e.OldItems == null) return;
            {
                foreach (ServerAddress address in e.OldItems)
                {
                    address.PropertyChanged -= OnServerAddressPropertyChanged;
                }
            }
        }

        private void OnServerAddressPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Saving after changing default favourite is done by OnDefaultChanged
            if (e.PropertyName != "IsDefault")
            {
                Save();
            }
        }
    }
}