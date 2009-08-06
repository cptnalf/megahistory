
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;

/** print the changeset when we see it.
 */
class HistoryViewer : Visitor
{
	public enum Printwhat : short
	{
		None,
		NameOnly,
		NameStatus
	}
	
	private Printwhat _printWhat = Printwhat.None;
	
	public HistoryViewer(Printwhat printWhat) { _printWhat = printWhat; }
	
	protected override void _visit(PatchInfo p) { _print(0, p); }
	
	private void _print(int parentID, PatchInfo p)
	{
		if (parentID == 0)
			{
				p.print(Console.Out);
				
				/* only print the list if it wasn't a merge changeset. 
				 * merge changesets have tree-branches
				 */
				if (p.treeBranches == null || p.treeBranches.Count == 0)
					{
						if (_printWhat != Printwhat.None)
							{
								for(int i=0; i < p.cs.Changes.Length; ++i)
									{
										switch(_printWhat)
											{
											case(Printwhat.NameOnly): 
												{ Console.WriteLine("{0}", p.cs.Changes[i].Item.ServerItem); break; }
											case(Printwhat.NameStatus):
												{
													Console.WriteLine("{0:20}{1}", 
																						p.cs.Changes[i].ChangeType, p.cs.Changes[i].Item.ServerItem);
													break;
												}
											}
									}
								Console.WriteLine();
							}
					}
			}
		else
			{
				Console.WriteLine("Changeset: {0}", p.cs.ChangesetId);
				Console.WriteLine();
			}
	}
	
	protected override void _seen(int parentID, PatchInfo p)
	{
		//Console.WriteLine("again! {0} -> ({1}) {2}", parentID, p.parent, p.cs.ChangesetId);
		_print(parentID, p);
	}
	
	public override void visit(int parentID, int csID, Exception e)
	{
		Console.WriteLine("Changeset: {0}", csID);
		Console.WriteLine(" --Error retrieving changeset-- ");
		Console.WriteLine();
		Console.WriteLine(e);
	}

}
