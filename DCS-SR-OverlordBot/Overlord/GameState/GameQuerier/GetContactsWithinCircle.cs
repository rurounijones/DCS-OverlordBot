using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Geo.Geometries;
using Npgsql;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    partial class GameQuerier
    {
        public static async Task<List<Contact>> GetContactsWithinCircle(Point center, double radius)
        {
            const string command = @"SELECT id, coalition from public.units contact
                            WHERE contact.type ilike 'Air+%'
                            AND contact.speed >= 26
                            AND ST_DWithin(@center, contact.position, @radius)";

            var contacts = new List<Contact>();

            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    cmd.Parameters.AddWithValue("center", new NetTopologySuite.Geometries.Point(center.Coordinate.Longitude, center.Coordinate.Latitude));
                    cmd.Parameters.AddWithValue("radius", radius);

                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    while (await dbDataReader.ReadAsync())
                    {
                        // We only really care about coalition at the moment
                        var contact = new Contact
                        {
                            Id = dbDataReader.GetString(0),
                            Coalition = (Coalition)dbDataReader.GetInt32(1)
                        };
                        contacts.Add(contact);
                    }
                    dbDataReader.Close();
                }
            }

            return contacts;
        }
    }
}
