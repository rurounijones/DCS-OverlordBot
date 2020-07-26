using NetTopologySuite.Geometries;
using Npgsql;
using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    public partial class GameQuerier
    {
        private static readonly int AIRFIELD_SEARCH_DISTANCE = 18520; // 10nm in meters
        private static readonly int AIRFIELD_SEARCH_HEIGHT = 915; // 3000ft in meters

        public static async Task<List<GameObject>> GetAircraftNearAirfield(Airfield airfield)
        {
            var gameObjects = new List<GameObject>();

            var command = @"SELECT contact.id, contact.pilot, contact.position, contact.altitude, contact.heading
            FROM units as contact
            WHERE ST_DWithin(@airfield, contact.position, @radius)
            AND contact.altitude < @altitude
            AND contact.type LIKE 'Air+%';";

            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    var position = new Point(airfield.Position.Coordinate.Longitude, airfield.Position.Coordinate.Latitude);

                    cmd.Parameters.AddWithValue("airfield", position);
                    cmd.Parameters.AddWithValue("radius", AIRFIELD_SEARCH_DISTANCE);
                    cmd.Parameters.AddWithValue("altitude", AIRFIELD_SEARCH_HEIGHT + airfield.Altitude);

                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    while (await dbDataReader.ReadAsync())
                    {
                        var point = (Point)dbDataReader[2];
                        var gameObject = new GameObject
                        {
                            Id = dbDataReader.GetString(0),
                            Pilot = dbDataReader.GetString(1),
                            Position = new Geo.Geometries.Point(point.Y, point.X),
                            Altitude = dbDataReader.GetDouble(3),
                            Heading = (int)dbDataReader.GetDouble(4)

                        };
                        gameObjects.Add(gameObject);
                    }
                    dbDataReader.Close();
                }
            }

            return gameObjects;
        }
    }
}
