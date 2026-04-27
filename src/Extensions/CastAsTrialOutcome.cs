using Bonsai;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using AindBehaviorTelekinesisDataSchema;

[Combinator]
[Description("Casts the input object to a TrialOutCome type.")]
[WorkflowElementCategory(ElementCategory.Transform)]
public class CastAsTrialOutcome
{
    public IObservable<TrialOutCome> Process(IObservable<Object> source)
    {
        return source.Cast<TrialOutCome>();
    }
}
