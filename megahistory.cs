/*
 * chiefengineer@neghvar
 * 2009-04-13 18:57:49 -0700
 */

using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;

/** pulls a chain of changesets out of merges
 */
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
														visitor.visit(cs.ChangesetId, child, branches);
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
