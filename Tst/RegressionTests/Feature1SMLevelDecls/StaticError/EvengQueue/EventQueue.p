event e: int;

eventQueue machine Main {
	var m1: M1;
	var prev: int;
	start state Init {
		entry {
			m1 = new M1((sender= this));
			prev = 0;
		}
		on e do (p: int) {
			assert p >= prev;
			prev = p;
		}
	}
}

machine M1 {
	var i: int;
	var j: int;
	start state Init {
		entry (payload: (sender: Main)) {
			i = 0;
			while (i < 10)
			{
				send payload.sender, e, i;
				i = i + 1;
			}
		}
	}
}