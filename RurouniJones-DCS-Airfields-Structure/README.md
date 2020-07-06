# DCS Airfields Structure

This library provides datastructures and algorithms designed to make dealing with DCS Airfields easier.

# Features

* Provides a navigation graph for aifield taxiways.
* Provides ATC taxi instructions for a given Taxi start point and destination

# Known issues

* Currently only contains the navigation graph for Anapa-Vityatzevo when Runway 4 is active.

## Installation

Currently this project is part of the DCS-OverlordBot project and is not released separately. Please contact
@rurounijones if you wish to see this be released as a Nuget package.

## Usage

```cs
using RurouniJones.DCS.Airfields.Structure;

Airfield Anapa = Populator.Airfields.First(airfield => airfield.Name.Equals("Anapa-Vityazevo"));

ParkingSpot source = Anapa.ParkingSpot.First(spot => spot.Name.Equals("Apron 1"));
Runway target = Anapa.Runways.First(runway => runway.Name.Equals("Runway 0 4"));

string taxiInstructions = Anapa.GetTaxiInstructions(source, target);
```

Data for each airfield is stored in the `Data` folder as a JSON configuration file.

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate in the `DCS-Airfields-Structure-Tests` project.

## License
[MIT](https://choosealicense.com/licenses/mit/) License