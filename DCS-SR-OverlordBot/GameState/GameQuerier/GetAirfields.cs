using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Npgsql;
using RurouniJones.DCS.OverlordBot.Models;

namespace RurouniJones.DCS.OverlordBot.GameState
{
    public partial class GameQuerier
    {
        public static async Task<IEnumerable<Airfield>> GetAirfields()
        {
            var airfields = new List<Airfield>();
            const string command = @"SELECT name, position, altitude, coalition, heading, speed
            FROM units
            WHERE type = 'Ground+Static+Aerodrome'
            AND name != 'FARP';";

            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString()))
                {
                    await connection.OpenAsync();
                    using (var cmd = new NpgsqlCommand(command, connection))
                    {

                        DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                        while (await dbDataReader.ReadAsync())
                        {
                            var point = (Point) dbDataReader[1];
                            var airfield = new Airfield
                            {
                                Name = dbDataReader.GetString(0),
                                Position = new Geo.Geometries.Point(point.Y, point.X),
                                Altitude = dbDataReader.GetDouble(2),
                                Coalition = dbDataReader.GetInt32(3),
                                WindHeading = (int) dbDataReader.GetDouble(4),
                                WindSpeed = dbDataReader.GetInt32(5)

                            };
                            airfields.Add(airfield);
                        }

                        dbDataReader.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error retrieving airfields");
            }
            return airfields;
        }
    }
}
