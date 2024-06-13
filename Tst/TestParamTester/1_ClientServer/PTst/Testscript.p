/* This file contains three different model checking scenarios */

// assert the properties for the single client and single server scenario
test tcSingleClient [main=TestWithSingleClient]:
  assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
  (union Client, Bank, { TestWithSingleClient });

// assert the properties for the two clients and single server scenario
test tcMultipleClients [main=TestWithMultipleClients]:
  assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
  (union Client, Bank, { TestWithMultipleClients });

// assert the properties for the single client and single server scenario but with abstract server
 test tcAbstractServer [main=TestWithSingleClient]:
  assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
  (union Client, AbstractBank, { TestWithSingleClient });

paramtest (globalnumClients in [2, 3, 4], global1 in [1,2], global2 in [4, 5]) aaaa1 [main=TestWithConfig]:
  assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
  (union Client, Bank, { TestWithConfig });

paramtest (globalnumClients in [1]) aaa2 [main=TestWithConfig]:
  assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
  (union Client, Bank, { TestWithConfig });

// Syntax error
// paramtest () wrong1 [main=TestWithSingleClient]:
//   assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
//   (union Client, Bank, { TestWithSingleClient });

// Syntax error
// paramtest (globalnumClients in []) wrong2 [main=TestWithSingleClient]:
//   assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
//   (union Client, Bank, { TestWithSingleClient });

// Duplicate Assign
// paramtest (globalnumClients in [1], globalnumClients in [1]) wrong2 [main=TestWithSingleClient]:
//   assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
//   (union Client, Bank, { TestWithSingleClient });

// Undelared global variable
// paramtest (x in [1], globalnumClients in [1]) wrong2 [main=TestWithSingleClient]:
//   assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
//   (union Client, Bank, { TestWithSingleClient });

// Type mismatch
// constant dummyGv : bool; 
// paramtest (globalnumClients in [1], dummyGv in [2, 3]) wrong2 [main=TestWithSingleClient]:
//   assert BankBalanceIsAlwaysCorrect, GuaranteedWithDrawProgress in
//   (union Client, Bank, { TestWithSingleClient });