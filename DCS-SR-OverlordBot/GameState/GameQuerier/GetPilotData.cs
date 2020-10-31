using System.Data.Common;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Npgsql;
using RurouniJones.DCS.OverlordBot.RadioCalls;

namespace RurouniJones.DCS.OverlordBot.GameState
{
    partial class GameQuerier
    {
        public static async Task PopulatePilotData(BaseRadioCall radioCall)
        {
            await PopulatePilotData(radioCall.Sender);
        }

        public static async Task PopulatePilotData(Player sender)
        {
            if (sender == null)
                return;
            var group = sender.Group;
            var flight = sender.Flight;
            var plane = sender.Plane;

            var command = @"SELECT id, position, coalition, altitude, pilot, speed, heading FROM public.units WHERE (pilot ILIKE '" + $"%{group} {flight}-{plane}%' OR pilot ILIKE '" + $"%{group} {flight}{plane}%')";
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
                        var speed = dbDataReader.GetInt32(5);
                        var heading = dbDataReader.GetInt32(6);
                        dbDataReader.Close();
                        sender.Id = id;
                        sender.Position = new Geo.Geometries.Point(position.Y, position.X);
                        sender.Coalition = (Coalition)coalition;
                        sender.Pilot = pilot;
                        sender.Altitude = altitude;
                        sender.Heading = heading;
                        sender.Speed = speed;
                    }
                    else
                    {
                        sender.Id = null;
                        dbDataReader.Close();
                    }
                }
            }
        }
    }
}
