
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;

/** what happens when we see a changeset?
 */
public abstract class Visitor
{
	/** private version of a 'Changeset' object */
	public class PatchInfo
	{
		public int parent;
		public Changeset cs;
		public List<string> treeBranches;
		
		public PatchInfo(int p, Changeset c, List<string> tb)
		{
			parent = p;
			cs = c;
			treeBranches = tb;
		}
		
		public void print(System.IO.TextWriter writer) { print(0, writer); }
		
		public void print(int parentID, System.IO.TextWriter writer)
		{
			int p = 0;
			
			if (parentID != 0) { p = parentID; }
			else { p = parent; }
			
			if (p != 0) { writer.WriteLine("Parent: {0}", p); }
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
	
	protected Dictionary<int,PatchInfo> _cache = new Dictionary<int,PatchInfo>();
	
	public PatchInfo this[int changesetid]
	{
		get
			{
				PatchInfo p;
				_cache.TryGetValue(changesetid, out p);
				return p;
			}
	}
	
	/** have we already visited this changeset?
	 */
	public bool visited(int changesetid) { return _cache.ContainsKey(changesetid); }
	
	public void visit(int parentID, Changeset cs) { visit(parentID, cs, null); }
	public virtual void visit(int parentID, Changeset cs, List<string> branches)
	{
		PatchInfo p;
		
		if (_cache.TryGetValue(cs.ChangesetId, out p)) 
			{
				_seen(parentID, p);
			}
		else
			{
				/* only print stuff we haven't already seen. */
				List<string> treeBranches = branches;
				if (treeBranches == null) { treeBranches = MegaHistory.FindChangesetBranches(cs); }
				p = new PatchInfo(parentID, cs, treeBranches);
				
				_cache.Add(p.cs.ChangesetId, p);
				_visit(p);
			}
	}
	
	protected abstract void _visit(PatchInfo p);
	protected abstract void _seen(int parentID, PatchInfo p);
	
	/* for errors. */
	public abstract void visit(int parentID, int changesetID, Exception e);
}

/*
 * so for a nice changeset tree like :
 *    cm
 *    /\
 *  cm  c1
 *  /\
 * c  cm
 *    /\
 *   c  c1
 * 
 */