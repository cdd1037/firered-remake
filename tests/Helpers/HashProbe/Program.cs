using System.Text.Json;
using FireRed.Tests.Processes;

Console.WriteLine(JsonSerializer.Serialize(HashProbeScenario.HashProbe()));
return Environment.Version.Major == 10 ? 0 : 1;
