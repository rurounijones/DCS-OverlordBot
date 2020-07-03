using NLog;
using Npgsql;
using System;
using System.Data.Common;
using System.Threading.Tasks;
using NewRelic.Api.Agent;
using NetTopologySuite.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    partial class GameQuerier
    {

        [Trace]
        public static async Task<Contact> GetBogeyDope(Coalition coalition, string group, int flight, int plane)
        {

            var command = @"SELECT bogey.id,
                                   degrees(ST_AZIMUTH(request.position, bogey.position)) as bearing,
                                   ST_DISTANCE(request.position, bogey.position) as distance,
                                   bogey.altitude, bogey.heading, bogey.pilot, bogey.group, bogey.name, bogey.position
            FROM public.units AS bogey CROSS JOIN LATERAL
              (SELECT requester.position, requester.coalition
                FROM public.units AS requester
                WHERE (requester.pilot ILIKE '" + $"%{group} {flight}-{plane}%" + @"' OR requester.pilot ILIKE '" + $"%{group} {flight}{plane}%" + @"' )
              ) as request
            WHERE bogey.coalition = " + (int) coalition.GetOpposingCoalition() + @"
            AND bogey.type LIKE 'Air+%'
            AND bogey.speed >= 26
            ORDER BY request.position<-> bogey.position ASC
            LIMIT 1";

            Contact output = null;

            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    await dbDataReader.ReadAsync();

                    if (dbDataReader.HasRows)
                    {
                        Logger.Debug($"{dbDataReader[0]}, {dbDataReader[1]}, {dbDataReader[2]}, {dbDataReader[3]}, {dbDataReader[4]}, {dbDataReader[5]}, {dbDataReader[6]}, {dbDataReader[7]}, {dbDataReader[8]} ");
                        var id = dbDataReader.GetString(0);
                        var bearing = dbDataReader.GetDouble(1);
                        // West == negative numbers so convert
                        if (bearing < 0) { bearing += 360; }

                        var range = (int)Math.Round((dbDataReader.GetDouble(2) * 0.539957d) / 1000); // Nautical Miles
                        var altitude = (int)Math.Round((dbDataReader.GetDouble(3) * 3.28d) / 1000d, 0) * 1000; // Feet
                        var heading = dbDataReader.GetDouble(4);
                        var point = (Point)dbDataReader[8];

                        string name;
                        try
                        {
                            name = dbDataReader.GetString(7);
                        } catch (Exception)
                        {
                            name = "unknown";
                        }
                        var position = new Geo.Geometries.Point(point.Y, point.X);

                        output = new Contact()
                        {
                            Id = id,
                            Position = position,
                            Bearing = (int)bearing,
                            Range = range,
                            Altitude = altitude,
                            Heading = (int)heading,
                            Name = name
                        };
                    }
                    dbDataReader.Close();
                }
            }

            return output;
        }
    }
}
