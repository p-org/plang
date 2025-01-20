event A: Main;
event B: M1;
event C: int;
event unblock;
event iter;

machine Main {
	var m1: M1;
	var m2: M2;
  var i: int;
	start state Init {
		entry {
      m1 = new M1();
      m2 = new M2();
			send m1, A, this;
      send m2, B, m1;
      i = 0;
		}
    // Repeat until M1 receives event C before A
    on iter do {
      send m1, A, this;
      send m2, B, m1;
    }
	}
}

eventchannel machine M1 {
  var m: Main;
	start state Init {
    on unblock do {
      goto dequeueEvents;
    }
    defer A;
    defer C;
  }
  state dequeueEvents {
    on A do (payload: Main){
      m = payload;
			goto receivedA;
    }
    on C do {
      assert false, "Event C was received before A";
    }
	}
  state receivedA {
    on C do {
      send m, iter;
      goto Init;
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
