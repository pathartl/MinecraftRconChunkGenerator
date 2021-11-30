using MinecraftConnectionCore;
using Sharprompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRconChunkGenerator
{
    class Program
    {
        private const int MaxDefaultWorldBorderSize = 59999968;
        private const int TeleportHeight = 320;
        private const int ChunkSize = 16;

        static void Main(string[] args)
        {
            int worldBorderSize = 0;

            MinecraftCommands MinecraftClient = null;

            Write("Minecraft RCON Chunk Generator v1");
            Write("Made by pathartl");
            Write("https://github.com/pathartl/MinecraftRconChunkGenerator");
            Write("");
            Write("Before you move forward with this utility, make sure you've set your world border properly.");
            Write("Also make sure you've enabled RCON and have changed the default password in server.properties");
            Write("");

            string host = Prompt.Input<string>("Enter the IP or hostname to your server (e.g. 192.168.1.10, minecraft.example.com)");
            int port = Prompt.Input<int>("RCON Port", 25575);
            string password = Prompt.Password("RCON Password", "*", null);

            Write("Trying to connect to your server...");

            string worldBorderGetResult;

            try
            {
                IPAddress ipAddress = Dns.GetHostEntry(host).AddressList.First();

                MinecraftClient = new MinecraftCommands(ipAddress, Convert.ToUInt16(port), password);

                worldBorderGetResult = MinecraftClient.SendCommand("/worldborder get");
                
                worldBorderSize = Convert.ToInt32(new Regex(@"The world border is currently (\d+) blocks wide").Match(worldBorderGetResult).Groups[1].Value);
            }
            catch
            {
                Write("Could not connect to your server. Check your server details and config and try again.");

                return;
            }

            Write("Connected!");
            Write("");

            #region Get Chunk Loading Player
            string playerListResult = MinecraftClient.SendCommand("/list");

            Write(playerListResult);
            Write("");

            if (playerListResult.StartsWith("There are 0 of a max"))
            {
                Write("There is no player connected to your server. A player is needed to load the chunks. Join the server and try again.");

                return;
            }

            var players = new Regex(@"There are \d+ of a max of \d+ players online:\s+(.+)").Match(playerListResult).Groups[1].Value.Split(", ");

            var player = Prompt.Select("Choose a player to act as the chunk loader", players);
            #endregion

            var spawnCoordinates = ExtractCoordinates(MinecraftClient.SendCommand("setworldspawn ~ ~ ~"));

            #region Check/Set World Border
            if (worldBorderSize == MaxDefaultWorldBorderSize)
            {
                worldBorderSize = Prompt.Input<int>("Your world border has not been set. How big should your border be? (in blocks)", 5000);

                Write("");
                Write("**NOTE** We are about to set your world border. If any players are outside the border they may die!");
                Write("");

                if (Prompt.Confirm($"Would you like to use {player}'s position as the center of your world border?"))
                {
                    var coordinates = ExtractCoordinates(MinecraftClient.SendCommand("tp pathartl ~ ~ ~"));

                    Write($"Set the world border to X={coordinates.X}, Z={coordinates.Z}");

                    MinecraftClient.SendCommand($"worldborder center {coordinates.X} {coordinates.Z}");
                }
                else if (Prompt.Confirm($"Would you like to use the world spawn as the center of your world border?"))
                {
                    MinecraftClient.SendCommand($"worldborder center {spawnCoordinates.X} {spawnCoordinates.Z}");
                    Write($"Set the world border to X={spawnCoordinates.X}, Z={spawnCoordinates.Z}");
                }

                MinecraftClient.SendCommand($"/worldborder set {worldBorderSize}");

                Write($"Set the world border to {worldBorderSize} blocks");
            }
            #endregion

            #region
            Write("");
            Write("Getting information about your world...");

            // Hack, shift the world border over and then back. Using only ~ ~ returns a "border already there" message
            MinecraftClient.SendCommand("worldborder center ~1 ~");

            var worldBorderCenter = ExtractCoordinates(MinecraftClient.SendCommand("worldborder center ~-1 ~"));

            Write($"The world border center is at X={worldBorderCenter.X}, Z={worldBorderCenter.Z}");

            worldBorderGetResult = MinecraftClient.SendCommand("/worldborder get");

            Write(worldBorderGetResult);

            worldBorderSize = Convert.ToInt32(new Regex(@"The world border is currently (\d+) blocks wide").Match(worldBorderGetResult).Groups[1].Value);

            var chunksPerTeleport = Prompt.Input<int>("How far would you like to move in each teleport? (in chunks)", 10);
            var teleportInterval = Prompt.Input<int>("How much delay should be between each teleport? (in milliseconds)", 5000);

            var worldBorderSizeInChunks = (int)Math.Ceiling((float)worldBorderSize / ChunkSize);

            Write($"World Border Dimensions: {worldBorderSizeInChunks}x{worldBorderSizeInChunks} chunks");

            var estimatedChunks = worldBorderSizeInChunks * worldBorderSizeInChunks;

            Write($"Total amount of chunks in world border: {estimatedChunks}");

            var teleportSizeInChunks = (int)Math.Ceiling((float)worldBorderSizeInChunks / chunksPerTeleport);

            var estimatedTeleports = teleportSizeInChunks * teleportSizeInChunks;

            Write($"Total amount of teleports needed to generate all chunks in world border: {estimatedTeleports}");

            var estimatedDuration = TimeSpan.FromMilliseconds(estimatedTeleports * teleportInterval);

            Write($"Estimated amount of time to execute all jumps: {(int)estimatedDuration.TotalMinutes} minutes");

            Write($"Calculating coordinates of all jumps...");

            var worldBorderMinX = worldBorderCenter.X - (worldBorderSize / 2);
            var worldBorderMaxX = worldBorderCenter.X + (worldBorderSize / 2);
            var worldBorderMinZ = worldBorderCenter.Z - (worldBorderSize / 2);
            var worldBorderMaxZ = worldBorderCenter.Z + (worldBorderSize / 2);

            var start = new Coordinates(worldBorderMinX, TeleportHeight, worldBorderMinZ);

            var teleportQueue = new List<Coordinates>();

            for (int x = (int)worldBorderMinX; x < worldBorderMaxX; x += ChunkSize * chunksPerTeleport)
            {
                for (int z = (int)worldBorderMinZ; z < worldBorderMaxZ; z += ChunkSize * chunksPerTeleport)
                {
                    teleportQueue.Add(new Coordinates(x, TeleportHeight, z));
                }
            }

            Write($"Calculated {teleportQueue.Count} jumps starting at X={worldBorderMinX}, Z={worldBorderMinZ} ending at X={worldBorderMaxX}, Z={worldBorderMaxZ}");

            if (Prompt.Confirm("Execute?"))
            {
                Write("Seized by God they cry for succor in the dark of the light. Mists of dreams dribble on the nascent echo and love no more");
                Write("Jump.");

                MinecraftClient.SendCommand($"gamemode creative {player}");

                foreach (var teleportCoordinates in teleportQueue)
                {
                    Write("");

                    Write(MinecraftClient.SendCommand($"tp {player} {teleportCoordinates.X} {teleportCoordinates.Y} {teleportCoordinates.Z}"));
                    Thread.Sleep(teleportInterval);
                }

                MinecraftClient.SendCommand($"playsound entity.player.levelup player {player}");

                Write("Done generating chunks! Press any key to exit");
                Console.ReadLine();
            }
            else
            {
                Write("Aborting...");
            }
            #endregion
        }

        private static void Write(string message)
        {
            Console.WriteLine(message);
        }

        private static Coordinates ExtractCoordinates(string message)
        {
            var matches = new Regex(@"(-?\d+\.?\d+)").Matches(message);

            var coordinates = matches.Select(c => Convert.ToDecimal(c.Value)).ToArray();

            switch (coordinates.Length)
            {
                case 2:
                    return new Coordinates(coordinates[0], coordinates[1]);
                case 3:
                    return new Coordinates(coordinates[0], coordinates[1], coordinates[2]);
                case 4:
                    return new Coordinates(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    public class Coordinates
    {
        public decimal X;
        public decimal Y;
        public decimal Z;
        public decimal Angle;

        public Coordinates() { }

        public Coordinates(decimal x, decimal z)
        {
            X = x;
            Z = z;
        }

        public Coordinates(decimal x, decimal y, decimal z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Coordinates(decimal x, decimal y, decimal z, decimal angle)
        {
            X = x;
            Y = y;
            Z = z;
            Angle = angle;
        }
    }
}
