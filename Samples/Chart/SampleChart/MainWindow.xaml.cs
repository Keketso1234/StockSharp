﻿#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: SampleChart.SampleChartPublic
File: MainWindow.xaml.cs
Created: 2015, 12, 2, 8:18 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace SampleChart
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Threading;

	using Ecng.Collections;
	using Ecng.Common;
	using Ecng.Xaml;

	using StockSharp.Algo;
	using StockSharp.Algo.Candles;
	using StockSharp.Algo.Candles.Compression;
	using StockSharp.Algo.Indicators;
	using StockSharp.Algo.Storages;
	using StockSharp.BusinessEntities;
	using StockSharp.Configuration;
	using StockSharp.Localization;
	using StockSharp.Messages;
	using StockSharp.Xaml.Charting;

	public partial class MainWindow
	{
		private ChartArea _areaComb;
		private ChartCandleElement _candleElement1;
		private TimeFrameCandle _candle;
		private VolumeProfile _volumeProfile;
		private readonly DispatcherTimer _chartUpdateTimer = new DispatcherTimer();
		private readonly SynchronizedDictionary<DateTimeOffset, TimeFrameCandle> _updatedCandles = new SynchronizedDictionary<DateTimeOffset, TimeFrameCandle>();
		private readonly CachedSynchronizedList<TimeFrameCandle> _allCandles = new CachedSynchronizedList<TimeFrameCandle>();
		private decimal _lastPrice;
		private const decimal _priceStep = 10m;
		private Security _security = new Security
		{
			Id = "RIZ2@FORTS",
			PriceStep = _priceStep,
			Board = ExchangeBoard.Forts
		};

		private int _timeframe;

		public MainWindow()
		{
			InitializeComponent();

			Title = Title.Put(LocalizedStrings.Str3200);

			Loaded += OnLoaded;

			_chartUpdateTimer.Interval = TimeSpan.FromMilliseconds(100);
			_chartUpdateTimer.Tick += ChartUpdateTimerOnTick;
			_chartUpdateTimer.Start();

			Theme.SelectedIndex = 1;
		}

		private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
		{
			Chart.FillIndicators();
			InitCharts();

			Chart.SubscribeIndicatorElement += Chart_OnSubscribeIndicatorElement;

			HistoryPath.Folder = @"..\..\..\..\Testing\HistoryData\".ToFullPath();
			LoadData();
		}

		private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var theme = (string)((ComboBoxItem)Theme.SelectedValue).Content;
			if(theme.IsEmpty())
				return;

			DevExpress.Xpf.Core.ThemeManager.ApplicationThemeName = theme;

			switch (theme)
			{
				case DevExpress.Xpf.Core.Theme.Office2016BlackName:
				case DevExpress.Xpf.Core.Theme.MetropolisDarkName:
					Chart.ChartTheme = "ExpressionDark";
					break;
				case DevExpress.Xpf.Core.Theme.Office2016WhiteName:
				case DevExpress.Xpf.Core.Theme.MetropolisLightName:
					Chart.ChartTheme = "Chrome";
					break;
			}
		}



		private void Chart_OnSubscribeIndicatorElement(ChartIndicatorElement element, CandleSeries series, IIndicator indicator)
		{
			var chartData = new ChartDrawData();

			foreach (var candle in _allCandles.Cache)
			{
				if (candle.State != CandleStates.Finished)
					candle.State = CandleStates.Finished;

				chartData.Group(candle.OpenTime).Add(element, indicator.Process(candle));
			}

			Chart.Draw(chartData);
		}

		private void InitCharts()
		{
			Chart.ClearAreas();

			_areaComb = new ChartArea();

			var yAxis = _areaComb.YAxises.First();

			yAxis.AutoRange = true;
			Chart.IsAutoRange = true;
			Chart.IsAutoScroll = true;

			Chart.AddArea(_areaComb);

			_timeframe = int.Parse((string)((ComboBoxItem)Timeframe.SelectedItem).Tag);

			var series = new CandleSeries(
				typeof(TimeFrameCandle),
				_security,
				TimeSpan.FromMinutes(_timeframe));

			_candleElement1 = new ChartCandleElement() { FullTitle = "Candles" };
			Chart.AddElement(_areaComb, _candleElement1, series);
		}

		private void Draw_Click(object sender, RoutedEventArgs e)
		{
			InitCharts();
			LoadData();
		}

		private void LoadData()
		{
			_candle = null;
			_lastPrice = 0m;
			_allCandles.Clear();

			var id = new SecurityIdGenerator().Split(SecurityId.Text);

			_security = new Security
			{
				Id = SecurityId.Text,
				PriceStep = _priceStep,
				Board = ExchangeBoard.GetBoard(id.BoardCode)
			};

			Chart.Reset(new IChartElement[] { _candleElement1 });

			var storage = new StorageRegistry();

			var maxDays = 2;

			BusyIndicator.IsBusy = true;

			var path = HistoryPath.Folder;

			Task.Factory.StartNew(() =>
			{
				var date = DateTime.MinValue;

				foreach (var tick in storage.GetTickMessageStorage(_security, new LocalMarketDataDrive(path)).Load())
				{
					AppendTick(_security, tick);
					_lastTime = tick.ServerTime;

					if (date != tick.ServerTime.Date)
					{
						date = tick.ServerTime.Date;

						this.GuiAsync(() =>
						{
							BusyIndicator.BusyContent = date.ToString();
						});

						maxDays--;

						if (maxDays == 0)
							break;
					}
				}
			})
			.ContinueWith(t =>
			{
				if (t.Exception != null)
					Error(t.Exception.Message);

				this.GuiAsync(() =>
				{
					BusyIndicator.IsBusy = false;
					Chart.IsAutoRange = false;
					_areaComb.YAxises.First().AutoRange = false;
				});

			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private DateTimeOffset _lastTime;

		private void ChartUpdateTimerOnTick(object sender, EventArgs eventArgs)
		{
			if (IsRealtime.IsChecked == true && _lastPrice != 0m)
			{
				var step = _priceStep;
				var price = Round(_lastPrice + (decimal)((RandomGen.GetDouble() - 0.5) * 5 * (double) step), step);
				AppendTick(_security, new ExecutionMessage
				{
					ServerTime = _lastTime,
					TradePrice = price,
					TradeVolume = RandomGen.GetInt(50) + 1,
					OriginSide = Sides.Buy,
				});
				_lastTime += TimeSpan.FromSeconds(10);
			}

			TimeFrameCandle[] candlesToUpdate;
			lock (_updatedCandles.SyncRoot)
			{
				candlesToUpdate = _updatedCandles.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
				_updatedCandles.Clear();
			}

			var lastCandle = _allCandles.LastOrDefault();
			_allCandles.AddRange(candlesToUpdate.Where(c => lastCandle == null || c.OpenTime != lastCandle.OpenTime));

			var chartData = new ChartDrawData();

			foreach (var candle in candlesToUpdate)
			{
				chartData.Group(candle.OpenTime).Add(_candleElement1, candle);
			}

			Chart.Draw(chartData);
		}

		private void AppendTick(Security security, ExecutionMessage tick)
		{
			var time = tick.ServerTime;
			var price = tick.TradePrice.Value;

			if (_candle == null || time >= _candle.CloseTime)
			{
				if (_candle != null)
				{
					_candle.State = CandleStates.Finished;
					lock(_updatedCandles.SyncRoot)
						_updatedCandles[_candle.OpenTime] = _candle;
					_lastPrice = _candle.ClosePrice;
				}

				//var t = TimeframeSegmentDataSeries.GetTimeframePeriod(time.DateTime, _timeframe);
				var tf = TimeSpan.FromMinutes(_timeframe);
				var bounds = tf.GetCandleBounds(time, _security.Board);
				_candle = new TimeFrameCandle
				{
					TimeFrame = tf,
					OpenTime = bounds.Min,
					CloseTime = bounds.Max,
					Security = security,
				};
				_volumeProfile = new VolumeProfile();
				_candle.PriceLevels = _volumeProfile.PriceLevels;

				_candle.OpenPrice = _candle.HighPrice = _candle.LowPrice = _candle.ClosePrice = price;
			}

			if (time < _candle.OpenTime)
				throw new InvalidOperationException("invalid time");

			if (price > _candle.HighPrice)
				_candle.HighPrice = price;

			if (price < _candle.LowPrice)
				_candle.LowPrice = price;

			_candle.ClosePrice = price;

			_candle.TotalVolume += tick.TradeVolume.Value;

			_volumeProfile.Update(new TickCandleBuilderSourceValue(security, tick));

			lock(_updatedCandles.SyncRoot)
				_updatedCandles[_candle.OpenTime] = _candle;
		}

		public static decimal Round(decimal value, decimal nearest)
		{
			return Math.Round(value / nearest) * nearest;
		}

		private void Error(string msg)
		{
			new MessageBoxBuilder()
				.Owner(this)
				.Error()
				.Text(msg)
				.Show();
		}
	}
}