lexer grammar PLexer;

// Type names

ANY       : 'any' ;
BOOL      : 'bool' ;
ENUM      : 'enum' ;
EVENT     : 'event' ;
EVENTSET  : 'eventset' ;
FLOAT     : 'float' ;
INT       : 'int' ;
MACHINE   : 'machine' ;
INTERFACE : 'interface' ;
MAP       : 'map' ;
SET       : 'set' ;
STRING    : 'string' ;
SEQ       : 'seq' ;
DATA      : 'data' ;

// Keywords

ANNOUNCE  : 'announce' ;
AS        : 'as' ;
ASSERT    : 'assert' ;
ASSUME    : 'assume' ;
BREAK     : 'break' ;
CASE      : 'case' ;
COLD      : 'cold' ;
CONTINUE  : 'continue' ;
DEFAULT   : 'default' ;
DEFER     : 'defer' ;
DO        : 'do' ;
ELSE      : 'else' ;
ENTRY     : 'entry' ;
EXIT      : 'exit' ;
FOREACH   : 'foreach';
FORMAT	  : 'format' ;
FUN       : 'fun' ;
GOTO      : 'goto' ;
HALT      : 'halt' ;
HOT       : 'hot' ;
IF        : 'if' ;
IGNORE    : 'ignore' ;
IN        : 'in' ;
KEYS      : 'keys' ;
NEW       : 'new' ;
OBSERVES  : 'observes' ;
ON        : 'on' ;
PRINT     : 'print' ;
RAISE     : 'raise' ;
RECEIVE   : 'receive' ;
RETURN    : 'return' ;
SEND      : 'send' ;
SIZEOF    : 'sizeof' ;
SPEC      : 'spec' ;
START     : 'start' ;
STATE     : 'state' ;
THIS      : 'this' ;
TYPE      : 'type' ;
DELAY     : 'delay' ;
VALUES    : 'values' ;
VAR       : 'var' ;
WHILE     : 'while' ;
WITH      : 'with' ;
CHOOSE    : 'choose' ;

// module-system-specific keywords

// module-test-implementation declarations
MODULE         : 'module' ;
IMPLEMENTATION : 'implementation' ;
TEST           : 'test' ;
REFINES        : 'refines' ;

// module constructors
COMPOSE        : 'compose' ;
UNION          : 'union'    ;
HIDEE          : 'hidee' ;
HIDEI          : 'hidei' ;
RENAME         : 'rename' ;
SAFE           : 'safe' ;
MAIN		   : 'main' ;

// machine annotations
RECEIVES  : 'receives' ;
SENDS     : 'sends' ;

// Common keywords
CREATES : 'creates' ;
TO      : 'to' ;

// Literals

BoolLiteral : 'true' | 'false' ;

IntLiteral : [0-9]+ ;

NullLiteral : 'null';

StringLiteral : '"' StringCharacters? '"' ;
fragment StringCharacters : StringCharacter+ ;
fragment StringCharacter : ~["\\] | EscapeSequence ;
fragment EscapeSequence : '\\' . ;

// Symbols

FAIRNONDET : '$$' ;
NONDET     : '$'  ;

LNOT   : '!' ;
LAND   : '&&' ;
LOR    : '||' ;

EQ     : '==' ;
NE     : '!=' ;
LE     : '<=' ;
GE     : '>=' ;
LT     : '<'  ;
GT     : '>'  ;
RARROW : '->' ;

ASSIGN : '=' ;
INSERT : '+=' ;
REMOVE : '-=' ;

ADD    : '+' ;
SUB    : '-' ;
MUL    : '*' ;
DIV    : '/' ;
MOD    : '%' ;

LBRACE : '{' ;
RBRACE : '}' ;
LBRACK : '[' ;
RBRACK : ']' ;
LPAREN : '(' ;
RPAREN : ')' ;
SEMI   : ';' ;
COMMA  : ',' ;
DOT    : '.' ;
COLON  : ':' ;

// Identifiers

Iden : PLetter PLetterOrDigit* ;
fragment PLetter : [a-zA-Z_] ;
fragment PLetterOrDigit : [a-zA-Z0-9_] ;

// Non-code regions

Whitespace : [ \t\r\n\f]+ -> skip ;
BlockComment : '/*' .*? '*/' -> channel(HIDDEN) ;
LineComment : '//' ~[\r\n]* -> channel(HIDDEN) ;
