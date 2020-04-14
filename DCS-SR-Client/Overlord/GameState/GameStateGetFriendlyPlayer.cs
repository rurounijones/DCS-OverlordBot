using NLog;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    partial class GameState
    {
        [Trace]
        public static async Task<Dictionary<string, int?>> GetFriendlyPlayer(string sourceGroup, int sourceFlight, int sourcePlane, string targetGroup, int targetFlight, int targetPlane)
        {
            var command = @"SELECT degrees(ST_AZIMUTH(request.position, friendly.position)) as bearing,
                                      ST_DISTANCE(request.position, friendly.position) as distance,
                                      friendly.altitude, friendly.heading, friendly.pilot, friendly.group
            FROM public.units AS friendly CROSS JOIN LATERAL
              (SELECT requester.position, requester.coalition
                FROM public.units AS requester
                WHERE (requester.pilot ILIKE '" + $"%{sourceGroup} {sourceFlight}-{sourcePlane}%" + @"' OR requester.pilot ILIKE '" + $"%{sourceGroup} {sourceFlight}{sourcePlane}%" + @"' )
              ) as request
            WHERE (friendly.pilot ILIKE '" + $"%{targetGroup} {targetFlight}-{targetPlane}%" + @"' OR friendly.pilot ILIKE '" + $"%{targetGroup} {targetFlight}{targetPlane}%" + @"' )
            LIMIT 1";

            Dictionary<string, int?> output = null;

            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    await dbDataReader.ReadAsync();

                    if (dbDataReader.HasRows)
                    {
                        Logger.Debug($"{dbDataReader[0]}, {dbDataReader[1]}, {dbDataReader[2]}, {dbDataReader[3]}, {dbDataReader[4]}, {dbDataReader[5]}");
                        var bearing = (int)Math.Round(dbDataReader.GetDouble(0));
                        // West == negative numbers so convert
                        if (bearing < 0) { bearing += 360; }

                        var range = (int)Math.Round((dbDataReader.GetDouble(1) * 0.539957d) / 1000); // Nautical Miles
                        var altitude = (int)Math.Round((dbDataReader.GetDouble(2) * 3.28d) / 1000d, 0) * 1000; // Feet
                        var heading = (int)dbDataReader.GetDouble(3);

                        output = new Dictionary<string, int?>();
                        output.Add("bearing", bearing - 6);
                        output.Add("range", range);
                        output.Add("altitude", altitude);
                        output.Add("heading", heading - 6);
                    }
                    dbDataReader.Close();
                }
            }

            return output;
        }
    }
}
