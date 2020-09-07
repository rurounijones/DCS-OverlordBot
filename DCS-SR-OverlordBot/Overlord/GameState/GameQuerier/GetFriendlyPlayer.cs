using System;
using System.Data.Common;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Npgsql;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    partial class GameQuerier
    {
        public static async Task<Contact> GetFriendlyPlayer(string sourceGroup, int sourceFlight, int sourcePlane, string targetGroup, int targetFlight, int targetPlane)
        {
            var command = @"SELECT friendly.id,
                                   friendly.position,
                                   degrees(ST_AZIMUTH(request.position, friendly.position)) as bearing,
                                   ST_DISTANCE(request.position, friendly.position) as distance,
                                   friendly.altitude,
                                   friendly.heading
            FROM public.units AS friendly CROSS JOIN LATERAL
              (SELECT requester.position, requester.coalition
                FROM public.units AS requester
                WHERE (requester.pilot ILIKE '" + $"%{sourceGroup} {sourceFlight}-{sourcePlane}%" + @"' OR requester.pilot ILIKE '" + $"%{sourceGroup} {sourceFlight}{sourcePlane}%" + @"' )
              ) as request
            WHERE (friendly.pilot ILIKE '" + $"%{targetGroup} {targetFlight}-{targetPlane}%" + @"' OR friendly.pilot ILIKE '" + $"%{targetGroup} {targetFlight}{targetPlane}%" + @"' )
            AND friendly.coalition = request.coalition
            LIMIT 1";

            Contact friendly = null;

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
                        var bearing = Math.Round(dbDataReader.GetDouble(2));
                        // West == negative numbers so convert
                        if (bearing < 0) { bearing += 360; }

                        var id = dbDataReader.GetString(0);
                        var position = (Point)dbDataReader[1];
                        var range = (int)Math.Round(dbDataReader.GetDouble(3) * 0.539957d / 1000); // Nautical Miles
                        var altitude = (int)Math.Round(dbDataReader.GetDouble(4) * 3.28d / 1000d, 0) * 1000; // Feet
                        var heading = dbDataReader.GetDouble(5);

                        friendly = new Contact
                        {
                            Id = id,
                            Position = new Geo.Geometries.Point(position.Y, position.X),
                            Pilot = $"{targetGroup} {targetFlight} {targetPlane}",
                            Altitude = altitude,
                            Range = range,
                            Bearing = (int)bearing,
                            Heading = (int)heading
                        };
                    }
                    dbDataReader.Close();
                }
            }
            return friendly;
        }
    }
}
