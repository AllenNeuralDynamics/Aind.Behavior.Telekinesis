using Bonsai;
using System;
using System.Linq;
using System.Reactive.Linq;
using Bonsai.Harp;
using AindTelekinesisDataSchema.TaskLogic;
using System.Xml.Serialization;
using System.ComponentModel;
using OpenCV.Net;

public class ParseAction : Transform<Timestamped<ActionVector>, Timestamped<ActionVectorFromLut<ActionVector>>>
{

    public ActionLookUpTableFactory LutSettings { get; set; }

    [XmlIgnore]
    [TypeConverter("Bonsai.Dsp.MatConverter, Bonsai.Dsp")]
    public Mat LookUpTable { get; set; }

    public IObservable<Timestamped<ActionVectorFromLut<ActionVector>>> Process(IObservable<Timestamped<Tuple<double, double>>> source)
    {
        return Process(source.Select(value => Timestamped.Create(new ActionVector(value.Value), value.Seconds)));
    }


    public override IObservable<Timestamped<ActionVectorFromLut<ActionVector>>> Process(IObservable<Timestamped<ActionVector>> source)
    {
        Mat lookUpTable;
        ActionLookUpTableFactory lutSettings = LutSettings;
        SubPixelBilinearInterpolator interpolator = new SubPixelBilinearInterpolator();
        if (LookUpTable == null)
        {
            throw new InvalidOperationException("LookUpTable must be specified.");
        }
        lookUpTable = LookUpTable.Clone();
        interpolator = new SubPixelBilinearInterpolator(lutSettings, lookUpTable);
        interpolator.Validate();
        return source.Select(value =>
        {
            return Timestamped.Create(interpolator.LookUp(value.Value), value.Seconds);
        });
    }
}

public class ActionVector
{
    public double Action0 { get; set; }

    public double Action1 { get; set; }


    public ActionVector(double action0, double action1)
    {
        Action0 = action0;
        Action1 = action1;
    }

    public ActionVector(Tuple<double, double> value)
    {
        Action0 = value.Item1;
        Action1 = value.Item2;
    }

    public double this[int index]
    {
        get
        {
            if (index == 0) return Action0;
            if (index == 1) return Action1;
            throw new IndexOutOfRangeException();
        }
    }

    public override string ToString()
    {
        return string.Format("Action0={0}, Action1={1}", Action0, Action1);
    }

}

public class ActionVectorFromLut<T> where T : ActionVector
{
    public T ActionVector { get; set; }

    public double ProjectedAction { get; set; }

    public T LookUpIndex { get; set; }

    public ActionVectorFromLut(T ActionVector, double ProjectedAction, T LookUpIndex)
    {
        this.ActionVector = ActionVector;
        this.ProjectedAction = ProjectedAction;
        this.LookUpIndex = LookUpIndex;
    }
}


public class SubPixelBilinearInterpolator
{
    public SubPixelBilinearInterpolator(ActionLookUpTableFactory settings, Mat lookUpTable)
    {
        Settings = settings;
        LookUpTable = lookUpTable;
    }

    public SubPixelBilinearInterpolator() { }

    public ActionLookUpTableFactory Settings { get; private set; }

    public Mat LookUpTable { get; private set; }

    public void Validate()
    {
        if (Settings == null)
        {
            throw new InvalidOperationException("LUT Settings must be specified.");
        }
        if (Settings.Action0Min >= Settings.Action0Max || Settings.Action1Min >= Settings.Action1Max)
        {
            throw new ArgumentException("Minimum must be strictly lower than maximum.");
        }
        if (LookUpTable.Channels > 1)
        {
            throw new ArgumentException("Input matrix must have a single channel");
        }
    }

    public ActionVectorFromLut<ActionVector> LookUp(ActionVector value)
    {
        var rescaled_action0Value = Rescale(value.Action0, Settings.Action0Min, Settings.Action0Max, 0, LookUpTable.Size.Height);
        var clamped_action0Value = ClampValue(rescaled_action0Value, 0, LookUpTable.Size.Height);
        if (double.IsNaN(value.Action1))
        {
            return new ActionVectorFromLut<ActionVector>(
                value,
                GetSubPixel(LookUpTable, clamped_action0Value, double.NaN),
                new ActionVector(clamped_action0Value, double.NaN));
        }
        else{
            var rescaled_action1Value = Rescale(value.Action1, Settings.Action1Min, Settings.Action1Max, 0, LookUpTable.Size.Width);
            var clamped_action1Value = ClampValue(rescaled_action1Value, 0, LookUpTable.Size.Width);
            return new ActionVectorFromLut<ActionVector>(
                value,
                GetSubPixel(LookUpTable, clamped_action0Value, clamped_action1Value),
                new ActionVector(clamped_action0Value, clamped_action1Value));
            }
    }

    private static double Rescale(double value, double minFrom, double maxFrom, double minTo, double maxTo)
    {
        return (value - minFrom) / (maxFrom - minFrom) * (maxTo - minTo) + minTo;
    }

    private static double ClampValue(double value, double MinBoundTo, double MaxBoundTo)
    {
        return Math.Min(Math.Max(value, MinBoundTo), MaxBoundTo);
    }

    private static double GetSubPixel(Mat src, double action0Value, double action1Value)
    {
        var idx0 = (int)action0Value;
        var d0 = action0Value - idx0;
        idx0 = Math.Min(idx0, src.Size.Height - 2);

        if (double.IsNaN(action1Value)){
            var idx1 = (int)action1Value;
            var d1 = action1Value - idx1;
            idx1 = Math.Min(idx1, src.Size.Width - 2);

            var p00 = src[idx0, idx1];
            var p01 = src[idx0, idx1 + 1];
            var p10 = src[idx0 + 1, idx1];
            var p11 = src[idx0 + 1, idx1 + 1];
            return (double)(p00.Val0 * (1 - d1) * (1 - d0) + p01.Val0 * d1 * (1 - d0) + p10.Val0 * (1 - d1) * d0 + p11.Val0 * d1 * d0);
        }
        else
        {
            var p00 = src[idx0, 0];
            var p10 = src[idx0 + 1, 0];
            return (double)(p00.Val0 * (1 - d0) + p10.Val0 * d0);
        }

    }
}
