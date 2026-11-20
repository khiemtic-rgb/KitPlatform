#!/usr/bin/env dotnet-script
#r "nuget: Newtonsoft.Json, 13.0.3"

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// FAMIXA-EP01-PERMIT-001: Issue Official Execution Permit and Execute One Real EP01-SC01-SH01 CREATE_VIDEO
/// </summary>

var buildId = Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f");
var shotCode = "EP01-SC01-SH01";
var action = "CREATE_VIDEO";
var providerId = "runway";
var modelId = "gen4_turbo";

var baseUrl = "https://localhost:5290"; // Local dev server
var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

Console.WriteLine("=== FAMIXA-EP01-PERMIT-001 ===");
Console.WriteLine($"BuildId: {buildId}");
Console.WriteLine($"ShotCode: {shotCode}");
Console.WriteLine($"Action: {action}");
Console.WriteLine($"Provider: {providerId}");
Console.WriteLine($"Model: {modelId}");
Console.WriteLine();

// STEP 1: Issue Permit
Console.WriteLine("STEP 1: ISSUE PERMIT");
Console.WriteLine("Searching for Production Execution API endpoint...");

// Based on the codebase, permits are issued through ProductionReadinessAuthority
// which is not exposed via REST API directly. This is an internal service.
// We need to use the in-memory test harness or create a dedicated API endpoint.

Console.WriteLine("ERROR: No REST API endpoint found for direct permit issuance.");
Console.WriteLine("The permit system is internal to the Production OS.");
Console.WriteLine();
Console.WriteLine("REQUIRED ACTION:");
Console.WriteLine("1. Permits are issued through IProductionExecutionPermitStore");
Console.WriteLine("2. This is accessed via ProductionReadinessAuthority");
Console.WriteLine("3. No public REST endpoint exists for direct permit issuance");
Console.WriteLine();
Console.WriteLine("BLOCKER: Production OS execution requires either:");
Console.WriteLine("  A) A dedicated REST API endpoint for permit issuance");
Console.WriteLine("  B) Direct database INSERT into pack_content.production_execution_permit");
Console.WriteLine("  C) Execution through the test harness");
Console.WriteLine();
Console.WriteLine("RECOMMENDATION: Use SQL script to issue permit directly");
