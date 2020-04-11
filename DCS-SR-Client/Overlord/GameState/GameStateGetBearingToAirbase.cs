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
        public static async Task<Dictionary<string, int>> GetBearingToAirbase(string group, int flight, int plane, string airbase)
        {

            if (Database.State != System.Data.ConnectionState.Open)
            {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            string command = @"SELECT degrees(ST_AZIMUTH(request.position, airbase.position)) as bearing,
                                      ST_DISTANCE(request.position, airbase.position) as distance,
									  airbase.name
            FROM public.units AS airbase CROSS JOIN LATERAL
              (SELECT requester.position, requester.coalition
                FROM public.units AS requester
                WHERE (requester.pilot ILIKE '" + $"%{group} {flight}-{plane}%" + @"' OR requester.pilot ILIKE '" + $"%{group} {flight}{plane}%" + @"' )
              ) as request
            WHERE (
				airbase.type = 'Ground+Static+Aerodrome'
			    AND airbase.name = " + $"'{airbase}'" + @"
			) OR (
                airbase.type = 'Sea+Watercraft+AircraftCarrier'
				AND airbase.name = " + $"'{airbase}'" + @"
			)
            LIMIT 1";

            Logger.Trace(command);

            Dictionary<string, int> output = null;

            using (var cmd = new NpgsqlCommand(command, Database))
            {
                dbDataReader = await cmd.ExecuteReaderAsync();
                await dbDataReader.ReadAsync();

                if (dbDataReader.HasRows)
                {
                    var bearing = (int)Math.Round(dbDataReader.GetDouble(0) - 6);
                    // West == negative numbers so convert
                    if (bearing < 0) { bearing += 360; }

                    var range = (int)Math.Round((dbDataReader.GetDouble(1) * 0.539957d) / 1000); // Nautical Miles

                    output = new Dictionary<string, int>();
                    output.Add("bearing", bearing);
                    output.Add("range", range);
                }
                dbDataReader.Close();
            }

            return output;

        }
    }
}
