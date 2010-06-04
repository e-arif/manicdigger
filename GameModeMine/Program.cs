﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace ManicDigger
{
    public class ManicDiggerProgram2 : IInternetGameFactory
    {
        public string GameUrl = null;
        public string User = null;
        ManicDiggerGameWindow w;
        AudioOpenAl audio;
        public void Start()
        {
            w = new ManicDiggerGameWindow();
            audio = new AudioOpenAl();
            w.audio = audio;
            MakeGame(true);
            w.GameUrl = GameUrl;
            if (User != null)
            {
                w.username = User;
            }
            w.Run();
        }
        private void MakeGame(bool singleplayer)
        {
            var gamedata = new GameDataTilesMinecraft();
            
            INetworkClient network;
            if (singleplayer)
            {
                network = new NetworkClientDummy();
            }
            else
            {
                network = new NetworkClientMinecraft();
            }
            var clientgame = new GameMinecraft();
            var mapstorage = clientgame;
            var getfile = new GetFilePath(new[] { "mine", "minecraft" });
            var config3d = new Config3d();
            var mapManipulator = new MapManipulator();
            var terrainDrawer = new TerrainDrawer3d();
            var the3d = w;
            var exit = w;
            var localplayerposition = w;
            var worldfeatures = new WorldFeaturesDrawer();
            var physics = new CharacterPhysics();
            var mapgenerator = new MapGeneratorPlain();
            var internetgamefactory = this;
            if (singleplayer)
            {
                var n = (NetworkClientDummy)network;
                n.player = localplayerposition;
                n.Gui = w;
                n.Map1 = w;
                n.Map = mapstorage;
                n.Data = gamedata;
                n.Gen = new fCraft.MapGenerator();
                n.Gen.data = gamedata;
                n.Gen.log = new fCraft.FLogDummy();
                n.Gen.map = new MyFCraftMap() { data = gamedata, map = mapstorage, mapManipulator = mapManipulator };
                n.Gen.rand = new GetRandomDummy();
                n.DEFAULTMAP = "mountains";
            }
            else
            {
                var n = (NetworkClientMinecraft)network;
                n.Map = w;
                n.Clients = clientgame;
                n.Chatlines = w;
                n.Position = localplayerposition;
            }
            terrainDrawer.the3d = the3d;
            terrainDrawer.getfile = getfile;
            terrainDrawer.config3d = config3d;
            terrainDrawer.mapstorage = new TerrainInfoMapStorage() { mapstorage = mapstorage };
            terrainDrawer.data = gamedata;
            terrainDrawer.exit = exit;
            terrainDrawer.localplayerposition = localplayerposition;
            terrainDrawer.worldfeatures = worldfeatures;
            terrainDrawer.OnCrash += (a, b) => { CrashReporter.Crash(b.exception); };
            worldfeatures.getfile = getfile;
            worldfeatures.localplayerposition = localplayerposition;
            worldfeatures.mapstorage = mapstorage;
            worldfeatures.the3d = the3d;
            mapManipulator.getfile = getfile;
            mapManipulator.mapgenerator = mapgenerator;
            w.map = clientgame;
            w.physics = physics;
            w.clients = clientgame;
            w.network = network;
            w.data = gamedata;
            w.getfile = getfile;
            w.config3d = config3d;
            w.mapManipulator = mapManipulator;
            w.terrain = terrainDrawer;
            w.weapon = new WeaponDrawer() { info = new WeaponBlockInfo() { data = gamedata, terrain = terrainDrawer, viewport = w } };
            //w.characterdrawer = new CharacterDrawerMd2() { getfile = getfile, the3d = w };
            w.characterdrawer = new CharacterDrawerBlock() { getfile = getfile };
            clientgame.terrain = terrainDrawer;
            clientgame.network = network;
            clientgame.viewport = w;
            clientgame.data = gamedata;
            w.game = clientgame;
            w.login = new LoginClientMinecraft();
            w.internetgamefactory = internetgamefactory;
            w.skinserver = "http://minecraft.net/skin/";
            physics.map = clientgame;
            physics.data = gamedata;
            mapgenerator.data = gamedata;
            audio.getfile = getfile;
            audio.gameexit = w;
        }
        #region IInternetGameFactory Members
        public void NewInternetGame()
        {
            MakeGame(false);
        }
        #endregion
    }
    public class ManicDiggerProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            new CrashReporter().Start(Start, args);
        }
        private static void Start(string[] args)
        {
            if (!Debugger.IsAttached)
            {
                string appPath = Path.GetDirectoryName(Application.ExecutablePath);
                System.Environment.CurrentDirectory = appPath;
            }
            if (args.Length > 0)
            {
                if (args[0].EndsWith(".mdlink", StringComparison.InvariantCultureIgnoreCase))
                {
                    var p = new ManicDiggerProgram2();
                    XmlDocument d = new XmlDocument();
                    d.Load(args[0]);
                    string mode = XmlTool.XmlVal(d, "/ManicDiggerLink/GameMode");
                    if (mode != "Mine")
                    {
                        throw new Exception("Invalid game mode: " + mode);
                    }
                    p.GameUrl = XmlTool.XmlVal(d, "/ManicDiggerLink/Ip");
                    int port = int.Parse(XmlTool.XmlVal(d, "/ManicDiggerLink/Port"));
                    p.GameUrl += ":" + port;
                    p.User = XmlTool.XmlVal(d, "/ManicDiggerLink/User");
                }
            }
            new ManicDiggerProgram2().Start();
        }
    }
}