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
	public static readonly string version = "f998a4d529c1cb415b0efaeb072dd3cda173d3e1";
	public static uint FindChangesetBranchesCalls = 0;
	public delegate bool ChangeTypeToConsiderDelegate(Change cng);

	/** what ChangeType(s) do we want to consider when we query for decomposition.
	 *  this defaults to a function which only considers:
	 *   changes which are not just ChangeType.Merge
	 *   and changes which contain a ChangeType.Merge
	 */
	public static ChangeTypeToConsiderDelegate IsChangeToConsider = _isChangeToConsider;

	private static bool _isChangeToConsider(Change cng)
	{
		/* look at only merge and branch changes, 
		 * but ignore only 'Merge' changes (these are TFS's way of syncing it's internal merge state)
		 */
		return (cng.ChangeType != ChangeType.Merge) &&
			(
			 //((cng.ChangeType & ChangeType.Branch) == ChangeType.Branch) ||
			 ((cng.ChangeType & ChangeType.Merge) == ChangeType.Merge)
			 );
	}

	static internal readonly log4net.ILog logger = log4net.LogManager.GetLogger("megahistory_logger");
	
	public class Options
	{
		private bool _noRecurse = false;          /**< do we want recursion? */
		/** always decompose changesets 
		 *  (even if they didn't result in any branches)
		 */
		private bool _forceDecomposition = false;
		
		/** allow recursive queries to revisit already seen branches 
		 *  aka full recursion (decompose all merge changesets...)
		 *
		 *  so, you might think this option is broken, but it really isn't, 
		 *  see an explaination of how this does its stuff first.
		 */
		private bool _allowBranchRevisiting = false;
		
		public Options() { }
		public bool NoRecurse { get { return _noRecurse; } set { _noRecurse = value; } }
		public bool ForceDecomposition 
		{ get { return _forceDecomposition; } set { _forceDecomposition = value; } }
		public bool AllowBranchRevisiting
		{ get { return _allowBranchRevisiting; } set { _allowBranchRevisiting = value; } }
	}
	
	private Options _options;
	private VersionControlServer _vcs;
	private Visitor _visitor;
	private uint _queries = 0;
	private Timer _queryTimer = new Timer();

	public TimeSpan QueryTime { get { return _queryTimer.Total; } }
	public uint Queries { get { return _queries; } }
	
	public MegaHistory(VersionControlServer vcs, Visitor visitor)
	: this(new Options(), vcs, visitor) { }
	
	public MegaHistory(Options options, VersionControlServer vcs, Visitor visitor)
	{ 
		_options = options;
		_vcs=vcs;
		_visitor = visitor;
	}
	
	/** so, what this does is this:
	 *  uses VersionControlServer.QueryMerges to get a list of target and source changeset.
	 *  a private method trees that list into:
	 *   target changeset [ list o' source changests ]
	 *  we then march through that list
	 *   target(s) (here is where we visit the target)
	 *     source(s)
	 *  
	 *  if the source contains any TFS paths we want to query,
	 *   then we do a recursive query on that changeset (it is now the target)
	 *  otherwise we'll just visit the changeset
	 */
	public virtual bool visit(int parentID,
														string targetPath, 
														VersionSpec targetVer,
														VersionSpec fromVer,
														VersionSpec toVer)
	{ 
		bool result = _visit(parentID, null, 
												 null, null, 
												 targetPath, targetVer, 
												 fromVer, toVer, RecursionType.Full); 
		
		/* dump some stats out to the log file. */
		logger.DebugFormat("{0} queries took {1}", _queries, _queryTimer.Total);
		logger.DebugFormat("{0} findchangesetbranchcalls for {1} changesets.", 
											 MegaHistory.FindChangesetBranchesCalls, _visitor.visitedCount());
		
		return result;
	}
	
	public virtual bool visit(string srcPath, VersionSpec srcVer, 
														string target, VersionSpec targetVer,
														VersionSpec fromVer, VersionSpec toVer,
														RecursionType recursionType)
	{
		bool result = _visit(0, null, 
												 srcPath, srcVer, 
												 target, targetVer, 
												 fromVer, toVer, recursionType);
		
		/* dump some stats out to the log file. */
		logger.DebugFormat("{0} queries took {1}", _queries, _queryTimer.Total);
		logger.DebugFormat("{0} findchangesetbranchcalls for {1} changesets.", 
											 MegaHistory.FindChangesetBranchesCalls, _visitor.visitedCount());
		return result;
	}
	
	/** visit an explicit list of changesets. 
	 */
	private bool _visit(int parentID,
											List<string> targetBranches,
											string srcPath, VersionSpec srcVer, 
											string target, VersionSpec targetVer,
											VersionSpec fromVer, VersionSpec toVer,
											RecursionType recursionType)
	{
		logger.DebugFormat("{{_visit: parent={0}",parentID);
		
		/* so, here we might have a few top-level merge changesets. 
		 * the red-black binary tree sorts the changesets in decending order
		 */
		RBDictTree<int,SortedDictionary<int,ChangesetMerge>> merges = 
			query_merges(_vcs, srcPath, srcVer, target, targetVer, fromVer, toVer, recursionType);
		
		RBDictTree<int,SortedDictionary<int,ChangesetMerge>>.iterator it = merges.begin();
		
		/* walk through the merge changesets
		 * - this should return only one merge changeset for the recursive calls.
		 */
		for(; it != merges.end(); ++it)
			{
				int csID = it.value().first;
				try
					{
						/* visit the 'target' merge changeset here. 
						 * it's parent is the one passed in.
						 */
						Changeset cs = _vcs.GetChangeset(csID);
						string path_part = _get_path_part(target);
						ChangesetVersionSpec cstargetVer = targetVer as ChangesetVersionSpec;
						
						/* pass in the known set of branches in this changeset, or let it figure that out. */
						if (cstargetVer.ChangesetId == csID) { _visitor.visit(parentID, cs, targetBranches); }
						else { _visitor.visit(parentID, cs); }
						
						{
							Visitor.PatchInfo p = _visitor[csID];
							/* if the user chose to heed our warnings, 
							 *   and there are no branches to visit for this changeset?
							 *   move on to the next result, ignoring all the 'composites' of this 'fake' changeset.
							 */
							if (! _options.ForceDecomposition &&
									(p != null && (p.treeBranches == null || p.treeBranches.Count ==0)))
								{ continue; }
						}
						
						foreach(KeyValuePair<int,ChangesetMerge> cng in it.value().second)
							{
								ChangesetMerge csm = cng.Value;
								/* now visit each of the children.
								 * we've already expanded cs.ChangesetId (hopefully...)
								 */
								try
									{
										Changeset child = _vcs.GetChangeset(csm.SourceVersion);
										List<string> branches = null;
										
										{
											/* speed-up. if we already have the branches, don't do it again. 
											 * looking through 40K+ records takes some time...
											 */
											Visitor.PatchInfo p = _visitor[child.ChangesetId];
											if (p != null) { branches = p.treeBranches; }
											else { branches = FindChangesetBranches(child); }
										}
										
										/* - this is for the recursive query -
										 * you have to have specific branches here.
										 * a query of the entire project will probably not return.
										 *
										 * eg: 
										 * query target $/IGT_0803/main/EGS,78029 = 41s
										 * query target $/IGT_0803,78029 = DNF (waited 6+m)
										 */
										
										if (_options.NoRecurse)
											{
												/* they just want the top-level query. */
												_visitor.visit(cs.ChangesetId, child, branches);
											}
										else
											{
												if (!_visitor.visited(child.ChangesetId))
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
																if (_options.AllowBranchRevisiting || !_visitor.visited(branches[i]))
																	{
																		logger.DebugFormat("visiting ({0}) {1}{2}",
																											 child.ChangesetId, branches[i], path_part);
																		
																		/* this recurisve call needs to then 
																		 * handle visiting the results of this query. 
																		 */
																		bool branchResult = _visit(cs.ChangesetId, branches, 
																															 null, null,
																															 branches[i]+path_part, tv, 
																															 tv, tv, RecursionType.Full);
																		
																		if (branchResult) { results = true; }
																	}
															}
														
														if (!results)
															{
																/* we got no results from our query, so display the changeset
																 * (it won't be displayed otherwise)
																 */
																_visitor.visit(cs.ChangesetId, child, branches); //, branches);
															}
													}
												else
													{
														/* do we want to see it again? */
														_visitor.visit(cs.ChangesetId, child, branches);//, branches);
													}
											}
									}
								catch(Exception e) { _visitor.visit(cs.ChangesetId, csm.SourceVersion, e); }
							}
					}
				catch(Exception e) { _visitor.visit(parentID, csID, e); }
			}
		
		logger.DebugFormat("}}_visit:{0}", parentID);
		
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
	public static List<string> FindChangesetBranches(Changeset cs)
	{
		Timer timer = new Timer();
		List<string> itemBranches = new List<string>();
		
		++MegaHistory.FindChangesetBranchesCalls;
		
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
						
						if (IsChangeToConsider(cs.Changes[i])	)
							{
								string itemPath = cs.Changes[i].Item.ServerItem;
								bool found = false;
								int idx = 0;
								
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
		logger.DebugFormat("branches for {0} took: {1}", cs.ChangesetId, timer.Delta);
		
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
				if (IsChangeToConsider(args.changes[res]))
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
	

	private RBDictTree<int,SortedDictionary<int,ChangesetMerge>> 
		query_merges(VersionControlServer vcs,
								 string srcPath, VersionSpec srcVer,
								 string targetPath, VersionSpec targetVer,
								 VersionSpec fromVer, VersionSpec toVer,
								 RecursionType recurType)
	{
		RBDictTree<int,SortedDictionary<int,ChangesetMerge>> merges = 
			new RBDictTree<int,SortedDictionary<int,ChangesetMerge>>();

		++_queries;
		logger.DebugFormat("query_merges {0}, {1}, {2}, {3}, {4}, {5}",
											 ( srcPath == null ? "(null)": srcPath), 
											 (srcVer == null ? "(null)" : srcVer.DisplayString),
											 targetPath, targetVer.DisplayString, 
											 (fromVer == null ? "(null)" : fromVer.DisplayString),
											 (toVer == null ? "(null)" : toVer.DisplayString));
		
		_queryTimer.start();
		try
			{
				ChangesetMerge[] mergesrc = vcs.QueryMerges(srcPath, srcVer, targetPath, targetVer,
																										fromVer, toVer, recurType);
				/* group by merged changesets. */
				for(int i=0; i < mergesrc.Length; ++i)
					{
						RBDictTree<int,SortedDictionary<int,ChangesetMerge>>.iterator it = 
							merges.find(mergesrc[i].TargetVersion);
						
						if (merges.end() == it)
							{
								/* create the list... */
								SortedDictionary<int,ChangesetMerge> group = new SortedDictionary<int,ChangesetMerge>();
								group.Add(mergesrc[i].SourceVersion, mergesrc[i]);
								merges.insert(mergesrc[i].TargetVersion, group);
							}
						else
							{ it.value().second.Add(mergesrc[i].SourceVersion, mergesrc[i]); }
					}
			}
		catch(Exception e)
			{
				Console.Error.WriteLine("Error querying: {0},{1}", targetPath, targetVer);
				Console.Error.WriteLine(e.ToString());
			}
		_queryTimer.stop();
		
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
