using Bonsai;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Collections.ObjectModel;
using AindTelekinesisDataSchema.Rig;

[Combinator]
[Description("Parses LoadCell calibration data into a LoadCellsCalibrations object.")]
[WorkflowElementCategory(ElementCategory.Transform)]
public class ParseLoadCellsCalibration
{
    public IObservable<LoadCellsCalibrations> Process(IObservable<IEnumerable<Tuple<int, int, int>>> source)
    {
        return source.Select(value => {
            var calibrations = new LoadCellsCalibrations();
            foreach (var calibration in value)
            {
                calibrations.Add(new LoadCellCalibration
                {
                    Offset = calibration.Item1,
                    Baseline = calibration.Item2,
                    LoadCellIndex = calibration.Item3,
                    Slope=1,
                });
            }
            return calibrations;
        });
    }

    public IObservable<LoadCellsCalibrations> Process(IObservable<Tuple<IList<Tuple<int, int, int>>, LoadCellsCalibrations>> source)
    {
        return source.Select(value => {
            var previousCalibration = value.Item2;
            var calibrations = new LoadCellsCalibrations();
            foreach (var calibration in value.Item1)
            {
                calibrations.Add(new LoadCellCalibration
                {
                    Offset = calibration.Item1,
                    Baseline = calibration.Item2,
                    LoadCellIndex = calibration.Item3,
                    Slope=previousCalibration.Contains(calibration.Item3) ? previousCalibration[calibration.Item3].Slope : 1,
                });
            }
            return calibrations;
        });
    }
    public IObservable<LoadCellsCalibrations> Process(IObservable<IEnumerable<LoadCellCalibrationOutput>> source)
    {
        return source.Select(value => {
            var calibrations = new LoadCellsCalibrations();
            foreach (var calibration in value)
            {
                calibrations.Add(new LoadCellCalibration
                {
                    Offset = calibration.Offset,
                    Baseline = (int)calibration.Baseline,
                    LoadCellIndex = calibration.Channel,
                    Slope = calibration.Slope,
                });
            }
            return calibrations;
        });
    }

}


public class LoadCellCalibration{
    public int Offset { get; set; }
    public int Baseline { get; set; }
    public int LoadCellIndex { get; set; }
    public double Slope { get; set; }

    public LoadCellCalibration()
    {
        Offset = 0;
        Baseline = 0;
        LoadCellIndex = 0;
        Slope = 1;
    }

    public LoadCellCalibration(LoadCellCalibration other)
    {
        Offset = other.Offset;
        Baseline = other.Baseline;
        LoadCellIndex = other.LoadCellIndex;
        Slope = other.Slope;
    }
}

public class LoadCellsCalibrations : KeyedCollection<int, LoadCellCalibration>
{
    protected override int GetKeyForItem(LoadCellCalibration item)
    {
        return item.LoadCellIndex;
    }
}
