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
            if (Database.State != System.Data.ConnectionState.Open)
            {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            var command = @"SELECT id, coalition from public.units contact
                            WHERE contact.type ilike 'Air+%'
                            AND ST_DWithin(@center, contact.position, @radius)";

            List<Contact> contacts = new List<Contact>();

            using (var cmd = new NpgsqlCommand(command, Database))
            {
                cmd.Parameters.AddWithValue("center", center);
                cmd.Parameters.AddWithValue("radius", radius);

                dbDataReader = await cmd.ExecuteReaderAsync();
                while (await dbDataReader.ReadAsync() )
                {
                    // We only really care about coalition at the moment
                    var contact = new Contact {
                        Id = dbDataReader.GetString(0),
                        Coalition = dbDataReader.GetInt32(1)
                    };
                    contacts.Add(contact);
                }
                dbDataReader.Close();
            }

            return contacts;
        }
    }
}
