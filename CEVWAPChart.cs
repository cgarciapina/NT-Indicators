#region Using declarations
using System;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Windows.Media;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class CEVWAPChart : Indicator
    {
        private double cumulativeVolume;
        private double cumulativeValue;
        private double cumulativeValue2;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "CE VWAP recreation using NinjaTrader trading-session reset.";
                Name = "CEVWAPChart";

                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;

                AddPlot(Brushes.Orange, "VWAP");
                AddPlot(Brushes.Magenta, "Upper1");
                AddPlot(Brushes.Magenta, "Lower1");
            }
            else if (State == State.DataLoaded)
            {
                cumulativeVolume = 0;
                cumulativeValue = 0;
                cumulativeValue2 = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            // Tradovate's tradeDate() follows the futures trading
            // session rather than simply resetting at midnight.
            if (Bars.IsFirstBarOfSession)
            {
                cumulativeVolume = 0;
                cumulativeValue = 0;
                cumulativeValue2 = 0;
            }

            // Tradovate fallback calculation:
            // typicalPrice = (High + Low + Close) / 3
            double price =
                (High[0] + Low[0] + Close[0]) / 3.0;

            double volume = Volume[0];

            cumulativeVolume += volume;
            cumulativeValue += volume * price;
            cumulativeValue2 += volume * price * price;

            if (cumulativeVolume <= 0)
                return;

            double vwap =
                cumulativeValue / cumulativeVolume;

            double variance =
                (cumulativeValue2 / cumulativeVolume)
                - (vwap * vwap);

            if (variance < 0)
                variance = 0;

            double stdDev = Math.Sqrt(variance);

            VWAP[0] = vwap;
            Upper1[0] = vwap + stdDev;
            Lower1[0] = vwap - stdDev;
        }

        #region Plots

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> VWAP
        {
            get { return Values[0]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Upper1
        {
            get { return Values[1]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Lower1
        {
            get { return Values[2]; }
        }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private CEVWAPChart[] cacheCEVWAPChart;
		public CEVWAPChart CEVWAPChart()
		{
			return CEVWAPChart(Input);
		}

		public CEVWAPChart CEVWAPChart(ISeries<double> input)
		{
			if (cacheCEVWAPChart != null)
				for (int idx = 0; idx < cacheCEVWAPChart.Length; idx++)
					if (cacheCEVWAPChart[idx] != null &&  cacheCEVWAPChart[idx].EqualsInput(input))
						return cacheCEVWAPChart[idx];
			return CacheIndicator<CEVWAPChart>(new CEVWAPChart(), input, ref cacheCEVWAPChart);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.CEVWAPChart CEVWAPChart()
		{
			return indicator.CEVWAPChart(Input);
		}

		public Indicators.CEVWAPChart CEVWAPChart(ISeries<double> input )
		{
			return indicator.CEVWAPChart(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.CEVWAPChart CEVWAPChart()
		{
			return indicator.CEVWAPChart(Input);
		}

		public Indicators.CEVWAPChart CEVWAPChart(ISeries<double> input )
		{
			return indicator.CEVWAPChart(input);
		}
	}
}

#endregion
