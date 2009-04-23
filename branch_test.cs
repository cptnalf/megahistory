
using System;
using Microsoft.TeamFoundation.VersionControl.Client;
using System.Collections.Generic;

class foo
{
	struct Options
	{
		public bool help;
		public string src_path;
		public int src_id;
		public string server;
	}
	
	static Options parse_args(string[] args)
	{
		Options options;
		
		options.help = false;
		options.src_path = null;
		options.src_id = 0;
		options.server = null;
		
		for(int i=0; i < args.Length; ++i)
			{
				if (args[i] == "--help") { options.help = true; }
				else if (args[i] == "--server" || args[i] == "-s")
					{
						if ( (i+1) < args.Length)
							{
								options.server = args[++i];
							}
					}
				else
					{
						options.src_id = Int32.Parse(args[i]);
					}
			}
					
		return options;
	}
	
	static void print_help()
	{
		Console.WriteLine("foo [--server|-s <server>] [--help] <version>");
		Console.WriteLine("version - changeset which you're looking to find branches for");
		Console.WriteLine();
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

	static void dump_details(ChangesetMergeDetails details)
	{
		foreach(Changeset cmd in details.Changesets)
			{
				Console.WriteLine("{0} {1}", cmd.ChangesetId, cmd.CreationDate);
				Console.WriteLine(cmd.Comment);
				Console.WriteLine();
			}
		
		foreach(ItemMerge im in details.MergedItems)
			{ Console.WriteLine("{0} => {1}", im.SourceVersionFrom, im.TargetVersionFrom); }
		
		foreach(ItemMerge im in details.UnmergedItems)
			{ Console.WriteLine("{0} =?> {1}", im.SourceVersionFrom, im.TargetVersionFrom); }
	}

	static int Main(string[] args)
	{
		Options options = parse_args(args);
		
		if (options.help || args.Length == 0)
			{
				print_help();
				return 1;
			}
		
		Changeset cs;
		VersionSpec bhVer;
		VersionControlServer vcs = _get_tfs_server(options.server);
		RBTree<MyItem> contribs = null;
		bhVer = new ChangesetVersionSpec(options.src_id);
		
		contribs = get_branches(vcs, "$/IGT_0803/main/EGS", RecursionType.None, bhVer);
		
		/* 2009-04-22 18:24:39 -0700 as of that date there were 47 branches. */
		//for(RBTree<MyItem>.iterator it=contribs.begin(); it != contribs.end(); ++it)
 		//	{
 		//		if (it.value().me.DeletionId == 0)
		//			{
					//RBTree<MyItem> more_contribs = get_branches(vcs, it.value().me.ServerItem, RecursionType.None);
		//				for(RBTree<MyItem>.iterator m_it=more_contribs.begin();
		//						m_it != more_contribs.end();
		//						++m_it)
		//					{
		//						if (contribs.find(m_it.value()) == contribs.end()) { contribs.insert_td(m_it.value()); }
		//					}
		//			}
		//	}
		
		for(RBTree<MyItem>.iterator it=contribs.begin(); it!=contribs.end(); ++it)
			{
				Console.WriteLine("{0}", it.value().me.ServerItem);
			}
		
		cs = vcs.GetChangeset(options.src_id);
		
		List<string> paths = new List<string>();
		
		for(int i=0; i < cs.Changes.Length; ++i)
			{
				if ((cs.Changes[i].ChangeType & ChangeType.Merge) == ChangeType.Merge)
					{
						Console.WriteLine("hey, use the real tool if you've got a merge changeset.");
						return 99;
					}
				
				int idx = cs.Changes[i].Item.ServerItem.IndexOf("/EGS/");
				if (idx >= 0)
					{
						bool found = false;
						string f = cs.Changes[i].Item.ServerItem.Substring(0, idx+4);
						foreach(string s in paths) { found = (s == f); if (found) break; }
						
						if (!found) { paths.Add(f); }
					}
			}
		
		/* so brute force yielded a 5 minute query which sometimes doesn't work.
		 * limiting the range to csid => latest == ?
		 */
		
		foreach(string branchpath in paths)
			{
				Console.WriteLine(branchpath);
				
				for(RBTree<MyItem>.iterator it=contribs.begin(); it != contribs.end(); ++it)
					{
						try
							{
								ChangesetMergeDetails details = vcs.QueryMergesWithDetails(branchpath,
																																					 bhVer, 0,
																																					 it.value().me.ServerItem,
																																					 ChangesetVersionSpec.Latest,
																																					 it.value().me.DeletionId,
																																					 bhVer, null,
																																					 RecursionType.Full);
								dump_details(details);
							}
						catch(ItemNotFoundException infe)
							{
								/* who cares! */
							}
						catch(Exception e)
							{
								Console.WriteLine("query: {0} {1} {2} {3} {4} {5} {6} failed with:",
																	branchpath, bhVer, it.value().me.ServerItem, 
																	ChangesetVersionSpec.Latest, it.value().me.DeletionId,
																	bhVer);
								Console.WriteLine(e);
							}
					}
			}
		
		return 0;

// 		foreach(string branchpath in paths)
// 			{
// 				Console.WriteLine(branchpath);
// 				ItemSpec itm = new ItemSpec(branchpath, RecursionType.None);//.Full);
				
// 				BranchHistoryTreeItem[][] branches = vcs.GetBranchHistory(new ItemSpec[] { itm }, bhVer);
				
// 				BranchHistoryTreeItem bhti = branches[0][0].GetRequestedItem();
				
// 				List<Item> items = getPotentialContributors(bhti);
// 				foreach(Item contrib in items)
// 					{
// 						Console.WriteLine(contrib.ServerItem);
						
// 						ChangesetMergeDetails details = vcs.QueryMergesWithDetails(branchpath,
// 																																			 bhVer, 0,
// 																																			 contrib.ServerItem, 
// 																																			 new ChangesetVersionSpec(contrib.ChangesetId),
// 																																			 contrib.DeletionId,
// 																																			 bhVer, bhVer,
// 																																			 RecursionType.Full);
						
// 						foreach(Changeset cmd in details.Changesets)
// 							{
// 								Console.WriteLine("{0} {1}", cmd.ChangesetId, cmd.CreationDate);
// 								Console.WriteLine(cmd.Comment);
// 								Console.WriteLine();
// 							}
						
// 						foreach(ItemMerge im in details.MergedItems)
// 							{ Console.WriteLine("{0} => {1}", im.SourceVersionFrom, im.TargetVersionFrom); }
						
// 						foreach(ItemMerge im in details.UnmergedItems)
// 							{ Console.WriteLine("{0} =?> {1}", im.SourceVersionFrom, im.TargetVersionFrom); }
// 					}
// 			}
		
// 		return 0;
	}

	class MyItem : IComparable<MyItem>
	{
		public MyItem(Item i) { me = i; }
		public Item me;
		int IComparable<MyItem>.CompareTo(MyItem two)
		{ return me.ServerItem.CompareTo(two.me.ServerItem); }
	}

	static RBTree<MyItem> get_branches(VersionControlServer vcs, string branch, RecursionType rtype, VersionSpec ver)
	{
		/* i want to collect all of the branches which came from main. */
		ItemSpec itm = new ItemSpec("$/IGT_0803/main/EGS", RecursionType.None);
		BranchHistoryTreeItem[][] branches = vcs.GetBranchHistory(new ItemSpec[] { itm }, 
																															ver);
		
		RBTree<MyItem> contribs = new RBTree<MyItem>();
		
		walk_tree(branches[0][0].GetRequestedItem(), contribs);
		
		return contribs;
	}	
	
	static void walk_tree(BranchHistoryTreeItem item, RBTree<MyItem> tree)
	{
		MyItem mi;
		
		/* bail early. */
		if (item == null) { return; }
		
		if (item.Parent != null)
			{
				if (item.Parent.Relative.BranchToItem != null)
					{
						mi = new MyItem(item.Parent.Relative.BranchToItem);
						if (tree.find(mi) == tree.end()) { tree.insert_td(mi); }
					}
			}
		
		foreach(BranchHistoryTreeItem ti in item.Children)
			{
				mi = new MyItem(ti.Relative.BranchToItem);
				if (tree.find(mi) == tree.end()) { tree.insert_td(mi); }
				
				walk_tree(ti, tree);
			}
		
		Item[] brs = new Item[] { item.Relative.BranchFromItem};
		foreach(Item br in brs)
			{
				if (br != null)
					{
						mi = new MyItem(br);
						if (tree.find(mi) == tree.end()) { tree.insert_td(mi); }
					}
			}
	}
	
	static List<Item> getPotentialContributors(BranchHistoryTreeItem item)
	{
		List<Item> pots = new List<Item>();
		
		if (item.Parent != null) { pots.Add(item.Parent.Relative.BranchToItem); }
		
		foreach (BranchHistoryTreeItem i in item.Children) { pots.Add(i.Relative.BranchToItem); }
		
		if (item.Relative.BranchFromItem != null) { pots.Add(item.Relative.BranchFromItem); }
		
		return pots;
	}
}
