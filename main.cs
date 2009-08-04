/*
 * 2009-03-26 15:21:01 -0700
 * stephen.alfors@igt.com
 */

/* this generates duplicates
 * i'm not sure if its because there really are duplicates in the merge history, or if 
 * the recursive queries are picking them up.
 * also, the rather innocent query i did turned into a frigg'n diasater.
 * 40 minutes later i have 720 different changesets (again multiples exist)
 * graphing the directed changes with dot, i get a png that's 7500x14000.
 *
 * reduced the call time to 8 minutes.
 */

using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;

/* for log4net. */
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

class main
{
	/* ChangesetMerge.TargetVersion - the changeset which the merge took place
	 * ChangesetMerge.SourceVersion - the changeset containing the changes which we merged.
	 */

	static void print_help()
	{
		Console.WriteLine("megahistory <options>");
		Console.WriteLine("lib version {0}", MegaHistory.version);
		Console.WriteLine("queries tfs for the list of changesets which make up a merge");
		Console.WriteLine();
		Console.WriteLine("eg: megahistory -s foo --src $/foo,45 --from 10,45 $/bar,43");
		Console.WriteLine();
		Console.WriteLine("-s <server name>\tthe tfs server to connect to");
		Console.WriteLine("--src <path>[,<version>]\tthe source of the changesets");
		Console.WriteLine("--from version[,version]\tthe changeset range to look in.");
		Console.WriteLine("--no-recurse            \tdo not recursively query merge history");
		Console.WriteLine("                        \t this will execute only one QueryMerges");
		Console.WriteLine("--name-only             \tadd the path of the files to the changeset info");
		Console.WriteLine("--name-status           \tprint the path and the change type in the changeset info");
		Console.WriteLine("target,version\tthe required path we're looking at");
	}

	static VersionControlServer _get_tfs_server(string serverName)
	{
		Microsoft.TeamFoundation.Client.TeamFoundationServer srvr;
		
		if (serverName != null && serverName != string.Empty)
			{
				srvr = Microsoft.TeamFoundation.Client.TeamFoundationServerFactory.GetServer(serverName);
			}
		else
			{
				/* hmm, they didn't specify one, so get the first in the list. */
				Microsoft.TeamFoundation.Client.TeamFoundationServer[] servers =
					Microsoft.TeamFoundation.Client.RegisteredServers.GetServers();
				
				srvr = servers[0];
			}
		
		return (srvr.GetService(typeof(VersionControlServer)) as VersionControlServer);
	}
	
	internal struct Values
	{
		internal bool noRecurse;
		internal string server;
		internal string srcPath;
		internal VersionSpec srcVer;
		internal string target;
		internal VersionSpec targetVer;
		internal VersionSpec fromVer;
		internal VersionSpec toVer;
		internal VersionControlServer vcs;
		internal HistoryViewer.Printwhat printWhat;
		
		internal Values(byte b)
		{
			noRecurse = false;
			server = null;
			srcPath = null;
			srcVer = null;
			target = null;
			targetVer = null;
			fromVer = null;
			toVer = null;
			vcs = null;
			printWhat = HistoryViewer.Printwhat.None;
		}
	}
	
	static int Main(string[] args)
	{
		Values values =  new Values(0x4);
		
		if (args.Length == 0 || 
				(args.Length == 1 && (args[0] == "-h" || 
															args[0] == "--help")))
			{
				print_help();
				return 1;
			}
		
		/* parse the command line arguments. */
		for(int i =0; i<args.Length; ++i)
			{
				if (args[i] == "-s" && (i+1 < args.Length)) { values.server = args[++i]; }
				else if (args[i] == "--src" && (i+1) < args.Length)
					{
						string[] globs = args[++i].Split(',');
						if (globs.Length == 2) { values.srcVer = new ChangesetVersionSpec(globs[1]); }
						values.srcPath = globs[0];
					}
				else if (args[i] == "--from" && (i+1) < args.Length)
					{
						string[] globs = args[++i].Split(',');
						if (globs.Length == 2) { values.toVer = new ChangesetVersionSpec(globs[1]); }
						values.fromVer = new ChangesetVersionSpec(globs[0]);
					}
				else if (args[i] == "--no-recurse") { values.noRecurse = true; }
				else if (args[i] == "--name-only") { values.printWhat = HistoryViewer.Printwhat.NameOnly; }
				else if (args[i] == "--name-status") { values.printWhat = HistoryViewer.Printwhat.NameStatus; }
				else
					{
						/* anything not caught by the above items is considered a target path and changeset. */
						string[] globs = args[i].Split(',');
						values.target = globs[0];
						
						if (globs.Length == 2) { values.targetVer = new ChangesetVersionSpec(globs[1]); }
					}
			}
		
		values.vcs = _get_tfs_server(values.server);
		
		Visitor visitor = new HistoryViewer(values.printWhat);
		MegaHistory megahistory = new MegaHistory(values.noRecurse, values.vcs);
		
		bool result = megahistory.visit(visitor,
																		values.srcPath, values.srcVer, 
																		values.target, values.targetVer, 
																		values.fromVer, values.toVer, RecursionType.Full);
		
		if (!result)
			{
				Console.WriteLine("no changesets found.");
			}
		
		return 0;
	}	
}
