using Bonsai.Design;
using Bonsai.Expressions;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Numerics;
using System.Windows.Forms;
using AllenNeuralDynamics.Core.Design;
using AindBehaviorTelekinesisDataSchema;

public class TrialTableVisualizer : BufferedVisualizer
{
    ImGuiControl imGuiCanvas;
    private uint history = 3;
    private float fontSize = 16.0f;

    private readonly Queue<Trial> trials = new Queue<Trial>();

    /// <inheritdoc/>
    public override void Show(object value)
    {
    }

    /// <inheritdoc/>
    protected override void ShowBuffer(IList<System.Reactive.Timestamped<object>> values)
    {
        imGuiCanvas.Invalidate();
        var casted = values.Select(v => (Trial)v.Value);
        foreach (var trial in casted)
        {
            trials.Enqueue(trial);
            while (trials.Count > history)
            {
                trials.Dequeue();
            }
        }
        base.ShowBuffer(values);
    }

    void StyleColors()
    {
        ImGui.StyleColorsLight();
        ImPlot.StyleColorsLight(ImPlot.GetStyle());
    }

    static void DrawCenteredText(string text, float rowHeight)
    {
        float textHeight = ImGui.GetTextLineHeight();
        float offsetY = (rowHeight - textHeight) / 2.0f;
        var cursorPos = ImGui.GetCursorPos();
        ImGui.SetCursorPosY(cursorPos.Y + offsetY);
        ImGui.Text(text);
    }

    static void DrawBoldCenteredText(string text, float rowHeight)
    {
        float textHeight = ImGui.GetTextLineHeight();
        float offsetY = (rowHeight - textHeight) / 2.0f;
        var cursorPos = ImGui.GetCursorPos();
        ImGui.SetCursorPosY(cursorPos.Y + offsetY);
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Text(text);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(new Vector2(pos.X + 1, pos.Y), ImGui.GetColorU32(ImGuiCol.Text), text);
    }

    static void DrawTrialPropertiesTable<T>(Queue<T> items, uint historyCount, float fontSize) where T : class
    {
        var properties = typeof(T).GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        int columnCount = 1 + (int)historyCount;
        int rowCount = properties.Length + 1;

        var headerColor = new Vector4(0.7f, 0.8f, 0.9f, 1.0f);

        ImGui.PushFont(ImGui.GetFont(), fontSize);

        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame;
        var availableSize = ImGui.GetContentRegionAvail();
        float rowHeight = availableSize.Y / rowCount;

        if (ImGui.BeginTable("TrialPropertiesTable", columnCount, tableFlags, availableSize))
        {
            ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.None);
            for (int i = 0; i < (int)historyCount; i++)
            {
                string label = i == 0 ? "Trial" : "Trial-" + i;
                ImGui.TableSetupColumn(label, ImGuiTableColumnFlags.None);
            }

            ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
            for (int col = 0; col < columnCount; col++)
            {
                ImGui.TableSetColumnIndex(col);
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.ColorConvertFloat4ToU32(headerColor));
                string headerText = col == 0 ? "Property" : (col == 1 ? "Trial" : "Trial-" + (col - 1));
                DrawBoldCenteredText(headerText, rowHeight);
            }

            var itemList = items != null ? items.ToList() : new List<T>();
            itemList.Reverse();

            foreach (var prop in properties)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);

                ImGui.TableSetColumnIndex(0);
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.ColorConvertFloat4ToU32(headerColor));
                DrawBoldCenteredText(prop.Name, rowHeight);

                for (int i = 0; i < (int)historyCount; i++)
                {
                    ImGui.TableSetColumnIndex(i + 1);
                    if (i < itemList.Count)
                    {
                        var value = prop.GetValue(itemList[i]);
                        DrawCenteredText(value != null ? value.ToString() : "null", rowHeight);
                    }
                    else
                    {
                        DrawCenteredText("-", rowHeight);
                    }
                }
            }

            ImGui.EndTable();
        }

        ImGui.PopFont();
    }

    /// <inheritdoc/>
    public override void Load(IServiceProvider provider)
    {
        var context = (ITypeVisualizerContext)provider.GetService(typeof(ITypeVisualizerContext));
        var visualizerBuilder = ExpressionBuilder.GetVisualizerElement(context.Source).Builder as TrialTableVisualizerBuilder;
        if (visualizerBuilder != null)
        {
            history = visualizerBuilder.History;
            fontSize = visualizerBuilder.FontSize;
        }
        imGuiCanvas = new ImGuiControl();
        imGuiCanvas.Dock = DockStyle.Fill;
        imGuiCanvas.Render += (sender, e) =>
        {
            var dockspaceId = ImGui.DockSpaceOverViewport(
                0,
                ImGui.GetMainViewport(),
                ImGuiDockNodeFlags.AutoHideTabBar | ImGuiDockNodeFlags.NoUndocking);

            StyleColors();


            if (ImGui.Begin("TrialTableVisualizer"))
            {
                DrawTrialPropertiesTable(trials, history, fontSize);
            }

            ImGui.End();
            var centralNode = ImGuiP.DockBuilderGetCentralNode(dockspaceId);
            if (!ImGui.IsWindowDocked() && !centralNode.IsNull)
            {
                unsafe
                {
                    var handle = centralNode.Handle;
                    uint dockId = handle->ID;
                    ImGuiP.DockBuilderDockWindow("TrialTableVisualizer", dockId);
                }
            }
        };

        var visualizerService = (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
        if (visualizerService != null)
        {
            visualizerService.AddControl(imGuiCanvas);
        }
    }

    /// <inheritdoc/>
    public override void Unload()
    {
        if (imGuiCanvas != null)
        {
            imGuiCanvas.Dispose();
        }
    }
}

