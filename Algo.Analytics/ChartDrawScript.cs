﻿namespace StockSharp.Algo.Analytics
{
	/// <summary>
	/// The analytic script, shows chart drawing possibilities.
	/// </summary>
	public class ChartDrawScript : IAnalyticsScript
	{
		Task IAnalyticsScript.Run(ILogReceiver logs, IAnalyticsPanel panel, Security[] securities, DateTime from, DateTime to, IStorageRegistry storage, IMarketDataDrive drive, StorageFormats format, TimeSpan timeFrame, CancellationToken cancellationToken)
		{
			var lineChart = panel.CreateChart<DateTimeOffset, decimal>();
			var volChart = panel.CreateChart<DateTimeOffset, decimal>();
			var bubbleChart = panel.CreateChart<DateTimeOffset, decimal, decimal>();

			foreach (var security in securities)
			{
				var candlesSeries = new Dictionary<DateTimeOffset, decimal>();
				var volsSeries = new Dictionary<DateTimeOffset, decimal>();

				// get candle storage
				var candleStorage = storage.GetCandleStorage(typeof(TimeFrameCandle), security, timeFrame, format: format);

				foreach (var candle in candleStorage.Load(from, to))
				{
					// fill series
					candlesSeries[candle.OpenTime] = candle.ClosePrice;
					volsSeries[candle.OpenTime] = candle.TotalVolume;
				}

				// draw series on chart
				lineChart.Append(candlesSeries.Keys, candlesSeries.Values, ChartIndicatorDrawStyles.DashedLine, Color.Red);
				volChart.Append(volsSeries.Keys, volsSeries.Values, ChartIndicatorDrawStyles.Histogram, Color.LightGreen);
			}

			return Task.CompletedTask;
		}
	}
}