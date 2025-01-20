event e: int;

eventbag machine Main {
	var m1: M1;
	var i: int;
	var prev: int;
	start state Init {

		entry {
			m1 = new M1(this);
			i = 0;
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
		entry (payload: Main) {
			i = 0;
			while (i < 10)
			{
				send payload, e, i;
				i = i + 1;
			}
		}
	}
}