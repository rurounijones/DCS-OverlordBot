using NetTopologySuite.Geometries;
using Npgsql;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    partial class GameState
    {
        public static async Task<List<Contact>> GetContactsWithinCircle(Point center, double radius)
        {
            var command = @"SELECT id, coalition from public.units contact
                            WHERE contact.type ilike 'Air+%'
                            AND contact.speed >= 26
                            AND ST_DWithin(@center, contact.position, @radius)";

            List<Contact> contacts = new List<Contact>();

            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    cmd.Parameters.AddWithValue("center", center);
                    cmd.Parameters.AddWithValue("radius", radius);

                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    while (await dbDataReader.ReadAsync())
                    {
                        // We only really care about coalition at the moment
                        var contact = new Contact
                        {
                            Id = dbDataReader.GetString(0),
                            Coalition = dbDataReader.GetInt32(1)
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
