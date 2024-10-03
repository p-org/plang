machine Main {
	start state S {
		entry {
		    var x: int;
		    var y: int;
		    
		    x = 10000;
		    y = choose(x); // OK
		    print format("y is {0}", y);
		    
		    x = 10001;
		    y = choose(x); // error
		    print format("y is {0}", y);
		}
	}
}
