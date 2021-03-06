﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Terraria;

namespace InvincibleTiles
{
    [ApiVersion(1,16)]
    public class InvincibleTiles : TerrariaPlugin
    {
        private List<int> blacklistedTiles = new List<int>();
        private List<int> blacklistedWalls = new List<int>();
        private IDbConnection db;

        public override Version Version
        {
            get { return new Version("1.0"); }
        }

        public override string Name
        {
            get { return "Invincible Tiles"; }
        }

        public override string Author
        {
            get { return "Zack"; }
        }

        public override string Description
        {
            get { return "Makes certain tiles indestructable"; }
        }

        public InvincibleTiles(Main game) : base(game)
        {
            Order = 4;
            TShock.Initialized += Start;
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("blackTile", AddTile, "blacktile", "bt"));
            Commands.ChatCommands.Add(new Command("whiteTile", DelTile, "whitetile", "wt"));
            Commands.ChatCommands.Add(new Command("blackWall", AddWall, "blackwall","bw"));
            Commands.ChatCommands.Add(new Command("whiteWall", DelWall, "whitewall","ww"));
            GetDataHandlers.TileEdit += TileKill;
        }

        private void Start()
        {
            SetupDb();
            ReadDb();
            
        }

        private void SetupDb()
        {
            if (TShock.Config.StorageType.ToLower() == "sqlite")
            {
                string sql = Path.Combine(TShock.SavePath, "invincible_tiles.sqlite");
                db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
            }
            else if (TShock.Config.StorageType.ToLower() == "mysql")
            {
                try
                {
                    var hostport = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection();
                    db.ConnectionString =
                        String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                                      hostport[0],
                                      hostport.Length > 1 ? hostport[1] : "3306",
                                      TShock.Config.MySqlDbName,
                                      TShock.Config.MySqlUsername,
                                      TShock.Config.MySqlPassword
                            );
                }
                catch (MySqlException ex)
                {
                    Log.Error(ex.ToString());
                    throw new Exception("MySql not setup correctly");
                }
            }
            else
            {
                throw new Exception("Invalid storage type");
            }
            
            var table2 = new SqlTable("BlacklistedTiles",
                                     new SqlColumn("ID", MySqlDbType.Int32),
                                     new SqlColumn("Type", MySqlDbType.Int32){ DefaultValue = "0"}
                );
            var creator2 = new SqlTableCreator(db,
                                              db.GetSqlType() == SqlType.Sqlite
                                                ? (IQueryBuilder)new SqliteQueryCreator()
                                                : new MysqlQueryCreator());
            creator2.EnsureExists(table2);
        }

        private void ReadDb()
        {
            String query = "SELECT * FROM BlacklistedTiles";

            var reader = db.QueryReader(query);
	
			while (reader.Read())
			{
			    int id = reader.Get<int>("ID");
			    int type = reader.Get<int>("Type");
			    if (type == 0)
			    {
			        blacklistedTiles.Add(id);
			    }
			    else
			    {
			        blacklistedWalls.Add(id);
			    }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if( disposing )
            {
                TShock.Initialized -= Start;
                GetDataHandlers.TileEdit -= TileKill;
            }
             base.Dispose(disposing);
        }

        private void AddTile(CommandArgs args)
        {
            if( args.Parameters.Count < 1 )
            {
                args.Player.SendMessage("You must specify a tile to add.", Color.Red);
                return;
            }
            string tile = args.Parameters[0];
            int id;
            if( !int.TryParse(tile, out id) )
            {
                args.Player.SendMessage(String.Format("Tile id '{0}' is not a valid number.", id), Color.Red);
                return;
            }

            String query = "INSERT INTO BlacklistedTiles (ID, Type) VALUES (@0,@1);";

            if(db.Query(query, id,0) != 1)
            {
                Log.ConsoleError("Inserting into the database has failed!");
                args.Player.SendMessage(String.Format("Inserting into the database has failed!", id), Color.Red);
            }
            else
            {
                args.Player.SendMessage(String.Format("Successfully banned {0}",id), Color.Red);
                blacklistedTiles.Add(id);
            }
        }

        private void DelTile(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("You must specify a tile to remove.", Color.Red);
                return;
            }
            string tile = args.Parameters[0];
            int id;
            if (!int.TryParse(tile, out id))
            {
                args.Player.SendMessage(String.Format("Tile id '{0}' is not a valid number.", id), Color.Red);
                return;
            }
            String query = "DELETE FROM BlacklistedTiles WHERE ID = @0 AND Type = @1;";

            if (db.Query(query, id, 0) != 1)
            {
                Log.ConsoleError("Removing from the database has failed!");
                args.Player.SendMessage(String.Format("Removing from the database has failed!  Are you sure {0} is banned?", id), Color.Red);
            }
            else
            {
                args.Player.SendMessage(String.Format("Successfully unbanned {0}", id), Color.Green);
                blacklistedTiles.Remove(id);
            }
        }

        private void AddWall(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("You must specify a wall to add.", Color.Red);
                return;
            }
            string tile = args.Parameters[0];
            int id;
            if (!int.TryParse(tile, out id))
            {
                args.Player.SendMessage(String.Format("Wall id '{0}' is not a valid number.", id), Color.Red);
                return;
            }

            String query = "INSERT INTO BlacklistedTiles (ID, Type) VALUES (@0, @1);";

            if (db.Query(query, id, 1) != 1)
            {
                Log.ConsoleError("Inserting into the database has failed!");
                args.Player.SendMessage(String.Format("Inserting into the database has failed!", id), Color.Red);
            }
            else
            {
                args.Player.SendMessage(String.Format("Successfully banned {0}", id), Color.Red);
                blacklistedWalls.Add(id);
            }
        }

        private void DelWall(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("You must specify a wall to remove.", Color.Red);
                return;
            }
            string tile = args.Parameters[0];
            int id;
            if (!int.TryParse(tile, out id))
            {
                args.Player.SendMessage(String.Format("Wall id '{0}' is not a valid number.", id), Color.Red);
                return;
            }
            String query = "DELETE FROM BlacklistedTiles WHERE ID = @0 AND Type = @1;";

            if (db.Query(query, id, 1) != 1)
            {
                Log.ConsoleError("Removing from the database has failed!");
                args.Player.SendMessage(String.Format("Removing from the database has failed!  Are you sure {0} is banned?", id), Color.Red);
            }
            else
            {
                args.Player.SendMessage(String.Format("Successfully unbanned {0}", id), Color.Green);
                blacklistedWalls.Remove(id);
            }
        }

        private void TileKill(object sender, GetDataHandlers.TileEditEventArgs args)
        {
            if (args.Player.Group.HasPermission("breakinvincible"))
                return;

            if (args.Action == GetDataHandlers.EditAction.KillWall && blacklistedWalls.Contains(Main.tile[args.X, args.Y].wall))
            {
                args.Handled = true;
                TSPlayer.All.SendTileSquare(args.X, args.Y, 1);
            }
			else if ((args.Action == GetDataHandlers.EditAction.KillTile || args.Action == GetDataHandlers.EditAction.KillTileNoItem || args.Action == GetDataHandlers.EditAction.PoundTile) && blacklistedTiles.Contains(Main.tile[args.X, args.Y].type))
            {
                args.Handled = true;
                TSPlayer.All.SendTileSquare(args.X,args.Y, 1);
            }
        }
    }
}