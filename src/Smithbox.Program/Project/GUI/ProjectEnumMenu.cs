using Google.Protobuf.WellKnownTypes;
using Hexa.NET.ImGui;
using StudioCore;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using static SoulsFormats.MQB;

namespace StudioCore.Application;

public class ProjectEnumMenu
{
    public bool IsDisplayed = false;
    public ProjectOrchestrator Orchestrator;

    private ProjectEnumEntry CurrentEnum;
    private ProjectEnumOption CurrentOption;

    private string OptionEntryFilter = "";
    private bool InitialLayout = false;

    public ProjectEnumMenu(ProjectOrchestrator orchestrator)
    {
        Orchestrator = orchestrator;
    }

    public void Draw()
    {
        if (!IsDisplayed)
            return;

        if (!InitialLayout)
        {
            UIHelper.SetupPopupWindow();
            InitialLayout = true;
        }

        if (!ImGui.Begin(LocalizationManager.Instance.Get("Project Enums"), ref IsDisplayed, UIHelper.GetEditorPopupWindowFlags()))
        {
            ImGui.End();
            return;
        }

        ImGui.BeginMenuBar();
        if (ImGui.BeginMenu(LocalizationManager.Instance.Get("File")))
        {
            if (ImGui.MenuItem(LocalizationManager.Instance.Get("Save Enums")))
            {
                IsDisplayed = false;
                Save();
            }
            UIHelper.Tooltip(LocalizationManager.Instance.Get("Save enum changes to the project"));

            ImGui.EndMenu();
        }
        ImGui.EndMenuBar();

        DrawMainLayout();

        ImGui.End();
    }

    #region Layout

    private void DrawMainLayout()
    {
        ImGui.Columns(3, "enumLayout", true);

        DrawEnumList();
        ImGui.NextColumn();

        DrawOptionList();
        ImGui.NextColumn();

        DrawEditor();

        ImGui.Columns(1);
    }

    #endregion

    #region Enum Sidebar

    private void DrawEnumList()
    {
        ImGui.BeginChild("EnumSidebar", new Vector2(0, 0));

        ImGui.Text(LocalizationManager.Instance.Get("Enums"));
        ImGui.Separator();

        foreach (var entry in Orchestrator.SelectedProject.Handler.ProjectData.ProjectEnums.List)
        {
            bool selected = entry == CurrentEnum;

            if (selected)
                ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);

            if (ImGui.Selectable($"{Icons.List} {entry.Name}", selected))
            {
                CurrentEnum = entry;
                CurrentOption = null;
            }

            if (selected)
                ImGui.PopStyleColor();
        }

        ImGui.EndChild();
    }

    #endregion

    #region Option List

    private void DrawOptionList()
    {
        ImGui.BeginChild("EnumOptionPanel", new Vector2(0, 0));

        if (CurrentEnum == null)
        {
            ImGui.TextDisabled(LocalizationManager.Instance.Get("Select an enum."));
            ImGui.EndChild();
            return;
        }

        var options = CurrentEnum.Options ?? new List<ProjectEnumOption>();

        ImGui.Text($"{LocalizationManager.Instance.Get("Options")} ({options.Count})");
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint(
            "##optionFilter",
            LocalizationManager.Instance.Get("Filter by name..."),
            ref OptionEntryFilter,
            128
        );

        ImGui.Separator();
        ImGui.BeginChild("OptionListInner");

        if (options.Count == 0)
        {
            ImGui.TextDisabled(LocalizationManager.Instance.Get("No options defined."));
            ImGui.Spacing();

            if (ImGui.Button($"{Icons.Plus} {LocalizationManager.Instance.Get("Add Option")}"))
            {
                CurrentEnum.Options ??= new();
                CurrentEnum.Options.Add(new ProjectEnumOption
                {
                    ID = "NEW_ID",
                    Name = LocalizationManager.Instance.Get("New Option"),
                    Description = ""
                });
            }

            ImGui.EndChild();
            ImGui.EndChild();
            return;
        }

        var filter = OptionEntryFilter.ToLowerInvariant();

        var filteredList = new List<ProjectEnumOption>();

        foreach (var entry in options)
        {
            if (!string.IsNullOrWhiteSpace(filter) &&
                !entry.Name.ToLowerInvariant().Contains(filter))
                continue;

            filteredList.Add(entry);
        }

        var clipper = new ImGuiListClipper();
        clipper.Begin(filteredList.Count);

        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var option = filteredList[i];

                bool selected = option == CurrentOption;

                if (ImGui.Selectable($"[{option.ID}] {option.Name}", selected))
                {
                    CurrentOption = option;
                }

                if (ImGui.BeginPopupContextItem($"option_ctx_{i}"))
                {
                    if (ImGui.Selectable(LocalizationManager.Instance.Get("Duplicate")))
                    {
                        Orchestrator.ActionManager.ExecuteAction(
                            new ChangeEnumList(
                                CurrentEnum,
                                option,
                                new ProjectEnumOption
                                {
                                    ID = option.ID,
                                    Name = $"{option.Name}_1",
                                    Description = option.Description
                                },
                                ProjectEnumListOperation.Add,
                                i + 1));
                    }

                    if (ImGui.Selectable(LocalizationManager.Instance.Get("Remove")))
                    {
                        Orchestrator.ActionManager.ExecuteAction(
                            new ChangeEnumList(
                                CurrentEnum,
                                option,
                                null,
                                ProjectEnumListOperation.Remove,
                                i));
                    }

                    ImGui.EndPopup();
                }
            }
        }

        ImGui.EndChild();
        ImGui.EndChild();
    }

    #endregion

    #region Editor

    private void DrawEditor()
    {
        ImGui.BeginChild("EnumEditorPanel", new Vector2(0, 0));

        if (CurrentEnum == null)
        {
            ImGui.TextDisabled(LocalizationManager.Instance.Get("Select an enum or option to edit."));
            ImGui.EndChild();
            return;
        }

        DrawEnumEditor();

        if (CurrentOption != null)
        {
            ImGui.Separator();
            DrawOptionEditor();
        }

        ImGui.EndChild();
    }

    private void DrawEnumEditor()
    {
        ImGui.Text(LocalizationManager.Instance.Get("Enum Details"));
        ImGui.Separator();

        ImGui.Columns(2, "enumEditorCols", false);

        DrawEnumField(LocalizationManager.Instance.Get("Display Name"),
            CurrentEnum.DisplayName,
            ProjectEnumFieldType.DisplayName);

        DrawEnumField(LocalizationManager.Instance.Get("Description"),
            CurrentEnum.Description,
            ProjectEnumFieldType.Description);

        ImGui.Columns(1);
    }

    private void DrawEnumField(string label, string value, ProjectEnumFieldType field)
    {
        string original = value;

        ImGui.Text(label);
        ImGui.NextColumn();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"##{label}", ref value, 256);

        if (ImGui.IsItemDeactivatedAfterEdit() && original != value)
        {
            Orchestrator.ActionManager.ExecuteAction(
                new ChangeEnumField(CurrentEnum, original, value, field));
        }

        ImGui.NextColumn();
    }

    private void DrawOptionEditor()
    {
        ImGui.Text(LocalizationManager.Instance.Get("Option Details"));
        ImGui.Separator();

        ImGui.Columns(2, "optionEditorCols", false);

        DrawOptionField(LocalizationManager.Instance.Get("ID"),
            CurrentOption.ID,
            ProjectEnumOptionFieldType.ID);

        DrawOptionField(LocalizationManager.Instance.Get("Name"),
            CurrentOption.Name,
            ProjectEnumOptionFieldType.Name);

        DrawOptionField(LocalizationManager.Instance.Get("Description"),
            CurrentOption.Description,
            ProjectEnumOptionFieldType.Description);

        ImGui.Columns(1);
    }

    private void DrawOptionField(string label, string value, ProjectEnumOptionFieldType field)
    {
        string original = value;

        ImGui.Text(label);
        ImGui.NextColumn();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"##opt_{label}", ref value, 256);

        if (ImGui.IsItemDeactivatedAfterEdit() && original != value)
        {
            Orchestrator.ActionManager.ExecuteAction(
                new ChangeEnumOptionField(CurrentOption, original, value, field));
        }

        ImGui.NextColumn();
    }

    #endregion

    #region Save

    public void Save()
    {
        var projectFolder = Path.Join(
            Orchestrator.SelectedProject.Descriptor.ProjectPath,
            ".smithbox",
            "Project");

        var projectFile = Path.Combine(projectFolder, "Shared Param Enums.json");

        var json = JsonSerializer.Serialize(
            Orchestrator.SelectedProject.Handler.ProjectData.ProjectEnums,
            ProjectJsonSerializerContext.Default.ProjectEnumResource);

        if (!Directory.Exists(projectFolder))
            Directory.CreateDirectory(projectFolder);

        File.WriteAllText(projectFile, json);
    }

    #endregion
}
