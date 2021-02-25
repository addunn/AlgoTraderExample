# Algorithmic Trader

This is a very incomplete project. It's missing integration with Alpaca, UI things, etc. Also, there are a lot of issues that need to be fixed before this becomes actually usable in the wild.

## Features

- Custom scheduler for a "set it and forget it" application.
- Integrates with polygon.io to pull historical raw trades and quotes. (this project only includes one week of thinned out ticks in Jan 2019 for AAPL, SPY, QQQ, MSFT, GLD, AMD)
- Takes trades and quotes ticks and creates custom "candles" to have values of your choice: Average Trade Price, Trade Price Open/Close/High/Low, Average Bid Price, Average Ask Price, and Trade Volume.
- Configure "candle" timespan to HalfSecond, OneSecond, OneMinute, FiveMinutes, FifteenMinutes, HalfHour, and OneHour. (Currently set to HalfSecond).
- Multithreaded via Task.Factory
- Easily add new stategies by just creating a new strategy file in AlgoTraderExample/AT/AlgoTrader/Strategies/ that inherits from Strategy. Reflection is used to detect new strategies and will automatically incorporate it when running. Project has 4 random example strategies for demonstration purposes.
- Fast database lookups when retrieving quotes and trade ticks.
- Uses an embedded chromium browser (CefSharp) as the UI for a flexible and flowing interface. Files are in AlgoTraderExample\AT\compiled\html
- Fast caching (MessagePack) to disk for faster loading after a "candle" series is constructed from DBs.
- Run in different modes that use the same code paths so backtesting should actually be similar to live scenerio.
  - BackTesting: Testing days are only used for back testing to avoid over-fitting a strategy
  - GatherStats: Runs all strategies on all symbols on all available days EXCEPT testing days. This is for gathering statistics on strategies for future picking what strategies go with what symbols (not implemented).
  - Simulating: Just like gather stats but no actual gathering of stats.
  - FakeLive: Works on live data but orders are simulated locally (no remote order API calls).
  - Live: Real money, real orders, real time.
  - Paper: Same as live but on the paper API URL.

## UI Snapshots

Sorry about the low quality GIFs. The UI doesn't look that bad.

![alt text](https://i.imgur.com/Rui5Iid.gif)

![alt text](https://i.imgur.com/pJudeGB.gif)


## Installing and Running

1) Clone this repository. Don't open the solution yet.
2) Download this 7z: https://drive.google.com/file/d/1vtd03CL5V0qM2mbvRSPkZJrA8b4vZpkx/view?usp=sharing 
3) Create folder AlgoTraderExample/AT/compiled and extract contents into that folder. (this is DLLs and DBs that were too large)
4) Open the soluton.
5) Set the build to x64 that outputs to that /compiled/ folder.
6) Build/Debug (should launch the application)
7) Click "Start Schedular" and it should start the GatherStats mode and simulate trades for about a week.

