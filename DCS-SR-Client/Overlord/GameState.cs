﻿using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    class GameState
    {
        private static string connectionString = $"Host=192.168.1.27;Port=5432;Database=tac_scribe;Username=tac_scribe;Password=tac_scribe;";
        private static NpgsqlConnection Database = new NpgsqlConnection(connectionString);


        public static async Task<bool> DoesPilotExist(string group, int flight, int plane)
        {
            if (Database.State != System.Data.ConnectionState.Open)
            {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            String command = @"SELECT id FROM public.units WHERE pilot LIKE '" + $"%{group.ToUpper()} {flight}-{plane}%'";

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
                WHERE requester.pilot LIKE '" + $"%{group.ToUpper()} {flight}-{plane}%" + @"'
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
