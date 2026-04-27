using Bonsai;
using System;
using System.ComponentModel;
using AindBehaviorTelekinesisDataSchema;
using System.Reactive.Linq;

[Combinator]
[Description("Calculates the outcome of the action based on the success of the action and its arrival time.")]
[WorkflowElementCategory(ElementCategory.Combinator)]
public class ResolveActionOutcome
{
    public IObservable<TrialOutCome> Process(IObservable<Tuple<bool, AindBehaviorTelekinesisDataSchema.Action>> source)
    {
        var initialTimestamp = DateTimeOffset.UtcNow;
        return source.Select(value =>
        {
            var isSuccessful = value.Item1;
            var action = value.Item2;
            return new TrialOutCome()
            {
                IsSuccessful = isSuccessful,
                ResponseTime = isSuccessful ? (DateTimeOffset.UtcNow - initialTimestamp).TotalSeconds : (double?)null,
                Action = action
            };
        }).Take(1);
    }
}
