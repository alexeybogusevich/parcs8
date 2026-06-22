using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

var filePath = @"C:\Users\obohusevych\.claude\projects\C--Users-obohusevych-source-repos-parcs7\961e3c59-8a26-4560-a90b-3dab13acea33\tool-results\mcp-parcs-run_layer-1776550285681.txt";

Console.WriteLine("Reading file...");
var raw = File.ReadAllText(filePath);
Console.WriteLine($"File length: {raw.Length} chars");

// Parse outer JSON
var outer = JsonNode.Parse(raw)!;
var status = outer["status"]?.GetValue<string>();
var totalElapsed = outer["TotalElapsedSeconds"]?.GetValue<double>() ?? 0;
Console.WriteLine($"Status: {status}, TotalElapsedSeconds: {totalElapsed}");

// Parse resultJson (escaped JSON string)
var resultJsonStr = outer["resultJson"]?.GetValue<string>() ?? throw new Exception("No resultJson");
var resultJson = JsonNode.Parse(resultJsonStr)!;

var resultsArray = resultJson["Results"]?.AsArray() ?? throw new Exception("No Results array");
Console.WriteLine($"Number of results: {resultsArray.Count}");

var workers = new List<(int workerIndex, double localVaR, double localCVaR, double[] topLosses)>();

foreach (var result in resultsArray)
{
    var outputDataStr = result!["OutputData"]?.GetValue<string>() ?? throw new Exception("No OutputData");
    var outputData = JsonNode.Parse(outputDataStr)!;

    var workerIndex = outputData["workerIndex"]?.GetValue<int>() ?? -1;
    var localVaR = outputData["localVaR"]?.GetValue<double>() ?? 0;
    var localCVaR = outputData["localCVaR"]?.GetValue<double>() ?? 0;
    var topLossesArr = outputData["topLosses"]?.AsArray() ?? throw new Exception($"No topLosses for worker {workerIndex}");
    var topLosses = topLossesArr.Select(v => v!.GetValue<double>()).ToArray();

    workers.Add((workerIndex, localVaR, localCVaR, topLosses));
    Console.WriteLine($"  Worker {workerIndex}: localVaR={localVaR:F6}, localCVaR={localCVaR:F6}, topLosses count={topLosses.Length}");
}

// Sort workers by index for clean display
workers.Sort((a, b) => a.workerIndex.CompareTo(b.workerIndex));

// Aggregate all topLoss values
Console.WriteLine("\nAggregating losses...");
var allLosses = new List<double>(workers.Sum(w => w.topLosses.Length));
foreach (var w in workers)
    allLosses.AddRange(w.topLosses);

Console.WriteLine($"Total topLoss values: {allLosses.Count}");

allLosses.Sort();

// Global VaR = sorted_losses[60000 - 20000] = sorted_losses[40000]
// 20 workers x 100,000 scenarios = 2,000,000 total; 1% = 20,000 tail
int totalCount = allLosses.Count;   // should be 60,000
int tailCount = 20000;              // 1% of 2,000,000
int varIndex = totalCount - tailCount; // 40,000

double globalVaR = allLosses[varIndex];

// Global CVaR = mean of top 20,000 values (indices 40000..59999)
double globalCVaR = allLosses.Skip(varIndex).Average();

// Local VaR stats
double localVarMean = workers.Average(w => w.localVaR);
double localVarStd = Math.Sqrt(workers.Average(w => Math.Pow(w.localVaR - localVarMean, 2)));

// Local CVaR stats
double localCVarMean = workers.Average(w => w.localCVaR);
double localCVarStd = Math.Sqrt(workers.Average(w => Math.Pow(w.localCVaR - localCVarMean, 2)));

double minLoss = allLosses[0];
double maxLoss = allLosses[^1];

// Print results
Console.WriteLine("\n====================================================");
Console.WriteLine("          VaR MONTE CARLO AGGREGATION RESULTS       ");
Console.WriteLine("====================================================");
Console.WriteLine();
Console.WriteLine($"Global 99% VaR  = {globalVaR:F6}");
Console.WriteLine($"Global 99% CVaR = {globalCVaR:F6}");
Console.WriteLine();
Console.WriteLine($"TotalElapsedSeconds = {totalElapsed}");
Console.WriteLine();
Console.WriteLine($"All 60,000 top-loss range: min = {minLoss:F6}, max = {maxLoss:F6}");
Console.WriteLine();
Console.WriteLine($"Local VaR  across workers: mean = {localVarMean:F6}, std = {localVarStd:F6}");
Console.WriteLine($"Local CVaR across workers: mean = {localCVarMean:F6}, std = {localCVarStd:F6}");
Console.WriteLine();
Console.WriteLine($"{"workerIndex",12} | {"localVaR",14} | {"localCVaR",14}");
Console.WriteLine(new string('-', 46));
foreach (var w in workers)
    Console.WriteLine($"{w.workerIndex,12} | {w.localVaR,14:F6} | {w.localCVaR,14:F6}");
Console.WriteLine();
Console.WriteLine("VaR index used: sorted_losses[{0}] (out of {1} total)", varIndex, totalCount);
Console.WriteLine("CVaR = mean of sorted_losses[{0}..{1}]", varIndex, totalCount - 1);
