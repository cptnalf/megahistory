
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