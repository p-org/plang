A Function in P can be arbitrary piece of imperative code which enables programmers to capture complex protocol logic in their state machines.
P supports the common imperative programming language expressions (just like in [Java](https://docs.oracle.com/javase/tutorial/java/nutsandbolts/expressions.html)).

??? note "P Expressions Grammar"

    ```
        expr :
        | (expr)                        # ParenExpr
        | primitiveExpr                 # PrimitiveExpr
        | formatedString                # FormatStringExpr
        | (tupleBody)                   # TupleExpr
        | (namedTupleBody)              # NamedTupleExpr
        | expr.int                      # TupleAccessExpr
        | expr.iden                     # NamedTupleAccessExpr
        | expr[expr]                    # AccessExpr
        | keys(expr)                    # KeysExpr
        | values(expr)                  # ValuesExpr
        | sizeof(expr)                  # SizeofExpr
        | expr in expr                  # ContainsExpr
        | default(type)                 # DefaultExpr
        | new iden ( rvalueList? )      # NewExpr
        | iden ( rvalueList? )          # FunCallExpr
        | (- | !) expr                  # UnaryExpr
        | expr (* | / | + | -) expr     # ArithBinExpr
        | expr (== | !=) expr           # EqualityBinExpr
        | expr (&& | ||) expr           # LogicalBinExpr
        | expr (< | > | >= | <= ) expr  # CompBinExpr
        | expr as type                  # CastExpr
        | expr to type                  # CoerceExpr
        | choose ( expr? )              # ChooseExpr
        ;

        # Formated strings for creating strings
        formatedString
        | format ( StringLiteral (, rvalueList)? )
        ;

        primitiveExpr
        : iden                          # Identifier
        | FloatLiteral                  # FloatConstant
        | BoolLiteral                   # BooleanConstant
        | IntLiteral                    # IntConstant
        | NullLiteral                   # Null
        | StringLiteral                 # StringConstant
        | $                             # BooleanNonDeterministicChoice
        | halt                          # HaltEvent
        | this                          # SelfMachineReference
        ;

        # Body of a tuple
        tupleBody :
        | rvalue ,
        | rvalue (, rvalue)+
        ;

        # Body of a named tuple
        namedTupleBody:
        | iden = rvalue ,
        | iden = rvalue (, iden = rvalue)+
        ;

        # r-value is an expression that can’t have a value assigned to it which
        # means r-value can appear on right but not on left hand side of an assignment operator(=)
        rvalue : expr ;
        # rvalueList is a comma separated list of rvalue.
    ```

### Primitives

P allows the common primitive expressions like literal-constants for integers, floats, strings, booleans, and the null value.
There are three unique primitive expressions in P:

#### $

`$` represents a nondeterministic boolean choice. It is a short hand for `choose()` which randomly returns a boolean value and the P Checker explores the behavior of the system for both the possibilities i.e., both when `$` evaluates to `true` or `false`.

#### Halt

`halt` is a special event in P used for destroying an instance of a P machine. The semantics of an `halt` event is that whenever a P machine throws an `unhandled event` exception because of a `halt` event then the machine is automatically destroyed or halted and all events sent to that machine instance there after are equivalent to being dropped to ether. There are two ways of using the `halt` event: (1) **self-halt** by doing `raise halt;` raising a `halt` event which is not handled in the state machine will lead to that machine being halted or (2) by sending the `halt` event to a machine, and that machine on dequeueing `halt`, would halt itself. Please checkout the [FailureDetector](../tutorial/failuredetector.md) example in tutorials to know more about halt event

#### This

`this` represents the `self` machine reference of the current machine. It can be used to send self reference to other machines in the program so that they can send messages to this machine.

### Formatted String

P allows creating formatted strings.

**Syntax:**: `format ( formatString (, rvalueList)? )`

`formatString` is the format string and `rvalueList` is a comma separated list of arguments for the formatted string.

=== "Formatted String"

    ``` java
    var hw, h, w: string;
    var tup: (string, int);

    h = "Hello"; w = "World";
    tup = ("tup value", 100);
    hw = format("{0} {1}, and {2} is {3}!",
            h, w, tup.0, tup.1);
    // hw value is "Hello World, and tup value is 100!"
    ```

=== "Print Formatted String"
    Formatted strings are most useful for printing logs. Checkout [`print` statement](statements.md#print).

    ``` java hl_lines="6 7"
    var hw, h, w: string;
    var tup: (string, int);

    h = "Hello"; w = "World";
    tup = ("tup value", 100);
    print format("{0} {1}, and {2} is {3}!",
            h, w, tup.0, tup.1);
    // prints "Hello World, and tup value is 100!"
    ```

### Tuple and Named Tuple Values

A tuple or named tuple value can be created using the following expressions:

**Syntax (tuple value):**: `(rvalue ,)` for a single field tuple value or `(rvalue (, rvalue)+)` for tuple with multiple fields.

``` java
// tuple value of type (int,)
(10,)
// tuple value of type (string, (string, string))
("Hello", ("World", "!"))
// assume x: int and y: string
// tuple value of type (int, string)
(x, y)
```

**Syntax (named tuple value):**: `(iden = rvalue ,)` for a single field named tuple value or `(iden = rvalue (, iden = rvalue)+)` for named tuple with multiple fields.

``` java
// named tuple value of type (reqId: int,)
(reqId = 10,)
// named tuple value of type (h: string, (w: string, a: string))
(h = "Hello", (w = "World", a = "!"))
// assume x: int and y: string
// named tuple value of type (a:int, b:string)
(a = x, b = y)
```

### Access Field of Tuple and Named Tuple

The fields of a tuple can be
accessed by using the `.` operation followed by the field index.

**Syntax (tuple value):** `expr.int` where `expr` is the tuple value and `int` is the field index.

``` java
// tuple with three fields
var tupleEx: (int, bool, int);
tupleEx = (20, false, 21);

// accessing the first and third element of the tupleEx
tupleEx.0 = tupleEx.0 + tupleEx.2;
```

Named tuples are similar to tuples with each field having an associated name. The fields
of a named tuple can be accessed by using the `.` operation followed by the field name.

**Syntax (named tuple value)**: `expr.iden` where `expr` is the named tuple value and `iden` is the field name.

``` java
// named tuple with three fields
var namedTupleEx: (x1: int, x2: bool, x3: int);
namedTupleEx = (x1 = 20, x2 = false, x3 = 21);

// accessing the first and third element of the namedTupleEx
namedTupleEx.x1 = namedTupleEx.x1 + namedTupleEx.x3;
```

### Indexing into a Collection

P supports three collection types: `map`, `seq`, and `set`. We can index into these collection types to access its elements.

**Syntax:** `expr_c[expr_i]`

If `expr_c` is a value of sequence type then `expr_i` must be an integer expression and `expr_c[expr_i]` represents the element at index `expr_i`. Similarly, If `expr_c` is a value of set type then `expr_i` must be an integer expression and `expr_c[expr_i]` represents the element at index `expr_i` but note that for a set there is no guarantee for the order in which elements are stored in the set.
Finally, if `expr_c` is a value of map type then `expr_i` represents the key to look up and `expr_c[expr_i]` represents the value for the key `expr_i`.

### Operations on Collections

P supports four other operations on collection types:

#### sizeof

**Syntax:**: `sizeof(expr)`, where `expr` is a value of type `set`, `seq` or `map`, returns an integer value representing the size or length of the collection.

``` java
var sq: seq[int];
while (i < sizeof(sq)) {
    ...
    i = i + 1;
}
```

#### keys and values

Programmers can use `keys` and `values` functions to get access to a sequence of all the keys or values in map respectively.

**Syntax:**: `keys(expr)` or `values(expr)`

`expr` must be a map value, if `expr : map[K, V]` then `keys(expr)` returns a sequence of all keys in the map (of type `seq[K]`) and similarly, `values(expr)` returns a sequence of all values in the map (of type `seq[V]`).

Primarily, `keys` and `values` are used to get contents of the map and then operate over it.

#### contains (or `in`)

To check if an element (or key in the case of a map) belongs to a collection, P provides the `in` operation.

**Syntax:** `expr_e in expr_c`

`expr_e` is the element (or key in the case of map) and `expr_c` is the collection value. The `in` expression evaluates to `true` if the element belongs to the collection and `false` otherwise.

``` java
var sq: seq[tRequest];
var mp: map[int, tRequest];
var rr: tRequest; var i: int;
...
if(rr in sq && rr in values(mp) && i in mp) { ... }
```

### Default value for a type

The `default` primitive in P can be used to obtain the default value for any type.

**Syntax:** `default(type)`

`type` is any P type and `default(type)` represents the default value for the `type`. The default values for all P types is provided [here](datatypes.md#default-values-for-p-data-types).

### New

New expression is used to create an instance of a machine, `new` returns a machine reference to the newly created instance of the machine.

**Syntax**: `new iden (rvalue?) ;`

`iden` is the name of the machine to be created and `rvalue` is the optional constructor parameter that becomes the input parameter of the entry function of the start state of the machine.

=== "Create a machine"

    ``` java
    new Client((id = 1, server = this));
    ```
    Creates a dynamic instance of a Client machine and passes the constructor parameter `(id = 1, server = this)`
    which is delivered as a payload to the entry function of the start state of the created Client machine.

### Function Call

Function calls in P are similar to any other imperative programming languages.

!!! Note ""
    Note that the parameters passed to the functions and the return values are pass-by-value!

**Syntax**: `iden ( rvalueList? ) ;`

`iden` is the name of the function and `rvalueList` is the comma separated list of function arguments.

=== "Function call"

    ``` java
    x = Foo();
    y = Bar(10, "haha");
    ```

### Negation and Not

P supports two unary operations: `-` on integers and floats values (i.e., negation) and `!` on boolean values (i.e., logical not).

### Arithmetic

P supports the following arithmetic binary operations on integers or floats: `+` (i.e., addition), `-` (i.e., subtraction), `*` (i.e., multiplication), `%` (i.e., modulo), and `/` (i.e., division).

### Comparison

P supports the following comparison binary operations on integers or floats: `<` (i.e., less-than), `<=` (i.e., less-than-equal), `>` (i.e., greater-than), and `>=` (i.e., greater-than-equal).

### Cast

P supports two super types `any` and `data` ([more details](datatypes.md#universal-supertypes)). To cast values from these supertypes to the actual types, P supports the `as` cast expression.

**Syntax:** `expr as T`

`expr` expression is cast to type `T` and if the cast is not valid then it leads to dynamic type-cast error.

``` java
type tRecord = (key: int, val: any);
...
var st: set[tRecord];
var x: any;
var x_i: string;
var st_i: set[(key: int, val: string)];

x_i = x as string;
st += ((key = 1, val = "hello"));
st_i = st as set[(key: int, val: string)];
...
```

### Coerce

P supports coercing of any value of type `float` to `int` and also any `enum` element to `int`.

**Syntax:** `expr to T`

`expr` expression is coerced to type `T`. We currently support only coercing of type `float` to `int` and also any `enum` element to `int`.

``` java
enum Status {
    ERROR = 101,
    SUCCESS = 102
}
...
var x_f : float;
var x_i: int;

x_f = 101.0;
x_i = x_f to int;
assert x_i == ERROR to int;
```

### Choose

P provides the `choose` primitive to model data nondeterminism in P programs. The P checker then explores the behavior of the program for all possible values that can be returned by the `choose` operation.

**Syntax:** `choose()` or `choose(expr)`

`expr` can either be a `int` value or a collection. For `choose(x)`, when `x` is an integer, `choose(x)` returns a random value between `0 to x` (excluding x), when `x` is a collection then `choose(x)` returns a random element from the collection.

``` java
choose() // returns true or false, is equivalent to $
choose(10) // returns an integer x, 0 <= x < 10
choose(x) // if x is set or seq then returns a value from that collection
```

The choose operation can be used to model nondeterministic environment machines that generate random inputs for the system. Another use case could be to model nondeterministic behavior within the system itself where the system can randomly choose to `timeout` or `fail` or `drop messages`.

!!! note ""
    Performing a `choose` over an empty collection leads to an error. Also, `choose` from a `map` value returns a random key from the map.

Note that the maximum number of choices allowed in a `choose(expr)` is 10,000. Performing a `choose` with an `int` value greater than 10000 or over a collection with more than 10000 elements leads to an error.

``` java
choose(10001) // throws a compile-time error
choose(x)     // throws a runtime error if x is of integer type and has value greater than 10000
choose(x)     // throws a runtime error if x is of seq/set/map type and has size greater than 10000 elements
```

