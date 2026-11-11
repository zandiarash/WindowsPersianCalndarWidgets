#!/usr/bin/env dotnet
// Expands the widget's Adaptive Card templates with sample data, exactly like the Widgets host does,
// and fails on unresolved ${...} bindings. Usage: dotnet run tools/expand-cards.cs -- <previewFolder>
#:package AdaptiveCards.Templating@2.0.5
#:property ManagePackageVersionsCentrally=false
using System.Text.Json;
using AdaptiveCards.Templating;

var folder = args[0];
var failed = false;
foreach (var size in new[] { "small", "medium", "large" })
{
    var template = new AdaptiveCardTemplate(File.ReadAllText(Path.Combine(folder, $"{size}.template.json")));
    var card = template.Expand(new EvaluationContext { Root = File.ReadAllText(Path.Combine(folder, $"{size}.data.json")) });
    var path = Path.Combine(folder, $"{size}.card.json");
    File.WriteAllText(path, JsonSerializer.Serialize(JsonDocument.Parse(card), new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    var unresolved = card.Contains("${");
    failed |= unresolved;
    Console.WriteLine($"{size,-7} {(unresolved ? "UNRESOLVED BINDINGS" : "ok")}  ({card.Length:N0} chars) → {path}");
}
return failed ? 1 : 0;
