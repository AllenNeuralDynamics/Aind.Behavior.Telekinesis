using Bonsai;
using AllenNeuralDynamics.AindBehaviorServices.DataTypes;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

public interface IPlotter
{
    string EventName { get; set; }
}

public class ShadedAreaPlotter : IPlotter
{
    private string _eventName;
    private Color _color;
    private float _alpha;

    public ShadedAreaPlotter()
    {
        _eventName = "";
        _color = Color.CornflowerBlue;
        _alpha = 0.3f;
    }

    [Description("The software event name to filter on.")]
    public string EventName
    {
        get { return _eventName; }
        set { _eventName = value; }
    }

    [XmlIgnore]
    [Description("The color of the shaded area.")]
    public Color Color
    {
        get { return _color; }
        set { _color = value; }
    }

    [Browsable(false)]
    [XmlElement("Color")]
    public string ColorHtml
    {
        get { return ColorTranslator.ToHtml(Color); }
        set { try { Color = ColorTranslator.FromHtml(value); } catch { } }
    }

    [Description("The transparency of the shaded area (0.0 to 1.0).")]
    public float Alpha
    {
        get { return _alpha; }
        set { _alpha = value; }
    }
}

public class PointPlotter : IPlotter
{
    private string _eventName;
    private Color _color;
    private float _yPosition;
    private float _markerSize;
    private ImPlotMarker _marker;

    public PointPlotter()
    {
        _eventName = "";
        _color = Color.Red;
        _yPosition = 0.5f;
        _markerSize = 6.0f;
        _marker = ImPlotMarker.Circle;
    }

    [Description("The software event name to filter on.")]
    public string EventName
    {
        get { return _eventName; }
        set { _eventName = value; }
    }

    [XmlIgnore]
    [Description("The color of the marker.")]
    public Color Color
    {
        get { return _color; }
        set { _color = value; }
    }

    [Browsable(false)]
    [XmlElement("Color")]
    public string ColorHtml
    {
        get { return ColorTranslator.ToHtml(Color); }
        set { try { Color = ColorTranslator.FromHtml(value); } catch { } }
    }

    [Description("The fixed Y position of the marker (0.0 to 1.0).")]
    public float YPosition
    {
        get { return _yPosition; }
        set { _yPosition = value; }
    }

    [Description("The size of the marker in pixels.")]
    public float MarkerSize
    {
        get { return _markerSize; }
        set { _markerSize = value; }
    }

    [Description("The marker style.")]
    public ImPlotMarker Marker
    {
        get { return _marker; }
        set { _marker = value; }
    }
}

[Combinator]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Renders software events as shaded areas and/or point markers inside an ImPlot window on each frame.")]
public class SoftwareEventVisualizer
{
    private const float MinPlotHeight = 100.0f;
    private const double YAxisMin = 0.0;
    private const double YAxisMax = 1.0;
    private const float InputWidth = 80.0f;

    private bool visible = true;
    public bool Visible { get { return visible; } set { visible = value; } }

    private float fontSize = 16.0f;
    public float FontSize { get { return fontSize; } set { fontSize = value; } }

    private float timeWindow = 30.0f;
    public float TimeWindow { get { return timeWindow; } set { timeWindow = value; } }

    private List<ShadedAreaPlotter> shadedAreaPlotters = new List<ShadedAreaPlotter>();
    public List<ShadedAreaPlotter> ShadedAreaPlotters { get { return shadedAreaPlotters; } set { shadedAreaPlotters = value; } }

    private List<PointPlotter> pointPlotters = new List<PointPlotter>();
    public List<PointPlotter> PointPlotters { get { return pointPlotters; } set { pointPlotters = value; } }

    private string trialBreakEventName = "";
    [Description("Software event name that triggers a new trial row. Leave empty to disable trial breaks.")]
    public string TrialBreakEventName { get { return trialBreakEventName; } set { trialBreakEventName = value; } }

    private int maxTrials;
    [Description("Maximum number of trial rows to display. When exceeded, only the last N trials are shown. 0 = show all.")]
    public int MaxTrials { get { return maxTrials; } set { maxTrials = value; } }

    private readonly object bufferLock = new object();
    private readonly Dictionary<string, List<EventRecord>> eventHistory = new Dictionary<string, List<EventRecord>>();
    private double latestTimestamp = 0;
    private readonly List<double> trialBreaks = new List<double>();
    private DateTimeOffset startTime;

    private bool HasTrialBreaks { get { return !string.IsNullOrEmpty(TrialBreakEventName); } }
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

    public IObservable<Unit> Process<TTick>(IObservable<TTick> frames, IObservable<SoftwareEvent> data)
    {
        return Observable.Create<Unit>(observer =>
        {
            startTime = DateTimeOffset.Now;

            var dataSub = data.Subscribe(
                softwareEvent =>
                {
                    if (softwareEvent == null) return;
                    string name = softwareEvent.Name;
                    if (string.IsNullOrEmpty(name)) return;

                    double timestamp = (DateTimeOffset.Now - startTime).TotalSeconds;

                    lock (bufferLock)
                    {
                        if (HasTrialBreaks && name == TrialBreakEventName)
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

                        CleanupOldEvents();
                    }
                },
                observer.OnError);

            var frameSub = frames.SubscribeSafe(Observer.Create<TTick>(
                _ =>
                {
                    unsafe { ImGui.GetIO().Handle->ConfigErrorRecoveryEnableAssert = 0; }

                    if (Visible)
                    {
                        lock (bufferLock)
                        {
                            ImGui.StyleColorsLight();
                            ImPlot.StyleColorsLight(ImPlot.GetStyle());
                            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
                            var childFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                            if (ImGui.BeginChild("##SoftwareEventVisualizer", new Vector2(0, 0), ImGuiChildFlags.None, childFlags))
                            {
                                ImGui.PushFont(ImGui.GetFont(), FontSize);
                                DrawEvents();
                                ImGui.PopFont();
                            }
                            ImGui.EndChild();
                            ImGui.PopStyleVar();
                        }
                    }

                    observer.OnNext(Unit.Default);
                },
                observer.OnError,
                observer.OnCompleted));

            return new CompositeDisposable(dataSub, frameSub);
        });
    }

    /// <summary>
    /// Removes old event records outside the visible window.
    /// Keeps the last event before the window for shaded area continuity.
    /// Must be called with <see cref="bufferLock"/> held.
    /// </summary>
    private void CleanupOldEvents()
    {
        latestTimestamp = (DateTimeOffset.Now - startTime).TotalSeconds;

        double cutoffTime = latestTimestamp - TimeWindow;

        if (HasTrialBreaks && MaxTrials > 0 && trialBreaks.Count > 0)
        {
            int firstVisible = Math.Max(0, TrialCount - MaxTrials);
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
        else if (MaxTrials > 0 && total > MaxTrials)
        {
            firstVisible = total - MaxTrials;
            numVisible = MaxTrials;
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

        foreach (var config in ShadedAreaPlotters)
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

        merged.Sort(delegate (ShadedSegment a, ShadedSegment b)
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
        if (ShadedAreaPlotters.Count == 0) return;

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

    /// <summary>
    /// Must be called with <see cref="bufferLock"/> held.
    /// </summary>
    private void DrawEvents()
    {
        latestTimestamp = (DateTimeOffset.Now - startTime).TotalSeconds;

        ImGui.Text("Time Window (s):");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(InputWidth);
        float timeWindowValue = TimeWindow;
        ImGui.InputFloat("##timewindow", ref timeWindowValue);
        TimeWindow = Math.Max(1.0f, timeWindowValue);

        var availableSize = ImGui.GetContentRegionAvail();
        float plotHeight = Math.Max(availableSize.Y, MinPlotHeight);

        double plotTMin = -(double)TimeWindow;
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

            foreach (var config in PointPlotters)
            {
                DrawPointMarkers(config, plotTMin, plotTMax);
            }

            ImPlot.EndPlot();
        }
    }
}
