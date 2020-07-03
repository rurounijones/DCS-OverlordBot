using NLog;
using Npgsql;
using System.Data.Common;
using System.Threading.Tasks;
using NewRelic.Api.Agent;
using NetTopologySuite.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    partial class GameQuerier
    {
        [Trace]
        public static async Task<Player> GetPilotData(string group, int flight, int plane)
        {
            string command = @"SELECT id, position, coalition, altitude, pilot FROM public.units WHERE (pilot ILIKE '" + $"%{group} {flight}-{plane}%' OR pilot ILIKE '" + $"%{group} {flight}{plane}%')";
            using (var connection = new NpgsqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand(command, connection))
                {
                    DbDataReader dbDataReader = await cmd.ExecuteReaderAsync();
                    await dbDataReader.ReadAsync();
                    if (dbDataReader.HasRows)
                    {
                        var id = dbDataReader.GetString(0);
                        var position = (Point)dbDataReader[1];
                        var coalition = dbDataReader.GetInt32(2);
                        var altitude = dbDataReader.GetDouble(3);
                        var pilot = dbDataReader.GetString(4);
                        dbDataReader.Close();
                        return new Player
                        {
                            Id = id,
                            Position = new Geo.Geometries.Point(position.Y, position.X),
                            Coalition = (Coalition) coalition,
                            Pilot = pilot,
                            Altitude = altitude,
                            Group = group,
                            Flight = flight,
                            Plane = plane
                        };
                    }
                    else
                    {
                        dbDataReader.Close();
                        return null;
                    }
                }
            }
        }
    }
}
