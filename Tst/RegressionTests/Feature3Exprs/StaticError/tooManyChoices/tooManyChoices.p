machine Main {
	start state S {
		entry {
		    var x: int;
		    var y: int;
		    
		    x = choose(10000); // OK
		    print format("x is {0}", x);
		    
		    y = choose(10001); // error
		    print format("y is {0}", y);
		}
	}
}
