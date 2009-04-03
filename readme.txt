retrieves merge history.

this will do a recursive expansion of ALL merge changesets from a source tree+version to any related changesets.
It uses the querymerges function to get the associated merges.

It is a command-line tool.
Be careful with your queries.  It is easy to construct queries which may never return.


graph_list.pl:
Converts the output of the megahistory command into a dot graph.
(dot is a graphviz program)
eg:
megahistory ... | perl graph_list.pl | dot -Tpng -Kfdp -oC88324.png

help:
megahistory <options>
queries tfs for the list of changesets which make up a merge

eg: megahistory -s foo --src $/foo,45 --from 10,45 $/bar,43

-s <server name>	the tfs server to connect to
--src <path>[,<version>]	the source of the changesets
--from version[,version]	the changeset range to look in.
--no-recurse            	do not recursively query merge history
                        	 this will execute only one QueryMerges
--name-only                     add the path of the files to the changeset info
--name-status                   print the path and the change type in the changeset info
target,version	the required path we're looking at



this program prints out a git-like history of the changes:
>megahistory -s rnotfsat --from 79087 $/IGT_0803/development/dev_advantage/EGS,79087

Changeset: 79087
Author: IGTMASTER\FordB
Date: 03/05/2009 08:17:37


Parent: 79087
Changeset: 78710
Author: IGTMASTER\robertty
Date: 03/03/2009 13:21:44
RI dev_build/egs/Shared to main/egs/Shared

Parent: 78710
Changeset: 78029
Author: IGTMASTER\LewisL
Date: 02/26/2009 15:19:15
Changed to display who the commiter was for the changeset.

Parent: 79087
Changeset: 79084
Author: IGTMASTER\FordB
Date: 03/05/2009 08:10:59
Merge Dev_SP to main
....
