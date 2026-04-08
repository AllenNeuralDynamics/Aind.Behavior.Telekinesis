﻿
using Bonsai.Expressions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using Bonsai;
using AindBehaviorTelekinesisDataSchema;


[TypeVisualizer(typeof(TrialTableVisualizer))]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Visualizes a table of recent Trial properties.")]
public class TrialTableVisualizerBuilder : SingleArgumentExpressionBuilder
{
    private uint history = 3;
    public uint History
    {
        get { return history; }
        set { history = value; }
    }

    private float fontSize = 16.0f;
    public float FontSize
    {
        get { return fontSize; }
        set { fontSize = value; }
    }

    /// <inheritdoc/>
    public override Expression Build(IEnumerable<Expression> arguments)
    {
        var source = arguments.First();

        return Expression.Call(typeof(TrialTableVisualizerBuilder), "Process", null, source);
    }

    static IObservable<Trial> Process(IObservable<Trial> source)
    {
        return source;
    }
}

