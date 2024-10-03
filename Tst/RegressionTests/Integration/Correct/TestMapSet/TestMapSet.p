  // Recreating set in map error

machine Main
{
  start state Init {
    entry {
      ReplicateMapSet();
    }
  }
}

fun ReplicateMapSet()
{
  var seqmap: map[seq[int], int]; // Creating example for comparison
  var mapmap: map[map[int, int], int];
  var setmap: map[set[int], int]; 

  seqmap[default(seq[int])] = 0;
  mapmap[default(map[int,int])] = 1;
  setmap[default(set[int])] = 2;

  print format("seqmap: {0}", seqmap[default(seq[int])]);
  print format("mapmap: {0}", mapmap[default(map[int,int])]);
  print format("setmap: {0}", setmap[default(set[int])]);
}
