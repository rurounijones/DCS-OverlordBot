using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Geo.Geometries;
using Npgsql;
using RurouniJones.DCS.OverlordBot.Util;

namespace RurouniJones.DCS.OverlordBot.GameState
{
    partial class GameQuerier
    {
        public static async Task<Dictionary<string, int>> GetBearingToNamedAirbase(Point callerPosition, string group, int flight, int plane, string airbase)
        {
            var command = @"SELECT degrees(ST_AZIMUTH(request.position, airbase.position)) as bearing,
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

            Dictionary<string, int> output = null;

            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    await dbDataReader.ReadAsync();
                    if (dbDataReader.HasRows)
                    {
                        var bearing = Geospatial.TrueToMagnetic(callerPosition, Math.Round(dbDataReader.GetDouble(0)));
                        // West == negative numbers so convert
                        if (bearing < 0) { bearing += 360; }

                        var range = (int)Math.Round(dbDataReader.GetDouble(1) * 0.539957d / 1000); // Nautical Miles

                        output = new Dictionary<string, int>
                        {
                            { "bearing", (int) Math.Round(bearing) },
                            { "range", range }
                        };
                    }
                    dbDataReader.Close();
                }
            }
            return output;
        }
    }
}
