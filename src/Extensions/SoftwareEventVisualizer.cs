using Bonsai.Design;
using Bonsai.Expressions;
using AllenNeuralDynamics.AindBehaviorServices.DataTypes;
using AllenNeuralDynamics.Core.Design;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class SoftwareEventVisualizer : BufferedVisualizer
{
    private const float MinPlotHeight = 100.0f;
    private const double YAxisMin = 0.0;
    private const double YAxisMax = 1.0;
    private const float InputWidth = 80.0f;

    private float fontSize = 16.0f;
    private float timeWindow = 30.0f;

    private List<ShadedAreaPlotter> shadedAreaPlotters = new List<ShadedAreaPlotter>();
    private List<PointPlotter> pointPlotters = new List<PointPlotter>();

    private ImGuiControl imGuiCanvas;
    private DateTimeOffset startTime;

    private readonly Dictionary<string, List<EventRecord>> eventHistory = new Dictionary<string, List<EventRecord>>();
    private double latestTimestamp = 0;
    private string trialBreakEventName = "";
    private int maxTrials = 0;
    private readonly List<double> trialBreaks = new List<double>();

    private bool HasTrialBreaks { get { return !string.IsNullOrEmpty(trialBreakEventName); } }
    private int TrialCount { get { return HasTrialBreaks ? trialBreaks.Count + 1 : 1; } }

    private struct EventRecord
    {
        public double Timestamp;
    }

    private struct ShadedSegment
    {
        public double Timestamp;
        public ShadedAreaPlotter Config;
    }

    /// <inheritdoc/>
    public override void Show(object value)
    {
    }

    /// <inheritdoc/>
    protected override void ShowBuffer(IList<System.Reactive.Timestamped<object>> values)
    {
        foreach (var v in values)
        {
            if (!(v.Value is SoftwareEvent)) continue;
            var softwareEvent = (SoftwareEvent)v.Value;

            double timestamp = (v.Timestamp - startTime).TotalSeconds;

            string name = softwareEvent.Name;
            if (string.IsNullOrEmpty(name)) continue;

            if (HasTrialBreaks && name == trialBreakEventName)
            {
                trialBreaks.Add(timestamp);
            }

            List<EventRecord> records;
            if (!eventHistory.TryGetValue(name, out records))
            {
                records = new List<EventRecord>();
                eventHistory[name] = records;
            }
            records.Add(new EventRecord { Timestamp = timestamp });
        }
        
        CleanupOldEvents();
        
        base.ShowBuffer(values);
        if (imGuiCanvas != null) imGuiCanvas.Invalidate();
    }

    /// <summary>
    /// Removes old event records outside the visible window.
    /// Keeps the last event before the window for shaded area continuity.
    /// </summary>
    private void CleanupOldEvents()
    {
        latestTimestamp = (DateTimeOffset.Now - startTime).TotalSeconds;
        
        double cutoffTime = latestTimestamp - timeWindow;
        
        if (HasTrialBreaks && maxTrials > 0 && trialBreaks.Count > 0)
        {
            int firstVisible = Math.Max(0, TrialCount - maxTrials);
            if (firstVisible > 0 && firstVisible <= trialBreaks.Count)
            {
                double trialCutoff = trialBreaks[firstVisible - 1];
                cutoffTime = Math.Min(cutoffTime, trialCutoff);
            }
        }
        
        foreach (var kvp in eventHistory)
        {
            var records = kvp.Value;
            if (records.Count <= 1) continue;
            
            int keepFromIndex = -1;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (records[i].Timestamp < cutoffTime)
                {
                    keepFromIndex = i;
                    break;
                }
            }
            
            if (keepFromIndex > 0)
            {
                records.RemoveRange(0, keepFromIndex);
            }
        }
        
        if (HasTrialBreaks && trialBreaks.Count > 1)
        {
            int keepFromIndex = -1;
            for (int i = trialBreaks.Count - 1; i >= 0; i--)
            {
                if (trialBreaks[i] < cutoffTime)
                {
                    keepFromIndex = i;
                    break;
                }
            }
            if (keepFromIndex > 0)
            {
                trialBreaks.RemoveRange(0, keepFromIndex);
            }
        }
    }

    private static Vector4 ToVec4(Color color)
    {
        return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    void StyleColors()
    {
        ImGui.StyleColorsLight();
        ImPlot.StyleColorsLight(ImPlot.GetStyle());
    }

    /// <summary>
    /// Converts absolute timestamp to plot-relative time where 0 = now.
    /// </summary>
    private double ToPlotTime(double timestamp)
    {
        return timestamp - latestTimestamp;
    }

    /// <summary>
    /// Returns which trial a timestamp belongs to.
    /// </summary>
    private int GetTrialIndex(double timestamp)
    {
        if (!HasTrialBreaks || trialBreaks.Count == 0) return 0;
        for (int i = trialBreaks.Count - 1; i >= 0; i--)
        {
            if (timestamp >= trialBreaks[i])
                return i + 1;
        }
        return 0;
    }

    /// <summary>
    /// Computes visible trial range based on MaxTrials rolling window.
    /// </summary>
    private void GetVisibleTrialRange(out int firstVisible, out int numVisible)
    {
        int total = TrialCount;
        if (!HasTrialBreaks)
        {
            firstVisible = 0;
            numVisible = 1;
        }
        else if (maxTrials > 0 && total > maxTrials)
        {
            firstVisible = total - maxTrials;
            numVisible = maxTrials;
        }
        else
        {
            firstVisible = 0;
            numVisible = total;
        }
    }

    /// <summary>
    /// Builds merged timeline of shaded area events, sorted by timestamp.
    /// </summary>
    private List<ShadedSegment> BuildMergedTimeline()
    {
        var merged = new List<ShadedSegment>();

        foreach (var config in shadedAreaPlotters)
        {
            List<EventRecord> records;
            if (!eventHistory.TryGetValue(config.EventName, out records))
                continue;

            for (int i = 0; i < records.Count; i++)
            {
                merged.Add(new ShadedSegment
                {
                    Timestamp = records[i].Timestamp,
                    Config = config
                });
            }
        }

        merged.Sort(delegate(ShadedSegment a, ShadedSegment b)
        {
            return a.Timestamp.CompareTo(b.Timestamp);
        });

        return merged;
    }

    /// <summary>
    /// Draws a single shaded rectangle in a given trial row.
    /// </summary>
    unsafe private void DrawShadedRect(ShadedAreaPlotter config, double tStart, double tEnd, int trialIndex)
    {
        double x0 = ToPlotTime(tStart);
        double x1 = ToPlotTime(tEnd);
        double yLow = HasTrialBreaks ? (double)trialIndex : 0.0;
        double yHigh = HasTrialBreaks ? (double)(trialIndex + 1) : 1.0;

        var color = ToVec4(config.Color);
        ImPlot.SetNextLineStyle(color, 0f);
        ImPlot.SetNextFillStyle(color, config.Alpha);

        fixed (double* xs = new double[] { x0, x1 })
        fixed (double* ysL = new double[] { yLow, yLow })
        fixed (double* ysH = new double[] { yHigh, yHigh })
        {
            ImPlot.PlotShaded(config.EventName, xs, ysL, ysH, 2);
        }
    }

    /// <summary>
    /// Draws shaded areas as mutually exclusive regions, split at trial boundaries when active.
    /// </summary>
    unsafe private void DrawAllShadedAreas(double plotTMin, double plotTMax)
    {
        if (shadedAreaPlotters.Count == 0) return;

        var timeline = BuildMergedTimeline();
        if (timeline.Count == 0) return;

        double absMin = latestTimestamp + plotTMin;
        double absMax = latestTimestamp + plotTMax;

        int firstVisible, numVisible;
        GetVisibleTrialRange(out firstVisible, out numVisible);
        int lastVisible = firstVisible + numVisible;

        // Find the last event at or before the visible window start
        int startIdx = -1;
        for (int i = timeline.Count - 1; i >= 0; i--)
        {
            if (timeline[i].Timestamp <= absMin)
            {
                startIdx = i;
                break;
            }
        }

        if (startIdx < 0 && timeline.Count > 0 && timeline[0].Timestamp < absMax)
            startIdx = 0;
        if (startIdx < 0) return;

        for (int i = startIdx; i < timeline.Count; i++)
        {
            var segment = timeline[i];
            double segStart = Math.Max(segment.Timestamp, absMin);
            double segEnd = (i + 1 < timeline.Count) ? timeline[i + 1].Timestamp : absMax;

            if (segStart >= absMax) break;
            segEnd = Math.Min(segEnd, absMax);

            if (HasTrialBreaks)
            {
                int startTrial = GetTrialIndex(segStart);
                double currentStart = segStart;
                int currentTrial = startTrial;

                while (currentStart < segEnd)
                {
                    double trialEnd = (currentTrial < trialBreaks.Count)
                        ? trialBreaks[currentTrial]
                        : double.MaxValue;
                    double currentEnd = Math.Min(trialEnd, segEnd);

                    if (currentTrial >= firstVisible && currentTrial < lastVisible)
                    {
                        DrawShadedRect(segment.Config, currentStart, currentEnd, currentTrial);
                    }

                    currentStart = currentEnd;
                    currentTrial++;
                }
            }
            else
            {
                DrawShadedRect(segment.Config, segStart, segEnd, 0);
            }
        }
    }

    /// <summary>
    /// Draws scatter markers for a PointPlotter, offset by trial row when active.
    /// </summary>
    unsafe private void DrawPointMarkers(PointPlotter config, double plotTMin, double plotTMax)
    {
        List<EventRecord> records;
        if (!eventHistory.TryGetValue(config.EventName, out records) || records.Count == 0)
            return;

        double absMin = latestTimestamp + plotTMin;
        double absMax = latestTimestamp + plotTMax;

        int firstVisible, numVisible;
        GetVisibleTrialRange(out firstVisible, out numVisible);
        int lastVisible = firstVisible + numVisible;

        var xsList = new List<double>();
        var ysList = new List<double>();

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].Timestamp < absMin) continue;
            if (records[i].Timestamp > absMax) continue;

            if (HasTrialBreaks)
            {
                int trial = GetTrialIndex(records[i].Timestamp);
                if (trial < firstVisible || trial >= lastVisible) continue;
                xsList.Add(ToPlotTime(records[i].Timestamp));
                ysList.Add((double)trial + (double)config.YPosition);
            }
            else
            {
                xsList.Add(ToPlotTime(records[i].Timestamp));
                ysList.Add((double)config.YPosition);
            }
        }

        if (xsList.Count == 0) return;

        var color = ToVec4(config.Color);
        ImPlot.SetNextMarkerStyle(config.Marker, config.MarkerSize, color, 1.5f, color);
        ImPlot.SetNextLineStyle(color, 0f);

        var xArr = xsList.ToArray();
        var yArr = ysList.ToArray();

        fixed (double* xs = xArr)
        fixed (double* ys = yArr)
        {
            ImPlot.PlotScatter(config.EventName, xs, ys, xArr.Length);
        }
    }

    /// <summary>
    /// Sets up Y axis ticks with trial labels at row centers.
    /// </summary>
    unsafe private void SetupTrialAxisTicks(int firstVisibleTrial, int numVisibleTrials)
    {
        if (numVisibleTrials <= 0) return;

        var positions = new double[numVisibleTrials];
        var labelData = new byte[numVisibleTrials][];

        for (int t = 0; t < numVisibleTrials; t++)
        {
            int trialNum = firstVisibleTrial + t;
            positions[t] = trialNum + 0.5;
            labelData[t] = System.Text.Encoding.UTF8.GetBytes(trialNum.ToString() + '\0');
        }

        var handles = new GCHandle[numVisibleTrials];
        var ptrs = new IntPtr[numVisibleTrials];

        try
        {
            for (int t = 0; t < numVisibleTrials; t++)
            {
                handles[t] = GCHandle.Alloc(labelData[t], GCHandleType.Pinned);
                ptrs[t] = handles[t].AddrOfPinnedObject();
            }

            fixed (double* posPtr = positions)
            fixed (IntPtr* labelPtrs = ptrs)
            {
                ImPlot.SetupAxisTicks(ImAxis.Y1, posPtr, numVisibleTrials, (byte**)labelPtrs, false);
            }
        }
        finally
        {
            for (int t = 0; t < numVisibleTrials; t++)
            {
                if (handles[t].IsAllocated) handles[t].Free();
            }
        }
    }

    private void DrawEvents()
    {
        latestTimestamp = (DateTimeOffset.Now - startTime).TotalSeconds;

        ImGui.Text("Time Window (s):");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(InputWidth);
        ImGui.InputFloat("##timewindow", ref timeWindow);
        if (timeWindow < 1.0f) timeWindow = 1.0f;

        var availableSize = ImGui.GetContentRegionAvail();
        float plotHeight = Math.Max(availableSize.Y, MinPlotHeight);

        double plotTMin = -(double)timeWindow;
        double plotTMax = 0.0;

        int firstVisible, numVisible;
        GetVisibleTrialRange(out firstVisible, out numVisible);

        double yMin = HasTrialBreaks ? (double)firstVisible : YAxisMin;
        double yMax = HasTrialBreaks ? (double)(firstVisible + numVisible) : YAxisMax;

        ImPlot.SetNextAxesLimits(plotTMin, plotTMax, yMin, yMax, ImPlotCond.Always);
        if (ImPlot.BeginPlot("Software Events", new Vector2(-1, plotHeight), ImPlotFlags.NoTitle))
        {
            ImPlot.SetupAxes("Time (s)", HasTrialBreaks ? "Trial" : "Value");
            ImPlot.SetupAxisLimits(ImAxis.Y1, yMin, yMax, ImPlotCond.Always);
            ImPlot.SetupLegend(ImPlotLocation.North, ImPlotLegendFlags.Outside | ImPlotLegendFlags.Horizontal);

            if (HasTrialBreaks && numVisible > 0)
            {
                SetupTrialAxisTicks(firstVisible, numVisible);
            }

            DrawAllShadedAreas(plotTMin, plotTMax);

            foreach (var config in pointPlotters)
            {
                DrawPointMarkers(config, plotTMin, plotTMax);
            }

            ImPlot.EndPlot();
        }
    }

    /// <inheritdoc/>
    public override void Load(IServiceProvider provider)
    {
        var context = (ITypeVisualizerContext)provider.GetService(typeof(ITypeVisualizerContext));
        var builder = ExpressionBuilder.GetVisualizerElement(context.Source).Builder as SoftwareEventVisualizerBuilder;
        if (builder != null)
        {
            fontSize = builder.FontSize;
            timeWindow = builder.TimeWindow;
            shadedAreaPlotters = builder.ShadedAreaPlotters ?? new List<ShadedAreaPlotter>();
            pointPlotters = builder.PointPlotters ?? new List<PointPlotter>();
            trialBreakEventName = builder.TrialBreakEventName ?? "";
            maxTrials = builder.MaxTrials;
        }

        if (startTime == default(DateTimeOffset))
        {
            startTime = DateTimeOffset.Now;
        }

        imGuiCanvas = new ImGuiControl();
        imGuiCanvas.Dock = DockStyle.Fill;
        imGuiCanvas.Render += (sender, e) =>
        {
            var dockspaceId = ImGui.DockSpaceOverViewport(
                0,
                ImGui.GetMainViewport(),
                ImGuiDockNodeFlags.AutoHideTabBar | ImGuiDockNodeFlags.NoUndocking);

            StyleColors();
            ImGui.PushFont(ImGui.GetFont(), fontSize);

            if (ImGui.Begin("SoftwareEventVisualizer"))
            {
                DrawEvents();
            }

            ImGui.End();
            ImGui.PopFont();
            var centralNode = ImGuiP.DockBuilderGetCentralNode(dockspaceId);
            if (!ImGui.IsWindowDocked() && !centralNode.IsNull)
            {
                unsafe
                {
                    var handle = centralNode.Handle;
                    uint dockId = handle->ID;
                    ImGuiP.DockBuilderDockWindow("SoftwareEventVisualizer", dockId);
                }
            }
        };

        var visualizerService = (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
        if (visualizerService != null)
        {
            visualizerService.AddControl(imGuiCanvas);
        }
    }

    /// <inheritdoc/>
    public override void Unload()
    {
        if (imGuiCanvas != null)
        {
            imGuiCanvas.Dispose();
            imGuiCanvas = null;
        }
    }
}
