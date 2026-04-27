using Bonsai;
using Bonsai.Expressions;
using AindBehaviorTelekinesisDataSchema;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

[TypeVisualizer(typeof(TrialOutcomeVisualizer))]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Visualizes trial outcomes as a bar chart where bar height = response time and color indicates success or failure.")]
public class TrialOutcomeVisualizerBuilder : SingleArgumentExpressionBuilder
{
    public TrialOutcomeVisualizerBuilder()
    {
        FontSize = 16.0f;
        YMax = 10.0;
        WindowSize = 50;
        RollingWindowSize = 20;
    }

    [Description("Font size for text rendering.")]
    public float FontSize { get; set; }

    [Description("Maximum response time shown on the Y axis (seconds).")]
    public double YMax { get; set; }

    [Description("Number of recent trials to display. 0 = show all.")]
    public int WindowSize { get; set; }

    [Description("Number of trials to use for the rolling average of response time and P(success).")]
    public int RollingWindowSize { get; set; }

    /// <inheritdoc/>
    public override Expression Build(IEnumerable<Expression> arguments)
    {
        var source = arguments.First();
        return Expression.Call(typeof(TrialOutcomeVisualizerBuilder), "Process", null, source);
    }

    static IObservable<TrialOutCome> Process(IObservable<TrialOutCome> source)
    {
        return source;
    }
}
