using System.Globalization;
using System.Text;
using WireWarp.Frontend.Shared.Conversion;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.File;

public static class ReportFile
{
    public static string PathName =>
        Path.ChangeExtension(Access.Instance.WorldPathName, ".md");

    private static string TempPathName => PathName + ".tmp";

    public static void Write()
    {
        var path = PathName;
        var temp = TempPathName;

        try
        {
            System.IO.File.WriteAllText(temp, Build());
            System.IO.File.Move(temp, path, overwrite: true);
        }
        catch (Exception e)
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            throw new Exception($"Failed to write report {path}: {e.Message}", e);
        }
    }

    private static string Build()
    {
        var sb = new StringBuilder();

        var hash = Report.Hash is { Length: > 0 } h ? Convert.ToHexString(h) : "-";
        var total = Report.Stages.Count > 0 ? Report.Stages.Sum(s => s.time) : 0;

        sb.AppendLine("# WireWarp Wiring Report");
        sb.AppendLine();
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| World | `{Report.WorldPath}` |");
        sb.AppendLine($"| Hash | `{hash}` |");
        sb.AppendLine($"| Result | {(Report.Success ? "OK" : "FAILED")} |");
        sb.AppendLine($"| Total time | {total / 1000:F2}s |");
        sb.AppendLine();

        sb.AppendLine("## Stages");
        sb.AppendLine();
        sb.AppendLine("| Stage | Time |");
        sb.AppendLine("|---|---|");
        if (Report.Stages.Count == 0)
            sb.AppendLine("| - | - |");
        else
            foreach (var (name, ms) in Report.Stages)
                sb.AppendLine($"| {name} | {ms / 1000:F2}s ({Percent(ms, total)}) |");
        sb.AppendLine();

        sb.AppendLine("## Components");
        sb.AppendLine();
        sb.AppendLine("| Component | Count | Type |");
        sb.AppendLine("|---|---|---|");
        if (Report.Components.Count == 0)
            sb.AppendLine("| - | - | - |");
        else
            foreach (var (component, types) in Report.Components)
            {
                var count = types.Values.Sum();
                sb.AppendLine($"| {component} | {count} | {FormatTypes(types)} |");
            }
        sb.AppendLine();

        WriteWires(sb);
        WriteGates(sb);
        WritePorts(sb);

        if (Report.SkippedFaultGates.Count > 0)
        {
            sb.AppendLine("## Skipped faulty gates (non-fatal)");
            sb.AppendLine();
            sb.AppendLine($"Ignored {Report.SkippedFaultGates.Count} faulty-looking gates without a usable fault-lamp stack. Their world tiles were not edited; these gates do not emit accelerated pulses.");
            sb.AppendLine();
            sb.AppendLine("| # | Coordinate |");
            sb.AppendLine("|---|---|");
            for (var i = 0; i < Report.SkippedFaultGates.Count; i++)
            {
                var (x, y) = Report.SkippedFaultGates[i];
                sb.AppendLine($"| {i + 1} | ({x}, {y}) |");
            }
            sb.AppendLine();
        }

        if (Report.Errors.Count > 0)
        {
            sb.AppendLine("## Errors");
            sb.AppendLine();
            sb.AppendLine("| # | Message |");
            sb.AppendLine("|---|---|");
            for (var i = 0; i < Report.Errors.Count; i++)
                sb.AppendLine($"| {i + 1} | {Report.Errors[i]} |");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void WriteWires(StringBuilder sb)
    {
        WriteSection(sb, "Wires", Types("Wire"), "Wire",
        [
            ("Fanin", Reporter.WiresFanin),
            ("InputPort", Reporter.WiresInputPort),
            ("Normal gate", Reporter.WiresNormalGate),
            ("Fault gate", Reporter.WiresFaultGate),
            ("Fanout", Reporter.WiresFanout),
            ("OutputPort", Reporter.WiresOutputPort),
            ("Normal lamp", Reporter.WiresNormalLamp),
            ("Fault lamp", Reporter.WiresFaultLamp),
        ]);
    }

    private static void WriteGates(StringBuilder sb)
    {
        var gateTypes = Types("Gate");
        var normal = gateTypes.Where(kv => kv.Key != "Fault")
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var fault = gateTypes.Where(kv => kv.Key == "Fault")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        WriteSection(sb, "Normal Gates", normal, "Gate",
        [
            ("Lamp wires", Reporter.NormalGatesLampWires),
            ("Lamps", Reporter.NormalGatesLamps),
            ("Gate wires", Reporter.NormalGatesGateWires),
        ]);

        WriteSection(sb, "Fault Gates", fault, "FaultGate",
        [
            ("Fault lamp wires", Reporter.FaultGatesFaultLampWires),
            ("Fault lamps", Reporter.FaultGatesFaultLamps),
            ("Normal lamp wires", Reporter.FaultGatesNormalLampWires),
            ("Normal lamps", Reporter.FaultGatesNormalLamps),
            ("Gate wires", Reporter.FaultGatesGateWires),
        ]);
    }

    private static void WritePorts(StringBuilder sb)
    {
        WriteSection(sb, "Input Ports", Types("InputPort"), "InputPort",
            [("Wires", Reporter.InputPortsWires)]);

        WriteSection(sb, "Output Ports", Types("OutputPort"), "OutputPort",
            [("Wires", Reporter.OutputPortsWires)]);
    }

    private static void WriteSection(StringBuilder sb, string title, Dictionary<string, int> types,
        string pruned, (string Label, string Key)[] rows)
    {
        var count = types.Values.Sum();

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Count | {count} |");
        sb.AppendLine($"| Pruned | {Report.Pruned.GetValueOrDefault(pruned)} |");
        sb.AppendLine($"| Type | {FormatTypes(types)} |");
        foreach (var (label, key) in rows)
            sb.AppendLine($"| {label} | {Histogram(key)} |");
        sb.AppendLine();
    }

    private static Dictionary<string, int> Types(string name) =>
        Report.Components.TryGetValue(name, out var types) ? types : [];

    private static string FormatTypes(Dictionary<string, int> types)
    {
        if (types.Count == 0) return "-";

        var total = types.Values.Sum();
        return string.Join(", ", types
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}×{kv.Value} ({Percent(kv.Value, total)})"));
    }

    private static string Histogram(string name) =>
        Report.Histograms.TryGetValue(name, out var histogram) ? FormatHistogram(histogram) : "-";

    private static string FormatHistogram(Dictionary<int, int> histogram)
    {
        if (histogram.Count == 0) return "-";

        var total = histogram.Values.Sum();
        return string.Join(", ", histogram
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => kv.Key)
            .Select(kv => $"{kv.Key}->{kv.Value} ({Percent(kv.Value, total)})"));
    }

    private static string Percent(double value, double total) =>
        total > 0
            ? $"{(value * 100.0 / total).ToString("F2", CultureInfo.InvariantCulture)}%"
            : "-";
}
