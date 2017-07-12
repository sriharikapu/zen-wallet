using System;
using NDesk.Options;
using System.IO;
using System.Linq;
using Microsoft.FSharp.Collections;
using System.Text;
using Microsoft.FSharp.Core;
using BlockChain.Data;

namespace Zen
{
	class Program {
        enum LaunchModeEnum {
            TUI,
            GUI,
            Headless
        }

		public static void Main (string[] args)
		{
            var app = new App();

            var launchMode = LaunchModeEnum.GUI;
			bool show_help = false;
			bool genesis = false;
			bool rpcServer = false;
            bool wipe = false;

			var p = new OptionSet() {
				{ "headless", "start in headless mode",
					v => launchMode = LaunchModeEnum.Headless },

				{ "t|tui", "show TUI",
					v => launchMode = LaunchModeEnum.TUI },

				{ "n|network=", "use network profile",
					v => app.SetNetwork(v) },

				{ "wallet=", "wallet DB", 
					v => app.Settings.WalletDB = v },

				{ "blockchain=", "blockchain DB suffix",
					v => app.Settings.BlockChainDBSuffix = v },

				{ "m|miner", "enable miner",
					v => app.MinerEnabled = true },

				{ "r|rpc", "enable RPC",
					v => rpcServer = true },

				{ "g|genesis", "add the genesis block",
					v => genesis = true },

				{ "w|wipe db's on startup",
					v => wipe = v != null },

                { "h|help",  "show this message and exit", 
					v => show_help = v != null },
			};

			try {
				p.Parse (args);
			}
			catch (OptionException e) {
				Console.Write ("greet: ");
				Console.WriteLine (e.Message);
				Console.WriteLine ("Try `greet --help' for more information.");
				return;
			}

			if (show_help) {
				ShowHelp (p);
				return;
			}

            if (wipe)
            {
                app.ResetWalletDB();
                app.ResetBlockChainDB();
				Console.WriteLine("Databases wiped.");
			}

            app.Init();

            if (genesis)
            {
                app.AddGenesisBlock();
                Console.WriteLine("Genesis block added.");
            }
			
			if (rpcServer)
			{
                app.StartRPCServer();
			}
           
            switch (launchMode)
            {
				case LaunchModeEnum.TUI:
                    TUI.Start(app);
					break;
				case LaunchModeEnum.GUI:
					app.Connect();
					app.GUI(true);
					break;
				case LaunchModeEnum.Headless:
                    Console.WriteLine("Running headless.");
					app.Connect();
					//TODO: wait for kill signal and only then dispose
					//app.Dispose();
					break;
			}
		}

		static void ShowHelp (OptionSet p)
		{
			Console.WriteLine ("Usage: Zen [OPTIONS]");
			Console.WriteLine ();
			Console.WriteLine ("Options:");
			p.WriteOptionDescriptions (Console.Out);
			Console.WriteLine ();
		}
	}
}