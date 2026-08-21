using Bonsai;
using Hexa.NET.ImGui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using AindBehaviorTelekinesisDataSchema;

[Combinator]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Renders a table of recent Trial properties inside an ImGui window on each new Trial.")]
public class TrialTableVisualizer
{
    private bool visible = true;
    public bool Visible { get { return visible; } set { visible = value; } }

    private uint history = 3;
    public uint History { get { return history; } set { history = value; } }

    private float fontSize = 16.0f;
    public float FontSize { get { return fontSize; } set { fontSize = value; } }

    private readonly Queue<Trial> trials = new Queue<Trial>();

    public IObservable<Trial> Process(IObservable<Trial> source)
    {
        return Observable.Create<Trial>(observer =>
        {
            var sourceObserver = Observer.Create<Trial>(
                value =>
                {
                    unsafe { ImGui.GetIO().Handle->ConfigErrorRecoveryEnableAssert = 0; }

                    trials.Enqueue(value);
                    while (trials.Count > History)
                    {
                        trials.Dequeue();
                    }

                    if (Visible)
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
                        var childFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                        if (ImGui.BeginChild("##TrialTableVisualizer", new Vector2(0, 0), ImGuiChildFlags.None, childFlags))
                        {
                            ImGui.PushFont(ImGui.GetFont(), FontSize);
                            DrawTrialPropertiesTable(trials, History);
                            ImGui.PopFont();
                        }
                        ImGui.EndChild();
                        ImGui.PopStyleVar();
                    }

                    observer.OnNext(value);
                },
                observer.OnError,
                observer.OnCompleted);
            return source.SubscribeSafe(sourceObserver);
        });
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

    static void DrawTrialPropertiesTable<T>(Queue<T> items, uint historyCount) where T : class
    {
        var properties = typeof(T).GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        int columnCount = 1 + (int)historyCount;
        int rowCount = properties.Length + 1;

        var headerColor = new Vector4(0.7f, 0.8f, 0.9f, 1.0f);

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
    }
}
