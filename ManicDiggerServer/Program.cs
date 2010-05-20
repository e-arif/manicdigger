﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Collections;
using ManicDigger;
using System.Threading;
using OpenTK;
using System.Xml;

namespace ManicDiggerServer
{
    public class ClientException : Exception
    {
        public ClientException(Exception innerException, int clientid)
            : base("Client exception", innerException)
        {
            this.clientid = clientid;
        }
        public int clientid;
    }
    public class Server
    {
        [Inject]
        public Water water { get; set; }
        [Inject]
        public IGameWorld gameworld { get; set; }
        public IMapStorage map;
        bool ENABLE_FORTRESS = true;
        public void Start()
        {
            LoadConfig();
            if (ENABLE_FORTRESS)
            {
                if (File.Exists(manipulator.defaultminesave))
                {
                    gameworld.LoadState(File.ReadAllBytes(manipulator.defaultminesave));
                    Console.WriteLine("Savegame loaded: " + manipulator.defaultminesave);
                }
            }
            else
            {
                map = new MapStorage();
                ((MapStorage)map).Map = new byte[256, 256, 64];
                map.MapSizeX = 256;
                map.MapSizeY = 256;
                map.MapSizeZ = 64;
                var gamedata = new ManicDigger.GameDataTilesMinecraft();
                fCraft.MapGenerator Gen = new fCraft.MapGenerator();
                manipulator = new MapManipulator() { getfile = new GetFilePathDummy(), mapgenerator = new MapGeneratorPlain() };
                Gen.data = gamedata;
                Gen.log = new fCraft.FLogDummy();
                Gen.map = new MyFCraftMap() { data = gamedata, map = map, mapManipulator = manipulator };
                Gen.rand = new GetRandomDummy();
                //"mountains"
                bool hollow = false;
                if (File.Exists(manipulator.defaultminesave))
                {
                    manipulator.LoadMap(map, manipulator.defaultminesave);
                    Console.WriteLine("Savegame loaded: " + manipulator.defaultminesave);
                }
                else
                {
                    Gen.GenerateMap(new fCraft.MapGeneratorParameters(8, 1, 0.5, 0.45, 0.1, 0.5, hollow));
                }
            }
            Start(cfgport);
        }
        MapManipulator manipulator = new MapManipulator() { getfile = new GetFilePathDummy() };
        public void Process11()
        {
            if ((DateTime.Now - lastsave).TotalMinutes > 2)
            {
                if (!ENABLE_FORTRESS)
                {
                    manipulator.SaveMap(map, manipulator.defaultminesave);
                }
                else
                {
                    File.WriteAllBytes(manipulator.defaultminesave, gameworld.SaveState());
                }
                Console.WriteLine("Game saved.");
                lastsave = DateTime.Now;
            }
        }
        DateTime lastsave = DateTime.Now;
        void LoadConfig()
        {
            string filename = "ServerConfig.xml";
            if (!File.Exists(filename))
            {
                return;
            }
            using (Stream s = new MemoryStream(File.ReadAllBytes(filename)))
            {
                StreamReader sr = new StreamReader(s);
                XmlDocument d = new XmlDocument();
                d.Load(sr);
                int format = int.Parse(XmlTool.XmlVal(d, "/ManicDiggerServerConfig/FormatVersion"));
                cfgname = XmlTool.XmlVal(d, "/ManicDiggerServerConfig/Name");
                cfgmotd = XmlTool.XmlVal(d, "/ManicDiggerServerConfig/Motd");
                cfgport = int.Parse(XmlTool.XmlVal(d, "/ManicDiggerServerConfig/Port"));
            }
            Console.WriteLine("Server configuration loaded.");
        }
        string cfgname = "Manic Digger server";
        string cfgmotd = "MOTD";
        public int cfgport = 25565;
        Socket main;
        IPEndPoint iep;
        string fListUrl = "http://list.fragmer.net/announce.php";
        public void SendHeartbeat()
        {
            try
            {
                StringWriter sw = new StringWriter();//&salt={4}
                string staticData = String.Format("name={0}&max={1}&public={2}&port={3}&version={4}"
                    , System.Web.HttpUtility.UrlEncode(cfgname),
                    32, "true", cfgport, "7");

                List<string> playernames = new List<string>();
                lock (clients)
                {
                    foreach (var k in clients)
                    {
                        playernames.Add(k.Value.playername);
                    }
                }
                string requestString = staticData +
                                        "&users=" + clients.Count +
                                        "&hash=" + "0123456789abcdef0123456789abcdef" +
                                        "&motd=" + System.Web.HttpUtility.UrlEncode(cfgmotd) +
                                        "&server=Manic Digger f" +
                                        "&players=" + string.Join(",", playernames.ToArray());

                var request = (HttpWebRequest)WebRequest.Create(fListUrl);
                request.Method = "POST";
                request.Timeout = 15000; // 15s timeout
                request.ContentType = "application/x-www-form-urlencoded";
                request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

                byte[] formData = Encoding.ASCII.GetBytes(requestString);
                request.ContentLength = formData.Length;

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(formData, 0, formData.Length);
                    requestStream.Flush();
                }
                request.Abort();
                Console.WriteLine("Heartbeat sent.");
            }
            catch
            {
                Console.WriteLine("Unable to send heartbeat.");
            }
        }
        void Start(int port)
        {
            main = new Socket(AddressFamily.InterNetwork,
                   SocketType.Stream, ProtocolType.Tcp);

            iep = new IPEndPoint(IPAddress.Any, port);
            main.Bind(iep);
            main.Listen(10);
        }
        int lastclient;
        public void Process()
        {
            try
            {
                Process11();
                Process1();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        public void Process1()
        {
            if (main == null)
            {
                return;
            }
            byte[] data = new byte[1024];
            string stringData;
            int recv;
            if (main.Poll(0, SelectMode.SelectRead)) //Test for new connections
            {
                Socket client1 = main.Accept();
                IPEndPoint iep1 = (IPEndPoint)client1.RemoteEndPoint;

                Client c = new Client();
                c.socket = client1;
                lock (clients)
                {
                    clients[lastclient++] = c;
                }
                //client join event
            }
            ArrayList copyList = new ArrayList();
            foreach (var k in clients)
            {
                copyList.Add(k.Value.socket);
            }
            if (copyList.Count == 0)
            {
                return;
            }
            Socket.Select(copyList, null, null, 0);//10000000);

            foreach (Socket clientSocket in copyList)
            {
                int clientid = -1;
                foreach (var k in new List<KeyValuePair<int, Client>>(clients))
                {
                    if (k.Value != null && k.Value.socket == clientSocket)
                    {
                        clientid = k.Key;
                    }
                }
                Client client = clients[clientid];

                data = new byte[1024];
                try
                {
                    recv = clientSocket.Receive(data);
                }
                catch
                {
                    recv = 0;
                }
                //stringData = Encoding.ASCII.GetString(data, 0, recv);

                if (recv == 0)
                {
                    //client problem. disconnect client.
                    KillPlayer(clientid);
                }
                else
                {
                    for (int i = 0; i < recv; i++)
                    {
                        client.received.Add(data[i]);
                    }
                }
            }
            foreach (var k in new List<KeyValuePair<int, Client>>(clients))
            {
                Client c = k.Value;
                try
                {
                    for (; ; )
                    {
                        int bytesRead = TryReadPacket(k.Key);
                        if (bytesRead > 0)
                        {
                            clients[k.Key].received.RemoveRange(0, bytesRead);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (ClientException e)
                {
                    //client problem. disconnect client.
                    KillPlayer(e.clientid);
                }
            }
            water.Update();
            try
            {
                foreach (var v in water.tosetwater)
                {
                    byte watertype = (byte)TileTypeMinecraft.Water;
                    map.SetBlock((int)v.X, (int)v.Y, (int)v.Z, watertype);
                    foreach (var k in clients)
                    {
                        SendSetBlock(k.Key, (int)v.X, (int)v.Y, (int)v.Z, watertype);
                        //SendSetBlock(k.Key, x, z, y, watertype);
                    }
                }
                foreach (var v in water.tosetempty)
                {
                    byte emptytype = (byte)TileTypeMinecraft.Empty;
                    map.SetBlock((int)v.X, (int)v.Y, (int)v.Z, emptytype);
                    foreach (var k in clients)
                    {
                        SendSetBlock(k.Key, (int)v.X, (int)v.Y, (int)v.Z, emptytype);
                        //SendSetBlock(k.Key, x, z, y, watertype);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            water.tosetwater.Clear();
            water.tosetempty.Clear();
        }
        private void KillPlayer(int clientid)
        {
            string name = clients[clientid].playername;
            clients.Remove(clientid);
            foreach (var kk in clients)
            {
                SendDespawnPlayer(kk.Key, (byte)clientid);
            }
            SendMessageToAll(string.Format("Player {0} disconnected.", name));
        }
        Vector3i DefaultSpawnPosition()
        {
            if (ENABLE_FORTRESS)
            {
                return new Vector3i();
            }
            return new Vector3i((map.MapSizeX / 2) * 32, (map.MapSizeY / 2) * 32,
                        MapUtil.blockheight(map, 0, map.MapSizeX / 2, map.MapSizeY / 2) * 32);
        }
        //returns bytes read.
        private int TryReadPacket(int clientid)
        {
            Client c = clients[clientid];
            BinaryReader br = new BinaryReader(new MemoryStream(c.received.ToArray()));
            if (c.received.Count == 0)
            {
                return 0;
            }
            int packetid = br.ReadByte();
            int totalread = 1;
            switch (packetid)
            {
                case (int)MinecraftClientPacketId.PlayerIdentification:
                    totalread += 1 + NetworkHelper.StringLength + NetworkHelper.StringLength + 1; if (c.received.Count < totalread) { return 0; }
                    byte protocolversion = br.ReadByte();
                    string username = NetworkHelper.ReadString64(br);
                    string verificationkey = NetworkHelper.ReadString64(br);
                    byte unused1 = br.ReadByte();

                    SendServerIdentification(clientid);
                    SendLevel(clientid);
                    //todo verificationkey
                    clients[clientid].playername = username;
                    //send new player spawn to all players
                    foreach (var k in clients)
                    {
                        var cc = k.Key == clientid ? byte.MaxValue : clientid;
                        SendSpawnPlayer(k.Key, (byte)cc, username, DefaultSpawnPosition().x, DefaultSpawnPosition().y, DefaultSpawnPosition().z, 0, 0);
                    }
                    //send all players spawn to new player
                    foreach (var k in clients)
                    {
                        if (k.Key != clientid)
                        {
                            SendSpawnPlayer(clientid, (byte)k.Key, k.Value.playername, 0, 0, 0, 0, 0);
                        }
                    }
                    SendMessageToAll(string.Format("Player {0} joins.", username));
                    break;
                case (int)MinecraftClientPacketId.SetBlock:
                    totalread += 3 * 2 + 1 + 1; if (c.received.Count < totalread) { return 0; }
                    int x;
                    int y;
                    int z;
                    if (ENABLE_FORTRESS)
                    {
                        throw new Exception();
                    }
                    else
                    {
                        x = NetworkHelper.ReadInt16(br);
                        y = NetworkHelper.ReadInt16(br);
                        z = NetworkHelper.ReadInt16(br);
                    }
                    BlockSetMode mode = br.ReadByte() == 0 ? BlockSetMode.Destroy : BlockSetMode.Create;
                    byte blocktype = br.ReadByte();
                    if (mode == BlockSetMode.Destroy)
                    {
                        blocktype = 0; //data.TileIdEmpty
                    }
                    //todo check block type.
                    map.SetBlock(x, z, y, blocktype);
                    foreach (var k in clients)
                    {
                        //original player did speculative update already.
                        if (k.Key != clientid)
                        {
                            SendSetBlock(k.Key, x, z, y, blocktype);
                        }
                    }
                    //water
                    water.BlockChange(map, x, z, y);
                    break;
                case (int)MinecraftClientPacketId.PositionandOrientation:
                    byte playerid;
                    int xx;
                    int yy;
                    int zz;
                    if (ENABLE_FORTRESS)
                    {
                        totalread += 1 + 3 * 4 + 1 + 1; if (c.received.Count < totalread) { return 0; }
                        playerid = br.ReadByte();
                        xx = NetworkHelper.ReadInt32(br);
                        yy = NetworkHelper.ReadInt32(br);
                        zz = NetworkHelper.ReadInt32(br);
                    }
                    else
                    {
                        totalread += 1 + 3 * 2 + 1 + 1; if (c.received.Count < totalread) { return 0; }
                        playerid = br.ReadByte();
                        xx = NetworkHelper.ReadInt16(br);
                        yy = NetworkHelper.ReadInt16(br);
                        zz = NetworkHelper.ReadInt16(br);
                    }
                    byte heading = br.ReadByte();
                    byte pitch = br.ReadByte();
                    foreach (var k in clients)
                    {
                        if (k.Key != clientid)
                        {
                            SendPlayerTeleport(k.Key, (byte)clientid, xx, yy, zz, heading, pitch);
                        }
                    }
                    break;
                case (int)MinecraftClientPacketId.Message:
                    totalread += 1 + 64; if (c.received.Count < totalread) { return 0; }
                    byte unused2 = br.ReadByte();
                    string message = NetworkHelper.ReadString64(br);
                    //todo sanitize text
                    SendMessageToAll(string.Format("{0}: {1}", clients[clientid].playername, message));
                    break;
                case (int)MinecraftClientPacketId.ExtendedPacketCommand:
                    totalread += 4; if (c.received.Count < totalread) { return 0; }
                    int length = NetworkHelper.ReadInt32(br);
                    totalread += length; if (c.received.Count < totalread) { return 0; }
                    byte[] commandData = br.ReadBytes(length);
                    gameworld.DoCommand(commandData, clientid);
                    foreach (var k in clients)
                    {
                        if (k.Key != clientid)
                        {
                            SendCommand(k.Key, clientid, commandData);
                        }
                    }
                    break;
                default:
                    throw new Exception();
            }
            return totalread;
        }
        private void SendCommand(int clientid, int commandplayerid, byte[] commandData)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.ExtendedPacketCommand);
            bw.Write((byte)commandplayerid);
            NetworkHelper.WriteInt32(bw, commandData.Length);
            bw.Write((byte[])commandData);
            SendPacket(clientid, ms.ToArray());
        }
        private void SendMessageToAll(string message)
        {
            Console.WriteLine(message);
            foreach (var k in clients)
            {
                SendMessage(k.Key, message);
            }
        }
        private void SendSpawnPlayer(int clientid, byte playerid, string playername, int x, int y, int z, int heading, int pitch)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.SpawnPlayer);
            bw.Write((byte)playerid);
            NetworkHelper.WriteString64(bw, playername);
            if (ENABLE_FORTRESS)
            {
                NetworkHelper.WriteInt32(bw, (int)x);
                NetworkHelper.WriteInt32(bw, (int)y);
                NetworkHelper.WriteInt32(bw, (int)z);
            }
            else
            {
                NetworkHelper.WriteInt16(bw, (short)x);
                NetworkHelper.WriteInt16(bw, (short)y);
                NetworkHelper.WriteInt16(bw, (short)z);
            }
            bw.Write((byte)heading);
            bw.Write((byte)pitch);
            SendPacket(clientid, ms.ToArray());
        }
        private void SendSetBlock(int clientid, int x, int y, int z, int blocktype)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.SetBlock);
            if (ENABLE_FORTRESS)
            {
                throw new Exception();
            }
            else
            {
                NetworkHelper.WriteInt16(bw, (short)x);
                NetworkHelper.WriteInt16(bw, (short)z);
                NetworkHelper.WriteInt16(bw, (short)y);
            }
            bw.Write((byte)blocktype);
            SendPacket(clientid, ms.ToArray());
        }
        private void SendPlayerTeleport(int clientid, byte playerid, int x, int y, int z, int heading, int pitch)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.PlayerTeleport);
            bw.Write((byte)playerid);
            if (ENABLE_FORTRESS)
            {
                NetworkHelper.WriteInt32(bw, (int)x);
                NetworkHelper.WriteInt32(bw, (int)y);
                NetworkHelper.WriteInt32(bw, (int)z);
            }
            else
            {
                NetworkHelper.WriteInt16(bw, (short)x);
                NetworkHelper.WriteInt16(bw, (short)y);
                NetworkHelper.WriteInt16(bw, (short)z);
            }
            bw.Write((byte)heading);
            bw.Write((byte)pitch);
            SendPacket(clientid, ms.ToArray());
        }
        //SendPositionAndOrientationUpdate //delta
        //SendPositionUpdate //delta
        //SendOrientationUpdate
        private void SendDespawnPlayer(int clientid, byte playerid)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.DespawnPlayer);
            bw.Write((byte)playerid);
            SendPacket(clientid, ms.ToArray());
        }
        private void SendMessage(int clientid, string message)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.Message);
            bw.Write((byte)clientid);
            NetworkHelper.WriteString64(bw, message);
            SendPacket(clientid, ms.ToArray());
        }
        private void SendDisconnectPlayer(int clientid, string disconnectReason)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.DisconnectPlayer);
            NetworkHelper.WriteString64(bw, disconnectReason);
            SendPacket(clientid, ms.ToArray());
        }
        public void SendPacket(int clientid, byte[] packet)
        {
            try
            {
                int sent = clients[clientid].socket.Send(packet);
                if (sent != packet.Length)
                {
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                throw new ClientException(e, clientid);
            }
        }
        private void SendLevel(int clientid)
        {
            SendLevelInitialize(clientid);
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            byte[] compressedmap;
            if (!ENABLE_FORTRESS)
            {
                NetworkHelper.WriteInt32(bw, map.MapSizeX * map.MapSizeY * map.MapSizeZ);
                for (int z = 0; z < map.MapSizeZ; z++)
                {
                    for (int y = 0; y < map.MapSizeY; y++)
                    {
                        for (int x = 0; x < map.MapSizeX; x++)
                        {
                            bw.Write((byte)map.GetBlock(x, y, z));
                        }
                    }
                }
                compressedmap = GzipCompression.Compress(ms.ToArray());
            }
            else
            {
                compressedmap = gameworld.SaveState();
            }
            MemoryStream ms2 = new MemoryStream(compressedmap);
            byte[] buf = new byte[levelchunksize];
            int totalread = 0;
            for (; ; )
            {
                int read = ms2.Read(buf, 0, levelchunksize);
                if (read == 0)
                {
                    break;
                }
                if (read < levelchunksize)
                {
                    byte[] buf2 = new byte[levelchunksize];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf2[i] = buf[i];
                    }
                    buf = buf2;
                }
                SendLevelDataChunk(clientid, buf, (int)((totalread / compressedmap.Length) * 100));
                totalread += read;
                //Thread.Sleep(100);
            }
            SendLevelFinalize(clientid);
        }
        int levelchunksize = 1024;
        private void SendLevelInitialize(int clientid)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.LevelInitialize);
            SendPacket(clientid, ms.ToArray());
        }
        private void SendLevelDataChunk(int clientid, byte[] chunk, int percentcomplete)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.LevelDataChunk);
            NetworkHelper.WriteInt16(bw, (short)chunk.Length);
            bw.Write((byte[])chunk);
            bw.Write((byte)percentcomplete);
            SendPacket(clientid, ms.ToArray());
        }
        private void SendLevelFinalize(int clientid)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.LevelFinalize);
            if (ENABLE_FORTRESS)
            {
                NetworkHelper.WriteInt16(bw, (short)256);
                NetworkHelper.WriteInt16(bw, (short)256);
                NetworkHelper.WriteInt16(bw, (short)64);
            }
            else
            {
                NetworkHelper.WriteInt16(bw, (short)map.MapSizeX);
                NetworkHelper.WriteInt16(bw, (short)map.MapSizeZ);
                NetworkHelper.WriteInt16(bw, (short)map.MapSizeY);
            }
            SendPacket(clientid, ms.ToArray());
        }
        int CurrentProtocolVersion = 7;
        private void SendServerIdentification(int clientid)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MinecraftServerPacketId.ServerIdentification);
            bw.Write((byte)CurrentProtocolVersion);
            NetworkHelper.WriteString64(bw, cfgname);
            NetworkHelper.WriteString64(bw, cfgmotd);
            bw.Write((byte)0);
            SendPacket(clientid, ms.ToArray());
        }
        class Client
        {
            public Socket socket;
            public List<byte> received = new List<byte>();
            public string playername = "player";
        }
        Dictionary<int, Client> clients = new Dictionary<int, Client>();
    }
    class Program
    {
        static void Main(string[] args)
        {
            Server s = new Server();
            s.water = new Water() { data = new GameDataTilesMinecraft() };
            //s.map = server.map;

            var g = new GameModeFortress.GameFortress();
            var data = new GameModeFortress.GameDataTilesManicDigger();
            g.audio = new AudioDummy();
            g.data = data;
            var gen = new GameModeFortress.WorldGeneratorSandbox();
            g.map = new GameModeFortress.InfiniteMap() { gen=gen };
            g.worldgeneratorsandbox = gen;
            g.network = new NetworkClientDummy();
            g.pathfinder = new Pathfinder3d();
            g.physics = new CharacterPhysics() { data = data, map = g.map };
            g.terrain = new TerrainDrawerDummy();
            g.ticks = new TicksDummy();
            g.viewport = new ViewportDummy();
            s.gameworld = g;
            g.generator = File.ReadAllText("WorldGenerator.cs");

            s.Start();
            new Thread((a) => { for (; ; ) { s.SendHeartbeat(); Thread.Sleep(TimeSpan.FromMinutes(1)); } }).Start();
            for (; ; )
            {
                s.Process();
                Thread.Sleep(1);
            }
        }
    }
}