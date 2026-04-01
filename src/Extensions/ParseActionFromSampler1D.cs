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
public class ParseActionFromSampler1D
{
    public Sampler1D Sampler { get; set; }
    public IObservable<Timestamped<ParsedAction>> Process(IObservable<Timestamped<double>> source)
    {
        var sampler = Sampler;
        Func<double, double> remap = (value) =>
        {
            var t = (value - sampler.MinFrom) / (sampler.MaxFrom - sampler.MinFrom);
            var mapped = sampler.MinTo + t * (sampler.MaxTo - sampler.MinTo);
            return Math.Max(sampler.MinTo, Math.Min(sampler.MaxTo, mapped));
        };
        return source.Select(ts => Timestamped.Create(
            new ParsedAction()
            {
                Action0 = ts.Value,
                Action1 = null,
                ProjectedAction = remap(ts.Value),
                SampledCoordinate0 = ts.Value,
                SampledCoordinate1 = null
            }, ts.Seconds));
    }
}
