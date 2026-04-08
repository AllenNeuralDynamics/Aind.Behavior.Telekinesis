using Bonsai;
using Bonsai.Expressions;
using AllenNeuralDynamics.AindBehaviorServices.DataTypes;
using Hexa.NET.ImPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
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

[TypeVisualizer(typeof(SoftwareEventVisualizer))]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Visualizes software events as shaded areas and/or point markers over time.")]
public class SoftwareEventVisualizerBuilder : SingleArgumentExpressionBuilder
{
    private float _fontSize;
    private float _timeWindow;

    public SoftwareEventVisualizerBuilder()
    {
        _fontSize = 16.0f;
        _timeWindow = 30.0f;
        ShadedAreaPlotters = new List<ShadedAreaPlotter>();
        PointPlotters = new List<PointPlotter>();
    }

    [Description("Font size for text rendering.")]
    public float FontSize
    {
        get { return _fontSize; }
        set { _fontSize = value; }
    }

    [Description("Number of seconds to display on the X axis.")]
    public float TimeWindow
    {
        get { return _timeWindow; }
        set { _timeWindow = value; }
    }

    [Description("Shaded area plotter configurations.")]
    public List<ShadedAreaPlotter> ShadedAreaPlotters { get; set; }

    [Description("Point plotter configurations.")]
    public List<PointPlotter> PointPlotters { get; set; }

    [Description("Software event name that triggers a new trial row. Leave empty to disable trial breaks.")]
    public string TrialBreakEventName { get; set; }

    [Description("Maximum number of trial rows to display. When exceeded, only the last N trials are shown. 0 = show all.")]
    public int MaxTrials { get; set; }

    /// <inheritdoc/>
    public override Expression Build(IEnumerable<Expression> arguments)
    {
        var source = arguments.First();
        return Expression.Call(typeof(SoftwareEventVisualizerBuilder), "Process", null, source);
    }

    static IObservable<SoftwareEvent> Process(IObservable<SoftwareEvent> source)
    {
        return source;
    }
}
