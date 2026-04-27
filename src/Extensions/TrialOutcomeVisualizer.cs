using Bonsai.Design;
using Bonsai.Expressions;
using AindBehaviorTelekinesisDataSchema;
using AllenNeuralDynamics.Core.Design;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Forms;

public class TrialOutcomeVisualizer : BufferedVisualizer
{
    private const float MinPlotHeight = 100.0f;
    private const float InputWidth = 80.0f;
    private const float MarkerAlpha = 0.85f;
    private const float MarkerSize  = 8f;

    private static readonly Vector4 SuccessColor = new Vector4(0.1f, 0.55f, 0.15f, MarkerAlpha);
    private static readonly Vector4 FailureColor = new Vector4(0.65f, 0.08f, 0.08f, 0.25f);

    private float fontSize = 16.0f;
    private int windowSize = 50;
    private int rollingWindowSize = 20;

    private struct TrialRecord
    {
        public double ResponseTime;
        public bool IsSuccessful;
    }

    private readonly List<TrialRecord> trials = new List<TrialRecord>();
    private ImGuiControl imGuiCanvas;

    /// <inheritdoc/>
    public override void Show(object value) { }

    /// <inheritdoc/>
    protected override void ShowBuffer(IList<System.Reactive.Timestamped<object>> values)
    {
        foreach (var v in values)
        {
            if (!(v.Value is TrialOutCome)) continue;
            var outcome = (TrialOutCome)v.Value;

            bool wasNull = !outcome.ResponseTime.HasValue;
            trials.Add(new TrialRecord
            {
                ResponseTime = wasNull ? 0.0 : outcome.ResponseTime.Value,
                IsSuccessful = outcome.IsSuccessful
            });
        }

        base.ShowBuffer(values);
        if (imGuiCanvas != null) imGuiCanvas.Invalidate();
    }

    private void GetVisibleRange(out int start, out int count)
    {
        if (windowSize > 0 && trials.Count > windowSize)
        {
            start = trials.Count - windowSize;
            count = windowSize;
        }
        else
        {
            start = 0;
            count = trials.Count;
        }
    }

    /// <summary>
    /// Computes rolling average of response time for successful trials only.
    /// Returns NaN for positions where no successful trials exist in the window.
    /// </summary>
    private double[] ComputeRollingResponseTime(int start, int count)
    {
        var result = new double[count];
        for (int i = 0; i < count; i++)
        {
            int windowStart = Math.Max(0, i - rollingWindowSize + 1);
            double sum = 0.0;
            int n = 0;
            for (int j = windowStart; j <= i; j++)
            {
                var r = trials[start + j];
                if (r.IsSuccessful)
                {
                    sum += r.ResponseTime;
                    n++;
                }
            }
            result[i] = (n > 0) ? sum / n : double.NaN;
        }
        return result;
    }

    /// <summary>
    /// Computes rolling average of IsSuccessful (0-1).
    /// </summary>
    private double[] ComputeRollingSuccessRate(int start, int count)
    {
        var result = new double[count];
        for (int i = 0; i < count; i++)
        {
            int windowStart = Math.Max(0, i - rollingWindowSize + 1);
            int successes = 0;
            int total = i - windowStart + 1;
            for (int j = windowStart; j <= i; j++)
            {
                if (trials[start + j].IsSuccessful) successes++;
            }
            result[i] = (double)successes / total;
        }
        return result;
    }

    void StyleColors()
    {
        ImGui.StyleColorsLight();
        ImPlot.StyleColorsLight(ImPlot.GetStyle());
    }

    unsafe private void DrawChart()
    {
        ImGui.Text("Window:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(InputWidth);
        ImGui.InputInt("##window", ref windowSize);
        if (windowSize < 0) windowSize = 0;

        var availableSize = ImGui.GetContentRegionAvail();
        float plotHeight = Math.Max(availableSize.Y, MinPlotHeight);

        int start, count;
        GetVisibleRange(out start, out count);

        double xMin = start - 0.5;
        double xMax = (count == 0) ? 1.0 : start + count - 0.5;

        // Y axis driven by successful response times only; floor at 1.0
        double yMax = 1.0;
        for (int i = 0; i < count; i++)
        {
            var r = trials[start + i];
            if (r.IsSuccessful && r.ResponseTime > yMax)
                yMax = r.ResponseTime;
        }

        if (ImPlot.BeginPlot("Trial Outcomes", new Vector2(-1, plotHeight), ImPlotFlags.NoTitle))
        {
            ImPlot.SetupAxes("Trial #", "Response Time (s)");
            ImPlot.SetupAxisLimits(ImAxis.X1, xMin, xMax, ImPlotCond.Always);
            ImPlot.SetupAxisLimits(ImAxis.Y1, 0.0, yMax, ImPlotCond.Always);
            var axisBlue = new Vector4(0.1f, 0.1f, 0.8f, 1f);
            ImPlot.PushStyleColor(ImPlotCol.AxisText, axisBlue);
            ImPlot.SetupAxis(ImAxis.Y2, "P(success)", ImPlotAxisFlags.AuxDefault | ImPlotAxisFlags.Opposite);
            ImPlot.PopStyleColor();
            ImPlot.SetupAxisLimits(ImAxis.Y2, -0.05, 1.05, ImPlotCond.Always);
            ImPlot.SetupLegend(ImPlotLocation.North, ImPlotLegendFlags.Outside | ImPlotLegendFlags.Horizontal);

            // Partition visible trials into failures (full-height bars) and hits (circle markers)
            var failureXs = new List<double>();
            var successXs = new List<double>();
            var successYs = new List<double>();

            for (int i = 0; i < count; i++)
            {
                var record = trials[start + i];
                double x = start + i;
                if (record.IsSuccessful)
                {
                    successXs.Add(x);
                    successYs.Add(record.ResponseTime);
                }
                else
                {
                    failureXs.Add(x);
                }
            }

            // Failure bars span the full Y range
            if (failureXs.Count > 0)
            {
                double[] fxArr = failureXs.ToArray();
                double[] fyArr = new double[failureXs.Count];
                for (int i = 0; i < fyArr.Length; i++) fyArr[i] = yMax;
                ImPlot.SetNextFillStyle(FailureColor, 1f);
                ImPlot.SetNextLineStyle(FailureColor, 0f);
                fixed (double* pxs = fxArr)
                fixed (double* pys = fyArr)
                    ImPlot.PlotBars("Failure", pxs, pys, failureXs.Count, 0.8);
            }
            else
            {
                double* dummy = stackalloc double[1];
                dummy[0] = 0;
                ImPlot.SetNextFillStyle(FailureColor, 1f);
                ImPlot.PlotBars("Failure", dummy, dummy, 0, 0.8);
            }

            // Success circles at response time
            if (successXs.Count > 0)
            {
                double[] sxArr = successXs.ToArray();
                double[] syArr = successYs.ToArray();
                ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, MarkerSize, SuccessColor, 1.5f, SuccessColor);
                ImPlot.SetNextLineStyle(SuccessColor, 2f);
                fixed (double* pxs = sxArr)
                fixed (double* pys = syArr)
                    ImPlot.PlotLine("Success", pxs, pys, successXs.Count);
            }
            else
            {
                double* dummy = stackalloc double[1];
                dummy[0] = 0;
                ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, MarkerSize, SuccessColor, 1.5f, SuccessColor);
                ImPlot.SetNextLineStyle(SuccessColor, 2f);
                ImPlot.PlotLine("Success", dummy, dummy, 0);
            }

            // Rolling average response time (hits only)
            if (count > 0)
            {
                double[] avgRt = ComputeRollingResponseTime(start, count);
                int validCount = 0;
                for (int i = 0; i < count; i++)
                    if (!double.IsNaN(avgRt[i])) validCount++;

                if (validCount > 0)
                {
                    double[] lineXs = new double[validCount];
                    double[] lineYs = new double[validCount];
                    int vi = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (!double.IsNaN(avgRt[i]))
                        {
                            lineXs[vi] = start + i;
                            lineYs[vi] = avgRt[i];
                            vi++;
                        }
                    }
                    var rtLineColor = new Vector4(0.0f, 0.0f, 0.0f, 1f);
                    ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, 3f, rtLineColor, 1f, rtLineColor);
                    ImPlot.SetNextLineStyle(rtLineColor, 6f);
                    fixed (double* pxs = lineXs)
                    fixed (double* pys = lineYs)
                        ImPlot.PlotLine("Avg Response Time", pxs, pys, validCount);
                }

                // Rolling P(success) on Y2
                ImPlot.SetAxes(ImAxis.X1, ImAxis.Y2);
                double[] avgSr = ComputeRollingSuccessRate(start, count);
                double[] srXs = new double[count];
                double[] srYs = new double[count];
                for (int i = 0; i < count; i++)
                {
                    srXs[i] = start + i;
                    srYs[i] = avgSr[i];
                }
                var srLineColor = new Vector4(0.1f, 0.1f, 0.8f, 1f);
                ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, 3f, srLineColor, 1f, srLineColor);
                ImPlot.SetNextLineStyle(srLineColor, 6f);
                fixed (double* pxs = srXs)
                fixed (double* pys = srYs)
                    ImPlot.PlotLine("P(success)", pxs, pys, count);
                ImPlot.SetAxes(ImAxis.X1, ImAxis.Y1);
            }

            ImPlot.EndPlot();
        }
    }

    /// <inheritdoc/>
    public override void Load(IServiceProvider provider)
    {
        var context = (ITypeVisualizerContext)provider.GetService(typeof(ITypeVisualizerContext));
        var builder = ExpressionBuilder.GetVisualizerElement(context.Source).Builder as TrialOutcomeVisualizerBuilder;
        if (builder != null)
        {
            fontSize = builder.FontSize;
            windowSize = builder.WindowSize;
            rollingWindowSize = builder.RollingWindowSize;
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

            if (ImGui.Begin("TrialOutcomeVisualizer"))
            {
                DrawChart();
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
                    ImGuiP.DockBuilderDockWindow("TrialOutcomeVisualizer", dockId);
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
