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
 */

using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;

/** what happens when we see a changeset?
 */
abstract class Visitor
{
	/** private version of a 'Changeset' object */
	public class PatchInfo
	{
		public int parent;
		public Changeset cs;
		public List<string> treeBranches;
		
		public PatchInfo(int p, Changeset c, List<string> tps)
		{
			parent = p;
			cs = c;
			treeBranches = tps;
		}
		
		public void print(System.IO.TextWriter writer)
		{
			if (parent != 0) { writer.WriteLine("Parent: {0}", parent); }
			writer.WriteLine("Changeset: {0}", cs.ChangesetId);
			writer.WriteLine("Author: {0}", cs.Owner);
			writer.WriteLine("Date: {0}", cs.CreationDate);
			
			if (treeBranches != null)
				{ for(int i=0; i < treeBranches.Count; ++i) { writer.WriteLine(treeBranches[i]); } }
			
			/* @TODO pretty print the comment */
			writer.WriteLine(cs.Comment);
			writer.WriteLine();
		}
	};
	
	private RBDictTree<int,PatchInfo> _cache = new RBDictTree<int,PatchInfo>();
	
	/** have we already visited this changeset?
	 */
	public bool visited(int changesetid)
	{
		RBDictTree<int,PatchInfo>.iterator it = _cache.find(changesetid);
		
		return it != _cache.end();
	}

	public virtual void visit(int parentID, Changeset cs, List<string> trees)
	{
		RBDictTree<int,PatchInfo>.iterator it = _cache.find(cs.ChangesetId);
		
		if (it != _cache.end()) 
			{
				_seen(parentID, it.value().second);
			}
		else
			{
				/* only print stuff we haven't already seen. */
				PatchInfo p = new PatchInfo(parentID, cs, trees);
				
				_cache.insert(p.cs.ChangesetId, p);
				_visit(p);
			}
	}
	
	protected abstract void _visit(PatchInfo p);
	protected abstract void _seen(int parentID, PatchInfo p);
	
	/* for errors. */
	public abstract void visit(int parentID, int changesetID, Exception e);
}

/** print the changeset when we see it.
 */
class HistoryViewer : Visitor
{
	public HistoryViewer() { }
	
	protected override void _visit(PatchInfo p) { p.print(Console.Out); }
	
	protected override void _seen(int parentID, PatchInfo p)
	{ Console.WriteLine("again! {0} -> ({1}) {2}", parentID, p.parent, p.cs.ChangesetId); }
	
	public override void visit(int parentID, int csID, Exception e)
	{
		Console.WriteLine("Changeset: {0}", csID);
		Console.WriteLine(" --Error retrieving changeset-- ");
		Console.WriteLine();
		Console.WriteLine(e);
	}
}

class MegaHistory
{
	private bool _noRecurse = false;
	private VersionControlServer _vcs;
	
	public MegaHistory(bool noRecurse, VersionControlServer vcs) { _noRecurse = noRecurse; _vcs=vcs; }

	public virtual bool visit(Visitor visitor, int parentID,
														string targetPath, 
														VersionSpec targetVer,
														VersionSpec fromVer,
														VersionSpec toVer)
	{ return visit(visitor, parentID, null, null, targetPath, targetVer, fromVer, toVer, RecursionType.Full); }
	
	public virtual bool visit(Visitor visitor, 
														string srcPath, VersionSpec srcVer, 
														string target, VersionSpec targetVer,
														VersionSpec fromVer, VersionSpec toVer,
														RecursionType recursionType)
	{ return visit(visitor, 0, srcPath, srcVer, target, targetVer, fromVer, toVer, recursionType); }
	
	/** visit an explicit list of changesets. 
	 */
	public virtual bool visit(Visitor visitor, int parentID,
														string srcPath, VersionSpec srcVer, 
														string target, VersionSpec targetVer,
														VersionSpec fromVer, VersionSpec toVer,
														RecursionType recursionType)
	{
		/* so, here we might have a few top-level merge changesets. 
		 * the red-black binary tree sorts the changesets in decending order
		 */
		RBDictTree<int,List<ChangesetMerge>> merges = 
			main.query_merges(_vcs, srcPath, srcVer, 
												target, targetVer, fromVer, toVer, recursionType);
		
		RBDictTree<int,List<ChangesetMerge>>.iterator it = merges.begin();
		
		/* walk through the merge changesets
		 * - this should return only one merge changeset for the recursive calls.
		 */
		for(; it != merges.end(); ++it)
			{
				int csID = it.value().first;
				try
					{
						Changeset cs = _vcs.GetChangeset(csID);
						
						/* visit the 'target' merge changeset here. 
						 * it's parent is the one passed in.
						 */
						List<string> pbranches = main._get_EGS_branches(cs);
						visitor.visit(parentID, cs, pbranches);
						
						foreach(ChangesetMerge csm in it.value().second)
							{
								/* now visit each of the children.
								 * we've already expanded cs.ChangesetId (hopefully...)
								 */
								try
									{
										Changeset child = _vcs.GetChangeset(csm.SourceVersion);
										List<string> branches = main._get_EGS_branches(child);
										
										/* - this is for the recursive query -
										 * you have to have specific branches here.
										 * a query of the entire project will probably not return.
										 *
										 * eg: 
										 * query target $/IGT_0803/main/EGS,78029 = 41s
										 * query target $/IGT_0803,78029 = DNF (waited 6+m)
										 */
										
										if (_noRecurse)
											{
												/* they just want the top-level query. */
												visitor.visit(cs.ChangesetId, child, branches);
											}
										else
											{
												if (!visitor.visited(child.ChangesetId))
													{
														/* we just wanted to see the initial list, not a full tree of changes. */
														ChangesetVersionSpec tv = new ChangesetVersionSpec(child.ChangesetId);
														bool results = false;
														
														/* this is going to execute a number of queries to get
														 * all of the source changesets for this merge.
														 * since using a branch is an(several hundred) order(s) of magnitude faster, 
														 * we're doing this by branch(path)
														 */
														for(int i=0; i < branches.Count; ++i)
															{
																/* this recurisve call needs to then 
																 * handle visiting the results of this query. 
																 */
																bool branchResult = visit(visitor, cs.ChangesetId, branches[i], tv, tv, tv);
																
																if (branchResult) { results = true; }
															}
														
														if (!results)
															{
																/* we got no results from our query, so display the changeset
																 * (it won't be displayed otherwise)
																 */
																visitor.visit(cs.ChangesetId, child, branches);
															}
													}
												else
													{
														/* do we want to see it again? */
													}
											}
									}
								catch(Exception e) { visitor.visit(cs.ChangesetId, csm.SourceVersion, e); }
							}
					}
				catch(Exception e) { visitor.visit(parentID, csID, e); }
			}
		
		return it == merges.end();
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
	public static List<string> _get_EGS_branches(Changeset cs)
	{
		List<string> itemBranches = new List<string>();
		for(int i=0; i < cs.Changes.Length; ++i)
			{
				string itemPath = cs.Changes[i].Item.ServerItem;
				bool found = false;
				int idx = 0;
				
				/* skip all non-merge changesets. */
				if ((cs.Changes[i].ChangeType & ChangeType.Merge) == ChangeType.Merge)
					{
						for(int j=0; j < itemBranches.Count; ++j)
							{
								/* the stupid branches are not case sensitive. */
								idx = itemPath.IndexOf(itemBranches[j], StringComparison.InvariantCultureIgnoreCase);
								if (idx == 0) { found = true; break; }
							}
						
						if (!found)
							{
								/* yeah steve, '/EGS8.2' sucks now doesn't it... */
								string str = "/EGS/";
								
								/* the stupid branches are not case sensitive. */
								idx = itemPath.IndexOf(str, StringComparison.InvariantCultureIgnoreCase);
								
								if (idx > 0)
									{
										itemPath = itemPath.Substring(0,idx+str.Length);
#if DEBUG
						if (itemPath.IndexOf("$/IGT_0803/") == 0)
							{
#endif
								itemBranches.Add(itemPath);
#if DEBUG
							}
						else
							{
								Console.Error.WriteLine("'{0}' turned into '{1}'!", 
																				cs.Changes[i].Item.ServerItem,
																				itemPath);
							}
#endif
									}
							}
					}
			}
		
		return itemBranches;
	}
	
	public static RBDictTree<int,List<ChangesetMerge>> query_merges(VersionControlServer vcs,
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
