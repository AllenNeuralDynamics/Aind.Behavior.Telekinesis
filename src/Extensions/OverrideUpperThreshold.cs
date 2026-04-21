using Bonsai;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using AindBehaviorTelekinesisDataSchema;
using AllenNeuralDynamics.AindBehaviorServices.Distributions;

[Combinator]
[Description("")]
[WorkflowElementCategory(ElementCategory.Transform)]
public class OverrideUpperThreshold
{
    private double? upperThreshold = null;
    public double? UpperThreshold
    {
        get { return upperThreshold; }
        set { upperThreshold = value; }
    }

    public IObservable<Trial> Process(IObservable<Trial> source)
    {
        return source.Select(value =>
        {
            if (!UpperThreshold.HasValue)
            {
                return value;
            }
            var trial = value.Copy();
            trial.ResponsePeriod.Action.UpperActionThreshold = new Scalar(){
                DistributionParameters = new ScalarDistributionParameter() { Value = UpperThreshold.Value }
            };
            return trial;
        });
    }
}
