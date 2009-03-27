/*
 * 2009-03-26 15:21:01 -0700
 * stephen.alfors@igt.com
 */

using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;

/** what happens when we see a changeset?
 */
abstract class Visitor
{
	public Visitor() { }
	public abstract void visit(int parentID, Changeset cs);
	public abstract void visit(int parentID, int csID, Exception e);
}

/** print the changeset when we see it.
 */
class HistoryViewer : Visitor
{
	public override void visit(int parentID, Changeset cs)
	{
		if (parentID != 0) { Console.WriteLine("Parent: {0}", parentID); }
		Console.WriteLine("Changeset: {0}", cs.ChangesetId);
		Console.WriteLine("Author: {0}", cs.Owner);
		Console.WriteLine("Date: {0}", cs.CreationDate);
		
		/* @TODO pretty print the comment */
		Console.WriteLine(cs.Comment);
		Console.WriteLine();
	}
	
	public override void visit(int parentID, int csID, Exception e)
	{
		Console.WriteLine("Changeset: {0}", csID);
		Console.WriteLine(" --Error retrieving changeset-- ");
		Console.WriteLine();
	}
}

class main
{
	/* ChangesetMerge.TargetVersion - the changeset which the merge took place
	 * ChangesetMerge.SourceVersion - the changeset containing the changes which we merged.
	 */

	static void print_help()
	{
		Console.WriteLine("megahistory <options>");
		Console.WriteLine("queries tfs for the list of changesets which make up a merge");
		Console.WriteLine();
		Console.WriteLine("eg: megahistory -s foo --src $/foo,45 --from 10,45 $/bar,43");
		Console.WriteLine();
		Console.WriteLine("-s <server name>\tthe tfs server to connect to");
		Console.WriteLine("--src <path>[,<version>]\tthe source of the changesets");
		Console.WriteLine("--from version[,version]\tthe changeset range to look in.");
		Console.WriteLine("--no-recurse            \tdo not recursively query merge history");
		Console.WriteLine("                        \t this will execute only one QueryMerges");
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
				else
					{
						/* anything not caught by the above items is considered a target path and changeset. */
						string[] globs = args[i].Split(',');
						values.target = globs[0];
						
						if (globs.Length == 2) { values.targetVer = new ChangesetVersionSpec(globs[1]); }
					}
			}
		
		values.vcs = _get_tfs_server(values.server);
		
		Visitor visitor = new HistoryViewer();
		
		try
			{
				do_query(visitor, values);
			}
		catch(Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		
		return 0;
	}
	
	static void do_query(Visitor visitor, Values values)
	{
		/* i have to do this query here instead of just doing a recursive call
		 *  because of all of the extra arguments.
		 */
		RBDictTree<int,List<ChangesetMerge>> merges = 
			_query_merges(values.vcs, values.srcPath, values.srcVer, 
										values.target, values.targetVer, values.fromVer, values.toVer, RecursionType.Full);
		
		/* so, here we might have a few top-level merge changesets. 
		 * this sorts the changesets in decending order
		 */
		RBDictTree<int,List<ChangesetMerge>>.iterator it = merges.begin();
		
		if (it == merges.end())
			{
				Console.WriteLine("no changesets found.");
			}
		else
			{
				for(; it != merges.end(); ++it)
					{
						int csID = it.value().first;
						
						try
							{
								Changeset cs = values.vcs.GetChangeset(it.value().first);
								
								visitor.visit(0, cs);
							}
						catch(Exception getcsE1) 
							{
								visitor.visit(0, csID, getcsE1);
							}
						
						/* now walk through the changes which make up the merge change */
						foreach(ChangesetMerge csm in it.value().second)
							{
								try
									{
										Changeset childCS = values.vcs.GetChangeset(csm.SourceVersion);
										/* if we were asked not to do recursion,
										 * just display the top-level changes.
										 */
										if (values.noRecurse) { visitor.visit(csID, childCS); }
										else { publish_list(values.vcs, visitor, csID, childCS); }
									}
								catch(Exception getcsE)
									{
										/* report the problem to the visitor */
										visitor.visit(csID, csm.SourceVersion, getcsE);
									}
							}
					}
			}
	}
	
	/** walk the changes in the changeset and find all unique 'EGS' trees.
	 *  eg:
	 *  $/IGT_0803/main/EGS/shared/project.inc
	 *  $/IGT_0803/development/dev_advantage/EGS/advantage/advantage.sln
	 *  $/IGT_0803/main/EGS/advantage/advantageapps.sln
	 *
	 *  would yield just 2:
	 *  $/IGT_0803/main/EGS/
	 *  $/IGT_0803/development/dev_advantage/EGS/
	 *
	 */
	static List<string> _get_EGS_branches(Changeset cs)
	{
		List<string> itemBranches = new List<string>();
		for(int i=0; i < cs.Changes.Length; ++i)
			{
				string itemPath = cs.Changes[i].Item.ServerItem;
				bool found = false;
				int idx = 0;
				
				for(int j=0; j < itemBranches.Count; ++j)
					{
						idx = itemPath.IndexOf(itemBranches[j]);
						if (idx == 0) { found = true; break; }
					}
				
				if (!found)
					{
						/* yeah steve, '/EGS8.2' sucks now doesn't it... */
						string str = "/EGS/";
						idx = itemPath.IndexOf(str);
						itemPath = itemPath.Substring(0,idx+str.Length);
						itemBranches.Add(itemPath);
					}
			}
		
		return itemBranches;
	}
	
	/** recursively look for merges
	 * 
	 *  @param parentID  the changeset id of the top-level merge
	 *  @param cs        the changeset which the merge included,
	 *                   and to determine whether or not this changeset is a merge.
	 */
	static void publish_list(VersionControlServer vcs, Visitor visitor, int parentID, Changeset cs)
	{
		ChangesetVersionSpec tv = new ChangesetVersionSpec(cs.ChangesetId);
		RBDictTree<int,List<ChangesetMerge>> merges = null;
		
		visitor.visit(parentID, cs);
		
		/* do a full check of the changeset and get all of the source EGS paths. */
		List<string> itemBranches = _get_EGS_branches(cs);
		
		/* this is going to execute a number of queries to get
		 * all of the source changesets for this merge.
		 * since using a branch is an(several hundred) order(s) of magnitude faster, 
		 * we're doing this by branch(path)
		 */
		for(int i=0; i < itemBranches.Count; ++i)
			{
				/* you have to have specific branches here.
				 * a query of the entire project will probably not return.
				 *
				 * eg: 
				 * query target $/IGT_0803/main/EGS,78029 = 41s
				 * query target $/IGT_0803,78029 = DNF (waited 6+m)
				 */
				merges = _query_merges(vcs, itemBranches[i], tv, tv, tv);
				
				/* walk through the merge changesets
				 * - this should return only one merge changeset.
				 */
				RBDictTree<int,List<ChangesetMerge>>.iterator it = merges.begin();
				for(; it != merges.end(); ++it)
					{
						foreach(ChangesetMerge csm in it.value().second)
							{
								int id = csm.SourceVersion;
								try
									{
										Changeset csChild = vcs.GetChangeset(csm.SourceVersion);
										
										/* publish the list. */
										publish_list(vcs, visitor, cs.ChangesetId, csChild);
									}
								catch(Exception e)
									{
										/* catch potential changeset snar-foo */
										visitor.visit(cs.ChangesetId, id, e);
									}
							}
					}
			}
	}
	
	static RBDictTree<int,List<ChangesetMerge>> _query_merges(VersionControlServer vcs,
																														string targetPath, 
																														VersionSpec targetVer,
																														VersionSpec fromVer,
																														VersionSpec toVer
																														)
	{ return _query_merges(vcs, null, null, targetPath, targetVer, fromVer, toVer, RecursionType.Full); }
	
	static RBDictTree<int,List<ChangesetMerge>> _query_merges(VersionControlServer vcs,
																														string srcPath,
																														VersionSpec srcVer,
																														string targetPath,
																														VersionSpec targetVer,
																														VersionSpec fromVer,
																														VersionSpec toVer,
																														RecursionType recurType)
	{
		RBDictTree<int,List<ChangesetMerge>> merges = new RBDictTree<int,List<ChangesetMerge>>();
		
		try
			{
				ChangesetMerge[] mergesrc = vcs.QueryMerges(srcPath, srcVer, targetPath, targetVer,
																										fromVer, toVer, recurType);
				/* group by merged changesets. */
				for(int i=0; i < mergesrc.Length; ++i)
					{
						RBDictTree<int,List<ChangesetMerge>>.iterator it = 
							merges.find(mergesrc[i].TargetVersion);
						
						if (merges.end() == it)
							{
								/* create the list... */
								List<ChangesetMerge> group = new List<ChangesetMerge>();
								group.Add(mergesrc[i]);
								merges.insert(mergesrc[i].TargetVersion, group);
							}
						else
							{ it.value().second.Add(mergesrc[i]); }
					}
			}
		catch(Exception e)
			{
				Console.Error.WriteLine("Error querying: {0},{1}", targetPath, targetVer);
				Console.Error.WriteLine(e.ToString());
			}
		
		return merges;
	}
}
