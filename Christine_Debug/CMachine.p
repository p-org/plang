event E: int;
event F;
event G: int;

// machine CMachine {
//   var n: machine;
//   var n1: machine;
//   start state CMachineInit {
//     entry {
//       var m: machine;
//       var sq: seq[machine];
//       n = new M2();
//       n1 = new M2();
//       sq += (0,n);
//       sq += (1,n1);
//       m = new M(sq);
//       print format("Reached statemachine init");
//       // foo(m, 0);
//     }
//   }
//   fun foo(b: machine, p: int) {
//     receive {
//       case E: (payload: int) { print format("foo received E"); }
//     }
//   }
// }

// machine M {
//   var nodes: seq[machine];
//   start state Init {
//       entry  (payload : seq[machine]) { 
//         nodes = payload;
//         send nodes[0], E, 100; 
//       }
//   }
// }

// machine M2 {
//   start state Init {
//     entry {
//       print format("Machine M2 created");
//     }
//     on E do (payload: int) {
//       print format("M2 received E");
//       goto S;
//     }
//   }
//   state S {
//     entry {
//       print format("instate S");
//       assert false, "reached S";
//     }
//   }
// }

machine CMachine {
  var x: int;
	start state Init {
		entry {
			var b: machine;
		    b = new B(this);
			x = x + 1;
			assert x == 1;
			foo(b, 0);
			assert x == 2;
		}
	}
	fun foo(b: machine, p: int) {
		send b, E, 0;
		send b, G, 1;
		receive {
			case E: (payload: int) { x = x + p + 1; }
			case F: { x = x + p + 2; }
			case G: (payload: int) { x = x + p + payload; }
		}
	}
}

machine B {
	start state Init {
		entry (payload1: machine) {
			var y: machine;
			var z: int;
			z = z + 1;
			y = payload1;
			receive {
				case E: (payload2: int) {
					assert payload2 == 0;
					receive {
						case G: (payload3: int) {
							var x: int;
							var a, b: int;
							var c: event;	
							x = payload3;
							send y, F;

							a = 10;
							b = 11;
							assert b == a + z;
						}
					}
					assert payload2 == 0;
				}
			}
			assert y == payload1;
		}
	}
}

spec CMonitor observes F, G {
  start state Init {
    on G do {
      assert true;
    }
  }
}


module CMachine = { CMachine };

test tcCMachine [main=CMachine]:
  assert CMonitor in {CMachine, B};