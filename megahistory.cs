/*
 * chiefengineer@neghvar
 * 2009-04-13 18:57:49 -0700
 */

using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;

/** pulls a chain of changesets out of merges
 */
public class MegaHistory
{
	public static readonly string version = "af188ff3b2b9aff2e165e295b07ed9133358882c";

	static internal readonly log4net.ILog logger = log4net.LogManager.GetLogger("megahistory_logger");
	
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
		RBDictTree<int,List<ChangesetMerge>> merges = query_merges(_vcs, srcPath, srcVer, 
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
						List<string> pbranches = _get_EGS_branches(cs);
						string path_part = _get_path_part(target);
						visitor.visit(parentID, cs, pbranches);
						
						foreach(ChangesetMerge csm in it.value().second)
							{
								/* now visit each of the children.
								 * we've already expanded cs.ChangesetId (hopefully...)
								 */
								try
									{
										Changeset child = _vcs.GetChangeset(csm.SourceVersion);
										List<string> branches = _get_EGS_branches(child);
										
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
																bool branchResult = visit(visitor, cs.ChangesetId, branches[i]+path_part, tv, tv, tv);
																
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
														visitor.visit(cs.ChangesetId, child, branches);
													}
											}
									}
								catch(Exception e) { visitor.visit(cs.ChangesetId, csm.SourceVersion, e); }
							}
					}
				catch(Exception e) { visitor.visit(parentID, csID, e); }
			}
		
		return false == merges.empty();
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
	private static List<string> _get_EGS_branches(Changeset cs)
	{
		Timer timer = new Timer();
		List<string> itemBranches = new List<string>();
		
		timer.start();
		if (cs.Changes.Length > 1000)
			{
				itemBranches = _get_egs_branches_threaded(cs);
			}
		else
			{
				int changesLen = cs.Changes.Length;
				for(int i=0; i < changesLen; ++i)
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
			}
		timer.stop();
		logger.DebugFormat("getting branches took: {0}", timer.Delta);
		
		return itemBranches;
	}
	
	class _args
	{
		internal int changesLen;
		internal Change[] changes;
		internal int ptr;
		internal System.Threading.ReaderWriterLock rwlock;
		internal List<string> itemBranches;
	}
	
	private static List<string> _get_egs_branches_threaded(Changeset cs)
	{
		System.Threading.Thread[] threads = new System.Threading.Thread[8];
		_args args = new _args();
		
		args.itemBranches = new List<string>();
		args.rwlock = new System.Threading.ReaderWriterLock();
		args.changesLen = cs.Changes.Length;
		args.changes = cs.Changes;
		
		for(int i=0; i < threads.Length; ++i)
			{
				threads[i] = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(_egsbranches_worker));
				threads[i].Priority = System.Threading.ThreadPriority.Lowest;
				threads[i].Start(args);
			}
		
		for(int i=0; i < threads.Length; ++i) { threads[i].Join(); }
		
		return args.itemBranches;
	}
	
	private static void _egsbranches_worker(object o)
	{
		_args args = o as _args;
		bool done = false;

		int res = System.Threading.Interlocked.Increment(ref args.ptr);
		
		done = res >= args.changesLen;
		
		while(!done)
			{
				string itemPath = args.changes[res].Item.ServerItem;
				bool found = false;
				int idx = 0;
				int itemCount = 0;
				
				/* skip all non-merge changesets. */
				if ((args.changes[res].ChangeType & ChangeType.Merge) == ChangeType.Merge)
					{
						try {
							try {
								args.rwlock.AcquireReaderLock(10 * 1000); /* 10 second timeout. */
								itemCount = args.itemBranches.Count;
								for(int j=0; j < args.itemBranches.Count; ++j)
									{
										/* the stupid branches are not case sensitive. */
										idx = itemPath.IndexOf(args.itemBranches[j], 
																					 StringComparison.InvariantCultureIgnoreCase);
										if (idx == 0) { found = true; break; }
									}
							}
							finally
								{
									args.rwlock.ReleaseReaderLock();
								}
						} catch(ApplicationException) { /* we lost the lock. */ }
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
										try {
											try {
												bool reallyFound = false;
												args.rwlock.AcquireWriterLock(60 * 1000); /* 1 minute timeout. */
												
												if (itemCount != args.itemBranches.Count)
													{
														/* look again. */
														for(int j=0; j < args.itemBranches.Count; ++j)
															{
																idx = itemPath.IndexOf(args.itemBranches[j], 
																											 StringComparison.InvariantCultureIgnoreCase);
																if (idx == 0) { reallyFound = true; break; }
															}
													}
												
												if (! reallyFound) { args.itemBranches.Add(itemPath); }
											}
											finally { args.rwlock.ReleaseWriterLock(); }
										} catch(ApplicationException) { /* we lost the lock. */ }
#if DEBUG
									}
								else
									{
										Console.Error.WriteLine("'{0}' turned into '{1}'!", 
																						args.changes[res].Item.ServerItem,
																						itemPath);
									}
#endif
							}
					}
				
				/* setup for the next loop. */
				res = System.Threading.Interlocked.Increment(ref args.ptr);
				done = res >= args.changesLen;
			}
	}
	

	private static RBDictTree<int,List<ChangesetMerge>> query_merges(VersionControlServer vcs,
																																	string srcPath,
																																	VersionSpec srcVer,
																																	string targetPath,
																																	VersionSpec targetVer,
																																	VersionSpec fromVer,
																																	VersionSpec toVer,
																																	RecursionType recurType)
	{
		RBDictTree<int,List<ChangesetMerge>> merges = new RBDictTree<int,List<ChangesetMerge>>();

		logger.DebugFormat("query_merges {0}, {1}, {2}, {3}, {4}, {5}",
											 ( srcPath == null ? "(null)": srcPath), 
											 (srcVer == null ? "(null)" : srcVer.DisplayString),
											 targetPath, targetVer.DisplayString, 
											 (fromVer == null ? "(null)" : fromVer.DisplayString),
											 (toVer == null ? "(null)" : toVer.DisplayString));
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
		
		logger.Debug("done querying.");
		return merges;
	}
	
	private static string _get_path_part(string path)
	{
		string path_part = string.Empty;
		int idx = path.IndexOf("/EGS/", StringComparison.InvariantCultureIgnoreCase);
		
		if (idx >=0)
			{
				path_part = path.Substring(idx+5);
			}
		
		return path_part;
	}
}
