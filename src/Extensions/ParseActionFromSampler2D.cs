using Bonsai;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Bonsai.Harp;
using AindBehaviorTelekinesisDataSchema;

[Combinator]
[Description("Applies a linear remapping function to the input values based on the specified sampler settings.")]
[WorkflowElementCategory(ElementCategory.Transform)]
public class ParseActionFromSampler2D
{
    public Sampler2D Sampler { get; set; }
    public IObservable<Timestamped<ParsedAction>> Process(IObservable<Timestamped<Tuple<double, double>>> source)
    {
        var sampler = Sampler;
        Func<double, double> remap0 = (value) =>
        {
            var t = (value - sampler.MinFrom0) / (sampler.MaxFrom0 - sampler.MinFrom0);
            var mapped = sampler.MinTo0 + t * (sampler.MaxTo0 - sampler.MinTo0);
            return Math.Max(sampler.MinTo0, Math.Min(sampler.MaxTo0, mapped));
        };
        Func<double, double> remap1 = (value) =>
        {
            if (double.IsNaN(value))
            {
                return 0.0;
            }
            var t = (value - sampler.MinFrom1) / (sampler.MaxFrom1 - sampler.MinFrom1);
            var mapped = sampler.MinTo1 + t * (sampler.MaxTo1 - sampler.MinTo1);
            return Math.Max(sampler.MinTo1, Math.Min(sampler.MaxTo1, mapped));
        };
        return source.Select(ts => Timestamped.Create(
            new ParsedAction()
            {
                Action0 = ts.Value.Item1,
                Action1 = ts.Value.Item2,
                ProjectedAction = remap0(ts.Value.Item1) + remap1(ts.Value.Item2),
                SampledCoordinate0 = ts.Value.Item1,
                SampledCoordinate1 = ts.Value.Item2
            }, ts.Seconds));
    }
}
