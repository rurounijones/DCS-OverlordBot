using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    class GameState
    {

        private static NpgsqlConnection Database = new NpgsqlConnection(ConnectionString());

        private static string ConnectionString()
        {
            var connectionString = $"Host={Constants.TAC_SCRIBE_HOST};Port={Constants.TAC_SCRIBE_PORT};Database={Constants.TAC_SCRIBE_DATABASE};" +
                                                 $"Username={Constants.TAC_SCRIBE_USERNAME};Password={Constants.TAC_SCRIBE_PASSWORD};";

            if (Constants.TAC_SCRIBE_FORCE_SSL == true) {
                connectionString += "sslmode=Require;";
            }
            return connectionString;
        }

        public static async Task<bool> DoesPilotExist(string group, int flight, int plane)
        {
            if (Database.State != System.Data.ConnectionState.Open)
            {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            String command = @"SELECT id FROM public.units WHERE pilot ILIKE '" + $"%{group} {flight}%{plane} |%'";

            using (var cmd = new NpgsqlCommand(command, Database))
            {
                dbDataReader = await cmd.ExecuteReaderAsync();
                await dbDataReader.ReadAsync();
                if (dbDataReader.HasRows)
                {
                    dbDataReader.Close();
                    return true;
                } else {
                    dbDataReader.Close();
                    return false;
                }
            }
        }

        public static async Task<Dictionary<string,int>> GetBogeyDope(string group, int flight, int plane)
        {
            if(Database.State != System.Data.ConnectionState.Open) {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            String command = @"SELECT degrees(ST_AZIMUTH(request.position, bogey.position)) as bearing,
                                      ST_DISTANCE(request.position, bogey.position) as distance,
                                      bogey.altitude, bogey.heading
            FROM public.units AS bogey CROSS JOIN LATERAL
              (SELECT requester.position, requester.coalition
                FROM public.units AS requester
                WHERE requester.pilot ILIKE '" + $"%{group} {flight}%{plane} |%" + @"'
              ) as request
            WHERE NOT bogey.coalition = request.coalition
            AND bogey.type LIKE 'Air+%'
            ORDER BY request.position<-> bogey.position ASC
            LIMIT 1";

            Dictionary<string, int> output = null;

            using (var cmd = new NpgsqlCommand(command, Database))
            {
                dbDataReader = await cmd.ExecuteReaderAsync();
                await dbDataReader.ReadAsync();

                if (dbDataReader.HasRows)
                {
                    var bearing = (int)Math.Round(dbDataReader.GetDouble(0));
                    // West == negative numbers so convert
                    if( bearing < 0 ) { bearing += 360;}

                    var range = (int)Math.Round((dbDataReader.GetDouble(1) * 0.539957d) / 1000); // Nautical Miles
                    var altitude = (int)Math.Round((dbDataReader.GetDouble(2) * 3.28d) / 1000d, 0) * 1000; // Feet
                    var heading = (int) dbDataReader.GetDouble(2);

                    output = new Dictionary<string, int>();
                    output.Add("bearing", bearing - 6);
                    output.Add("range", range);
                    output.Add("altitude", altitude);
                    output.Add("heading", heading);
                }
                dbDataReader.Close();
            }

            return output;
        }
    }
}
