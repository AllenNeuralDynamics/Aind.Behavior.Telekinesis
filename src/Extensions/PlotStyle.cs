using Bonsai;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using System;
using System.ComponentModel;
using System.Reactive.Linq;

/// <summary>
/// Pushes font size and line width for all downstream plot operators before each
/// notification, and pops them after downstream processing completes.
/// </summary>
[Combinator]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Applies font size and line width style settings to all downstream plot operators for each plot frame.")]
public class PlotStyle
{
    private float fontSize = 20f;
    [Description("Font size to use when rendering series labels.")]
    public float FontSize
    {
        get { return fontSize; }
        set { fontSize = value; }
    }

    private float lineWidth = 1f;
    [Description("Width of plotted lines in pixels.")]
    public float LineWidth
    {
        get { return lineWidth; }
        set { lineWidth = value; }
    }

    public IObservable<T> Process<T>(IObservable<T> source)
    {
        return Observable.Create<T>(observer =>
            source.Subscribe(
                value =>
                {
                    PushStyle();
                    observer.OnNext(value);
                    PopStyle();
                },
                observer.OnError,
                observer.OnCompleted));
    }

    private unsafe void PushStyle()
    {
        ImGui.GetIO().Handle->ConfigErrorRecoveryEnableAssert = 0;
        ImGui.PushFont(ImGui.GetFont(), FontSize);
        ImPlot.PushStyleVar(ImPlotStyleVar.LineWeight, LineWidth);
    }

    private void PopStyle()
    {
        ImPlot.PopStyleVar();
        ImGui.PopFont();
    }
}
