parser grammar PParser;
options { tokenVocab=PLexer; }

// A small overview of ANTLRs parser rules:
//
// Parser rules begin with a lower case letter, lexer rules begin
// with an Uppercase letter. To create a parser rule, write the name
// followed by a colon (:) and then a list of alternatives, separated
// by pipe (|) characters. You can use parenthesis for sub-expressions,
// alternatives within those sub-expressions, and kleene * or + on any
// element in a rule.
//
// Every production rule corresponds to a class that gets generated
// in the target language for the ANTLR generator. If we use alternative
// labels, as in `type`, then subclasses of the rule-class will be created
// for each label. If one alternative is labelled, then they all must be.
// The purpose of labels is to call different functions in the generated
// listeners and visitors for the results of these productions.
//
// Lastly, ANTLR's DSL contains a feature that allows us to name the matched
// tokens and productions in an alternative (name=part) or collect multiple
// tokens or productions of the same type into a list (list+=part). The `type`
// production below uses this feature, too.

program : (topDecl)* EOF ;

iden : Iden ;
int  : IntLiteral ;

type : SEQ LBRACK type RBRACK     # SeqType
     | SET LBRACK type RBRACK     # SetType
     | MAP LBRACK keyType=type COMMA valueType=type RBRACK  # MapType
     | LPAREN tupTypes+=type (COMMA tupTypes+=type)* RPAREN # TupleType
     | LPAREN idenTypeList RPAREN # NamedTupleType
     | BOOL      # PrimitiveType
     | INT       # PrimitiveType
     | FLOAT     # PrimitiveType
     | STRING    # PrimitiveType
     | EVENT     # PrimitiveType
     | MACHINE   # PrimitiveType
     | DATA      # PrimitiveType
     | ANY       # PrimitiveType
     | name=iden # NamedType
     ;

idenTypeList : idenType (COMMA idenType)* ;
idenType : name=iden COLON type ;

funParamList : funParam (COMMA funParam)* ;
funParam : name=iden COLON type ;

topDecl : typeDefDecl
        | enumTypeDefDecl
        | eventDecl
        | eventSetDecl
        | interfaceDecl
        | implMachineDecl
        | specMachineDecl
        | funDecl
        | namedModuleDecl
        | testDecl
        | implementationDecl
        ;


typeDefDecl : TYPE name=iden SEMI # ForeignTypeDef
            | TYPE name=iden ASSIGN type SEMI # PTypeDef
            ;

enumTypeDefDecl : ENUM name=iden LBRACE enumElemList RBRACE
                | ENUM name=iden LBRACE numberedEnumElemList RBRACE
                ;
enumElemList : enumElem (COMMA enumElem)* ;
enumElem : name=iden ;
numberedEnumElemList : numberedEnumElem (COMMA numberedEnumElem)* ;
numberedEnumElem : name=iden ASSIGN value=IntLiteral ;

eventDecl : EVENT name=iden cardinality? (COLON type)? SEMI;
cardinality : ASSERT IntLiteral
            | ASSUME IntLiteral
            ;

eventSetDecl : EVENTSET name=iden ASSIGN LBRACE eventSetLiteral RBRACE SEMI ;
eventSetLiteral : events+=nonDefaultEvent (COMMA events+=nonDefaultEvent)* ;

interfaceDecl : INTERFACE name=iden LPAREN type? RPAREN (RECEIVES nonDefaultEventList?) SEMI ;

// has scope
dequeueOption : EVENTBAG
                | EVENTCHANNEL
                | EVENTQUEUE
                ;             
implMachineDecl : dequeueOption? MACHINE name=iden cardinality? receivesSends* machineBody ;
idenList : names+=iden (COMMA names+=iden)* ;

receivesSends : RECEIVES eventSetLiteral? SEMI # MachineReceive
              | SENDS eventSetLiteral? SEMI    # MachineSend
              ;

specMachineDecl : SPEC name=iden OBSERVES eventSetLiteral machineBody ;

machineBody : LBRACE machineEntry* RBRACE;
machineEntry : varDecl
             | funDecl
             | stateDecl
             ;

varDecl : VAR idenList COLON type SEMI ;

funDecl : FUN name=iden LPAREN funParamList? RPAREN (COLON type)? (CREATES interfaces+=iden)? SEMI # ForeignFunDecl
        | FUN name=iden LPAREN funParamList? RPAREN (COLON type)? functionBody # PFunDecl
        ;

stateDecl : START? temperature=(HOT | COLD)? STATE name=iden LBRACE stateBodyItem* RBRACE ;

stateBodyItem : ENTRY anonEventHandler       # StateEntry
              | ENTRY funName=iden SEMI      # StateEntry
              | EXIT noParamAnonEventHandler # StateExit
              | EXIT funName=iden SEMI       # StateExit
              | DEFER nonDefaultEventList SEMI    # StateDefer
              | IGNORE nonDefaultEventList SEMI   # StateIgnore
              | ON eventList DO funName=iden SEMI # OnEventDoAction
              | ON eventList DO anonEventHandler  # OnEventDoAction
              | ON eventList GOTO stateName SEMI  # OnEventGotoState
              | ON eventList GOTO stateName WITH anonEventHandler  # OnEventGotoState
              | ON eventList GOTO stateName WITH funName=iden SEMI # OnEventGotoState
              ;

nonDefaultEventList : events+=nonDefaultEvent (COMMA events+=nonDefaultEvent)* ;
nonDefaultEvent : HALT | iden ;

eventList : eventId (COMMA eventId)* ;
eventId : NullLiteral | HALT | iden ;

stateName : state=iden ;

functionBody : LBRACE varDecl* statement* RBRACE ;
statement : LBRACE statement* RBRACE							# CompoundStmt
          | ASSERT assertion=expr (COMMA message=expr)? SEMI	# AssertStmt
          | PRINT message=expr SEMI								# PrintStmt
          | RETURN expr? SEMI									# ReturnStmt
          | BREAK SEMI											# BreakStmt
          | CONTINUE SEMI										# ContinueStmt
          | lvalue ASSIGN rvalue SEMI							# AssignStmt
          | lvalue INSERT LPAREN expr COMMA rvalue RPAREN SEMI	# InsertStmt
		  | lvalue INSERT LPAREN rvalue RPAREN SEMI				# AddStmt
          | lvalue REMOVE expr SEMI								# RemoveStmt
          | WHILE LPAREN expr RPAREN statement					# WhileStmt
          | FOREACH LPAREN item=iden IN collection=expr
                                        RPAREN statement		# ForeachStmt
          | IF LPAREN expr RPAREN thenBranch=statement
                            (ELSE elseBranch=statement)?		# IfStmt
          | NEW iden LPAREN rvalueList? RPAREN SEMI				# CtorStmt
          | fun=iden LPAREN rvalueList? RPAREN SEMI				# FunCallStmt
          | RAISE expr (COMMA rvalueList)? SEMI					# RaiseStmt
          | SEND machine=expr COMMA event=expr
                              (COMMA rvalueList)? SEMI			# SendStmt
          | ANNOUNCE expr (COMMA rvalueList)? SEMI				# AnnounceStmt
          | GOTO stateName (COMMA rvalueList)? SEMI				# GotoStmt
          | RECEIVE LBRACE recvCase+ RBRACE						# ReceiveStmt
          | SEMI												# NoStmt
          ;

lvalue : name=iden                 # VarLvalue
       | lvalue DOT field=iden     # NamedTupleLvalue
       | lvalue DOT int            # TupleLvalue
       | lvalue LBRACK expr RBRACK # MapOrSeqLvalue
       ;

recvCase : CASE eventList COLON anonEventHandler ;
anonEventHandler : (LPAREN funParam RPAREN)? functionBody ;
noParamAnonEventHandler : functionBody;

expr : primitive                                      # PrimitiveExpr
     | LPAREN unnamedTupleBody RPAREN                 # UnnamedTupleExpr
     | LPAREN namedTupleBody RPAREN                   # NamedTupleExpr
     | LPAREN expr RPAREN                             # ParenExpr
     | expr DOT field=iden                            # NamedTupleAccessExpr
     | expr DOT field=int                             # TupleAccessExpr
     | seq=expr LBRACK index=expr RBRACK              # SeqAccessExpr
     | fun=KEYS LPAREN expr RPAREN                    # KeywordExpr
     | fun=VALUES LPAREN expr RPAREN                  # KeywordExpr
     | fun=SIZEOF LPAREN expr RPAREN                  # KeywordExpr
     | fun=DEFAULT LPAREN type RPAREN                 # KeywordExpr
     | NEW interfaceName=iden
                            LPAREN rvalueList? RPAREN # CtorExpr
     | fun=iden LPAREN rvalueList? RPAREN             # FunCallExpr
     | op=(SUB | LNOT) expr                           # UnaryExpr
     | lhs=expr op=(MUL | DIV | MOD) rhs=expr         # BinExpr
     | lhs=expr op=(ADD | SUB) rhs=expr               # BinExpr
     | expr cast=(AS | TO) type                       # CastExpr
     | lhs=expr op=(LT | GT | GE | LE | IN) rhs=expr  # BinExpr
     | lhs=expr op=(EQ | NE) rhs=expr                 # BinExpr
     | lhs=expr op=LAND rhs=expr                      # BinExpr
     | lhs=expr op=LOR rhs=expr                       # BinExpr
	 | CHOOSE LPAREN expr? RPAREN					  # ChooseExpr
	 | formatedString								  # StringExpr
     ;

formatedString	:	StringLiteral
				|	FORMAT LPAREN StringLiteral (COMMA rvalueList)? RPAREN
				;	

primitive : iden
          | floatLiteral
          | BoolLiteral
          | IntLiteral
          | NullLiteral
          | NONDET
          | FAIRNONDET
          | HALT
          | THIS
          ;

floatLiteral : pre=IntLiteral? DOT post=IntLiteral # DecimalFloat
             | FLOAT LPAREN base=IntLiteral COMMA exp=IntLiteral RPAREN # ExpFloat
             ;

unnamedTupleBody : fields+=rvalue COMMA
                 | fields+=rvalue (COMMA fields+=rvalue)+
                 ;

namedTupleBody : names+=iden ASSIGN values+=rvalue COMMA
               | names+=iden ASSIGN values+=rvalue (COMMA names+=iden ASSIGN values+=rvalue)+
               ;

rvalueList : rvalue (COMMA rvalue)* ;
rvalue : expr ;

// module system related

modExpr : LPAREN modExpr RPAREN												  # ParenModuleExpr
		| LBRACE bindslist+=bindExpr (COMMA bindslist+=bindExpr)* RBRACE      # PrimitiveModuleExpr
        | iden                                                                # NamedModule
        | op=COMPOSE mexprs+=modExpr (COMMA mexprs+=modExpr)+				  # ComposeModuleExpr
        | op=UNION   mexprs+=modExpr (COMMA  mexprs+=modExpr)+				  # UnionModuleExpr
        | op=HIDEE  nonDefaultEventList IN modExpr							# HideEventsModuleExpr
        | op=HIDEI idenList IN modExpr										# HideInterfacesModuleExpr
        | op=ASSERT  idenList IN modExpr									# AssertModuleExpr
        | op=RENAME  oldName=iden TO newName=iden IN modExpr				# RenameModuleExpr
		| op=MAIN mainMachine=iden IN modExpr								# MainMachineModuleExpr
        ;


bindExpr : (mName=iden | mName=iden RARROW iName=iden) ;

namedModuleDecl : MODULE name=iden ASSIGN modExpr SEMI ;

testDecl : TEST testName=iden (LBRACK MAIN ASSIGN mainMachine=iden RBRACK) COLON modExpr SEMI                  # SafetyTestDecl
         | TEST testName=iden (LBRACK MAIN ASSIGN mainMachine=iden RBRACK) COLON modExpr REFINES modExpr SEMI  # RefinementTestDecl
         ;

implementationDecl : IMPLEMENTATION implName= iden (LBRACK MAIN ASSIGN mainMachine=iden RBRACK)? COLON modExpr SEMI ;
