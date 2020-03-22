using NLog;
using Npgsql;
using System.Data.Common;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    partial class GameState
    {
        [Trace]
        public static async Task<string> DoesPilotExist(string group, int flight, int plane)
        {
            if (Database.State != System.Data.ConnectionState.Open)
            {
                await Database.OpenAsync();
            }
            DbDataReader dbDataReader;

            string command = @"SELECT id FROM public.units WHERE (pilot ILIKE '" + $"%{group} {flight}-{plane}%' OR pilot ILIKE '" + $"%{group} {flight}{plane}%')";

            Logger.Debug(command);

            using (var cmd = new NpgsqlCommand(command, Database))
            {
                dbDataReader = await cmd.ExecuteReaderAsync();
                await dbDataReader.ReadAsync();
                if (dbDataReader.HasRows)
                {
                    var id = dbDataReader.GetString(0);
                    dbDataReader.Close();
                    return id;
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
