using NLog;
using Npgsql;
using System.Data.Common;
using System.Threading.Tasks;
using NewRelic.Api.Agent;
using NetTopologySuite.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    partial class GameState
    {
        [Trace]
        public static async Task<GameObject> GetPilotData(string group, int flight, int plane)
        {
            string command = @"SELECT id, position, coalition FROM public.units WHERE (pilot ILIKE '" + $"%{group} {flight}-{plane}%' OR pilot ILIKE '" + $"%{group} {flight}{plane}%')";
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
                        dbDataReader.Close();
                        return new GameObject
                        {
                            Id = id,
                            Position = position,
                            Coalition = coalition,
                            Pilot = $"{group} {flight} {plane}"
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
