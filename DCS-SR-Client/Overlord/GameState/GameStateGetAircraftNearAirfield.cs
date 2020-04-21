using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    public partial class GameState
    {

        public static async Task<List<GameObject>> GetAircraftNearAirfield(Airfield airfield)
        {
           var gameObjects = new List<GameObject>();

            var command = @"SELECT contact.id, contact.pilot, contact.position
            FROM units as contact
            WHERE ST_DWithin(@airfield, contact.position, @radius)
            AND contact.altitude < 915
            AND contact.type LIKE 'Air+%';";

            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    var position = new Point(airfield.Position.Coordinate.Longitude, airfield.Position.Coordinate.Latitude);

                    cmd.Parameters.AddWithValue("airfield", position);
                    cmd.Parameters.AddWithValue("radius", 18520);

                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    while (await dbDataReader.ReadAsync())
                    {
                        var gameObject = new GameObject
                        {
                            Id = dbDataReader.GetString(0),
                            Pilot = dbDataReader.GetString(1),
                            Position = (Point)dbDataReader[2]
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
