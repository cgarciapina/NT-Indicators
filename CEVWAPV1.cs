#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class CEVWAPCombinedMorningLive : Strategy
    {
        private CEVWAPChart ceVwap;

        private bool pendingLong;
        private string pendingLongLine;

        private bool pendingShortLower1;
        private bool pendingShortUpper1;

        // Long module daily lock
        private bool longLockedForDay;

        // Used to determine whether a completed
        // LongMorning trade was a loss.
        private int lastProcessedTradeCount;


        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "CEVWAPCombinedMorningLive";

                Description =
                    "Combined CE VWAP morning strategy with 9:30-10 longs, Lower1 shorts, Upper1 strict shorts, and optional daily long lock after first long loss.";

                Calculate = Calculate.OnBarClose;

                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;

                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;

                BarsRequiredToTrade = 20;

                // =====================================
                // GLOBAL SETTINGS
                // =====================================

                Contracts = 1;

                ProfitTargetTicks = 35;
                StopLossTicks = 93;

                MaxCrossDistanceTicks = 70;

                // If a 9:30-10 long loses,
                // disable ONLY long module until next session.
                StopLongAfterFirstLoss = true;


                // =====================================
                // MODULE 1 - LONG
                // 9:30-10:00
                // Lower1 Up + VWAP Up
                // Normal hold
                // =====================================

                EnableLongModule = true;

                LongTradeMonday = false;
                LongTradeTuesday = true;
                LongTradeWednesday = true;
                LongTradeThursday = true;
                LongTradeFriday = true;

                LongStartHour = 9;
                LongStartMinute = 30;
                LongStartIsPM = false;

                LongEndHour = 10;
                LongEndMinute = 0;
                LongEndIsPM = false;

                EnableLongLower1Up = true;
                EnableLongVWAPUp = true;


                // =====================================
                // MODULE 2 - LOWER1 SHORT
                // 10:00-10:30
                // Tue / Thu / Fri
                // Normal hold
                // =====================================

                EnableLower1ShortModule = true;

                Lower1ShortMonday = false;
                Lower1ShortTuesday = true;
                Lower1ShortWednesday = false;
                Lower1ShortThursday = true;
                Lower1ShortFriday = true;

                Lower1ShortStartHour = 10;
                Lower1ShortStartMinute = 0;
                Lower1ShortStartIsPM = false;

                Lower1ShortEndHour = 10;
                Lower1ShortEndMinute = 30;
                Lower1ShortEndIsPM = false;


                // =====================================
                // MODULE 3 - UPPER1 SHORT
                // 10:00-10:30
                // Mon-Fri
                // STRICT HOLD
                // =====================================

                EnableUpper1ShortModule = true;

                Upper1ShortMonday = true;
                Upper1ShortTuesday = true;
                Upper1ShortWednesday = true;
                Upper1ShortThursday = true;
                Upper1ShortFriday = true;

                Upper1ShortStartHour = 10;
                Upper1ShortStartMinute = 0;
                Upper1ShortStartIsPM = false;

                Upper1ShortEndHour = 10;
                Upper1ShortEndMinute = 30;
                Upper1ShortEndIsPM = false;
            }

            else if (State == State.Configure)
            {
                SetProfitTarget(
                    "LongMorning",
                    CalculationMode.Ticks,
                    ProfitTargetTicks);

                SetStopLoss(
                    "LongMorning",
                    CalculationMode.Ticks,
                    StopLossTicks,
                    false);

                SetProfitTarget(
                    "ShortLower1",
                    CalculationMode.Ticks,
                    ProfitTargetTicks);

                SetStopLoss(
                    "ShortLower1",
                    CalculationMode.Ticks,
                    StopLossTicks,
                    false);

                SetProfitTarget(
                    "ShortUpper1",
                    CalculationMode.Ticks,
                    ProfitTargetTicks);

                SetStopLoss(
                    "ShortUpper1",
                    CalculationMode.Ticks,
                    StopLossTicks,
                    false);
            }

            else if (State == State.DataLoaded)
            {
                ceVwap = CEVWAPChart();

                ClearAllPending();

                longLockedForDay = false;
                lastProcessedTradeCount = 0;
            }
        }


        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;


            // ==========================================
            // NEW SESSION RESET
            // ==========================================

            if (Bars.IsFirstBarOfSession)
            {
                ClearAllPending();

                longLockedForDay = false;
            }


            // ==========================================
            // CHECK FOR NEWLY COMPLETED TRADES
            // AND LOCK LONG MODULE AFTER LONG LOSS
            // ==========================================

            ProcessCompletedTrades();


            // ==========================================
            // MODULE 1 HOLD
            // LONG - NORMAL HOLD
            // ==========================================

            if (pendingLong)
            {
                if (
                    longLockedForDay
                    ||
                    !EnableLongModule
                    ||
                    !IsAllowedDay(
                        LongTradeMonday,
                        LongTradeTuesday,
                        LongTradeWednesday,
                        LongTradeThursday,
                        LongTradeFriday)
                    ||
                    !IsInsideWindow(
                        LongStartHour,
                        LongStartMinute,
                        LongStartIsPM,
                        LongEndHour,
                        LongEndMinute,
                        LongEndIsPM)
                   )
                {
                    pendingLong = false;
                    pendingLongLine = "";
                }
                else
                {
                    double lineNow =
                        GetLineValue(
                            pendingLongLine,
                            0);

                    bool holdConfirmed =
                        Close[0] >= lineNow;

                    if (
                        holdConfirmed
                        &&
                        Position.MarketPosition
                            == MarketPosition.Flat
                       )
                    {
                        EnterLong(
                            Contracts,
                            "LongMorning");

                        pendingLong = false;
                        pendingLongLine = "";

                        return;
                    }

                    pendingLong = false;
                    pendingLongLine = "";
                }
            }


            // ==========================================
            // MODULE 2 HOLD
            // LOWER1 SHORT - NORMAL HOLD
            // ==========================================

            if (pendingShortLower1)
            {
                bool valid =
                    EnableLower1ShortModule
                    &&
                    IsAllowedDay(
                        Lower1ShortMonday,
                        Lower1ShortTuesday,
                        Lower1ShortWednesday,
                        Lower1ShortThursday,
                        Lower1ShortFriday)
                    &&
                    IsInsideWindow(
                        Lower1ShortStartHour,
                        Lower1ShortStartMinute,
                        Lower1ShortStartIsPM,
                        Lower1ShortEndHour,
                        Lower1ShortEndMinute,
                        Lower1ShortEndIsPM);

                if (!valid)
                {
                    pendingShortLower1 = false;
                }
                else
                {
                    double lowerNow =
                        ceVwap.Lower1[0];

                    bool holdConfirmed =
                        Close[0] <= lowerNow;

                    if (
                        holdConfirmed
                        &&
                        Position.MarketPosition
                            == MarketPosition.Flat
                       )
                    {
                        EnterShort(
                            Contracts,
                            "ShortLower1");

                        pendingShortLower1 = false;

                        return;
                    }

                    pendingShortLower1 = false;
                }
            }


            // ==========================================
            // MODULE 3 HOLD
            // UPPER1 SHORT - STRICT HOLD
            // ==========================================

            if (pendingShortUpper1)
            {
                bool valid =
                    EnableUpper1ShortModule
                    &&
                    IsAllowedDay(
                        Upper1ShortMonday,
                        Upper1ShortTuesday,
                        Upper1ShortWednesday,
                        Upper1ShortThursday,
                        Upper1ShortFriday)
                    &&
                    IsInsideWindow(
                        Upper1ShortStartHour,
                        Upper1ShortStartMinute,
                        Upper1ShortStartIsPM,
                        Upper1ShortEndHour,
                        Upper1ShortEndMinute,
                        Upper1ShortEndIsPM);

                if (!valid)
                {
                    pendingShortUpper1 = false;
                }
                else
                {
                    double upperNow =
                        ceVwap.Upper1[0];

                    // STRICT HOLD:
                    // entire confirmation candle
                    // must remain below Upper1.
                    bool holdConfirmed =
                        High[0] <= upperNow;

                    if (
                        holdConfirmed
                        &&
                        Position.MarketPosition
                            == MarketPosition.Flat
                       )
                    {
                        EnterShort(
                            Contracts,
                            "ShortUpper1");

                        pendingShortUpper1 = false;

                        return;
                    }

                    pendingShortUpper1 = false;
                }
            }


            // ==========================================
            // NO NEW SETUPS WHILE IN POSITION
            // ==========================================

            if (
                Position.MarketPosition
                != MarketPosition.Flat
               )
                return;


            // ==========================================
            // MODULE 1
            // 9:30-10 LONG
            // ==========================================

            if (
                EnableLongModule
                &&
                !longLockedForDay
                &&
                IsAllowedDay(
                    LongTradeMonday,
                    LongTradeTuesday,
                    LongTradeWednesday,
                    LongTradeThursday,
                    LongTradeFriday)
                &&
                IsInsideWindow(
                    LongStartHour,
                    LongStartMinute,
                    LongStartIsPM,
                    LongEndHour,
                    LongEndMinute,
                    LongEndIsPM)
               )
            {
                double lowerNow =
                    ceVwap.Lower1[0];

                double lowerPrior =
                    ceVwap.Lower1[1];

                double vwapNow =
                    ceVwap[0];

                double vwapPrior =
                    ceVwap[1];


                // LOWER1 UP

                if (EnableLongLower1Up)
                {
                    bool crossed =
                        Close[1] <= lowerPrior
                        &&
                        Close[0] > lowerNow;

                    if (crossed)
                    {
                        double distanceTicks =
                            (Close[0] - lowerNow)
                            / TickSize;

                        if (
                            CrossDistanceAllowed(
                                distanceTicks)
                           )
                        {
                            pendingLong = true;
                            pendingLongLine = "Lower1";
                            return;
                        }
                    }
                }


                // VWAP UP

                if (EnableLongVWAPUp)
                {
                    bool crossed =
                        Close[1] <= vwapPrior
                        &&
                        Close[0] > vwapNow;

                    if (crossed)
                    {
                        double distanceTicks =
                            (Close[0] - vwapNow)
                            / TickSize;

                        if (
                            CrossDistanceAllowed(
                                distanceTicks)
                           )
                        {
                            pendingLong = true;
                            pendingLongLine = "VWAP";
                            return;
                        }
                    }
                }
            }


            // ==========================================
            // MODULE 2
            // LOWER1 DOWN SHORT
            // ==========================================

            if (
                EnableLower1ShortModule
                &&
                IsAllowedDay(
                    Lower1ShortMonday,
                    Lower1ShortTuesday,
                    Lower1ShortWednesday,
                    Lower1ShortThursday,
                    Lower1ShortFriday)
                &&
                IsInsideWindow(
                    Lower1ShortStartHour,
                    Lower1ShortStartMinute,
                    Lower1ShortStartIsPM,
                    Lower1ShortEndHour,
                    Lower1ShortEndMinute,
                    Lower1ShortEndIsPM)
               )
            {
                double lowerNow =
                    ceVwap.Lower1[0];

                double lowerPrior =
                    ceVwap.Lower1[1];

                bool crossed =
                    Close[1] >= lowerPrior
                    &&
                    Close[0] < lowerNow;

                if (crossed)
                {
                    double distanceTicks =
                        (lowerNow - Close[0])
                        / TickSize;

                    if (
                        CrossDistanceAllowed(
                            distanceTicks)
                       )
                    {
                        pendingShortLower1 = true;
                        return;
                    }
                }
            }


            // ==========================================
            // MODULE 3
            // UPPER1 DOWN SHORT
            // ==========================================

            if (
                EnableUpper1ShortModule
                &&
                IsAllowedDay(
                    Upper1ShortMonday,
                    Upper1ShortTuesday,
                    Upper1ShortWednesday,
                    Upper1ShortThursday,
                    Upper1ShortFriday)
                &&
                IsInsideWindow(
                    Upper1ShortStartHour,
                    Upper1ShortStartMinute,
                    Upper1ShortStartIsPM,
                    Upper1ShortEndHour,
                    Upper1ShortEndMinute,
                    Upper1ShortEndIsPM)
               )
            {
                double upperNow =
                    ceVwap.Upper1[0];

                double upperPrior =
                    ceVwap.Upper1[1];

                bool crossed =
                    Close[1] >= upperPrior
                    &&
                    Close[0] < upperNow;

                if (crossed)
                {
                    double distanceTicks =
                        (upperNow - Close[0])
                        / TickSize;

                    if (
                        CrossDistanceAllowed(
                            distanceTicks)
                       )
                    {
                        pendingShortUpper1 = true;
                        return;
                    }
                }
            }
        }


        // ==============================================
        // LOCK LONG MODULE AFTER A LOSING LONG
        // ==============================================

        private void ProcessCompletedTrades()
        {
            int tradeCount =
                SystemPerformance.AllTrades.Count;

            if (tradeCount <= lastProcessedTradeCount)
                return;


            for (
                int i = lastProcessedTradeCount;
                i < tradeCount;
                i++
                )
            {
                Trade trade =
                    SystemPerformance.AllTrades[i];

                if (
                    StopLongAfterFirstLoss
                    &&
                    trade.Entry != null
                    &&
                    trade.Entry.Name == "LongMorning"
                    &&
                    trade.ProfitCurrency < 0
                   )
                {
                    longLockedForDay = true;

                    pendingLong = false;
                    pendingLongLine = "";

                    Print(
                        Time[0]
                        + " LONG MODULE LOCKED FOR DAY AFTER LOSS.");
                }
            }

            lastProcessedTradeCount =
                tradeCount;
        }


        // ==============================================
        // CROSS FILTER
        // ==============================================

        private bool CrossDistanceAllowed(
            double distanceTicks)
        {
            if (MaxCrossDistanceTicks == 0)
                return true;

            return
                distanceTicks
                <= MaxCrossDistanceTicks;
        }


        // ==============================================
        // LINE LOOKUP
        // ==============================================

        private double GetLineValue(
            string lineName,
            int barsAgo)
        {
            if (lineName == "Lower1")
                return ceVwap.Lower1[barsAgo];

            if (lineName == "Upper1")
                return ceVwap.Upper1[barsAgo];

            return ceVwap[barsAgo];
        }


        // ==============================================
        // DAY FILTER
        // ==============================================

        private bool IsAllowedDay(
            bool monday,
            bool tuesday,
            bool wednesday,
            bool thursday,
            bool friday)
        {
            DayOfWeek day =
                Time[0].DayOfWeek;

            if (day == DayOfWeek.Monday)
                return monday;

            if (day == DayOfWeek.Tuesday)
                return tuesday;

            if (day == DayOfWeek.Wednesday)
                return wednesday;

            if (day == DayOfWeek.Thursday)
                return thursday;

            if (day == DayOfWeek.Friday)
                return friday;

            return false;
        }


        // ==============================================
        // TIME WINDOW
        // ==============================================

        private bool IsInsideWindow(
            int startHour,
            int startMinute,
            bool startIsPM,
            int endHour,
            int endMinute,
            bool endIsPM)
        {
            int start =
                ConvertTo24HourTime(
                    startHour,
                    startMinute,
                    startIsPM);

            int end =
                ConvertTo24HourTime(
                    endHour,
                    endMinute,
                    endIsPM);

            int now =
                ToTime(Time[0]);

            if (start <= end)
            {
                return
                    now >= start
                    &&
                    now <= end;
            }

            return
                now >= start
                ||
                now <= end;
        }


        private int ConvertTo24HourTime(
            int hour,
            int minute,
            bool isPM)
        {
            int convertedHour = hour;

            if (!isPM && hour == 12)
                convertedHour = 0;

            if (isPM && hour != 12)
                convertedHour = hour + 12;

            return
                convertedHour * 10000
                +
                minute * 100;
        }


        private void ClearAllPending()
        {
            pendingLong = false;
            pendingLongLine = "";

            pendingShortLower1 = false;
            pendingShortUpper1 = false;
        }


        #region Properties

        // ==============================================
        // GLOBAL
        // ==============================================

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(
            Name = "Contracts",
            Order = 1,
            GroupName = "Global")]
        public int Contracts
        {
            get;
            set;
        }


        [NinjaScriptProperty]
        [Range(1, 2000)]
        [Display(
            Name = "Profit Target Ticks",
            Order = 2,
            GroupName = "Global")]
        public int ProfitTargetTicks
        {
            get;
            set;
        }


        [NinjaScriptProperty]
        [Range(1, 2000)]
        [Display(
            Name = "Stop Loss Ticks",
            Order = 3,
            GroupName = "Global")]
        public int StopLossTicks
        {
            get;
            set;
        }


        [NinjaScriptProperty]
        [Range(0, 2000)]
        [Display(
            Name = "Maximum Cross Distance Ticks",
            Order = 4,
            GroupName = "Global")]
        public int MaxCrossDistanceTicks
        {
            get;
            set;
        }


        [NinjaScriptProperty]
        [Display(
            Name = "Stop Long After First Loss",
            Description =
                "Locks only the 9:30-10:00 long module after its first losing trade of the day. Short modules remain active.",
            Order = 5,
            GroupName = "Global")]
        public bool StopLongAfterFirstLoss
        {
            get;
            set;
        }


        // ==============================================
        // LONG MODULE
        // ==============================================

        [NinjaScriptProperty]
        [Display(Name = "Enable Long Module", Order = 1, GroupName = "1 - Long")]
        public bool EnableLongModule { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Lower1 Up", Order = 2, GroupName = "1 - Long")]
        public bool EnableLongLower1Up { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "VWAP Up", Order = 3, GroupName = "1 - Long")]
        public bool EnableLongVWAPUp { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Monday", Order = 10, GroupName = "1 - Long")]
        public bool LongTradeMonday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Tuesday", Order = 11, GroupName = "1 - Long")]
        public bool LongTradeTuesday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Wednesday", Order = 12, GroupName = "1 - Long")]
        public bool LongTradeWednesday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Thursday", Order = 13, GroupName = "1 - Long")]
        public bool LongTradeThursday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Friday", Order = 14, GroupName = "1 - Long")]
        public bool LongTradeFriday { get; set; }

        [NinjaScriptProperty]
        [Range(1, 12)]
        [Display(Name = "Start Hour", Order = 20, GroupName = "1 - Long")]
        public int LongStartHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Start Minute", Order = 21, GroupName = "1 - Long")]
        public int LongStartMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Start Is PM", Order = 22, GroupName = "1 - Long")]
        public bool LongStartIsPM { get; set; }

        [NinjaScriptProperty]
        [Range(1, 12)]
        [Display(Name = "End Hour", Order = 23, GroupName = "1 - Long")]
        public int LongEndHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "End Minute", Order = 24, GroupName = "1 - Long")]
        public int LongEndMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "End Is PM", Order = 25, GroupName = "1 - Long")]
        public bool LongEndIsPM { get; set; }


        // ==============================================
        // LOWER1 SHORT
        // ==============================================

        [NinjaScriptProperty]
        [Display(Name = "Enable Lower1 Short", Order = 1, GroupName = "2 - Lower1 Short")]
        public bool EnableLower1ShortModule { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Monday", Order = 10, GroupName = "2 - Lower1 Short")]
        public bool Lower1ShortMonday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Tuesday", Order = 11, GroupName = "2 - Lower1 Short")]
        public bool Lower1ShortTuesday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Wednesday", Order = 12, GroupName = "2 - Lower1 Short")]
        public bool Lower1ShortWednesday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Thursday", Order = 13, GroupName = "2 - Lower1 Short")]
        public bool Lower1ShortThursday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Friday", Order = 14, GroupName = "2 - Lower1 Short")]
        public bool Lower1ShortFriday { get; set; }

        [NinjaScriptProperty]
        [Range(1, 12)]
        [Display(Name = "Start Hour", Order = 20, GroupName = "2 - Lower1 Short")]
        public int Lower1ShortStartHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Start Minute", Order = 21, GroupName = "2 - Lower1 Short")]
        public int Lower1ShortStartMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Start Is PM", Order = 22, GroupName = "2 - Lower1 Short")]
        public bool Lower1ShortStartIsPM { get; set; }

        [NinjaScriptProperty]
        [Range(1, 12)]
        [Display(Name = "End Hour", Order = 23, GroupName = "2 - Lower1 Short")]
        public int Lower1ShortEndHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "End Minute", Order = 24, GroupName = "2 - Lower1 Short")]
        public int Lower1ShortEndMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "End Is PM", Order = 25, GroupName = "2 - Lower1 Short")]
        public bool Lower1ShortEndIsPM { get; set; }


        // ==============================================
        // UPPER1 STRICT SHORT
        // ==============================================

        [NinjaScriptProperty]
        [Display(Name = "Enable Upper1 Strict Short", Order = 1, GroupName = "3 - Upper1 Strict Short")]
        public bool EnableUpper1ShortModule { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Monday", Order = 10, GroupName = "3 - Upper1 Strict Short")]
        public bool Upper1ShortMonday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Tuesday", Order = 11, GroupName = "3 - Upper1 Strict Short")]
        public bool Upper1ShortTuesday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Wednesday", Order = 12, GroupName = "3 - Upper1 Strict Short")]
        public bool Upper1ShortWednesday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Thursday", Order = 13, GroupName = "3 - Upper1 Strict Short")]
        public bool Upper1ShortThursday { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Friday", Order = 14, GroupName = "3 - Upper1 Strict Short")]
        public bool Upper1ShortFriday { get; set; }

        [NinjaScriptProperty]
        [Range(1, 12)]
        [Display(Name = "Start Hour", Order = 20, GroupName = "3 - Upper1 Strict Short")]
        public int Upper1ShortStartHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Start Minute", Order = 21, GroupName = "3 - Upper1 Strict Short")]
        public int Upper1ShortStartMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Start Is PM", Order = 22, GroupName = "3 - Upper1 Strict Short")]
        public bool Upper1ShortStartIsPM { get; set; }

        [NinjaScriptProperty]
        [Range(1, 12)]
        [Display(Name = "End Hour", Order = 23, GroupName = "3 - Upper1 Strict Short")]
        public int Upper1ShortEndHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "End Minute", Order = 24, GroupName = "3 - Upper1 Strict Short")]
        public int Upper1ShortEndMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "End Is PM", Order = 25, GroupName = "3 - Upper1 Strict Short")]
        public bool Upper1ShortEndIsPM { get; set; }

        #endregion
    }
}