package testcases.clientserver;


/***************************************************************************
 * This file was auto-generated on Monday, 08 August 2022 at 17:03:57.
 * Please do not edit manually!
 **************************************************************************/

import java.util.*;

public class PMachines {
    // PMachine BankServer elided
    // PMachine Database elided
    // PMachine Client elided
    // PMachine AbstractBankServer elided
    public static class BankBalanceIsAlwaysCorrect extends prt.Monitor<BankBalanceIsAlwaysCorrect.PrtStates> {

        public static class Supplier implements java.util.function.Supplier<BankBalanceIsAlwaysCorrect> {
            public BankBalanceIsAlwaysCorrect get() {
                BankBalanceIsAlwaysCorrect ret = new BankBalanceIsAlwaysCorrect();
                ret.ready();
                return ret;
            }
        }

        private HashMap<Long, Long> bankBalance = new HashMap<Long, Long>();
        public HashMap<Long, Long> get_bankBalance() { return this.bankBalance; };

        private HashMap<Long, PTypes.PTuple_src_accnt_amnt_rId> pendingWithDraws = new HashMap<Long, PTypes.PTuple_src_accnt_amnt_rId>();
        public HashMap<Long, PTypes.PTuple_src_accnt_amnt_rId> get_pendingWithDraws() { return this.pendingWithDraws; };


        public enum PrtStates {
            Init,
            WaitForWithDrawReqAndResp
        }

        public BankBalanceIsAlwaysCorrect() {
            super();
            addState(prt.State.keyedOn(PrtStates.Init)
                    .isInitialState(true)
                    .withEvent(PEvents.eSpec_BankBalanceIsAlwaysCorrect_Init.class, p -> { Anon(p); gotoState(PrtStates.WaitForWithDrawReqAndResp); })
                    .build());
            addState(prt.State.keyedOn(PrtStates.WaitForWithDrawReqAndResp)
                    .withEvent(PEvents.eWithDrawReq.class, this::Anon_1)
                    .withEvent(PEvents.eWithDrawResp.class, this::Anon_2)
                    .build());
        } // constructor

        public void reInitializeMonitor() {
            registerState(prt.State.keyedOn(PrtStates.Init)
                    .isInitialState(true)
                    .withEvent(PEvents.eSpec_BankBalanceIsAlwaysCorrect_Init.class, p -> { Anon(p); gotoState(PrtStates.WaitForWithDrawReqAndResp); })
                    .build());
            registerState(prt.State.keyedOn(PrtStates.WaitForWithDrawReqAndResp)
                    .withEvent(PEvents.eWithDrawReq.class, this::Anon_1)
                    .withEvent(PEvents.eWithDrawResp.class, this::Anon_2)
                    .build());
        }

        public java.util.List<Class<? extends prt.events.PEvent<?>>> getEventTypes() {
            return java.util.Arrays.asList(PEvents.eSpec_BankBalanceIsAlwaysCorrect_Init.class, PEvents.eWithDrawReq.class, PEvents.eWithDrawResp.class);
        }

        private void Anon(HashMap<Long, Long> balance) {
            bankBalance = prt.values.Clone.deepClone(balance);
        }
        private void Anon_1(PTypes.PTuple_src_accnt_amnt_rId req) {
            long TMP_tmp0;
            boolean TMP_tmp1;
            long TMP_tmp2;
            ArrayList<Long> TMP_tmp3;
            String TMP_tmp4;
            long TMP_tmp5;

            TMP_tmp0 = req.accountId;
            TMP_tmp1 = bankBalance.containsKey(TMP_tmp0);
            TMP_tmp2 = req.accountId;
            TMP_tmp3 = new ArrayList<Long>(bankBalance.keySet());
            TMP_tmp4 = java.text.MessageFormat.format("Unknown accountId {0} in the withdraw request. Valid accountIds = {1}", TMP_tmp2, TMP_tmp3);
            tryAssert(TMP_tmp1, TMP_tmp4);
            TMP_tmp5 = req.rId;
            pendingWithDraws.put(TMP_tmp5,req.deepClone());
        }
        private void Anon_2(PTypes.PTuple_stts_accnt_blnc_rId resp) {
            long TMP_tmp0_1;
            boolean TMP_tmp1_1;
            long TMP_tmp2_1;
            String TMP_tmp3_1;
            long TMP_tmp4_1;
            boolean TMP_tmp5_1;
            long TMP_tmp6;
            String TMP_tmp7;
            long TMP_tmp8;
            boolean TMP_tmp9;
            String TMP_tmp10;
            PTypes.tWithDrawRespStatus TMP_tmp11;
            boolean TMP_tmp12;
            long TMP_tmp13;
            long TMP_tmp14;
            long TMP_tmp15;
            long TMP_tmp16;
            PTypes.PTuple_src_accnt_amnt_rId TMP_tmp17;
            long TMP_tmp18;
            long TMP_tmp19;
            boolean TMP_tmp20;
            long TMP_tmp21;
            long TMP_tmp22;
            long TMP_tmp23;
            long TMP_tmp24;
            long TMP_tmp25;
            PTypes.PTuple_src_accnt_amnt_rId TMP_tmp26;
            long TMP_tmp27;
            long TMP_tmp28;
            String TMP_tmp29;
            long TMP_tmp30;
            long TMP_tmp31;
            long TMP_tmp32;
            long TMP_tmp33;
            long TMP_tmp34;
            long TMP_tmp35;
            PTypes.PTuple_src_accnt_amnt_rId TMP_tmp36;
            long TMP_tmp37;
            long TMP_tmp38;
            boolean TMP_tmp39;
            long TMP_tmp40;
            PTypes.PTuple_src_accnt_amnt_rId TMP_tmp41;
            long TMP_tmp42;
            long TMP_tmp43;
            long TMP_tmp44;
            String TMP_tmp45;
            long TMP_tmp46;
            long TMP_tmp47;
            long TMP_tmp48;
            boolean TMP_tmp49;
            long TMP_tmp50;
            long TMP_tmp51;
            long TMP_tmp52;
            String TMP_tmp53;

            TMP_tmp0_1 = resp.accountId;
            TMP_tmp1_1 = bankBalance.containsKey(TMP_tmp0_1);
            TMP_tmp2_1 = resp.accountId;
            TMP_tmp3_1 = java.text.MessageFormat.format("Unknown accountId {0} in the withdraw response!", TMP_tmp2_1);
            tryAssert(TMP_tmp1_1, TMP_tmp3_1);
            TMP_tmp4_1 = resp.rId;
            TMP_tmp5_1 = pendingWithDraws.containsKey(TMP_tmp4_1);
            TMP_tmp6 = resp.rId;
            TMP_tmp7 = java.text.MessageFormat.format("Unknown rId {0} in the withdraw response!", TMP_tmp6);
            tryAssert(TMP_tmp5_1, TMP_tmp7);
            TMP_tmp8 = resp.balance;
            TMP_tmp9 = TMP_tmp8 >= 10L;
            TMP_tmp10 = "Bank balance in all accounts must always be greater than or equal to 10!!";
            tryAssert(TMP_tmp9, TMP_tmp10);
            TMP_tmp11 = resp.status;
            TMP_tmp12 = TMP_tmp11 == PTypes.tWithDrawRespStatus.WITHDRAW_SUCCESS;
            if (TMP_tmp12) {
                TMP_tmp13 = resp.balance;
                TMP_tmp14 = resp.accountId;
                TMP_tmp15 = bankBalance.get(TMP_tmp14);
                TMP_tmp16 = resp.rId;
                TMP_tmp17 = pendingWithDraws.get(TMP_tmp16);
                TMP_tmp18 = TMP_tmp17.amount;
                TMP_tmp19 = TMP_tmp15 - TMP_tmp18;
                TMP_tmp20 = TMP_tmp13 == TMP_tmp19;
                TMP_tmp21 = resp.accountId;
                TMP_tmp22 = resp.balance;
                TMP_tmp23 = resp.accountId;
                TMP_tmp24 = bankBalance.get(TMP_tmp23);
                TMP_tmp25 = resp.rId;
                TMP_tmp26 = pendingWithDraws.get(TMP_tmp25);
                TMP_tmp27 = TMP_tmp26.amount;
                TMP_tmp28 = TMP_tmp24 - TMP_tmp27;
                TMP_tmp29 = java.text.MessageFormat.format("Bank balance for the account {0} is {1} and not the expected value {2}, Bank is lying!", TMP_tmp21, TMP_tmp22, TMP_tmp28);
                tryAssert(TMP_tmp20, TMP_tmp29);
                TMP_tmp30 = resp.accountId;
                TMP_tmp31 = resp.balance;
                TMP_tmp32 = TMP_tmp31;
                bankBalance.put(TMP_tmp30,TMP_tmp32);
            }
            else
            {
                TMP_tmp33 = resp.accountId;
                TMP_tmp34 = bankBalance.get(TMP_tmp33);
                TMP_tmp35 = resp.rId;
                TMP_tmp36 = pendingWithDraws.get(TMP_tmp35);
                TMP_tmp37 = TMP_tmp36.amount;
                TMP_tmp38 = TMP_tmp34 - TMP_tmp37;
                TMP_tmp39 = TMP_tmp38 < 10L;
                TMP_tmp40 = resp.rId;
                TMP_tmp41 = pendingWithDraws.get(TMP_tmp40);
                TMP_tmp42 = TMP_tmp41.amount;
                TMP_tmp43 = resp.accountId;
                TMP_tmp44 = bankBalance.get(TMP_tmp43);
                TMP_tmp45 = java.text.MessageFormat.format("Bank must accept the withdraw request for {0}, bank balance is {1}!", TMP_tmp42, TMP_tmp44);
                tryAssert(TMP_tmp39, TMP_tmp45);
                TMP_tmp46 = resp.accountId;
                TMP_tmp47 = bankBalance.get(TMP_tmp46);
                TMP_tmp48 = resp.balance;
                TMP_tmp49 = TMP_tmp47 == TMP_tmp48;
                TMP_tmp50 = resp.accountId;
                TMP_tmp51 = bankBalance.get(TMP_tmp50);
                TMP_tmp52 = resp.balance;
                TMP_tmp53 = java.text.MessageFormat.format("Withdraw failed BUT the account balance changed! actual: {0}, bank said: {1}", TMP_tmp51, TMP_tmp52);
                tryAssert(TMP_tmp49, TMP_tmp53);
            }
        }

    } // BankBalanceIsAlwaysCorrect monitor definition
    public static class GuaranteedWithDrawProgress extends prt.Monitor<GuaranteedWithDrawProgress.PrtStates> {

        public static class Supplier implements java.util.function.Supplier<GuaranteedWithDrawProgress> {
            public GuaranteedWithDrawProgress get() {
                GuaranteedWithDrawProgress ret = new GuaranteedWithDrawProgress();
                ret.ready();
                return ret;
            }
        }

        private LinkedHashSet<Long> pendingWDReqs = new LinkedHashSet<Long>();
        public LinkedHashSet<Long> get_pendingWDReqs() { return this.pendingWDReqs; };


        public enum PrtStates {
            NopendingRequests,
            PendingReqs
        }

        public GuaranteedWithDrawProgress() {
            super();
            addState(prt.State.keyedOn(PrtStates.NopendingRequests)
                    .isInitialState(true)
                    .withEvent(PEvents.eWithDrawReq.class, p -> { Anon_3(p); gotoState(PrtStates.PendingReqs); })
                    .build());
            addState(prt.State.keyedOn(PrtStates.PendingReqs)
                    .withEvent(PEvents.eWithDrawResp.class, this::Anon_4)
                    .withEvent(PEvents.eWithDrawReq.class, p -> { Anon_5(p); gotoState(PrtStates.PendingReqs); })
                    .build());
        } // constructor

        public void reInitializeMonitor() {
            registerState(prt.State.keyedOn(PrtStates.NopendingRequests)
                    .isInitialState(true)
                    .withEvent(PEvents.eWithDrawReq.class, p -> { Anon_3(p); gotoState(PrtStates.PendingReqs); })
                    .build());
            registerState(prt.State.keyedOn(PrtStates.PendingReqs)
                    .withEvent(PEvents.eWithDrawResp.class, this::Anon_4)
                    .withEvent(PEvents.eWithDrawReq.class, p -> { Anon_5(p); gotoState(PrtStates.PendingReqs); })
                    .build());
        };

        public java.util.List<Class<? extends prt.events.PEvent<?>>> getEventTypes() {
            return java.util.Arrays.asList(PEvents.eWithDrawReq.class, PEvents.eWithDrawResp.class);
        }

        private void Anon_3(PTypes.PTuple_src_accnt_amnt_rId req_1) {
            long TMP_tmp0_2;

            TMP_tmp0_2 = req_1.rId;
            pendingWDReqs.add(TMP_tmp0_2);
        }
        private void Anon_4(PTypes.PTuple_stts_accnt_blnc_rId resp_1) throws prt.exceptions.TransitionException {
            long TMP_tmp0_3;
            boolean TMP_tmp1_2;
            long TMP_tmp2_2;
            LinkedHashSet<Long> TMP_tmp3_2;
            String TMP_tmp4_2;
            long TMP_tmp5_2;
            long TMP_tmp6_1;
            boolean TMP_tmp7_1;

            TMP_tmp0_3 = resp_1.rId;
            TMP_tmp1_2 = pendingWDReqs.contains(TMP_tmp0_3);
            TMP_tmp2_2 = resp_1.rId;
            TMP_tmp3_2 = prt.values.Clone.deepClone(pendingWDReqs);
            TMP_tmp4_2 = java.text.MessageFormat.format("unexpected rId: {0} received, expected one of {1}", TMP_tmp2_2, TMP_tmp3_2);
            tryAssert(TMP_tmp1_2, TMP_tmp4_2);
            TMP_tmp5_2 = resp_1.rId;
            pendingWDReqs.remove(TMP_tmp5_2);
            TMP_tmp6_1 = pendingWDReqs.size();
            TMP_tmp7_1 = TMP_tmp6_1 == 0L;
            if (TMP_tmp7_1) {
                gotoState(PrtStates.NopendingRequests);
                return;
            }
        }
        private void Anon_5(PTypes.PTuple_src_accnt_amnt_rId req_2) {
            long TMP_tmp0_4;

            TMP_tmp0_4 = req_2.rId;
            pendingWDReqs.add(TMP_tmp0_4);
        }

    } // GuaranteedWithDrawProgress monitor definition
    // PMachine TestWithSingleClient elided
    // PMachine TestWithMultipleClients elided
}
