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
            if (Database.State != System.Data.ConnectionState.Open)
            {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            string command = @"SELECT id, position FROM public.units WHERE (pilot ILIKE '" + $"%{group} {flight}-{plane}%' OR pilot ILIKE '" + $"%{group} {flight}{plane}%')";

            Logger.Debug(command);

            using (var cmd = new NpgsqlCommand(command, Database))
            {
                dbDataReader = await cmd.ExecuteReaderAsync();
                await dbDataReader.ReadAsync();
                if (dbDataReader.HasRows)
                {
                    var id = dbDataReader.GetString(0);
                    var position = (Point) dbDataReader[1];
                    dbDataReader.Close();
                    return new GameObject
                    {
                        Id = id,
                        Position = position
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
