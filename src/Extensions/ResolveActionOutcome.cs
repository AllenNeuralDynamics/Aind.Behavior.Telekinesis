using Bonsai;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;

[Combinator]
[Description("Calculates the outcome of the action based on the success of the action and its arrival time.")]
[WorkflowElementCategory(ElementCategory.Transform)]
public class ResolveActionOutcome
{
    public IObservable<AindBehaviorTelekinesisDataSchema.TrialOutCome> Process(IObservable<Tuple<Tuple<Tuple<bool, AindBehaviorTelekinesisDataSchema.Action>, double>, double>> source)
    {
        return source.Select(value => {
            var isSuccessful = value.Item1.Item1.Item1;
            var action = value.Item1.Item1.Item2;
            var initialTimestamp = value.Item1.Item2;
            var responseTime = isSuccessful ? -(value.Item2 - initialTimestamp) : (double?)null;
            return new AindBehaviorTelekinesisDataSchema.TrialOutCome()
            {
                IsSuccessful = isSuccessful,
                ResponseTime = responseTime,
                Action = action
            };
        });
    }
}
