﻿using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using MarketHistory;
using DataModels;

namespace AIStockTradingConsole
{
    class Program
    {
        public static void Main(string[] args)
        {
            //Load up all the setting and operating parameters
            ConsoleRunningParameters cmdParams = new ConsoleRunningParameters();
            delay(475);

            //Get Watch lists
            var allWatchLists = AIStockTradingBotLogic.Watchlists.GetAllWatchLists(cmdParams.client);
            delay(475);

            ////looping timing variables
            DateTime lastExecutionOfMinuteData = DateTime.Now.AddHours(-8);
            
            //Saving data really only needs to happen once a day
            lastExecutionOfMinuteData = updateAllTrackedSymbolsData(cmdParams.client, cmdParams.apiKey, cmdParams.tradeDataPath, allWatchLists);


            //-----looping section-----

            //often Call
            //TODO Check For Trades
            //TODO Check Currently Trading stocks list

            Log.write($"EndTime {DateTime.Now.ToString()}");

        }

        public static void getAndSetAuthToken(System.Net.Http.HttpClient client, string refresh_token, string Consumer_Key)
        {
            //var message = TD_API_Interface.API_Calls.Authentication.refreshToken(client, refresh_token, Consumer_Key);
            string Authorization = TD_API_Interface.API_Calls.Authentication.refreshToken(client, refresh_token, Consumer_Key);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Authorization);
        }

        private static void delay(int Time_delay)
        {
            int i = 0;
            var _delayTimer = new System.Timers.Timer();
            _delayTimer.Interval = Time_delay;
            _delayTimer.AutoReset = false; //so that it only calls the method once
            _delayTimer.Elapsed += (s, args) => i = 1;
            _delayTimer.Start();
            while (i == 0) { };
        }

        private static DateTime updateAllTrackedSymbolsData(HttpClient client, string apiKey, string tradeDataPath, DataModels.AllWatchLists allWatchLists)
        {

            List<string> watchSymbols = new List<string>();
            foreach (var list in allWatchLists.Lists)
            {
                foreach (var item in list.watchlistItems)
                {
                    if (!watchSymbols.Contains(item.symbol) && !item.symbol.Contains("/") && item.assetType == "EQUITY")
                        watchSymbols.Add(item.symbol);
                }
            }
            Log.write($"{DateTime.Now.ToString()} Watching {watchSymbols.Count.ToString()} Symbols");
            foreach (string symbol in watchSymbols)
                Console.Write($"{symbol.PadRight(10)}");

            Log.write($"{watchSymbols.Count.ToString()} valid symbols found within {allWatchLists.Lists.Count.ToString()} watch lists.");

            int countTrackedStockSymbols = 0;
            List<string> TrackedStockSymbols = MarketHistory.ReadWriteJSONToDisk.getListOfStocksWithHistory(tradeDataPath);
            Log.write($"{DateTime.Now.ToString()} Tracked {TrackedStockSymbols.Count.ToString()} Symbols");
            foreach (string symbol in TrackedStockSymbols)
            {
                Console.Write($"{symbol.PadRight(10)}");
                countTrackedStockSymbols++;
            }
            Console.WriteLine("");

            List<string> newSymbolsToTrack = watchSymbols.Where(s => !TrackedStockSymbols.Contains(s)).ToList();

            StoreNewSymbolsByMinuteData(client, apiKey, tradeDataPath, newSymbolsToTrack);

            return DateTime.Now;
        }

        public static void StoreNewSymbolsByMinuteData(HttpClient client, string apiKey, string tradeDataPath, List<string> symbols)
        {
            foreach (string symbol in symbols)
            {
                Log.write($"Updating Market Hours for Symbols {symbol}");
                try
                {
                    string newStockPath = StockHistory.Gather10daysByTheMinute(client, apiKey, symbol, tradeDataPath);
                    Log.write($"{DateTime.Now.ToString()} {symbol} minute history as been added to {newStockPath}");
                }
                catch (Exception ex)
                {
                    Log.write(ex);
                }
                delay(250);
            }
        }

        public static void UpdateTrackedSymbolsMyMinuteData(HttpClient client, string apiKey, string tradeDataPath, List<string> TrackedStockSymbols)
        { 
            foreach (string symbol in TrackedStockSymbols)
            {
                Log.write($"Updating Market Hours for Symbols {symbol}");
                string storageFolderPath = StockHistory.getSymbolsPriceHistoryPath(tradeDataPath, symbol, "ByMinute");
                List<QuoteFile> priceByMinuteFiles = ReadWriteJSONToDisk.getQuotesFileListFromDirectory(storageFolderPath);
                DateTime MaxModDate = new DateTime(0);
                if (priceByMinuteFiles.Count() > 0)
                    MaxModDate = priceByMinuteFiles.Select(s => s.info.LastWriteTime).Max();
                if (DateTime.Now.AddDays(-1) > MaxModDate)
                {
                    string newUpdatedFile = StockHistory.UpdateStockByMinuteHistoryFile(client, apiKey, symbol, tradeDataPath, true);
                    Log.write($"Symbol By Minute updated {newUpdatedFile}");
                    delay(250);
                }
            }
        }
    }
}
