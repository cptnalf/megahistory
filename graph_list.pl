#!perl -w

print "digraph {\n";

my @edges;

my ($parent, $changeset, $author, $date);
my %changes;

while(<STDIN>) {
		chomp;
		
		if (/Parent: ([0-9]+)/) { $parent = $1; }
		if (/Changeset: ([0-9]+)/) { $changeset = $1; }
		
		if (/Author: (.+)/) { $author = $1; }
		if (/Date: (.+)/) {
				$date = $1;
				
				if ($parent) { push @edges, "C".$changeset." -> C".$parent.";\n"; }
				
				unless ($changes{$changeset}) {
						$changes{$changeset} = "C".$changeset." [ label = \"".$changeset."\\n".$date."\\n".$author."\" ];\n";
				}
		}
}

foreach $foo (sort keys %changes) { print $changes{$foo}; }

foreach( @edges) { print; }

print "\n};\n";
