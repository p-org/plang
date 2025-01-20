event A: int;
event B: M1;
event C: int;
event unblock;

machine Main {
	var m1: M1;
	var m2: M2;
	start state Init {
		entry {
      m1 = new M1();
      m2 = new M2();
			send m1, A, 1;
      send m2, B, m1;
		}
	}
}

eventqueue machine M1 {
	start state Init {
    on unblock do {
      goto dequeueEvents;
    }
    defer A;
    defer C;
  }
  state dequeueEvents {
    on A do {
			goto receivedA;
    }
    on C do {
      assert false, "Event C was received before A";
    }
	}
  state receivedA {
    on C do {
      assert true;
    }
  }
}

machine M2 {
	start state Init {
		on B do (payload: M1) {
      send payload, C, 1;
      send payload, unblock;
    }
	}
}

