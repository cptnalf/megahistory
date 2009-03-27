
using System.Collections.Generic;

public class pair<F,S> : System.IComparable<pair<F,S>> where F : System.IComparable<F>
{
	public F first;
	public S second;
	
	public pair(F f, S s) { first = f; second = s; }
	
	private int _cmp(pair<F,S> two)
	{
		int result = -1;
		object o = (object)two;
		
		if (o != null) { result = first.CompareTo(two.first); }
		
		return result;
	}
	
	public int CompareTo(pair<F,S> obj) { return _cmp(obj); }
	
	int System.IComparable<pair<F,S>>.CompareTo(pair<F,S> obj) { return _cmp(obj); }
	
	public override int GetHashCode() { return ((object)first).GetHashCode(); }
}

/*
public class RBDictNode<K,V> : RBNode<pair<K,V>>
{
	public RBDictNode(pair<K,V> v) : base(v) { }
	
	public V Value { get { return value.second; } set { this.value.second = value; } }
	public K Key { get { return value.first; } }
}
*/

public class RBDictTree<K,V> : RBTree<pair<K,V>> where K : System.IComparable<K>
{
	public void insert(K key, V value)
	{
		pair<K,V> p = new pair<K,V>(key, value);
		insert_td(p);
	}
	
	public iterator find(K key)
	{
		Stack<RBNode<pair<K,V>>> stack = new Stack<RBNode<pair<K,V>>>();
		bool done = false;
		RBNode<pair<K,V>> node = _tree;
		
		while(!done && node != null)
			{
				int res = node.value.first.CompareTo(key);
				if (res == 0)
					{
						/* the iterator now points to the matching node.*/
						stack.Push(node);
						done = true;
					}
				else if (res > 0 )
					{
						stack.Push(node);
						node = node.link[0];
					}
				else
					{
						stack.Push(node);
						node = node.link[1];
					}
			}
		
		return new iterator(_tree, stack);
	}
}

public class RBNode<T> where T : System.IComparable<T>
{
	public bool red;
	public T value;
	public RBNode<T>[] link = new RBNode<T>[2];
	
	public RBNode(T v)
	{
		red = true;
		value = v;
		link[0] = null;
		link[1] = null;
	}
	
	public RBNode() : this(default(T)) { }
	
	/** help compare two nodes. 
	 */
	private static int _cmp(RBNode<T> one, RBNode<T> two)
	{
		int res =0;
		object oone = one;
		object otwo = two;
		if (oone == null)
			{
				/* equal (both null
				 * or less than (two isn't null)
				 */
				res = (otwo == null ?  0 : -1 );
			}
		else 
			{
				if (otwo == null) { res = 1; }
				else { res = one.value.CompareTo(two.value); }
			}
		return res;
	}
	
	public static bool operator >(RBNode<T> one, RBNode<T> two)
	{
		int res = _cmp(one, two);
		return (res > 0);
	}
	public static bool operator <(RBNode<T> one, RBNode<T> two)
	{
		int res = _cmp(one, two);
		return (res < 0);
	}
	public static bool operator==(RBNode<T> one, RBNode<T> two)
	{
		int res = _cmp(one, two);
		return res == 0;
	}
	
	public static bool operator!=(RBNode<T> one, RBNode<T> two) { return !(one == two); }
	
	public static bool operator<=(RBNode<T> one, RBNode<T> two)
	{
		int res = _cmp(one, two);
		return res <= 0;
	}
	public static bool operator>=(RBNode<T> one, RBNode<T> two)
	{
		int res = _cmp(one,two);
		return res >= 0;
	}
	
	public override bool Equals(object o)
	{
		RBNode<T> one = o as RBNode<T>;
		bool result = false;
		
		if (one != null) { result = (this == one); }
		return result;
	}
	
	public override int GetHashCode()
	{
		object o = (object)value;
		return o.GetHashCode();
	}
}

public class RBTree<T> where T : System.IComparable<T>
{
	public class iterator
	{
		private RBNode<T> _root;
		private Stack<RBNode<T>> _nodes = new Stack<RBNode<T>>();
		
		public iterator() { _root = null; }
		public iterator(RBNode<T> t) { _root = t; }
		public iterator(RBNode<T> t, Stack<RBNode<T>> s) { _root = t; _nodes = s; }
		
		public T value() { return _nodes.Peek().value; }
		
		public void init()
		{
			while(_nodes.Count > 0) { _nodes.Pop(); }
			
			if (_root != null)
				{
					/* need to traverse to the bottom left of the tree. */
					RBNode<T> node = _root;
					
					while(node.link[0] != null) { _nodes.Push(node); node = node.link[0]; }
					_nodes.Push(node);
				}
		}
		
		public static iterator operator++(iterator it)
		{
			/* don't care about left.
			 * this means when you list the node as the active node, you're looking at that node.
			 * from there, you go to the right.
			 * then back up to the parent's parent: eg:
			 *    d
			 *  b    e
			 * a   c    f
			 * so the stack would look like this initially:
			 * a b d
			 * b d   - print a
			 * c d   - print b
			 * d     - print c
			 * e     - print d
			 */
			RBNode<T> node = it._nodes.Pop();
			if (node != null && node.link[1] != null) { it._nodes.Push(node.link[1]); }
			
			return it;
		}
		
		/** true if they are the equivilant object.
		 *  false if the object isn't an 'iterator' or if the object isn't the same iterator.
		 */
		public override bool Equals(object o)
		{
			iterator it = o as iterator;
			return (it != null ? (this == it) : false);
		}
		
		public override int GetHashCode()
		{
			if (_root != null) { return _root.GetHashCode(); }
			return 0;
		}
		
		public static bool operator==(iterator one, iterator two)
		{
			object o1, o2;
			bool result = false;
			
			o1 = one;
			o2 = two;
			
			/* if both are null, then we're equal.
			 * if they're the same object, then we're equal.
			 * so that leaves just the objects themselves.
			 * then if the top node is equal we're good.
			 * 
			 */
			
			if (o1 != null && o2 != null)
				{
					if ( one._root == two._root)
						{
							if ( one._nodes.Count > 0 && two._nodes.Count > 0)
								{
									result = one._nodes.Peek() == two._nodes.Peek();
								}
							else { result = one._nodes.Count == two._nodes.Count; }
						}
					else
						{
							/* so the roots are not equal. (eg: it != end() )*/
							if (one._root == null)
								{
									/* the first one's root is null, so this is an end() iterator 
									 * see if we're at the end in the second iterator.
									 */
									result = two._nodes.Count == 0;
								}
							else
								{
									/* is the second iterator's root null (an end() iterator) */
									if (two._root == null) { result = one._nodes.Count == 0; }
								}
						}
				}
			else { result = (o1 == null) && (o2 == null); }
			
			return result;
		}
		public static bool operator!=(iterator one, iterator two) { return !(one == two); }
	}

	protected RBNode<T> _tree;
	
// 	void insert(T value)
// 	{
// 		root = _insert(ref root, value);
// 	}
	
// 	internal RBTree<T> _insert(ref RBTree<T> root, T value)
// 	{
// 		if (tree == null) { tree = new RBNode<T>(value); }
// 		else
// 			{
// 				int dir = (root.value < value ? 1 : 0);
				
// 				root.link[dir] = _insert(root.link[dir], data);
// 			}
// 		return root;
// 	}
	
	public iterator begin() { iterator it=new iterator(_tree); it.init(); return it; }
	public iterator end() { return new iterator(); }
	
	public bool is_red(RBNode<T> root)
	{
		return root != null && root.red;
	}
	
	public RBNode<T> _single_rot(RBNode<T> root, int dir)
	{
		RBNode<T> save = root.link[dir ^ 0x1];
		root.link[dir ^ 0x1] = save.link[dir];
		save.link[dir] = root;
		
		root.red = true;
		save.red = false;
		
		return save;
	}
	
	public RBNode<T> _double_rot(RBNode<T> root, int dir)
	{
		root.link[dir ^ 0x1] = _single_rot(root.link[dir ^ 0x1], dir ^ 0x1);
		return _single_rot(root, dir);
	}
	
	public int assert() { return rb_assert(_tree); }
	
	/** makes sure the tree is a valid one.
	 *  0 means an error.
	 *  > 0 is the count of blank nodes.
	 */
	int rb_assert(RBNode<T> root)
	{
		int lh;
		int rh;
		
		if (root == null) { return 1; }
		else
			{
				RBNode<T> ln = root.link[0];
				RBNode<T> rn = root.link[1];
				
				if (is_red(root))
					{
						if (is_red(ln) || is_red(rn))
							{
								System.Console.WriteLine("red violation");
								return 0;
							}
					}
				
				lh = rb_assert(ln);
				rh = rb_assert(rn);
				
				/* invalid binary search tree */
				if ( (ln != null && ln >= root)
						 || (rn != null && rn <= root) )
					{
						System.Console.WriteLine("binary tree violation");
						return 0;
					}
				
				/* black heights mismatch */
				if (lh != 0 && rh != 0 && lh != rh)
					{
						System.Console.WriteLine("black violation");
						return 0;
					}
				
				/* only count black links */
				if (lh != 0 && rh != 0) { return is_red(root) ? lh : lh + 1; }
				else { return 0; }
			}
	}
	
	RBNode<T> insert_r(RBNode<T> root, T data)
	{
		if (root == null) { root = new RBNode<T>(data); }
		else if (root.value.CompareTo(data) != 0)
			{
				int dir = (root.value.CompareTo(data) < 0 ? 1 : 0);
				
				root.link[dir] = insert_r(root.link[dir], data);
				
				/* hey, rebalance tree.. */
				if (is_red(root.link[dir]))
					{
						if (is_red(root.link[dir ^ 0x1]))
							{
								/* case 1 */
								root.red = true;
								root.link[0].red = false;
								root.link[1].red = false;
							}
						else
							{
								/* case 2 and 3 */
								if (is_red(root.link[dir].link[dir])) { root = _single_rot(root, dir ^ 0x1); }
								else if (is_red(root.link[dir].link[dir ^ 0x1]))
									{
										root = _double_rot(root, dir ^ 0x1);
									}
							}
					}
			}
		
		return root;
	}
	
	int insert(T data)
	{
		_tree = insert_r(_tree, data);
		_tree.red = false;
		return 1;
	}
	
	internal RBNode<T> remove_r(RBNode<T> root, T data, ref bool done)
	{
		if (root == null) { done = true; }
		else
			{
				int dir;
				
				if (root.value.CompareTo(data) == 0)
					{
						if (root.link[0] == null || root.link[1] == null)
							{
								int id = (root.link[0] == null ? 1 : 0);
								RBNode<T> save = root.link[id];
								
								/* case 0 */
								if (is_red(root)) { done = true; }
								else if (is_red(save))
									{
										save.red = false;
										done = true;
									}
								
								/* delete root; */
								
								return save;
							}
						else
							{
								RBNode<T> heir = root.link[0];
								
								while(heir.link[1] != null) { heir = heir.link[1]; }
								
								root.value = heir.value;
								data = heir.value;
							}
					}
				
				dir = (root.value.CompareTo(data) < 0 ? 1 : 0);
				root.link[dir] = remove_r(root.link[dir], data, ref done);
				
				if (!done)
					{
						root = remove_balance(root, dir, ref done);
					}
			}
		
		return root;
	}
	
	internal RBNode<T> remove_balance(RBNode<T> root, int dir, ref bool done)
	{
		RBNode<T> p = root;
		RBNode<T> s = root.link[ dir ^ 0x1];
		
		if (s != null && !is_red(s))
			{
				/* black sibling cases */
				if (!is_red(s.link[0]) && !is_red(s.link[1]))
					{
						if (is_red(p)) { done = true; }
						p.red = false;
						s.red = true;
					}
				else
					{
						bool save = root.red;
						
						if (is_red(s.link[dir ^ 0x1])) { p = _single_rot(p, dir); }
						else { p = _double_rot(p, dir); }
						
						p.red = save;
						p.link[0].red = false;
						p.link[1].red = false;
						done = true;
					}
			}
		else if (s.link[dir] != null)
			{
				/* red sibling cases */
				RBNode<T> r = s.link[dir];
				
				if (!is_red(r.link[0]) && !is_red(r.link[1]))
					{
						p = _single_rot(p, dir);
						p.link[dir].link[dir ^ 0x1].red = true;
					}
				else
					{
						if (is_red(r.link[dir]))
							{
								s.link[dir] = _single_rot(r, dir ^ 0x1);
							}
						p = _double_rot(p, dir);
						s.link[dir].red = false;
						p.link[ dir ^ 0x1].red = true;
					}
				
				p.red = false;
				p.link[dir].red = false;
				done = true;
			}
		
		return p;
	}
	
	public int remove(T data)
	{
		bool done = false;
		
		_tree = remove_r(_tree, data, ref done);
		if (_tree != null) { _tree.red = false; }
		
		return 1;
	}
	
	public int insert_td(T data)
	{
		if (_tree == null) 
			{
				/* empty tree case */
				_tree = new RBNode<T>(data);
				if (_tree == null) { return 0; }
			}
		else
			{
				RBNode<T> head = new RBNode<T>(); /* False tree root */
				
				RBNode<T> g;
				RBNode<T> t;     /* Grandparent & parent */
				RBNode<T> p;
				RBNode<T> q;     /* Iterator & parent */
				int dir = 0;
				int last=0;
				
				/* Set up helpers */
				t = head;
				g = p = null;
				q = t.link[1] = _tree;
				
				/* Search down the tree */
				for ( ; ; ) 
					{
						if ( q == null ) 
							{
								/* Insert new node at the bottom */
								p.link[dir] = q = new RBNode<T>(data);
								if (q == null) { return 0; }
							}
						else if (is_red(q.link[0]) && is_red(q.link[1]))
							{
								/* Color flip */
								q.red = true;
								q.link[0].red = false;
								q.link[1].red = false;
							}
						
						/* Fix red violation */
						if ( is_red(q) && is_red(p) ) 
							{
								int dir2 = (t.link[1] == g ? 1 : 0);
								
								if (q == p.link[last])
									t.link[dir2] = _single_rot( g, last ^ 0x1);
								else
									t.link[dir2] = _double_rot( g, last ^ 0x1 );
							}
						
						/* Stop if found */
						if ( q.value.CompareTo(data) == 0 )
							break;
						
						last = dir;
						dir = (q.value.CompareTo(data) < 0 ? 1 : 0);
						
						/* Update helpers */
						if ( g != null )
							t = g;
						
						g = p;
						p = q;
						
						q = q.link[dir];
					}
				
				/* Update root */
				_tree = head.link[1];
			}
		
		/* Make root black */
		_tree.red = false;
		
		return 1;
	}
	
	public int remove_td(T data )
	{
    if ( _tree != null ) 
			{
				RBNode<T> head = new RBNode<T>(); /* False tree root */
				RBNode<T> q, p, g; /* Helpers */
				RBNode<T> f = null;  /* Found item */
				int dir = 1;
				
				/* Set up helpers */
				q = head;
				g = p = null;
				q.link[1] = _tree;
				
				/* Search and push a red down */
				while ( q.link[dir] != null ) 
					{
						int last = dir;
						
						/* Update helpers */
						g = p;
						p = q;
						q = q.link[dir];
						dir = (q.value.CompareTo(data) < 0 ? 1 : 0);
						
						/* Save found node */
						if ( q.value.CompareTo(data) == 0 )
							f = q;
						
						/* Push the red node down */
						if ( !is_red(q) && !is_red(q.link[dir])) 
							{
								if ( is_red(q.link[ dir ^ 0x1 ]) )
									p = p.link[last] = _single_rot(q, dir);
								else if ( !is_red(q.link[ dir ^ 0x1 ]))
									{
										RBNode<T> s = p.link[ last ^ 0x1];
										
										if ( s != null ) 
											{
												if ( !is_red(s.link[ last ^ 0x1 ]) && !is_red(s.link[last]))
													{
														/* Color flip */
														p.red = false;
														s.red = true;
														q.red = true;
													}
												else
													{
														int dir2 = (g.link[1] == p ? 1 : 0);
														
														if ( is_red(s.link[last]))
															g.link[dir2] = _double_rot(p, last);
														else if ( is_red(s.link[last ^ 0x1]))
															g.link[dir2] = _single_rot(p, last);
														
														/* Ensure correct coloring */
														q.red = g.link[dir2].red = true;
														g.link[dir2].link[0].red = false;
														g.link[dir2].link[1].red = false;
													}
											}
									}
							}
					}
				
				/* Replace and remove if found */
				if ( f != null ) 
					{
						f.value = q.value;
						p.link[(p.link[1] == q ? 1 : 0)] = q.link[(q.link[0] == null ? 1 : 0)];
						
						q = null;
						//free ( q );
					}
				
				/* Update root and make it black */
				_tree = head.link[1];
				if ( _tree != null )
					_tree.red = false;
			}
		
		return 1;
	}
	
	public virtual iterator find(T data)
	{
		Stack<RBNode<T>> stack = new Stack<RBNode<T>>();
		bool done = false;
		RBNode<T> node = _tree;
		
		while(!done && node != null)
			{
				int res = node.value.CompareTo(data);
				if (res == 0)
					{
						/* the iterator now points to the matching node.*/
						stack.Push(node);
						done = true;
					}
				else if (res > 0 )
					{
						stack.Push(node);
						node = node.link[0];
					}
				else
					{
						stack.Push(node);
						node = node.link[1];
					}
			}
		
		return new iterator(_tree, stack);
	}
}

class prog
{
	static void rb_tree(string filename)
	{		
		RBTree<string> tree = new RBTree<string>();
		System.Random r = new System.Random();
		System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
		long ts, tt;
		
		{
			System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open);
			System.IO.StreamReader sr = new System.IO.StreamReader(fs);
			string line;
			
			ts = System.DateTime.Now.Ticks;
			while((line = sr.ReadLine()) != null) { tree.insert_td(line); lines.Add(line); }
			tt = System.DateTime.Now.Ticks - ts;
			{ System.Console.WriteLine(new System.TimeSpan(tt)); }
			
			sr.Close();
			sr.Dispose();
		}
		
		ts = System.DateTime.Now.Ticks;
		int res = tree.assert();
		tt = System.DateTime.Now.Ticks - ts;
		{ System.Console.WriteLine(new System.TimeSpan(tt)); }
		
		System.Console.WriteLine("height={0}", res);
		System.Console.WriteLine("lines={0}; log2(lines)={1}", lines.Count, System.Math.Log((double)lines.Count, 2.0));
		
		long total = System.GC.GetTotalMemory(false);
		System.Console.WriteLine("used memory: {0}", total);
		{
			RBTree<string>.iterator it = tree.begin();
			for(; it != tree.end(); ++it) { System.Console.WriteLine(it.value()); }
		}
		
		tt = 0;
		for(int i=0; i<500; ++i)
			{
				int idx = r.Next(lines.Count);
				
				ts = System.DateTime.Now.Ticks;
				tree.remove_td(lines[idx]);
				tt += System.DateTime.Now.Ticks - ts;
				
				res = tree.assert();
				//System.Console.WriteLine("height={0}", res);
			}
		{
			System.Console.WriteLine("rb_tree total R: {0}", new System.TimeSpan(tt));
			System.Console.WriteLine("rb_tree avg R: {0}", new System.TimeSpan((tt/500)));
		}
		
		tt = 0;
		for(int i=0; i<500; ++i)
			{
				int idx = r.Next(lines.Count);
				
				ts = System.DateTime.Now.Ticks;
				tree.insert_td(lines[idx]);
				tt += System.DateTime.Now.Ticks - ts;
			}
		{
			System.Console.WriteLine("rb_tree total I: {0}", new System.TimeSpan(tt));
			System.Console.WriteLine("rb_tree avg I: {0}", new System.TimeSpan((tt/500)));
		}
		
		r = null;
		tree = null;
		lines = null;
		System.GC.Collect();
	}
	
	static void slist(string filename)
	{
		System.Collections.Generic.List<string> slist = new System.Collections.Generic.List<string>();
		System.Random r = new System.Random();
		System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
		long ts, tt;
		
		{
			System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open);
			System.IO.StreamReader sr = new System.IO.StreamReader(fs);
			string line;
			
			ts = System.DateTime.Now.Ticks;
			while((line = sr.ReadLine()) != null) { slist.Add(line); lines.Add(line); }
			tt = System.DateTime.Now.Ticks - ts;
			{ System.Console.WriteLine(new System.TimeSpan(tt)); }
			
			sr.Close();
			sr.Dispose();
		}
		
		slist.Sort();
		long total = System.GC.GetTotalMemory(false);
		System.Console.WriteLine("used memory: {0}", total);
		
		tt = 0;
		for(int i=0; i < 500; ++i)
			{
				int idx = r.Next(lines.Count);
				
				ts = System.DateTime.Now.Ticks;
				slist.Remove(lines[idx]);
				tt += System.DateTime.Now.Ticks - ts;
			}
		{
			System.Console.WriteLine("slist total R: {0}", new System.TimeSpan(tt));
			System.Console.WriteLine("slist avg R: {0}", new System.TimeSpan((tt/500)));
		}
		
		tt = 0;
		for(int i=0; i < 500; ++i)
			{
				int idx = r.Next(lines.Count);
				
				ts = System.DateTime.Now.Ticks;
				int slist_idx = slist.BinarySearch(lines[idx]);
				
				if (slist_idx < 0) { slist_idx = ~slist_idx; }
				slist.Insert(slist_idx, lines[idx]);
				tt += System.DateTime.Now.Ticks - ts;
			}
		{
			System.Console.WriteLine("slist total I: {0}", new System.TimeSpan(tt));
			System.Console.WriteLine("slist avg I: {0}", new System.TimeSpan((tt/500)));
		}
		
		r = null;
		slist = null;
		lines = null;
		System.GC.Collect();
	}

#if FLARGITY
	static void Main(string[] args)
	{
		rb_tree(args[0]);
		
		slist(args[0]);
	}
#endif
}
