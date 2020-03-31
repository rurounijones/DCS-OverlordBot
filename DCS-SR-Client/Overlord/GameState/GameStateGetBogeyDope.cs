using NLog;
using Npgsql;
using System;
using System.Data.Common;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    partial class GameState
    {

        [Trace]
        public static async Task<Contact> GetBogeyDope(string group, int flight, int plane)
        {
            if (Database.State != System.Data.ConnectionState.Open)
            {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            var command = @"SELECT bogey.id,
                                   degrees(ST_AZIMUTH(request.position, bogey.position)) as bearing,
                                   ST_DISTANCE(request.position, bogey.position) as distance,
                                   bogey.altitude, bogey.heading, bogey.pilot, bogey.group
            FROM public.units AS bogey CROSS JOIN LATERAL
              (SELECT requester.position, requester.coalition
                FROM public.units AS requester
                WHERE (requester.pilot ILIKE '" + $"%{group} {flight}-{plane}%" + @"' OR requester.pilot ILIKE '" + $"%{group} {flight}{plane}%" + @"' )
              ) as request
            WHERE NOT bogey.coalition = request.coalition
            AND bogey.type LIKE 'Air+%'
            ORDER BY request.position<-> bogey.position ASC
            LIMIT 1";

            Logger.Debug(command);

            Contact output = null;

            using (var cmd = new NpgsqlCommand(command, Database))
            {
                dbDataReader = await cmd.ExecuteReaderAsync();
                await dbDataReader.ReadAsync();

                if (dbDataReader.HasRows)
                {
                    Logger.Debug($"{dbDataReader[0]}, {dbDataReader[1]}, {dbDataReader[2]}, {dbDataReader[3]}, {dbDataReader[4]}, {dbDataReader[5]}, {dbDataReader[6]}");
                    var id = dbDataReader.GetString(0);
                    var bearing = (int)Math.Round(dbDataReader.GetDouble(1));
                    // West == negative numbers so convert
                    if (bearing < 0) { bearing += 360; }

                    var range = (int)Math.Round((dbDataReader.GetDouble(2) * 0.539957d) / 1000); // Nautical Miles
                    var altitude = (int)Math.Round((dbDataReader.GetDouble(3) * 3.28d) / 1000d, 0) * 1000; // Feet
                    var heading = (int)dbDataReader.GetDouble(4);

                    output = new Contact() {
                        Id = id,
                        Bearing = bearing - 6,
                        Range = range,
                        Altitude = altitude,
                        Heading = heading - 6
                    };
                }
                dbDataReader.Close();
            }

            return output;
        }
    }
}
